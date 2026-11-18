using System.Collections.Concurrent;
using WebMPS.Core.Interfaces.Services;

namespace WebMPS.Infrastructure.Services;

public class FileSystemPdfStorageService : IPdfStorageService
{
    private readonly string _storageDir;
    private readonly ConcurrentDictionary<string, byte[]> _originalPdfCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte[]> _splitPdfCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<FileSystemPdfStorageService> _logger;

    public FileSystemPdfStorageService(IWebHostEnvironment env, ILogger<FileSystemPdfStorageService> logger)
    {
        _storageDir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_storageDir);
        _logger = logger;
    }

    public void SaveOriginalPdf(string fileName, byte[] pdfBytes)
    {
        _originalPdfCache[fileName] = pdfBytes;
        _splitPdfCache.TryRemove(fileName, out _);

        try
        {
            var filePath = Path.Combine(_storageDir, fileName);
            File.WriteAllBytes(filePath, pdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write original PDF to disk: {FileName}", fileName);
        }
    }

    public byte[]? GetOriginalPdf(string fileName)
    {
        if (_originalPdfCache.TryGetValue(fileName, out var cachedBytes))
        {
            return cachedBytes;
        }

        var filePath = Path.Combine(_storageDir, fileName);
        if (File.Exists(filePath))
        {
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                _originalPdfCache[fileName] = bytes;
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read original PDF from disk: {FileName}", fileName);
            }
        }

        return null;
    }

    public byte[]? GetCachedSplitPdf(string fileName)
    {
        _splitPdfCache.TryGetValue(fileName, out var cached);
        return cached;
    }

    public void CacheSplitPdf(string fileName, byte[] splitBytes)
    {
        _splitPdfCache[fileName] = splitBytes;
    }

    public void InvalidateSplitPdf(string fileName)
    {
        _splitPdfCache.TryRemove(fileName, out _);
    }

    public void Clear()
    {
        _originalPdfCache.Clear();
        _splitPdfCache.Clear();
    }
}
