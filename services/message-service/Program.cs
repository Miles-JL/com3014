using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Auth;
using MessageService.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//
// Configure PostgreSQL database connection
//
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

//
// Configure JWT authentication for secure endpoints
//
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtConfig = builder.Configuration.GetSection("Jwt");
        var jwtKey = jwtConfig["Key"] ?? throw new InvalidOperationException("JWT key is missing from configuration");
        var key = Encoding.UTF8.GetBytes(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

//
// Define custom authorization policy for internal service-to-service requests
// Used by realtime-service to post messages without JWT
//
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InternalOnly", policy =>
    {
        policy.RequireAssertion(context =>
        {
            var httpContext = context.Resource as HttpContext;
            if (httpContext == null) return false;

            return httpContext.Request.Headers.TryGetValue("X-Internal-Api-Key", out var value)
                && value == "realtime-secret-123";
        });
    });
});

//
// Register scoped dependencies and core services
//
builder.Services.AddScoped<JwtService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

//
// Configure CORS to support:
// 1. file:// based static pages (origin = "null")
// 2. local API gateway running on http://api-gateway-1:80
//
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendAndGateway", policy =>
    {
        policy
            .WithOrigins(
                "null",
                "http://api-gateway-1:80"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

//
// Configure Swagger documentation and JWT support
//
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MessageService API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your JWT token like this: Bearer {your token}"
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

//
// Allow access to current HttpContext (used in policies, etc.)
//
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

//
// Dev-only middleware setup: enable Swagger and clear db
//
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply database migrations on startup
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Apply any pending migrations
    await db.Database.MigrateAsync();
    
    // Seed data in development
    if (app.Environment.IsDevelopment())
    {
        await db.SeedAsync(true);
    }
}
catch (Exception ex)
{
    // Log the error and continue
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
}

//
// Core middleware pipeline: HTTPS, CORS, auth, controller routing
//
app.UseHttpsRedirection();
app.UseCors("AllowFrontendAndGateway");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
