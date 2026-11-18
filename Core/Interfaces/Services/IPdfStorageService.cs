namespace WebMPS.Core.Interfaces.Services;

public interface IPdfStorageService
{
    void SaveOriginalPdf(string fileName, byte[] pdfBytes);
    byte[]? GetOriginalPdf(string fileName);
    byte[]? GetCachedSplitPdf(string fileName);
    void CacheSplitPdf(string fileName, byte[] splitBytes);
    void InvalidateSplitPdf(string fileName);
    void Clear();
}
