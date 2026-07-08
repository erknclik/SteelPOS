using Microsoft.AspNetCore.Identity;
using SanalPOS.Application.Common.Interfaces;
using SanalPOS.Domain.Entities;

namespace SanalPOS.Infrastructure.Security;

/// <summary>ASP.NET Core Identity PasswordHasher (PBKDF2) sarmalayıcısı.</summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
