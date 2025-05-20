using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer; // Make sure this is included
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens; // Required for SymmetricSecurityKey
using System.Text; // Required for Encoding.UTF8

var builder = WebApplication.CreateBuilder(args);

// Add CORS support with more permissive settings for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policyBuilder =>
    {
        policyBuilder
            .WithOrigins("http://localhost:3000") // React frontend
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Correlation-ID");
    });
});

// ---- START: Add Authentication Services ----
// Even if the gateway primarily proxies, if UseAuthentication() is called,
// basic authentication services need to be registered.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // If the gateway validates tokens, configure it similarly to auth-service
        // Otherwise, a minimal configuration might suffice if it only passes tokens.
        // For now, let's assume it might need to validate tokens or you want a consistent setup.
        var jwtSettings = builder.Configuration.GetSection("Jwt"); // Assuming you might add JWT settings to gateway's appsettings.json
        var keyInput = jwtSettings["Key"];

        if (string.IsNullOrEmpty(keyInput))
        {
    
            Console.WriteLine("Warning: JWT Key not configured in API Gateway. Using a default or dummy key for registration purposes.");
            keyInput = "a_default_dummy_key_for_gateway_longer_than_32_bytes_to_be_valid_for_hs256"; // Ensure it's long enough
        }
        var key = Encoding.UTF8.GetBytes(keyInput);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = jwtSettings.Exists() ? bool.Parse(jwtSettings["ValidateIssuer"] ?? "true") : false,
            ValidateAudience = jwtSettings.Exists() ? bool.Parse(jwtSettings["ValidateAudience"] ?? "true") : false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

    });

builder.Services.AddAuthorization(); 
// ---- END: Add Authentication Services ----

// Add reverse proxy capabilities
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add correlation ID middleware
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CorrelationIdMiddleware>();

// Configure OpenAPI for documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseRouting();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();  

app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out StringValues correlationIdValues)
        ? correlationIdValues.FirstOrDefault()
        : null;

    Console.WriteLine($"[{DateTime.UtcNow}] Incoming Request: {context.Request.Method} {context.Request.Path} - CorrelationID: {correlationId ?? "not set"}");

    var originalBodyStream = context.Response.Body;
    await using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    await next();

    Console.WriteLine($"[{DateTime.UtcNow}] Outgoing Response: {context.Response.StatusCode} - CorrelationID: {correlationId ?? "not set"}");

    responseBody.Seek(0, SeekOrigin.Begin);
    await responseBody.CopyToAsync(originalBodyStream);
});

app.MapReverseProxy();

app.Run(); 

// Correlation ID middleware (remains the same)
public class CorrelationIdMiddleware : IMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationIdValues) && !string.IsNullOrEmpty(correlationIdValues.FirstOrDefault()))
        {
            return correlationIdValues.ToString();
        }
        
        var newCorrelationId = Guid.NewGuid().ToString();
        context.Request.Headers[CorrelationIdHeader] = newCorrelationId;
        return newCorrelationId!;
    }
}