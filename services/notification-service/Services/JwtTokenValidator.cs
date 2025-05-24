using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace notification_service.Services;

public interface ITokenValidator
{
    Task<int?> ValidateTokenAsync(string token);
}

public class JwtTokenValidator : ITokenValidator
{
    private readonly IConfiguration _configuration;

    public JwtTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<int?> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Token is null or empty");
            return Task.FromResult<int?>(null);
        }

        // Basic JWT format validation
        var tokenParts = token.Split('.');
        if (tokenParts.Length != 3)
        {
            Console.WriteLine($"Invalid JWT format. Expected 3 parts, got {tokenParts.Length}");
            return Task.FromResult<int?>(null);
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? 
                throw new InvalidOperationException("JWT Key not configured"));
            
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            
            // Log all claims for debugging
            Console.WriteLine("JWT Token Claims:");
            foreach (var claim in jwtToken.Claims)
            {
                Console.WriteLine($"{claim.Type}: {claim.Value}");
            }
            
            // Try to get user ID from different possible claim types
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => 
                x.Type == "uid" || 
                x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                x.Type == ClaimTypes.NameIdentifier);
                
            if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value) || !int.TryParse(userIdClaim.Value, out int userIdInt))
            {
                Console.WriteLine($"User ID claim not found or invalid in token. Claims searched: uid, nameidentifier, NameIdentifier");
                return Task.FromResult<int?>(null);
            }
            
            return Task.FromResult<int?>(userIdInt);
        }
        catch (SecurityTokenExpiredException)
        {
            Console.WriteLine("Token has expired");
            return Task.FromResult<int?>(null);
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            Console.WriteLine("Token signature validation failed");
            return Task.FromResult<int?>(null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token validation failed: {ex.GetType().Name} - {ex.Message}");
            return Task.FromResult<int?>(null);
        }
    }
}
