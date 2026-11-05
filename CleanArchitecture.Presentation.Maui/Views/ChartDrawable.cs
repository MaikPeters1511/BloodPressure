using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

/// <summary>Zeichnet den Verlauf der Durchschnittswerte; Reihen unterscheiden sich durch Farbe und Linienart.</summary>
public sealed class ChartDrawable : IDrawable
{
    public IReadOnlyList<ChartSeriesData> Series { get; set; } = [];

    public IReadOnlyList<string> Labels { get; set; } = [];

    public void Draw(ICanvas canvas, RectF rect)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var text = dark ? Color.FromArgb("#F1F3F5") : Color.FromArgb("#212529");
        Color[] colors = dark
            ? [Color.FromArgb("#6EA8FE"), Color.FromArgb("#EA868F"), Color.FromArgb("#75B798")]
            : [Color.FromArgb("#0A58CA"), Color.FromArgb("#B02A37"), Color.FromArgb("#146C43")];

        const float left = 40, top = 8, bottom = 24, right = 8;
        var width = rect.Width - left - right;
        var height = rect.Height - top - bottom;
        var (min, max) = ChartGeometry.Range(Series.Select(s => s.Values));

        canvas.FontColor = text;
        canvas.FontSize = 11;
        for (var i = 0; i <= 4; i++)
        {
            var value = min + (max - min) * i / 4;
            var y = (float)ChartGeometry.Y(value, min, max, top, height);
            canvas.StrokeColor = text.WithAlpha(0.25f);
            canvas.StrokeSize = 1;
            canvas.StrokeDashPattern = null;
            canvas.DrawLine(left, y, left + width, y);
            canvas.DrawString(Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture), 0, y - 8, left - 6, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
        }

        for (var s = 0; s < Series.Count; s++)
        {
            var series = Series[s];
            canvas.StrokeColor = colors[s % colors.Length];
            canvas.StrokeSize = 2.5f;
            canvas.StrokeDashPattern = series.Style switch
            {
                LineStyle.Dashed => [6, 4],
                LineStyle.Dotted => [1, 3],
                _ => null
            };

            var path = new PathF();
            for (var i = 0; i < series.Values.Count; i++)
            {
                var x = (float)ChartGeometry.X(i, series.Values.Count, left, width);
                var y = (float)ChartGeometry.Y(series.Values[i], min, max, top, height);
                if (i == 0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            canvas.DrawPath(path);
        }

        canvas.StrokeDashPattern = null;
        if (Labels.Count > 0)
        {
            canvas.DrawString(Labels[0], left, rect.Height - bottom + 4, 120, 16, HorizontalAlignment.Left, VerticalAlignment.Top);
            canvas.DrawString(Labels[^1], rect.Width - right - 120, rect.Height - bottom + 4, 120, 16, HorizontalAlignment.Right, VerticalAlignment.Top);
        }
    }
}
