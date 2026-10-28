using ResumeMaker.Core.Application.Dtos;
using ResumeMaker.Core.Domain;
using ResumeMaker.Infrastructure.Fonts;
using ResumeMaker.Infrastructure.Templating;
using Scriban.Runtime;

namespace ResumeMaker.Infrastructure.Tests;

public class ModelBuilderTests
{
    [Fact]
    public void Build_FiltersSortsAndEscapes()
    {
        var m = TemplateModelBuilder.Build(Samples.Profile(), "R", "B");
        var profile = (ScriptObject)m["profile"];
        var sections = (ScriptArray)profile["sections"];
        Assert.Equal(2, sections.Count); // disabled omitted
        Assert.Equal("skills", ((ScriptObject)sections[0])["type"]);
        Assert.Equal("experience", ((ScriptObject)sections[1])["type"]);
        Assert.Equal("Pioneer &lt;b&gt;of&lt;/b&gt; computing &amp; more", profile["summary"]);
        var item = (ScriptObject)((ScriptArray)((ScriptObject)sections[1])["items"])[0]!;
        Assert.Equal("Babbage &amp; Co", item["company"]);
        Assert.Equal("Did &quot;things&quot;", item["description"]);
        Assert.False(item.ContainsKey("location")); // null omitted
        Assert.Equal(["math", "c#"], ((ScriptArray)item["tags"]).Cast<string>().ToArray());
        var contact = (ScriptObject)profile["contact"];
        Assert.False(contact.ContainsKey("phone")); // empty omitted
        Assert.Equal("https://github.com/ada", ((ScriptObject)contact["links"])["github"]);
    }

    [Fact]
    public void Build_Globals()
    {
        var fa = TemplateModelBuilder.Build(Samples.Profile("fa") with { Theme = new ThemeOverrides { PrimaryColor = "red;}</style>" } }, "R", "B");
        Assert.Equal("rtl", fa["direction"]);
        Assert.Equal("fa", fa["lang"]);
        Assert.Equal("Vazirmatn", fa["font_family"]);
        Assert.Equal("#1a73e8", fa["primary_color"]); // unsafe value rejected
        Assert.Equal("#ff6b6b", fa["accent_color"]);
        Assert.Equal("اکنون", ((ScriptObject)fa["labels"])["present"]);
        Assert.Equal("B", fa["font_bold_base64"]);

        var en = TemplateModelBuilder.Build(Samples.Profile("en") with { Theme = new ThemeOverrides { PrimaryColor = "#123456", FontFamily = "Roboto" } }, "R", "B");
        Assert.Equal("ltr", en["direction"]);
        Assert.Equal("Roboto", en["font_family"]);
        Assert.Equal("#123456", en["primary_color"]);
        Assert.Equal("Present", ((ScriptObject)en["labels"])["present"]);
        Assert.False(en.ContainsKey("letter"));
    }

    [Fact]
    public void Build_CoverLetterExtras()
    {
        var letter = new CoverLetter { Company = "A&B", Position = "Dev", Body = "One <1>\n\n  \nTwo\r\n\r\nThree" };
        var m = TemplateModelBuilder.Build(Samples.Profile(), "R", "B", letter);
        Assert.Equal("A&amp;B", ((ScriptObject)m["letter"])["company"]);
        var paras = ((ScriptArray)m["paragraphs"]).Cast<string>().ToArray();
        Assert.Equal(["One &lt;1&gt;", "Two", "Three"], paras);
    }

    [Fact]
    public void Escape_Works() => Assert.Equal("&lt;a href=&quot;x&quot;&gt;&amp;&#39;", TemplateModelBuilder.Escape("<a href=\"x\">&'"));
}

public class EngineTests
{
    private readonly ScribanTemplateEngine _engine = new(new FakeFonts());

    private Task<string> R(string tpl, Profile? p = null) => _engine.RenderAsync(tpl, p ?? Samples.Profile());

    [Fact]
    public async Task Output_Paths_AndEscaping()
    {
        Assert.Equal("Ada Lovelace|en|ltr|REG64|BOLD64|#1a73e8",
            await R("{{ profile.contact.full_name }}|{{ lang }}|{{ direction }}|{{ font_base64 }}|{{ font_bold_base64 }}|{{ primary_color }}"));
        Assert.Equal("Pioneer &lt;b&gt;of&lt;/b&gt; computing &amp; more", await R("{{ profile.summary }}"));
        Assert.Equal("Summary", await R("{{ labels.summary }}"));
    }

