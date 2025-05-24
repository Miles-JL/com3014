using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer; // Make sure this is included
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens; // Required for SymmetricSecurityKey
using System.Text; // Required for Encoding.UTF8
using Polly; // Required for Polly
using Polly.Extensions.Http; // Required for Polly HttpClient extensions
using System.Net.Http; // Required for HttpRequestException
using Microsoft.Extensions.Options; // Required for Options.DefaultName

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("Jwt");
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

// ---- START: Polly Policy Definitions ----
// Define a retry policy: retry 3 times with exponential backoff (2s, 4s, 8s) for transient errors
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError() // Handles HttpRequestException, 5xx status codes, 408 status code
    .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound) // Example: Retry on 404 Not Found
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryAttempt, context) =>
        {
            // Log or handle retry attempts
            string reason;
            if (outcome.Result != null)
            {
                reason = $"status code {outcome.Result.StatusCode}";
            }
            else if (outcome.Exception != null)
            {
                reason = $"exception {outcome.Exception.GetType().Name}";
            }
            else
            {
                reason = "an unknown issue";
            }
            Console.WriteLine($"Retrying due to {reason}, attempt {retryAttempt} after {timespan.TotalSeconds}s...");
        });

// Define a circuit breaker policy: break if 5 consecutive failures occur, for 30 seconds
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
        onBreak: (result, timespan, context) =>
        {
            // Log or handle break events
            string reason;
            if (result.Result != null)
            {
                reason = $"status code {result.Result.StatusCode}";
            }
            else if (result.Exception != null)
            {
                reason = $"exception {result.Exception.GetType().Name}";
            }
            else
            {
                reason = "an unknown issue";
            }
            Console.WriteLine($"Circuit broken for {timespan.TotalSeconds}s due to {reason}...");
        },
        onReset: (context) =>
        {
            Console.WriteLine("Circuit reset.");
        },
        onHalfOpen: () =>
        {
            Console.WriteLine("Circuit is half-open; next call is a trial.");
        });

// Define a timeout policy: timeout after 15 seconds
var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(15));
// ---- END: Polly Policy Definitions ----


// ---- START: Configure HttpClient for YARP with Polly ----
// This configures the default HttpClient that YARP's forwarder will use.
// Polly handlers are added to the HttpClient pipeline here.
// The order of AddPolicyHandler matters: the first one added is the outermost handler.
// A common order is Timeout -> Retry -> CircuitBreaker.
builder.Services.AddHttpClient(Options.DefaultName) // Options.DefaultName targets the default HttpClient configuration
                                                    // .AddPolicyHandler(timeoutPolicy) // Uncomment if you want Polly's timeout. Be mindful of YARP's own timeouts.
    .AddPolicyHandler(retryPolicy) // Retry policy will be outside the circuit breaker
    .AddPolicyHandler(circuitBreakerPolicy); // Circuit breaker will be inside the retry
                                             // If you need to configure the primary HttpMessageHandler (e.g., SocketsHttpHandler) for the default client globally,
                                             // you can do it here:
                                             // .ConfigurePrimaryHttpMessageHandler(() =>
                                             // {
                                             //     return new SocketsHttpHandler
                                             //     {
                                             //         MaxConnectionsPerServer = 10,
                                             //         // Other SocketsHttpHandler settings
                                             //     };
                                             // });
                                             // ---- END: Configure HttpClient for YARP with Polly ----


// Add reverse proxy capabilities
// Ensure you have the YARP NuGet package: Microsoft.ReverseProxy
// And Polly for resilience: Polly.Extensions.Http
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    // ConfigureHttpClient on IReverseProxyBuilder is used for fine-grained SocketsHttpHandler settings,
    // potentially per-cluster or per-route, based on the ForwarderHttpClientContext.
    // These settings apply to the SocketsHttpHandler *within* the HttpClient pipeline
    // (i.e., after Polly handlers).
    .ConfigureHttpClient((context, socketsHttpHandler) => // 'socketsHttpHandler' is an instance of SocketsHttpHandler
    {
        // Example: You can customize SocketsHttpHandler properties based on context
        // if (context.Cluster?.ClusterId == "mySpecificCluster")
        // {
        //     socketsHttpHandler.ConnectTimeout = TimeSpan.FromSeconds(5);
        // }
        // socketsHttpHandler.MaxConnectionsPerServer = 20; // General setting
    });


// Add correlation ID middleware
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CorrelationIdMiddleware>();

// Configure OpenAPI for documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapGet("/", (HttpContext ctx) =>
{
    var port = ctx.Connection.LocalPort;
    return $"API Gateway is running on port {port}!";
});

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
