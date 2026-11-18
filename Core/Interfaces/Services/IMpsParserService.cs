using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Services;

public interface IMpsParserService
{
    List<MpsSlip> ParsePdf(Stream pdfStream, string filename);
}
