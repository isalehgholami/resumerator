using System.ComponentModel;
using System.Text;
using ResumeMaker.Desktop.Localization;

namespace ResumeMaker.Desktop.Tests;

[Collection("Desktop")]
public class LocalizerTests
{
    private static Stream Json(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Indexer_Returns_Value_For_Active_Language()
    {
        var loc = new Localizer(code => Json(code == "fa" ? """{"a":"سلام"}""" : """{"a":"hello"}"""), "fa");
        Assert.Equal("سلام", loc["a"]);
        Assert.True(loc.IsRtl);
    }

    [Fact]
    public void Missing_Key_Falls_Back_To_English_Then_To_Key()
    {
        var loc = new Localizer(code => Json(code == "fa" ? """{"b":"ب"}""" : """{"a":"hello","b":"bee"}"""), "fa");
        Assert.Equal("hello", loc["a"]);
        Assert.Equal("ب", loc["b"]);
        Assert.Equal("unknown.key", loc["unknown.key"]);
    }

    [Fact]
    public void SetLanguage_Raises_Item_Indexer_Change_And_Updates_Direction()
    {
        var loc = new Localizer(code => Json(code == "fa" ? """{"a":"سلام"}""" : """{"a":"hello"}"""), "en");
        var changed = new List<string?>();
        loc.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        loc.SetLanguage("fa");

        Assert.Contains("Item[]", changed);
        Assert.Contains(nameof(Localizer.IsRtl), changed);
        Assert.Equal("سلام", loc["a"]);
        Assert.True(loc.IsRtl);

        changed.Clear();
        loc.SetLanguage("fa");
        Assert.Empty(changed);
    }

    [Fact]
    public void Unknown_Language_Normalizes_To_English()
    {
        Assert.Equal("en", Localizer.Normalize("de"));
        Assert.Equal("fa", Localizer.Normalize("FA"));
    }

    [Fact]
    public void Format_Applies_Arguments()
    {
        var loc = new Localizer(_ => Json("""{"x":"Saved to {0}"}"""));
        Assert.Equal("Saved to a.pdf", loc.Format("x", "a.pdf"));
    }

    [Fact]
    public void Embedded_Locales_Load_And_Have_Identical_Key_Sets()
    {
        var asm = typeof(Localizer).Assembly;
        Dictionary<string, string> Load(string code)
        {
            using var s = asm.GetManifestResourceStream($"locales/{code}.json");
            Assert.NotNull(s);
            return Localizer.Parse(s);
        }
        var en = Load("en");
        var fa = Load("fa");
        Assert.True(en.Count > 50);
        Assert.Empty(en.Keys.Except(fa.Keys));
        Assert.Empty(fa.Keys.Except(en.Keys));
        Assert.All(fa.Values, v => Assert.False(string.IsNullOrWhiteSpace(v)));
    }

    [Fact]
    public void Instance_Switches_Between_Embedded_Languages()
    {
        var loc = Localizer.Instance;
        try
        {
            loc.SetLanguage("en");
            Assert.Equal("Profiles", loc["nav.profiles"]);
            loc.SetLanguage("fa");
            Assert.Equal("پروفایل‌ها", loc["nav.profiles"]);
        }
        finally
        {
            loc.SetLanguage("en");
        }
    }
}
