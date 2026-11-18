namespace WebMPS.Core.Interfaces.Services;

public interface IBarcodeService
{
    string GenerateSvgBarcode(string code, int height = 24);
}
