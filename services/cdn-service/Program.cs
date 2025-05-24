using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CdnService.Middleware;
using CdnService.Services;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register file storage service
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add HTTP context accessor for logging
builder.Services.AddHttpContextAccessor();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("storage_health", () => 
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"healthcheck_{Guid.NewGuid()}.tmp");
            File.WriteAllText(tempFile, "healthcheck");
            File.Delete(tempFile);
            return HealthCheckResult.Healthy("Storage is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Storage is not accessible", ex);
        }
    });

// Configure Swagger with JWT Bearer Authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CDN Service API", Version = "v1" });
    
    // Add JWT Bearer Authentication
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = builder.Configuration.GetValue<bool>("Jwt:ValidateIssuer"),
        ValidateAudience = builder.Configuration.GetValue<bool>("Jwt:ValidateAudience"),
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")))
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = "swagger"; // Serve the Swagger UI at /swagger
    
    // Enable the "Authorize" button in Swagger UI
    options.OAuthClientId("swagger-ui");
    options.OAuthAppName("Swagger UI");
});

// Ensure storage directory exists
var storagePath = Path.Combine(app.Environment.ContentRootPath, "storage");
if (!Directory.Exists(storagePath))
{
    Directory.CreateDirectory(storagePath);
    app.Logger.LogInformation("Created storage directory at: {StoragePath}", storagePath);
}

// Seed profile images on startup
try
{
    ImageSeeder.SeedProfileImages(app.Environment.ContentRootPath, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "An error occurred while seeding profile images");
}

// Serve static files from the storage directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(storagePath),
    RequestPath = "/u",
    ServeUnknownFileTypes = false,
    DefaultContentType = "application/octet-stream"
});

app.Logger.LogInformation("Serving static files from {StoragePath} at /u", storagePath);

app.UseHttpsRedirection();

// Add exception handling middleware before other middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoint
// Configure health check endpoint
app.MapHealthChecks("/health", new()
{
    AllowCachingResponses = false,
    ResponseWriter = async (context, report) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Health check executed with status: {Status}", report.Status);
        
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            }),
            timestamp = DateTime.UtcNow
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapControllers();

app.Run(); 