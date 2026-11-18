using WebMPS.Models;

namespace WebMPS.Core.Interfaces.Services;

public interface IMpsPdfSplitterService
{
    byte[] CreateSplitPdf(Stream sourcePdfStream, IReadOnlyList<MpsSlip>? slips = null);
    byte[] CreateSingleSlipPdf(Stream sourcePdfStream, int pageNumber1Based, int quadrant1To4, MpsSlip? slip = null);
}
