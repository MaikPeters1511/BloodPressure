using System.Globalization;
using System.Resources;

namespace CleanArchitecture.Presentation.Maui.Core.Services;

public interface ILocalizer
{
    CultureInfo Culture { get; }

    string this[string key] { get; }

    string Format(string key, params object[] args);
}

/// <summary>Liest Texte aus <c>Resources/Strings(.en).resx</c> in der gewählten Kultur (Standard: Gerätesprache, sonst Deutsch).</summary>
public sealed class ResxLocalizer : ILocalizer
{
    private static readonly ResourceManager Resources =
        new("CleanArchitecture.Presentation.Maui.Core.Resources.Strings", typeof(ResxLocalizer).Assembly);

    public ResxLocalizer(CultureInfo? culture = null)
    {
        Culture = culture is not null && culture.TwoLetterISOLanguageName == "en" ? new CultureInfo("en") : new CultureInfo("de");
    }

    public CultureInfo Culture { get; }

    public string this[string key] => Resources.GetString(key, Culture) ?? key;

    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);
}
