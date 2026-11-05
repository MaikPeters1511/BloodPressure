using System.Globalization;
using System.Resources;
using CleanArchitecture.Presentation.UI;
using Microsoft.Extensions.Localization;

namespace CleanArchitecture.UI.Tests;

/// <summary>
/// Liefert die Texte immer in der gewählten Kultur, unabhängig von der Kultur des Threads, auf dem gerade gerendert wird
/// (bUnit rendert auf eigenen Threads; deren Kultur ist die Systemkultur des Rechners). Mit einer Funktion folgt sie einem Sprachwechsel.
/// </summary>
public sealed class FixedCultureLocalizer(Func<CultureInfo> culture) : IStringLocalizer<SharedResource>
{
    private static readonly ResourceManager Resources =
        new("CleanArchitecture.Presentation.UI.SharedResource", typeof(SharedResource).Assembly);

    public FixedCultureLocalizer(CultureInfo fixedCulture) : this(() => fixedCulture)
    {
    }

    public LocalizedString this[string name] => this[name, []];

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var current = culture();
            var value = Resources.GetString(name, current);
            return new LocalizedString(name, value is null ? name : string.Format(current, value, arguments), resourceNotFound: value is null);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
