using SanalPOS.Domain.Entities;

namespace SanalPOS.Application.Common.Interfaces;

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(User user, IReadOnlyCollection<string> roles);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}

public sealed record AccessTokenResult(string Token, string Jti, DateTime ExpiresAtUtc);

public interface IPasswordHasherService
{
    string Hash(string password);
    bool Verify(string passwordHash, string providedPassword);
}

/// <summary>Hassas alan (webhook secret vb.) uygulama seviyesi şifreleme soyutlaması.</summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
