using WebMPS.Core.Interfaces.Services;
using WebMPS.Services;

namespace WebMPS.Infrastructure.Services;

public class Code39BarcodeService : IBarcodeService
{
    public string GenerateSvgBarcode(string code, int height = 24)
    {
        return BarcodeHelper.GenerateSvgBarcode(code, height);
    }
}
