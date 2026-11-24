using LocalInvoiceManager.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LocalInvoiceManager.Infrastructure.Services;

public sealed class LocalFileStore(IOptions<StorageOptions> options) : IFileStore
{
    private readonly string _root = Path.GetFullPath(options.Value.DocumentsPath);

    public async Task<string> SaveAsync(string relativeFolder, string fileName, Stream content, CancellationToken ct)
    {
        var safeName = Path.GetFileName(fileName);
        var relative = Path.Combine(relativeFolder, safeName);
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return relative.Replace('\\', '/');
    }

    public Stream? OpenRead(string relativePath)
    {
        var full = Resolve(relativePath);
        return File.Exists(full) ? File.OpenRead(full) : null;
    }

    public void Delete(string relativePath)
    {
        var full = Resolve(relativePath);
        if (File.Exists(full)) File.Delete(full);
    }

    /// <summary>Resolves a stored path and refuses anything that escapes the documents folder.</summary>
    private string Resolve(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid file path.");
        return full;
    }
}
