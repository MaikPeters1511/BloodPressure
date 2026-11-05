using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Localization;

/// <summary>Zugriff auf die Texte (<c>Resources/Strings.resx</c>) aus XAML: <c>{loc:Translate Login_Title}</c>.</summary>
public static class Loc
{
    public static ILocalizer? Localizer { get; set; }
}

[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<string>
{
    public string Key { get; set; } = "";

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.Localizer?[Key] ?? Key;

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
