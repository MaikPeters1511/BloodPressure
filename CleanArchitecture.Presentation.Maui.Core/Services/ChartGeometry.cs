namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Koordinatenberechnung des Diagramms (rein, ohne Zeichenoberfläche, damit testbar). Entspricht der Web-Oberfläche.</summary>
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
}
