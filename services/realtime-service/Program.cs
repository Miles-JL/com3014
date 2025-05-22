using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Shared.Auth;
using RealtimeService.Handlers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//
// JWT Authentication setup
//
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwt["Key"]!);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };

        // Allow token via WebSocket query parameter: ?access_token=...
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

//
// Service registrations
//
builder.Services.AddScoped<JwtService>();          // Shared JWT validation logic
builder.Services.AddSingleton<DmHandler>();        // WebSocket message handler
builder.Services.AddControllers();                 // (optional) add HTTP API later
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

//
// Swagger UI in development only
//
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//
// Middleware pipeline
//
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)   // Helps detect dropped clients
});

//
// WebSocket endpoint for direct messaging
//
app.Map("/ws/dm", async context =>
{
    var handler = context.RequestServices.GetRequiredService<DmHandler>();
    await handler.HandleWebSocketAsync(context);
});

// Fallback for future HTTP routes
app.MapControllers();

app.Run();
