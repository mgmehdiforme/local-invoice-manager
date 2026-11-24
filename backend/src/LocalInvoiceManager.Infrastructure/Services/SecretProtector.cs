using System.Security.Cryptography;
using LocalInvoiceManager.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace LocalInvoiceManager.Infrastructure.Services;

public sealed class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("LocalInvoiceManager.Secrets.v1");

    public string Protect(string plain) => _protector.Protect(plain);

    public string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return null;
        try { return _protector.Unprotect(protectedValue); }
        catch (CryptographicException) { return null; } // keys lost (e.g. restored on a new machine): user must re-enter
    }
}
