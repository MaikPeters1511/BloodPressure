using System.Globalization;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Berechnet die Koordinaten des SVG-Diagramms (rein, ohne DOM, damit testbar).</summary>
public static class ChartGeometry
{
    /// <summary>Gemeinsamer Wertebereich aller Reihen, mit etwas Luft nach oben und unten.</summary>
    public static (double Min, double Max) Range(IEnumerable<IReadOnlyList<double>> series)
    {
        var all = series.SelectMany(s => s).ToList();
        if (all.Count == 0)
        {
            return (0, 1);
        }

        var min = Math.Floor(all.Min() / 10) * 10 - 10;
        var max = Math.Ceiling(all.Max() / 10) * 10 + 10;
        return (Math.Max(0, min), max);
    }

    public static double X(int index, int count, double left, double width)
        => count <= 1 ? left + width / 2 : left + width * index / (count - 1);

    public static double Y(double value, double min, double max, double top, double height)
        => top + height - (value - min) / (max - min) * height;

    /// <summary>Punktliste für ein <c>polyline</c>-Element (invariante Kultur).</summary>
    public static string Points(IReadOnlyList<double> values, double min, double max, double left, double top, double width, double height)
        => string.Join(' ', values.Select((v, i) => string.Create(CultureInfo.InvariantCulture,
            $"{Math.Round(X(i, values.Count, left, width), 1)},{Math.Round(Y(v, min, max, top, height), 1)}")));
}
