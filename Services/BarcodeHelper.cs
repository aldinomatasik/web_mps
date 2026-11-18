using System.Globalization;
using System.Text;

namespace WebMPS.Services;

public static class BarcodeHelper
{
    // Full Code 39 pattern mapping (digits, uppercase A-Z, and standard symbols)
    internal static readonly Dictionary<char, string> Code39Patterns = new()
    {
        { '0', "bwbWBwBwb" }, { '1', "BwbWbwbwB" }, { '2', "bwBWbwbwB" }, { '3', "BwBWbwbwb" },
        { '4', "bwbWBwbwB" }, { '5', "BwbWBwbwb" }, { '6', "bwBWBwbwb" }, { '7', "bwbWbwBwB" },
        { '8', "BwbWbwBwb" }, { '9', "bwBWbwBwb" },
        { 'A', "BwbwbWbwB" }, { 'B', "bwBwbWbwB" }, { 'C', "BwBwbWbwb" }, { 'D', "bwbwBWbwB" },
        { 'E', "BwbwBWbwb" }, { 'F', "bwBwBWbwb" }, { 'G', "bwbwbWBwB" }, { 'H', "BwbwbWBwb" },
        { 'I', "bwBwbWBwb" }, { 'J', "bwbwBWBwb" }, { 'K', "BwbwbwbWB" }, { 'L', "bwBwbwbWB" },
        { 'M', "BwBwbwbWb" }, { 'N', "bwbwBwbWB" }, { 'O', "BwbwBwbWb" }, { 'P', "bwBwBwbWb" },
        { 'Q', "bwbwbwBWB" }, { 'R', "BwbwbwBWb" }, { 'S', "bwBwbwBWb" }, { 'T', "bwbwBwBWb" },
        { 'U', "BWbwbwbwB" }, { 'V', "bWBwbwbwB" }, { 'W', "BWBwbwbwb" }, { 'X', "bWbwBwbwB" },
        { 'Y', "BWbwBwbwb" }, { 'Z', "bWBwBwbwb" },
        { '-', "bWbwbwBwB" }, { '.', "BWbwbwBwb" }, { ' ', "bWBwbwBwb" }, { '$', "bWbWbWbwb" },
        { '/', "bWbWbwbWb" }, { '+', "bWbwbWbWb" }, { '%', "bwbWbWbWb" }, { '*', "bWbwBwBwb" }
    };

    public static string GenerateSvgBarcode(string code, int height = 24)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";

        var inv = CultureInfo.InvariantCulture;
        var textToEncode = "*" + code.Trim().ToUpperInvariant() + "*";
        var sb = new StringBuilder();

        double narrowWidth = 1.1;
        double wideWidth = 2.6;
        double interCharGap = 1.1;

        double totalWidth = 0;
        foreach (char c in textToEncode)
        {
            if (Code39Patterns.TryGetValue(c, out var pattern))
            {
                foreach (char elem in pattern)
                {
                    totalWidth += (elem == 'B' || elem == 'W') ? wideWidth : narrowWidth;
                }
                totalWidth += interCharGap;
            }
        }

        if (totalWidth <= 0) return "";

        string wIntStr = Math.Ceiling(totalWidth).ToString(inv);
        string totalWStr = totalWidth.ToString("0.##", inv);
        string hStr = height.ToString(inv);

        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{wIntStr}\" height=\"{hStr}\" viewBox=\"0 0 {totalWStr} {hStr}\" preserveAspectRatio=\"xMinYMid meet\" shape-rendering=\"crispEdges\" style=\"display:block;margin:0;padding:0;max-width:100%;height:{hStr}px;background:#ffffff;\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

        double currentX = 0;
        foreach (char c in textToEncode)
        {
            if (!Code39Patterns.TryGetValue(c, out var pattern))
                continue;

            for (int i = 0; i < pattern.Length; i++)
            {
                char elem = pattern[i];
                bool isBar = (i % 2 == 0);
                double w = (elem == 'B' || elem == 'W') ? wideWidth : narrowWidth;

                if (isBar)
                {
                    sb.Append($"<rect x=\"{currentX.ToString("0.0", inv)}\" y=\"0\" width=\"{w.ToString("0.0", inv)}\" height=\"{hStr}\" fill=\"#000000\"/>");
                }
                currentX += w;
            }
            currentX += interCharGap;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
