using Microsoft.AspNetCore.DataProtection;
using SanalPOS.Application.Common.Interfaces;

namespace SanalPOS.Infrastructure.Security;

/// <summary>
/// Hassas alanları (webhook secret, banka API anahtarı) .NET Data Protection API'si ile
/// uygulama seviyesinde şifreler (bkz. docs/11-guvenlik.md §3).
/// </summary>
public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("SanalPOS.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
