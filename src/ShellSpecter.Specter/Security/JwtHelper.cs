using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ShellSpecter.Specter.Security;

/// <summary>
/// JWT token generation and validation configuration for ShellSpecter.
/// </summary>
public static class JwtHelper
{
    public static string GenerateToken(string username, string secret, int expiryMinutes = 480)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "ShellSpecter",
            audience: "ShellSpecter.Seer",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static TokenValidationParameters GetValidationParameters(string secret) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "ShellSpecter",
        ValidateAudience = true,
        ValidAudience = "ShellSpecter.Seer",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}
