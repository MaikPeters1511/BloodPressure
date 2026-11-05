using System.Xml.Linq;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class ResourcesTests
{
    [Test]
    public async Task GermanAndEnglish_HaveTheSameKeys()
    {
        var de = Keys("Strings.resx");
        var en = Keys("Strings.en.resx");

        await Assert.That(de.Except(en).ToList()).IsEmpty();
        await Assert.That(en.Except(de).ToList()).IsEmpty();
    }

    [Test]
    public async Task AllKeysUsedInCode_ExistInResources()
    {
        var keys = Keys("Strings.resx");
        var core = Path.Combine(FindRoot(), "CleanArchitecture.Presentation.Maui.Core");
        var used = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), "(?:_text|text|_l|L)\\[\"(\\w+)\"\\]|(?:Format|Get)\\(\"(\\w+)\"")
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value))
            .Distinct();

        await Assert.That(used.Except(keys).ToList()).IsEmpty();
    }

    private static HashSet<string> Keys(string file)
        => XDocument.Load(Path.Combine(FindRoot(), "CleanArchitecture.Presentation.Maui.Core", "Resources", file))
            .Descendants("data").Select(d => d.Attribute("name")!.Value).ToHashSet();

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
