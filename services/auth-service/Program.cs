using AuthService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shared.Auth;
using Shared.Logging;
using System.Text;
using CsvHelper;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add CORS support
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:5247", "http://localhost:3000") // Allow requests from API Gateway and Frontend (for direct calls if any)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); 
    });
});

// Add PostgreSQL EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services
builder.Services.AddScoped<JwtService>();
builder.Services.AddHttpClient();
builder.Services.AddLogging();

// JWT auth setup
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured"));
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

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// IMPORTANT: Place UseCors() before UseAuthentication() and UseAuthorization()
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed data in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "auth_users_seed.csv");
    
    try
    {
        await db.SeedFromCsvAsync(csvPath, isDevelopment: true);
        app.Logger.LogInformation("Database seeded successfully with test data");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while seeding the database");
    }
}

app.Run();