    [Fact]
    public async Task For_If_Else_Negation_Equality()
    {
        const string tpl =
            "{{ for s in profile.sections }}[{{ s.type }}:" +
            "{{ if s.type == \"experience\" }}E{{ else }}N{{ end }}" +
            "{{ if s.type != \"skills\" }}!S{{ end }}" +
            "{{ for it in s.items }}<{{ if it.company }}{{ it.company }}{{ else }}-{{ end }}" +
            "{{ if !it.tags }}/nt{{ end }}{{ for t in it.tags }}#{{ t }}{{ end }}>{{ end }}]{{ end }}";
        var o = await R(tpl);
        Assert.Equal("[skills:N<-/nt>][experience:E!S<Babbage &amp; Co#math#c#><Acme/nt>]", o);
    }


    [Fact]
    public async Task AbsentValues_RenderEmpty_AndFalsy()
    {
        Assert.Equal("[]", await R("[{{ profile.contact.phone }}]"));
        Assert.Equal("no", await R("{{ if profile.contact.phone }}yes{{ else }}no{{ end }}"));
        Assert.Equal("ok", await R("{{ if !profile.contact.phone }}ok{{ end }}"));
        Assert.Equal("[]", await R("[{{ nonexistent.deep.path }}]"));
    }

    [Fact]
    public async Task Item_End_Start_Keywords_Work()
    {
        Assert.Equal("2020-2023", await R("{{ for s in profile.sections }}{{ if s.type == \"experience\" }}{{ for i in s.items }}{{ if i.end }}{{ i.start }}-{{ i.end }}{{ end }}{{ end }}{{ end }}{{ end }}"));
    }

    [Fact]
    public async Task Digits_Filter_PersianOnlyForFa()
    {
        var en = await R("{{ \"a1b2\" | digits }}");
        Assert.Equal("a1b2", en);
        var fa = await R("{{ profile.contact.email | digits }}{{ \"2020\" | digits }}", Samples.Profile("fa") with { Contact = new ContactInfo { FullName = "x", Email = "a1" } });
        Assert.Equal("a۱۲۰۲۰", fa);
    }

    [Fact]
    public async Task Fa_UsesRtlAndPersianLabels()
    {
        Assert.Equal("rtl|fa|اکنون", await R("{{ direction }}|{{ lang }}|{{ labels.present }}", Samples.Profile("fa")));
    }

    [Fact]
    public async Task ParseError_ThrowsTemplateRenderException()
    {
        var ex = await Assert.ThrowsAsync<TemplateRenderException>(() => R("{{ if x }}unclosed"));
        Assert.NotEmpty(ex.Errors);
        await Assert.ThrowsAsync<TemplateRenderException>(() => R("{{ for x in }}"));
    }

    [Fact]
    public async Task Include_IsNotAllowed()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => R("{{ include 'secret.txt' }}"));
    }

    [Fact]
    public async Task LoopLimit_Stops_RunawayTemplates()
    {
        await Assert.ThrowsAsync<TemplateRenderException>(() => R("{{ for i in 1..1000000 }}x{{ end }}"));
    }

    [Fact]
    public async Task CoverLetter_Renders()
    {
        var letter = new CoverLetter { Company = "Acme", Position = "Dev", Body = "Hello\n\nWorld" };
        var o = await _engine.RenderCoverLetterAsync(
            "{{ letter.company }}/{{ letter.position }}{{ for p in paragraphs }}<p>{{ p }}</p>{{ end }}", Samples.Profile(), letter);
        Assert.Equal("Acme/Dev<p>Hello</p><p>World</p>", o);
    }

    [Fact]
    public async Task Cache_ReusesParsedTemplate_AndIsConsistent()
    {
        var a = await R("{{ profile.contact.full_name }}");
        var b = await R("{{ profile.contact.full_name }}", Samples.Profile(name: "Other"));
        Assert.Equal("Ada Lovelace", a);
        Assert.Equal("Other", b);
    }
}

public class FontTests
{
    [Theory]
    [InlineData("fa", false)]
    [InlineData("fa", true)]
    [InlineData("en", false)]
    [InlineData("en", true)]
    public async Task ReturnsValidWoff2(string lang, bool bold)
    {
        var b64 = await new EmbeddedFontProvider().GetFontBase64Async(lang, bold);
        var bytes = Convert.FromBase64String(b64);
        Assert.Equal("wOF2", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task EnglishBold_IsRegular()
    {
        var p = new EmbeddedFontProvider();
        Assert.Equal(await p.GetFontBase64Async("en"), await p.GetFontBase64Async("en", true));
        Assert.NotEqual(await p.GetFontBase64Async("fa"), await p.GetFontBase64Async("fa", true));
    }
}
