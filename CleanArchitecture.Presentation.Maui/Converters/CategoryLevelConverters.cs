using System.Globalization;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Converters;

/// <summary>Hintergrundfarbe des Kategorie-Badges. Der Text trägt die Information, die Farbe verstärkt sie nur (Kontrast ≥ 4,5:1).</summary>
public sealed class CategoryBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        CategoryLevel.Normal => Color.FromArgb("#146C43"),
        CategoryLevel.Elevated => Color.FromArgb("#087990"),
        CategoryLevel.Warning => Color.FromArgb("#FFC107"),
        CategoryLevel.High => Color.FromArgb("#B02A37"),
        _ => Color.FromArgb("#495057")
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CategoryTextColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CategoryLevel.Warning ? Colors.Black : Colors.White;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
