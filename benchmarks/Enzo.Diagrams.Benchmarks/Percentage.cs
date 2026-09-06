using System.Globalization;

namespace Enzo.Diagrams.Benchmarks;

public static class Percentage
{
    public static double Difference(int enzoValue, int mermaidValue)
    {
        return mermaidValue == 0 ? 0 : ((double)mermaidValue - enzoValue) / mermaidValue * 100;
    }

    public static string Direction(double differencePercent)
    {
        var absolute = Math.Abs(differencePercent).ToString("0.0", CultureInfo.InvariantCulture);

        return differencePercent switch
        {
            > 0 => $"Enzo uses {absolute}% fewer tokens than Mermaid.",
            < 0 => $"Enzo uses {absolute}% more tokens than Mermaid.",
            _ => "Enzo and Mermaid use the same number of tokens."
        };
    }
}
