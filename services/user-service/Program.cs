using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Auth;
using Shared.Logging;
using UserService.Data;
using System.Text;
using System.IO;
using Microsoft.AspNetCore.Http;
using UserService;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowConfiguredOrigins", policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:3000", "http://localhost:80") // Frontend and API Gateway
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .SetIsOriginAllowed(origin => 
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase));
    });
});

// PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Auth with proper configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "supersecretkey");
        
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
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Allow JWT token to be passed via query string
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtService>(); // Add JwtService

// Register custom services for CDN and Auth synchronization
builder.Services.AddHttpClient("CdnServiceClient", client =>
{
    var cdnServiceUrl = builder.Configuration["ServiceUrls:CdnService"] ?? "http://localhost:5250";
    client.BaseAddress = new Uri(cdnServiceUrl);
});
builder.Services.AddScoped<ICdnService, CdnService>();

builder.Services.AddHttpClient("AuthServiceClient", client =>
{
    var authServiceUrl = builder.Configuration["ServiceUrls:AuthService"] ?? "http://localhost:5106";
    client.BaseAddress = new Uri(authServiceUrl);
});
builder.Services.AddScoped<IAuthSyncService, AuthSyncService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "UserService API", Version = "v1" });

    // Support for IFormFile uploads
    c.MapType<IFormFile>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
    c.OperationFilter<FileUploadOperationFilter>();

    // JWT Bearer Auth support in Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Shared.Auth.UserSyncService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Create directory for profile images if it doesn't exist
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
Directory.CreateDirectory(uploadsFolder);

app.UseStaticFiles(); // Enable serving static files (for profile images)

app.UseHttpsRedirection();

// Apply CORS policy
app.UseCors("AllowConfiguredOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Clear db in development mode
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.SeedAsync(true);
    }
}

app.Run();