using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ResumeMaker.Web.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string DataDir { get; } = Path.Combine(Path.GetTempPath(), "rm-web-tests-" + Guid.NewGuid().ToString("N"));

    public ApiFactory()
    {
        // Read by Program at startup (InfrastructureFactory.Create -> AppPaths.Default).
        Environment.SetEnvironmentVariable("RESUMEMAKER_DATA", DataDir);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(DataDir, true); } catch (IOException) { }
    }
}

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<string> SampleProfileJson()
    {
        var json = await _client.GetStringAsync("/templates/sample-profile.en.json");
        Assert.Contains("Daniel Carter", json);
        return json;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var res = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Csp_And_SecurityHeaders_ArePresent()
    {
        var res = await _client.GetAsync("/api/health");
        Assert.Equal(
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; "
                + "font-src 'self' data:; connect-src 'self'; frame-src 'self' blob: data:",
            res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", res.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task StaticFiles_Templates_And_Fonts_AreServed()
    {
        var t = await _client.GetAsync("/templates/minimal.html");
        Assert.Equal(HttpStatusCode.OK, t.StatusCode);
        var f = await _client.GetAsync("/fonts/vazir.woff2");
        Assert.Equal(HttpStatusCode.OK, f.StatusCode);
    }

    [Fact]
    public async Task Profile_Crud_Roundtrip()
    {
        var create = await _client.PostAsync("/api/profiles", Json("""{"name":"Round Trip","language":"en"}"""));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetGuid();

        var get = await _client.GetAsync($"/api/profiles/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadAsStringAsync();
        Assert.Contains("Round Trip", body);

        var updated = body.Replace("Round Trip", "Renamed");
        var put = await _client.PutAsync($"/api/profiles/{id}", Json(updated));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Contains("Renamed", await (await _client.GetAsync($"/api/profiles/{id}")).Content.ReadAsStringAsync());

        var list = await _client.GetStringAsync("/api/profiles");
        Assert.Contains(id.ToString(), list);

        var dup = await _client.PostAsync($"/api/profiles/{id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, dup.StatusCode);
        using var dupDoc = JsonDocument.Parse(await dup.Content.ReadAsStringAsync());
        Assert.NotEqual(id, dupDoc.RootElement.GetProperty("id").GetGuid());

        var export = await _client.GetAsync($"/api/profiles/{id}/export");
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/profiles/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/profiles/{id}")).StatusCode);
    }

    [Fact]
    public async Task Templates_List_HasSixBuiltIns()
    {
        var res = await _client.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(6, doc.RootElement.EnumerateArray().Count(t => t.GetProperty("is_built_in").GetBoolean()));
    }

    [Fact]
    public async Task Render_ReturnsHtmlWithName()
    {
        var sample = await SampleProfileJson();
        var res = await _client.PostAsync("/api/render", Json($$"""{"profile":{{sample}},"template_id":"minimal"}"""));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/html", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Daniel Carter", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Render_UnknownTemplate_Is404()
    {
        var sample = await SampleProfileJson();
        var res = await _client.PostAsync("/api/render", Json($$"""{"profile":{{sample}},"template_id":"nope"}"""));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task CustomTemplate_WithScript_IsRejected422()
    {
        var html = "<html><body><script>alert(1)</script>{{ profile.contact.full_name }}</body></html>";
        var payload = JsonSerializer.Serialize(new { name = "Evil", html });
        var res = await _client.PostAsync("/api/templates/custom", Json(payload));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("issues").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Lead_Endpoints_ReturnPrompts()
    {
        var t = await _client.PostAsync("/api/lead/template", Json("""{"style":"modern","language":"en"}"""));
        Assert.Equal(HttpStatusCode.OK, t.StatusCode);
        using var td = JsonDocument.Parse(await t.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(td.RootElement.GetProperty("prompt").GetString()));

        var sample = await SampleProfileJson();
        var c = await _client.PostAsync(
            "/api/lead/cover-letter",
            Json($$$"""{"profile":{{{sample}}},"options":{"company":"Acme","position":"Engineer","language":"en"}}"""));
        Assert.Equal(HttpStatusCode.OK, c.StatusCode);
        Assert.Contains("Acme", await c.Content.ReadAsStringAsync());

        var s = await _client.GetStringAsync("/api/lead/shortcuts");
        Assert.Contains("ChatGPT", s);
    }

    [Fact]
    public async Task Export_Json_ReturnsProfileFile()
    {
        var sample = await SampleProfileJson();
        var res = await _client.PostAsync("/api/export/json", Json($$"""{"profile":{{sample}},"file_name":"daniel"}"""));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType?.MediaType);
        Assert.Contains("daniel.json", res.Content.Headers.ContentDisposition?.FileNameStar ?? res.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("Daniel Carter", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Export_Pdf_Is200WithPdf_Or503WithoutChrome()
    {
        var sample = await SampleProfileJson();
        var res = await _client.PostAsync("/api/export/pdf", Json($$$"""{"profile":{{{sample}}}}"""));
        if (res.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("application/pdf", res.Content.Headers.ContentType?.MediaType);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
        else
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("message").GetString()));
        }
    }

    [Fact]
    public async Task Settings_GetAndPut()
    {
        var get = await _client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.Equal("system", get.GetProperty("theme").GetString());
        var put = await _client.PutAsync("/api/settings", Json("""{"theme":"dark","ui_language":"en"}"""));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var again = await _client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.Equal("dark", again.GetProperty("theme").GetString());
    }
}
