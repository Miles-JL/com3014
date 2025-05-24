using Microsoft.EntityFrameworkCore;
using notification_service.Data;
using notification_service.Services;
using notification_service.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using System.Net.WebSockets;
using WebSocketManager = notification_service.Services.WebSocketManager;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Notification Service API", 
        Version = "v1",
        Description = "API for handling real-time notifications"
    });
    
    // Add JWT Authentication to Swagger
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? 
    throw new InvalidOperationException("JWT Key not configured"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

// Register services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();
builder.Services.AddSingleton<ITokenValidator, JwtTokenValidator>();

// Get allowed origins from configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:3000", "https://localhost:3000" };

// Configure CORS with explicit origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => 
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("x-signalr-user-agent");
    });

    // Specific policy for WebSocket connections
    options.AddPolicy("AllowWebSocket", policy => 
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("x-signalr-user-agent");
    });
});

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Attempting to connect to the database...");
        logger.LogInformation($"Connection string: {connectionString}");
        
        var context = services.GetRequiredService<AppDbContext>();
        
        // Test the connection
        if (await context.Database.CanConnectAsync())
        {
            logger.LogInformation("Successfully connected to the database.");
            
            // Get pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying pending migrations...");
                await context.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("No pending migrations to apply.");
            }
        }
        else
        {
            logger.LogError("Failed to connect to the database.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while setting up the database.");
        throw; // Re-throw to stop the application
    }
}

// Configure the HTTP request pipeline
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
    c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
    {
        swaggerDoc.Servers = new List<OpenApiServer> 
        { 
            new() { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" } 
        };
    });
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "Notification Service API v1");
    c.RoutePrefix = "swagger";
    c.DisplayRequestDuration();
});

// The order of middleware is important
app.UseHttpsRedirection();

// Enable CORS with the WebSocket policy
app.UseCors("AllowWebSocket");

// Configure WebSocket options
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
}; // Allowed origins are now handled by CORS

// Enable WebSockets with the configured options
app.UseWebSockets(webSocketOptions);

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Add WebSocket handling middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Handle WebSocket requests
        if (context.WebSockets.IsWebSocketRequest && 
            (context.Request.Path.StartsWithSegments("/ws/notification") || 
             context.Request.Path == "/"))
        {
            var token = context.Request.Query["access_token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                logger.LogWarning("WebSocket connection attempt without access token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            logger.LogInformation("Incoming WebSocket connection with token (first 20 chars): {TokenPrefix}...", 
                token.Length > 20 ? token.Substring(0, 20) : token);
            
            var tokenValidator = context.RequestServices.GetRequiredService<ITokenValidator>();
            var userId = await tokenValidator.ValidateTokenAsync(token);
            
            if (userId == null)
            {
                logger.LogWarning("WebSocket connection: Invalid or expired token");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            logger.LogInformation("WebSocket connection authorized for user {UserId}", userId);
            
            var webSocketManager = context.RequestServices.GetRequiredService<IWebSocketManager>();
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            
            try
            {
                logger.LogInformation("WebSocket connection established for user {UserId}", userId);
                await webSocketManager.AddConnection(userId.Value, webSocket);
                
                // Keep the connection alive until it's closed
                var buffer = new byte[1024 * 4];
                var receiveBuffer = new ArraySegment<byte>(buffer);
                
                while (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        var receiveResult = await webSocket.ReceiveAsync(
                            receiveBuffer, 
                            CancellationToken.None);

                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            logger.LogInformation("WebSocket close received for user {UserId}", userId);
                            await webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Closed by client",
                                CancellationToken.None);
                            break;
                        }
                        
                        // Log non-close messages
                        if (receiveResult.MessageType == WebSocketMessageType.Text && receiveResult.Count > 0)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
                            logger.LogInformation("Message from user {UserId}: {Message}", userId, message);
                        }
                    }
                    catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        logger.LogInformation(ex, "WebSocket connection closed unexpectedly for user {UserId}", userId);
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in WebSocket loop for user {UserId}", userId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.InternalServerError,
                            "Internal server error",
                            CancellationToken.None);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error for user {userId}: {ex}");
                if (webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.InternalServerError,
                            "Internal server error",
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore close errors
                    }
                }
            }
            finally
            {
                Console.WriteLine($"Cleaning up WebSocket connection for user {userId}");
                try
                {
                    await webSocketManager.RemoveConnection(userId.Value, webSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error removing WebSocket connection for user {userId}: {ex}");
                }
            }
        }
        else
        {
            await next(context);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in WebSocket middleware: {ex}");
        throw;
    }
});

app.MapControllers();
app.Run();