using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthService.Services.Interfaces;

namespace AuthService.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateToken(string userId, string email, string name, string provider)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var issuedAt = DateTime.UtcNow;
        var jwtExpiryMinutes = _config.GetValue("Jwt:ExpiryMinutes", 60);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId ?? ""),
            new(ClaimTypes.Email, email ?? ""),
            new(ClaimTypes.Name, name ?? ""),
            new("provider", provider),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAt).ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(jwtExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}