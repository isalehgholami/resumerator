using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization.Metadata;
using ResumeMaker.Core;
using ResumeMaker.Infrastructure;
using ResumeMaker.Web;
using ResumeMaker.Web.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"))
    && !args.Any(a => a.StartsWith("--urls", StringComparison.Ordinal)))
{
    builder.WebHost.UseUrls("http://localhost:5000");
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        ResumeMakerJsonContext.Default,
        WebJsonContext.Default);
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var services = new AppServices(InfrastructureFactory.Create());
builder.Services.AddSingleton(services);
builder.Services.AddSingleton(services.Infra);
builder.Services.AddSingleton(services.Profiles);
builder.Services.AddSingleton(services.Templates);
builder.Services.AddSingleton(services.Lead);
builder.Services.AddSingleton(services.CoverLetters);
builder.Services.AddSingleton(services.Export);
builder.Services.AddSingleton(services.Validation);

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ErrorMappingMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

// Linked templates/fonts are copied next to the binary (bin/publish wwwroot); serve them when the content root
// wwwroot (e.g. `dotnet run` from source) does not contain them.
var binWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(binWwwroot)
    && !string.Equals(Path.GetFullPath(binWwwroot), Path.GetFullPath(app.Environment.WebRootPath ?? ""), StringComparison.OrdinalIgnoreCase))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(binWwwroot) });
}

var api = app.MapGroup("/api");
HealthEndpoints.Map(api);
ProfileEndpoints.Map(api);
TemplateEndpoints.Map(api);
LeadEndpoints.Map(api);
RenderEndpoints.Map(api);
ExportEndpoints.Map(api);
SettingsEndpoints.Map(api);

app.Run();

public partial class Program;
