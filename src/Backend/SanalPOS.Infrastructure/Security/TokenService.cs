using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;

namespace SanalPOS.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration) => _configuration = configuration;

    public AccessTokenResult GenerateAccessToken(User user, IReadOnlyCollection<string> roles)
    {
        var jti = Guid.NewGuid().ToString("N");
        var expiryMinutes = _configuration.GetValue("Jwt:AccessTokenExpiryMinutes", 15);
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new("full_name", user.FullName)
        };
        if (user.MerchantId is not null)
            claims.Add(new Claim("merchant_id", user.MerchantId.Value.ToString()));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey()));
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), jti, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private string GetSigningKey() =>
        _configuration["Jwt:SigningKey"]
        ?? throw new InvalidOperationException(
            "Jwt:SigningKey tanımlı değil. Geliştirmede 'dotnet user-secrets set Jwt:SigningKey <değer>', " +
            "prod ortamında Key Vault/ortam değişkeni kullanın (bkz. docs/14-konfigurasyon-yonetimi.md §6).");
}
