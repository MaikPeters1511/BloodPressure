using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CleanArchitecture.Maui.Tests;

/// <summary>
/// Statische Barrierefreiheitsprüfung der MAUI-XAML-Dateien (läuft in der CI, ohne Gerät). Sie ersetzt keinen Screenreader-Test,
/// fängt aber die häufigsten Fehler ab: fehlende Beschriftungen, zu kleine Bedienelemente, fehlende Überschriften und
/// hartcodierte Texte.
/// </summary>
[Category("Unit")]
public class XamlAccessibilityTests
{
    private static readonly XNamespace Maui = "http://schemas.microsoft.com/dotnet/2021/maui";

    private static readonly string[] Labeled = ["Entry", "Picker", "DatePicker", "TimePicker", "Switch"];
    private static readonly string[] MinHeight = ["Entry", "Picker", "DatePicker", "TimePicker", "Button"];

    private static IEnumerable<(string File, XElement Root)> Pages()
    {
        var views = Path.Combine(FindRoot(), "CleanArchitecture.Presentation.Maui", "Views");
        foreach (var file in Directory.GetFiles(views, "*.xaml"))
        {
            yield return (Path.GetFileName(file), XDocument.Load(file).Root!);
        }
    }

    private static string? Attr(XElement element, string name)
        => element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;

    [Test]
    public async Task InputControls_HaveScreenreaderDescription()
    {
        var missing = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => e.Name.Namespace == Maui && Labeled.Contains(e.Name.LocalName))
            .Where(e => string.IsNullOrWhiteSpace(Attr(e, "SemanticProperties.Description")))
            .Select(e => $"{p.File}: {e.Name.LocalName}")).ToList();

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task Buttons_HaveVisibleTextOrDescription()
    {
        var missing = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => e.Name.Namespace == Maui && e.Name.LocalName == "Button")
            .Where(e => string.IsNullOrWhiteSpace(Attr(e, "Text")) && string.IsNullOrWhiteSpace(Attr(e, "SemanticProperties.Description")))
            .Select(e => $"{p.File}: Button")).ToList();

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task InteractiveControls_AreAtLeast44PointsHigh()
    {
        var tooSmall = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => e.Name.Namespace == Maui && MinHeight.Contains(e.Name.LocalName))
            .Where(e => !double.TryParse(Attr(e, "MinimumHeightRequest"), System.Globalization.CultureInfo.InvariantCulture, out var h) || h < 44)
            .Select(e => $"{p.File}: {e.Name.LocalName} {Attr(e, "Text") ?? Attr(e, "SemanticProperties.Description")}")).ToList();

        await Assert.That(tooSmall).IsEmpty();
    }

    [Test]
    public async Task Headlines_HaveHeadingLevel()
    {
        var missing = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => e.Name.Namespace == Maui && e.Name.LocalName == "Label")
            .Where(e => Attr(e, "Style") is { } style && (style.Contains("Headline") || style.Contains("SubHeadline")))
            .Where(e => string.IsNullOrWhiteSpace(Attr(e, "SemanticProperties.HeadingLevel")))
            .Select(e => $"{p.File}: {Attr(e, "Text")}")).ToList();

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task EveryPage_HasTitle_AndExactlyOneLevel1Heading()
    {
        var problems = new List<string>();
        foreach (var (file, root) in Pages())
        {
            if (string.IsNullOrWhiteSpace(Attr(root, "Title")))
            {
                problems.Add($"{file}: kein Title");
            }

            var level1 = root.Descendants().Count(e => Attr(e, "SemanticProperties.HeadingLevel") == "Level1");
            if (level1 != 1)
            {
                problems.Add($"{file}: {level1} Überschriften der Ebene 1");
            }
        }

        await Assert.That(problems).IsEmpty();
    }

    [Test]
    public async Task VisibleTexts_AreLocalized()
    {
        // Erlaubt: Bindungen/Markup-Erweiterungen und reine Trenn-/Einheitenzeichen (": ", " / ", " mmHg", "×").
        var allowed = new Regex(@"^[\s\p{P}\p{S}]*(mmHg)?[\s\p{P}\p{S}]*$");
        var hardcoded = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => e.Name.Namespace == Maui && e.Name.LocalName is "Label" or "Button" or "Span" or "ToolbarItem" or "ContentPage")
            .SelectMany(e => new[] { "Text", "Title" }.Select(n => (Element: e, Value: Attr(e, n))))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value) && !x.Value!.TrimStart().StartsWith('{') && !allowed.IsMatch(x.Value))
            .Select(x => $"{p.File}: \"{x.Value}\"")).ToList();

        await Assert.That(hardcoded).IsEmpty();
    }

    [Test]
    public async Task ColorOnlyInformation_HasTextAlternative()
    {
        // Kategorie-Badges: Hintergrundfarbe kommt aus dem Converter; der Text daneben trägt die Information.
        var badges = Pages().SelectMany(p => p.Root.Descendants()
            .Where(e => Attr(e, "BackgroundColor")?.Contains("CategoryBackgroundConverter") == true)
            .Select(e => (p.File, Badge: e))).ToList();

        foreach (var (file, badge) in badges)
        {
            var label = badge.Descendants().FirstOrDefault(e => e.Name.LocalName == "Label");
            await Assert.That(label is not null && Attr(label, "Text")?.Contains("Category") == true).IsTrue();
        }

        await Assert.That(badges.Count).IsGreaterThan(0);
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}
