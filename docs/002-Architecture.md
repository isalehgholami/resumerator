# سند معماری — Resume Maker Minimal
**نسخه:** 1.0  |  **تاریخ:** 28 اکتبر 2026  |  **وضعیت:** پیش‌نویس اولیه  |  **زبان:** فارسی
**سند مرجع:** Discovery-fa.md v1.0

---

## 0. خلاصه اجرایی

این سند معماری، طراحی فنی پروژه **Resume Maker Minimal** را به‌صورت تفصیلی توصیف می‌کند. معماری مبتنی بر الگوی **Clean Architecture** با سه لایه‌ی مجزا (Domain، Application، Infrastructure) است که در یک **Core Library** مشترک (C# Class Library با Native AOT) قرار گرفته. این Core Library توسط دو Frontend مجزا مصرف می‌شود: **Desktop** (Avalonia UI 11 با Native AOT برای ویندوز/لینوکس) و **Web** (ASP.NET Core Minimal API + HTML static برای میزبانی self-host).

الگوی کلیدی نوآوری، **Lead-Based AI Workflow** است: اپ یک پرامپت چندلایه‌ی ساختاریافته تولید می‌کند، کاربر با AI دلخواه خود قالب HTML/CSS تولید می‌کند، و خروجی به اپ وارد می‌شود. این الگو، اپ را از هرگونه وابستگی به SDKهای AI پولی، نیاز به API key، یا نگرانی حریم خصوصی مستقل می‌کند.

رندر از HTML به PDF/PNG با استفاده از **Chrome/Edge headless** (که روی اکثر سیستم‌ها نصب است) انجام می‌شود؛ این یعنی اپ به‌جای باندل کردن موتور رندر سنگین، از موجودی استفاده می‌کند که کاربر احتمالاً آن را دارد. برای کاربران بدون Chrome، fallback به `wkhtmltopdf` در v1.1 برنامه‌ریزی شده است. فونت Vazir به‌صورت embedded (یا base64 inline در HTML static خروجی) همراه می‌شود تا خروجی در هر محیطی بدون نیاز به نصب فونت نمایش درست داشته باشد.

---

## 1. مقدمه و دامنه

### 1.1 هدف سند

هدف این سند، تعریف معماری فنی پروژه به‌گونه‌ای است که توسعه‌دهنده‌ها بتوانند بر اساس آن پیاده‌سازی را شروع کنند. این سند شامل: نمایش معماری، اصول طراحی، جزئیات لایه‌ها، Data Model، Template Engine، Pipeline خروجی، ملاحظات Native AOT، مدیریت فونت،国际化، امنیت، استراتژی تست، Build و CI/CD است. مخاطب این سند، توسعه‌دهنده‌ی سی‌شارپ با دانش متوسط از .NET 9 و Native AOT است.

### 1.2 دامنه

این سند معماری **فاز MVP** را پوشش می‌دهد. Post-MVP فیچرها (QR Code، Multi-Profile، Theme Builder، DOCX Export، Portfolio، LinkedIn OCR، GitHub Activity، Cloud Sync) به‌صورت brief در بخش «مسیر آینده» (Roadmap معماری) اشاره می‌شوند، اما معماری تفصیلی آن‌ها در زمان نزدیک به پیاده‌سازی منتشر می‌شود. هر تغییر در معماری باید با tag `architecture-vN.M` در commit و در این سند منعکس شود.

### 1.3 اصول طراحی کلیدی

سه اصل راهنمای این معماری عبارت‌اند: **(1) مینیمالیسم** - هیچ وابستگی، لایه، یا انتزاع اضافی که لازم نباشد اضافه نشود؛ **(2) Separation of Concerns** - UI، Business Logic، و Infrastructure جدا نگه‌داری شوند تا قابل تست و تعویض باشند؛ **(3) AOT-First** - هر تصمیم فنی با توجه به محدودیت‌های Native AOT گرفته شود، نه به‌عنوان بعدی فکری.

---

## 2. مرور کلی سیستم (System Overview)

### 2.1 نمایش سطح بالا (High-Level View)

```
┌─────────────────────────────────────────────────────────────────┐
│                     کاربر (مریم / علی / زهرا)                     │
└──────────────────────────┬──────────────────────────────────────┘
                           │
       ┌───────────────────┴───────────────────┐
       │                                       │
┌──────▼─────────────┐                 ┌───────▼──────────────┐
│  Desktop Frontend  │                 │   Web Frontend       │
│  (Avalonia + AOT)  │                 │  (ASP.NET + Static)  │
└──────┬─────────────┘                 └───────┬──────────────┘
       │                                       │
       └───────────────────┬───────────────────┘
                           │
              ┌────────────▼────────────┐
              │      Core Library       │
              │  (Domain + Application) │
              │   Native AOT ClassLib   │
              └────────────┬────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼───────┐ ┌────────▼────────┐ ┌────────▼────────┐
│ Template      │ │ Storage         │ │ Export Pipeline │
│ Engine        │ │ (JSON/SQLite)   │ │ (Chrome headless│
│ (Scriban)     │ │                 │ │  + SkiaSharp)   │
└───────────────┘ └─────────────────┘ └─────────────────┘
                           │
              ┌────────────▼────────────┐
              │   OS-Level Resources     │
              │ (Fonts Vazir, File I/O)  │
              └─────────────────────────┘
```

### 2.2 جریان داده‌ی اصلی (Primary Data Flow)

1. کاربر در Frontend (Desktop یا Web) Profile را باز/ایجاد می‌کند.
2. Frontend با Core Library (به‌صورت in-process در Desktop یا به‌صورت local API در Web) تعامل می‌کند.
3. Core Library داده‌ها را در Storage (JSON یا SQLite) ذخیره و می‌خواند.
4. برای preview زنده، Core Library داده‌ها + قالب را به Template Engine (Scriban) می‌دهد و HTML تولید می‌کند.
5. HTML تولیدشده در WebView (Avalonia) یا iframe (Web) رندر می‌شود.
6. برای Export، HTML نهایی به Export Pipeline داده می‌شود که Chrome/Edge headless را اجرا می‌کند و PDF/PNG تولید می‌کند.

### 2.3 نقاط یکپارچه‌سازی (Integration Points)

| نقطه | نوع | توضیح |
|------|------|-------|
| Profile ↔ Storage | Sync, Local | فایل JSON یا SQLite در پوشه کاربر |
| Profile ↔ Template Engine | In-process | Scriban template + JSON data → HTML string |
| Template Engine ↔ Frontend | In-process (HTML string) | رندر در WebView/iframe |
| Export Pipeline ↔ Chrome | Process spawn | `chrome --headless --print-to-pdf` |
| Lead-Based Generator ↔ Clipboard | OS API | Copy پرامپت به clipboard |
| AI External ↔ User | Manual | کاربر به AI می‌رود، HTML را برمی‌گرداند |

---

## 3. اصول معماری و طراحی

### 3.1 Clean Architecture

سه لایه‌ی اصلی به‌صورت زیر تعریف می‌شوند:

- **Domain Layer**: موجودیت‌های کسب‌وکار (Profile، Section، ExperienceItem، Skill، Language، Template) و Value Objectهای مرتبط. این لایه هیچ وابستگی خارجی ندارد و به‌صورت POCO (Plain Old C# Objects) پیاده‌سازی می‌شود. Tallahassee از mutable state پرهیز می‌کند و به‌جای آن Record types با immutability استفاده می‌کند.
- **Application Layer**: Use Caseها و Serviceها (ProfileService، TemplateService، LeadGenerator، ExportService). این لایه Domain را مصرف می‌کند و Interfaceهای Infrastructure (IProfileRepository، ITemplateRepository، IExporter) را تعریف می‌کند. کنترل flow کسب‌وکار در این لایه است.
- **Infrastructure Layer**: پیاده‌سازی interfaceها با تکنولوژی واقعی (JsonProfileRepository، SqliteProfileRepository، ChromeHeadlessExporter، SkiaPngExporter). این لایه می‌تواند بدون تغییر در لایه‌های بالاتر تعویض شود.

### 3.2 اصول SOLID

- **S (Single Responsibility)**: هر کلاس فقط یک علت تغییر دارد. مثلاً TemplateService فقط template را رندر می‌کند، نه اینکه ذخیره‌سازی هم بکند.
- **O (Open/Closed)**: افزودن Exporter جدید (مثلاً DocxExporter در v1.1) بدون تغییر در کد موجود، با پیاده‌سازی IExporter انجام می‌شود.
- **L (Liskov Substitution)**: هر SqliteProfileRepository باید قابل تعویض با JsonProfileRepository باشد بدون شکستن کد.
- **I (Interface Segregation)**: interfaceهای کوچک و تخصصی (IProfileReader، IProfileWriter) به‌جای یک interface بزرگ (IProfileRepository).
- **D (Dependency Inversion)**: لایه‌های بالا به interfaceها وابسته‌اند، نه implementation. DI Container در Composition Root فایل Program.cs انجام می‌شود.

### 3.3 اصول مینیمالیسم

- **یکی از هر چیز**: یک Template engine (Scriban)، یک storage (JSON در MVP)، یک UI framework برای Desktop (Avalonia).
- **حذف abstractions بلااستفاده**: اگر فقط یک پیاده‌سازی از یک interface وجود دارد، interface حذف و کلاس مستقیماً استفاده می‌شود.
- **Configuration via Code**: نه via XML یا JSON file. تمام configuration در یک کلاس `AppOptions` با default values و override از environment variables.
- **No OR/M**: برای MVP، دسترسی به داده با JSON serialization ساده انجام می‌شود. SQLite + Dapper یا EF Core در v1.1 برای Multi-Profile در نظر گرفته می‌شود.

---

## 4. نمایش معماری (Architecture View)

### 4.1 دیاگرام لایه‌ها

```
┌─────────────────────────────────────────────────────────────┐
│                      Composition Root                       │
│  (Program.cs — Desktop: Avalonia, Web: ASP.NET)             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Frontend Layer                          │
│  ┌──────────────────────┐    ┌──────────────────────────┐ │
│  │ Avalonia UI (Desktop)│    │  ASP.NET Minimal API     │ │
│  │ - ViewModels (MVVM)  │    │  + HTML/JS Static Assets │ │
│  │ - Views (XAML)       │    │  - Endpoints              │ │
│  └──────────────────────┘    └──────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Application Layer (Core)                   │
│  ┌─────────────┐ ┌──────────────┐ ┌────────────────────┐ │
│  │ProfileService│ │TemplateService│ │LeadGenerator       │ │
│  │              │ │              │ │                    │ │
│  └─────────────┘ └──────────────┘ └────────────────────┘ │
│  ┌─────────────┐ ┌──────────────┐ ┌────────────────────┐ │
│  │ExportService│ │CoverLetterSvc│ │ValidationService   │ │
│  └─────────────┘ └──────────────┘ └────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer (Core)                      │
│  Entities: Profile, Section, ExperienceItem, Skill,        │
│            Language, Project, Award, Template, Theme        │
│  Value Objects: ContactInfo, DateRange, ProficiencyLevel   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                Infrastructure Layer                         │
│  ┌──────────────┐ ┌──────────────┐ ┌────────────────────┐ │
│  │JsonProfileRep│ │ScribanTplEng │ │ChromeHeadlessExp   │ │
│  │              │ │              │ │                    │ │
│  └──────────────┘ └──────────────┘ └────────────────────┘ │
│  ┌──────────────┐ ┌──────────────┐ ┌────────────────────┐ │
│  │SkiaPngExport │ │FileSystemIO  │ │FontResourceLoader  │ │
│  └──────────────┘ └──────────────┘ └────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 دیاگرام Physical (Deployment)

```
┌─────────────────────────────────────────────────────────────┐
│         ویندوز / لینوکس — دستگاه کاربر                      │
│                                                             │
│  ┌─────────────────┐     ┌──────────────────────────────┐  │
│  │ ResumeMaker.exe │────▶│  %APPDATA%/ResumeMaker/       │  │
│  │  (Native AOT)   │     │   - profiles/*.json           │  │
│  │  ~22 MB         │     │   - templates/custom/*.html   │  │
│  │                 │     │   - settings.json              │  │
│  │  - Avalonia UI  │     │   - backups/*.json            │  │
│  │  - Core Lib     │     └──────────────────────────────┘  │
│  └────────┬────────┘                                       │
│           │ Process spawn                                  │
│           ▼                                                │
│  ┌─────────────────┐                                       │
│  │ chrome.exe      │  (نصب‌شده توسط کاربر)                 │
│  │ --headless      │                                       │
│  │ --print-to-pdf  │                                       │
│  └─────────────────┘                                       │
└─────────────────────────────────────────────────────────────┘
```

برای Web deployment:

```
┌─────────────────────────────────────────────────────────────┐
│         سرور لینوکس — self-host یا Docker                   │
│                                                             │
│  ┌────────────────────┐    ┌──────────────────────────┐    │
│  │ resumemaker-server │───▶│   /var/lib/resumemaker/   │    │
│  │  (Native AOT bin)  │    │     - profiles/*.json      │    │
│  │  - ASP.NET Minimal │    │     - templates/           │    │
│  │  - Static HTML     │    └──────────────────────────┘    │
│  └─────────┬──────────┘                                     │
│            │ Process spawn                                  │
│            ▼                                                │
│  ┌────────────────────┐                                     │
│  │ chromium-browser   │  (نصب‌شده روی سرور)                 │
│  └────────────────────┘                                     │
└─────────────────────────────────────────────────────────────┘
                  ▲
                  │ HTTP
                  ▼
        ┌─────────────────┐
        │ مرورگر کاربر    │
        └─────────────────┘
```

---

## 5. Core Library — جزئیات

### 5.1 ساختار پروژه

```
src/
├── ResumeMaker.Core/                  ← Domain + Application
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── Profile.cs
│   │   │   ├── Section.cs
│   │   │   ├── ExperienceItem.cs
│   │   │   ├── Skill.cs
│   │   │   ├── Language.cs
│   │   │   ├── Project.cs
│   │   │   ├── Award.cs
│   │   │   ├── Template.cs
│   │   │   └── ContactInfo.cs
│   │   ├── ValueObjects/
│   │   │   ├── DateRange.cs
│   │   │   ├── ProficiencyLevel.cs
│   │   │   └── LanguageDirection.cs
│   │   └── Enums/
│   │       ├── TemplateStyle.cs
│   │       ├── SectionType.cs
│   │       └── ExportFormat.cs
│   ├── Application/
│   │   ├── Services/
│   │   │   ├── ProfileService.cs
│   │   │   ├── TemplateService.cs
│   │   │   ├── LeadGenerator.cs
│   │   │   ├── ExportService.cs
│   │   │   ├── CoverLetterService.cs
│   │   │   └── ValidationService.cs
│   │   ├── Interfaces/
│   │   │   ├── IProfileRepository.cs
│   │   │   ├── ITemplateRepository.cs
│   │   │   ├── IExporter.cs
│   │   │   └── IFontResourceProvider.cs
│   │   └── DTOs/
│   │       ├── ProfileDto.cs
│   │       ├── TemplateRenderRequest.cs
│   │       └── ExportRequest.cs
│   └── ResumeMaker.Core.csproj
│
├── ResumeMaker.Infrastructure/       ← Infrastructure implementations
│   ├── Persistence/
│   │   ├── JsonProfileRepository.cs
│   │   └── JsonSettingsStore.cs
│   ├── Templating/
│   │   ├── ScribanTemplateEngine.cs
│   │   └── TemplateValidator.cs
│   ├── Export/
│   │   ├── ChromeHeadlessPdfExporter.cs
│   │   ├── SkiaPngExporter.cs
│   │   ├── HtmlStaticExporter.cs
│   │   └── AtsFriendlyPdfExporter.cs
│   ├── Fonts/
│   │   └── EmbeddedFontProvider.cs
│   └── ResumeMaker.Infrastructure.csproj
│
├── ResumeMaker.Desktop/             ← Avalonia UI Frontend
│   ├── Program.cs                   ← Composition Root (AOT entry)
│   ├── App.axaml.cs
│   ├── ViewModels/
│   ├── Views/
│   └── ResumeMaker.Desktop.csproj
│
└── ResumeMaker.Web/                 ← ASP.NET Frontend
    ├── Program.cs                   ← Composition Root (AOT entry)
    ├── Endpoints/
    │   ├── ProfileEndpoints.cs
    │   ├── TemplateEndpoints.cs
    │   └── ExportEndpoints.cs
    ├── wwwroot/                     ← Static HTML/CSS
    └── ResumeMaker.Web.csproj
```

### 5.2 نمونه‌ی کد — Domain Entity

```csharp
// Domain/Entities/Profile.cs
namespace ResumeMaker.Core.Domain.Entities;

public sealed record Profile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string Language { get; init; } = "fa";   // "fa" یا "en"
    public string TemplateId { get; init; } = "minimal";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public ContactInfo Contact { get; init; } = new();
    public IReadOnlyList<Section> Sections { get; init; } = [];
    public ThemeOverrides Theme { get; init; } = new();
}

public sealed record ThemeOverrides
{
    public string? PrimaryColor { get; init; }     // مثلاً "#1a73e8"
    public string? AccentColor { get; init; }
    public string? FontFamily { get; init; }       // null = default Vazir
}
```

### 5.3 نمونه‌ی کد — Application Service

```csharp
// Application/Services/TemplateService.cs
namespace ResumeMaker.Core.Application.Services;

public sealed class TemplateService(
    ITemplateRepository templateRepo,
    ScribanTemplateEngine engine,
    IFontResourceProvider fontProvider)
{
    public async Task<string> RenderAsync(TemplateRenderRequest req, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(req.TemplateId, ct)
            ?? throw new TemplateNotFoundException(req.TemplateId);

        var model = new TemplateModel
        {
            Profile = req.Profile,
            Theme = req.Profile.Theme,
            FontBase64 = await fontProvider.GetVazirBase64Async(req.Profile.Language, ct),
            Direction = req.Profile.Language == "fa" ? "rtl" : "ltr"
        };

        return await engine.RenderAsync(template.HtmlContent, model, ct);
    }
}
```

### 5.4 نمونه‌ی کد — Lead Generator

```csharp
// Application/Services/LeadGenerator.cs
namespace ResumeMaker.Core.Application.Services;

public sealed class LeadGenerator
{
    public string GenerateTemplateLead(TemplateLeadOptions opts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# System Role");
        sb.AppendLine("You are an expert HTML/CSS resume template designer.");
        sb.AppendLine();
        sb.AppendLine("# Project Brief");
        sb.AppendLine("Design a single-page resume template with the following specs:");
        sb.AppendLine($"- Style: {opts.Style}");
        sb.AppendLine($"- Primary color: {opts.PrimaryColor}");
        sb.AppendLine($"- Accent color: {opts.AccentColor}");
        sb.AppendLine($"- Columns: {opts.ColumnCount}");
        sb.AppendLine($"- Sidebar: {opts.SidebarPosition}");
        sb.AppendLine($"- Language: {opts.Language} ({(opts.Language == "fa" ? "RTL" : "LTR")})");
        sb.AppendLine();
        sb.AppendLine("# JSON Schema (data we will inject)");
        sb.AppendLine("```json");
        sb.AppendLine(SampleProfileJson);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("# Constraints (CRITICAL)");
        sb.AppendLine("1. Output MUST be a single self-contained HTML file.");
        sb.AppendLine("2. ALL CSS MUST be inline (in <style> tag, no external .css).");
        sb.AppendLine("3. NO JavaScript allowed.");
        sb.AppendLine("4. Use Scriban template syntax: {{ profile.name }}, {{#each profile.sections }}.");
        sb.AppendLine("5. Embed Vazir font as @font-face with base64 (for Persian).");
        sb.AppendLine("6. Set <html dir=\"rtl\" lang=\"fa\"> for Persian, \"ltr\" lang=\"en\" for English.");
        sb.AppendLine("7. Use semantic HTML5: <header>, <main>, <section>, <article>.");
        sb.AppendLine("8. Print-friendly: A4 page, no overflow, 1cm margins.");
        sb.AppendLine();
        sb.AppendLine("# Example: One Section in Scriban");
        sb.AppendLine("```html");
        sb.AppendLine("<section class=\"experience\">");
        sb.AppendLine("  <h2>تجربه کاری</h2>");
        sb.AppendLine("  {{#each profile.sections.where(s => s.type == 'experience')}}");
        sb.AppendLine("  <article>");
        sb.AppendLine("    <h3>{{ this.title }} — {{ this.company }}</h3>");
        sb.AppendLine("    <span>{{ this.start }} تا {{ this.end }}</span>");
        sb.AppendLine("    <p>{{ this.description }}</p>");
        sb.AppendLine("  </article>");
        sb.AppendLine("  {{/each}}");
        sb.AppendLine("</section>");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("# Output Requirements");
        sb.AppendLine("- Return ONLY the HTML file content. No explanations, no markdown fences.");
        sb.AppendLine("- Make sure ALL placeholder variables in the JSON schema are used.");
        sb.AppendLine("- Ensure visual hierarchy: section titles > entry titles > body text.");
        sb.AppendLine("- White space: generous, breathing room, no clutter.");
        return sb.ToString();
    }

    private const string SampleProfileJson = """
    {
      "name": "مریم احمدی",
      "title": "کارشناس ارشد کامپیوتر",
      "contact": {
        "email": "maryam@example.com",
        "phone": "+98 912 345 6789",
        "location": "تهران",
        "linkedin": "linkedin.com/in/maryam",
        "github": "github.com/maryam"
      },
      "summary": "توسعه‌دهنده فول‌استک با 5 سال سابقه...",
      "sections": [
        {
          "type": "experience",
          "title": "تجربه کاری",
          "items": [
            {
              "title": "توسعه‌دهنده سنیور",
              "company": "شرکت X",
              "start": "1401",
              "end": "اکنون",
              "description": "مسئول..."
            }
          ]
        }
      ]
    }
    """;
}
```

---

## 6. Template Engine

### 6.1 انتخاب Scriban

**چرا Scriban؟** Scriban یک template engine pure C# است که هیچ وابستگی به Reflection.Emit ندارد و با Native AOT کاملاً سازگار است. alternatives مثل Razor Light یا Handlebars.NET یا به JIT compilation وابسته‌اند ( Razor) یا به dynamic IL emit (Handlebars.NET بعضی sections). Scriban با syntax `{{ }}` کار می‌کند که شبیه Liquid است و یادگیری‌اش ساده است. سرعت رندر در حد μs برای templateهای کوچک است.

### 6.2 ساختار Template

هر قالب به‌صورت یک فایل HTML با embedded Scriban syntax است. ساختار کلی:

```html
<!DOCTYPE html>
<html dir="{{ direction }}" lang="{{ profile.language }}">
<head>
  <meta charset="UTF-8">
  <title>{{ profile.name }} — {{ profile.title }}</title>
  <style>
    @font-face {
      font-family: 'Vazir';
      src: url(data:font/woff2;base64,{{ font_base64 }}) format('woff2');
    }
    body { font-family: 'Vazir', sans-serif; }
    /* ... styles ... */
  </style>
</head>
<body>
  <header>
    <h1>{{ profile.name }}</h1>
    <p>{{ profile.title }}</p>
  </header>
  <main>
    {{#each profile.sections }}
    <section class="{{ this.type }}">
      <h2>{{ this.title }}</h2>
      {{#each this.items }}
      <article>{{ this.title }}...</article>
      {{/each}}
    </section>
    {{/each}}
  </main>
</body>
</html>
```

### 6.3 اعتبارسنجی قالب سفارشی

`TemplateValidator` با AngleSharp HTML parsing، موارد زیر را بررسی می‌کند:

1. **HTML Well-formed بودن**: هیچ تگ بسته‌نشده یا mismatched.
2. **وجود placeholderهای مورد انتظار**: `{{ profile.name }}`، `{{ profile.title }}`، `{{#each profile.sections }}`.
3. **وجود `@font-face`**: حداقل یک font-face block برای Vazir (برای قالب فارسی).
4. **عدم وجود JavaScript**: هرگونه `<script>` tag باید حذف یا خطا داده شود.
5. **عدم وجود منابع خارجی**: هیچ `<link>` به فایل CSS یا `<img>` با URL خارجی نباشد (مگر در حالت portfolio که در v2.0 است).
6. **semantic HTML5**: presence of `<header>`, `<main>`, `<section>` (با warning نه خطا).

اگر اعتبارسنجی موفق باشد، قالب با metadata در `templates/custom/<id>.html` ذخیره می‌شود. اگر شکست بخورد، کاربر لیست خطاها را می‌بیند و می‌تواند HTML را در editor ساده ویرایش کند.

### 6.4 5 قالب پیش‌فرض

| شناسه | نام | سبک | چیدمان |
|-------|-----|-----|--------|
| `minimal` | مینیمال | فونت نازک، فضای سفید زیاد، بدون رنگ | تک‌ستون، header بالا |
| `classic` | کلاسیک | Serif font، حاشیه‌های سنتی، آبی تیره | تک‌ستون |
| `modern` | مدرن | Sans-serif، رنگ اکسنت، آیکن‌های ساده | تک‌ستون با summary badge |
| `compact` | فشرده | دو ستون، فونت کوچک‌تر، فضای کم | دو ستون با sidebar |
| `sidebar` | با سایدبار | ستون چپ sidebar، بدنه راست | دو ستون با sidebar رنگی |

هر قالب هم نسخه RTL و هم LTR دارد، با conditional CSS via `html[dir="rtl"]` selector.

---

## 7. Desktop Frontend (Avalonia UI)

### 7.1 چرا Avalonia؟

انتخاب Avalonia به‌جای WPF یا WinUI 3 به سه دلیل است: (1) **Cross-platform** - همان کد روی ویندوز و لینوکس اجرا می‌شود (و در v1.1 مک)، که با هدف Native AOT تک‌باینری همخوان است؛ (2) **Native AOT support** - Avalonia 11 با trim mode aggressive و RD.xml removal کار می‌کند که برای AOT ضروری است؛ (3) **Open source و فعال** - جامعه‌ی فعال و به‌روزرسانی‌های منظم. WPF ویندوز-محض است و WinUI 3 هنوز روی لینوکس اجرا نمی‌شود.

### 7.2 ساختار MVVM

الگوی MVVM با `CommunityToolkit.Mvvm` برای Source Generators (که با AOT سازگار است). ساختار:

- **ViewModels**: `MainViewModel`، `ProfileEditorViewModel`، `TemplatePickerViewModel`، `LeadBuilderViewModel`، `ExportViewModel`، `SettingsViewModel`. هر ViewModel با `[ObservableProperty]` و `[RelayCommand]` از CommunityToolkit.Mvvm.
- **Views**: XAML با DataBinding. Avalonia XAML با AOT کار می‌کند چون در compile-time به کد C# تبدیل می‌شود (نباید BAML runtime load).
- **Services**: در DI Container ثبت می‌شوند و در ViewModelها با Constructor Injection دریافت.

### 7.3 WebView برای Preview زنده

Avalonia برای رندر HTML نیاز به WebView دارد. گزینه‌ها: (1) `Avalonia.WebView` (بر اساس WebView2 در ویندوز، WebKitGTK در لینوکس) که نیاز به runtime موتور مرورگر دارد؛ (2) رندر کردن HTML به image در Core Library و نمایش Image در Avalonia (سنگین‌تر).

راهکار پیشنهادی: استفاده از WebView2 در ویندوز (با Edge نصب‌شده) و WebKitGTK در لینوکس (با webkit2gtk-4.1 نصب‌شده). اگر WebView در دسترس نباشد، fallback به رندر image با Chrome headless هر 500ms انجام می‌شود.

### 7.4 Dark Mode

Avalonia از FluentTheme و SimpleTheme پشتیبانی می‌کند. FluentTheme با `Application.Current.RequestedThemeColor = FluentThemeMode.Dark` به Dark سوئیچ می‌کند. تم اپ فقط UI را تحت تاثیر قرار می‌دهد، نه خروجی رزومه (که همیشه Light است برای خوانایی چاپ).

### 7.5 Configuration — `appsettings.json` یا Code?

برای MVP، configuration در یک کلاس ساده `AppOptions` با default values:

```csharp
public sealed class AppOptions
{
    public string DataDirectory { get; init; } = GetDefaultDataDir();
    public string ChromePath { get; init; } = AutoDetectChrome();
    public string DefaultLanguage { get; init; } = "fa";
    public ThemeMode Theme { get; init; } = ThemeMode.System;
    
    private static string GetDefaultDataDir() =>
        Environment.OSVersion.Platform == PlatformID.Win32NT
            ? Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), "ResumeMaker")
            : Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "ResumeMaker");
}
```

---

## 8. Web Frontend (ASP.NET Minimal API)

### 8.1 چرا Minimal API + Static HTML؟

ASP.NET Core 9 با Native AOT به‌خوبی کار می‌کند. Minimal API به‌جای Controller-based API (که به reflection وابسته است) با source generators کار می‌کند که با AOT سازگار است. Frontend به‌جای Blazor (که به runtime JavaScript پویا وابسته است) HTML static ساده با fetch API است. این یعنی:

- باینری تک‌فایل Native AOT (سرور)
- فایل‌های HTML/CSS static (با Vazir embedded)
- No JavaScript framework
- حداقل DOM manipulation با Vanilla JS (~50 خط)

### 8.2 Endpoints

```
GET    /api/profiles                    ← لیست همه‌ی Profileها
POST   /api/profiles                    ← ایجاد Profile جدید
GET    /api/profiles/{id}               ← گرفتن یک Profile
PUT    /api/profiles/{id}               ← آپدیت Profile
DELETE /api/profiles/{id}               ← حذف Profile
POST   /api/profiles/{id}/duplicate     ← کپی Profile

GET    /api/templates                   ← لیست قالب‌های پیش‌فرض و سفارشی
POST   /api/templates/custom             ← Import قالب سفارشی
DELETE /api/templates/custom/{id}        ← حذف قالب سفارشی

POST   /api/lead/template                ← تولید Lead پرامپت قالب
POST   /api/lead/cover-letter            ← تولید Lead پرامپت Cover Letter

POST   /api/render                       ← رندر HTML از Profile + Template
POST   /api/export/pdf                    ← Export به PDF
POST   /api/export/png                    ← Export به PNG
POST   /api/export/html                   ← Export به HTML static
POST   /api/export/json                   ← Export به JSON

GET    /api/settings                      ← تنظیمات فعلی
PUT    /api/settings                      ← آپدیت تنظیمات
```

### 8.3 Static Frontend

`wwwroot/` با فایل‌های زیر:

```
wwwroot/
├── index.html                    ← صفحه‌ی اصلی
├── css/
│   ├── app.css                   ← استایل اپ
│   └── vazir.woff2                ← فونت Vazir برای UI
├── js/
│   ├── app.js                    ← main app logic (vanilla)
│   ├── api.js                    ← fetch wrapper
│   └── preview.js                ← iframe preview logic
└── assets/
    └── icons.svg
```

### 8.4 CSP (Content Security Policy)

هدر CSP سختگیرانه:

```
Content-Security-Policy: 
  default-src 'self';
  script-src 'self';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data:;
  font-src 'self' data:;
  connect-src 'self';
  frame-src 'self';
```

این یعنی هیچ منبع خارجی، هیچ inline script، هیچ external image. `unsafe-inline` برای style لازم است چون قالب‌ها CSS inline دارند.

---

## 9. Data Model و ذخیره‌سازی

### 9.1 ساختار ذخیره‌سازی

در MVP، تمام داده‌ها به‌صورت فایل JSON در پوشه‌ی محلی کاربر:

```
%APPDATA%/ResumeMaker/
├── settings.json                  ← تنظیمات اپ (theme, language, chrome path)
├── profiles/
│   ├── {guid-1}.json               ← هر Profile یک فایل
│   ├── {guid-2}.json
│   └── index.json                  ← index برای لیست سریع (id, name, updatedAt)
├── templates/
│   ├── builtin/                    ← قالب‌های پیش‌فرض (embedded در باینری)
│   │   ├── minimal.html
│   │   ├── classic.html
│   │   ├── modern.html
│   │   ├── compact.html
│   │   └── sidebar.html
│   └── custom/                     ← قالب‌های سفارشی کاربر
│       ├── {guid-1}.html
│       └── {guid-1}.meta.json       ← metadata (name, createdAt, source lead)
└── backups/
    └── {guid-1}.{timestamp}.json   ← backup خودکار هر 5 دقیقه
```

### 9.2 JSON Schema (Profile)

```json
{
  "$schema": "https://resumemaker.app/schema/profile.json",
  "id": "3fa85f64-5717-4562-b3fc-752c91d29c1b",
  "name": "رزومه شغلی — شرکت X",
  "language": "fa",
  "templateId": "minimal",
  "createdAt": "2026-10-28T13:00:00Z",
  "updatedAt": "2026-10-28T13:30:00Z",
  "contact": {
    "fullName": "مریم احمدی",
    "title": "کارشناس ارشد کامپیوتر",
    "email": "maryam@example.com",
    "phone": "+98 912 345 6789",
    "location": "تهران، ایران",
    "links": {
      "linkedin": "https://linkedin.com/in/maryam",
      "github": "https://github.com/maryam",
      "website": "https://maryam.dev"
    }
  },
  "summary": "توسعه‌دهنده فول‌استک با 5 سال سابقه...",
  "sections": [
    {
      "id": "exp-1",
      "type": "experience",
      "title": "تجربه کاری",
      "enabled": true,
      "order": 1,
      "items": [
        {
          "title": "توسعه‌دهنده سنیور",
          "company": "شرکت فناوری X",
          "location": "تهران",
          "start": "1401/03",
          "end": "اکنون",
          "description": "رهندی تیم 4 نفره، طراحی معماری...",
          "tags": ["C#", "Azure", "React"]
        }
      ]
    },
    {
      "id": "edu-1",
      "type": "education",
      "title": "تحصیلات",
      "enabled": true,
      "order": 2,
      "items": [
        {
          "degree": "کارشناسی ارشد کامپیوتر",
          "institution": "دانشگاه شریف",
          "start": "1399",
          "end": "1401",
          "description": "گرایش هوش مصنوعی"
        }
      ]
    },
    {
      "id": "skl-1",
      "type": "skills",
      "title": "مهارت‌ها",
      "enabled": true,
      "order": 3,
      "items": [
        {
          "category": "Programming",
          "entries": [
            { "name": "C#", "level": "expert" },
            { "name": "TypeScript", "level": "advanced" }
          ]
        }
      ]
    },
    {
      "id": "lng-1",
      "type": "languages",
      "title": "زبان‌ها",
      "enabled": true,
      "order": 4,
      "items": [
        { "name": "فارسی", "level": "native" },
        { "name": "انگلیسی", "level": "c1" }
      ]
    }
  ],
  "theme": {
    "primaryColor": "#1a73e8",
    "accentColor": "#ff6b6b",
    "fontFamily": null
  }
}
```

### 9.3 Serialization با Source Generator

برای Native AOT، JSON serialization با `System.Text.Json` و Source Generator استفاده می‌شود (نه با reflection):

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(List<Profile>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(TemplateMetadata))]
public sealed partial class ResumeMakerJsonContext : JsonSerializerContext;
```

این کلاس در compile-time با کد serialization تولید می‌شود و هیچ runtime reflection لازم ندارد.

### 9.4 Backup Strategy

هر 5 دقیقه، اگر Profile فعلی تغییر کرده باشد، یک snapshot در `backups/` ذخیره می‌شود. تعداد نگه‌داشته‌شده حداکثر 10 است (پرهیز از پر شدن دیسک). کاربر می‌تواند در Settings، تعداد backupها و فاصله‌ی آنها را تنظیم کند.

---

## 10. Export Pipeline

### 10.1 دیاگرام Pipeline

```
Profile JSON     Template HTML (Scriban)
     │                │
     └────────┬───────┘
              ▼
    ┌──────────────────┐
    │ TemplateService  │  ← RenderAsync
    │ (Scriban)        │
    └────────┬─────────┘
             │ HTML string (with embedded fonts, CSS inline)
             ▼
    ┌──────────────────┐
    │ Temp File Write  │  ← write to %TEMP%/resumemaker-{guid}.html
    └────────┬─────────┘
             │
             ├─────────────────────────┐
             ▼                         ▼
    ┌──────────────────┐      ┌──────────────────┐
    │ ChromeHeadless   │      │ SkiaSharp        │
    │ Exporter         │      │ PNG Exporter     │
    │ (PDF)            │      │ (rasterize)      │
    └────────┬─────────┘      └────────┬─────────┘
             │                         │
             ▼                         ▼
         output.pdf              output.png
```

### 10.2 Chrome Headless Export

```csharp
public sealed class ChromeHeadlessPdfExporter : IPdfExporter
{
    private readonly string _chromePath;
    
    public ChromeHeadlessPdfExporter(string chromePath) => _chromePath = chromePath;
    
    public async Task<ExportResult> ExportAsync(string html, ExportOptions opts, CancellationToken ct)
    {
        var tempHtml = Path.Combine(Path.GetTempPath(), $"rm-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempHtml, html, ct);
        
        try
        {
            var pdfPath = Path.Combine(Path.GetTempPath(), $"rm-{Guid.NewGuid():N}.pdf");
            var args = $"--headless --disable-gpu --no-sandbox " +
                       $"--print-to-pdf=\"{pdfPath}\" " +
                       $"--print-to-pdf-no-header " +
                       $"--virtual-time-budget=5000 " +
                       $"\"file:///{tempHtml}\"";
            
            var psi = new ProcessStartInfo(_chromePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            
            using var proc = Process.Start(psi) ?? throw new ChromeNotFoundException();
            await proc.WaitForExitAsync(ct);
            
            if (!File.Exists(pdfPath))
                throw new ExportFailedException(await proc.StandardError.ReadToEndAsync(ct));
            
            var bytes = await File.ReadAllBytesAsync(pdfPath, ct);
            File.Delete(pdfPath);
            
            return new ExportResult(bytes, "application/pdf", $"{opts.FileName}.pdf");
        }
        finally
        {
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
        }
    }
}
```

### 10.3 ATS-Friendly Mode

در حالت ATS، به‌جای قالب فعلی، از قالب `ats.html` ساده استفاده می‌شود (بدون جدول، بدون ستون، فونت Arial/Helvetica از سیستمی). این تضمین می‌کند که متن قابل parse توسط ATSهای شرکت‌ها باشد.

### 10.4 PNG Export

با SkiaSharp (که native libraries خود را به‌صورت embedded در باینری Native AOT دارد) و HTML رندرشده به SVG (ساده‌ترین روش با SkiaSharp). در v1.1 می‌توان از Chrome headless `--screenshot` استفاده کرد که روش پایدارتری است.

### 10.5 HTML Static Export

ساده‌ترین حالت: همان HTML رندرشده با فونت Vazir embedded به‌صورت base64. حجم نهایی حدود 100-150 کیلوبایت است. کاربر می‌تواند فایل را روی هر سرویس استاتیک (GitHub Pages، Netlify، AWS S3) آپلود کند.

---

## 11. ملاحظات Native AOT

### 11.1 تنظیمات csproj

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>false</InvariantGlobalization>
  <TrimMode>full</TrimMode>
  <TrimmerSingleWarn>false</TrimmerSingleWarn>
  <StackTraceSupport>true</StackTraceSupport>
  <IlcOptimizationPreference>Speed</IlcOptimizationPreference>
</PropertyGroup>
```

### 11.2 چالش‌های شناخته‌شده و راهکارها

| چالش | راهکار |
|------|---------|
| Reflection.Emit توسط برخی کتابخانه‌ها | حذف آن libraryها یا replace با AOT-friendly alt |
| JSON serialization با reflection | Source Generator با `JsonSerializerContext` |
| Avalonia XAML loading | compile-time XAML (no runtime BAML) |
| DI Container با dynamic resolution | Microsoft.Extensions.DependencyInjection با source-gen activator |
| Dynamic LINQ expressions | اجتناب، یا compile با Roslyn Source Generator |
| Lazy<T> با reflection-based initialization | استفاده از `Lazy<T>(valueFactory)` صریح |

### 11.3 Trimming و سنجش حجم

با `dotnet publish -r win-x64 -c Release` و trim mode full، حجم باینری باید به حدود 22 مگابایت برسد. اگر بیشتر شد، باید با `ILRepack` یا `dotnet-tools` analyze کرد کدام وابستگی بزرگ است. rd.xml برای حذف false positives reflection می‌تواند اضافه شود.

### 11.4 روند انتشار (Publishing)

```
dotnet publish src/ResumeMaker.Desktop `
  -r win-x64 `
  -c Release `
  /p:PublishAot=true `
  --self-contained

dotnet publish src/ResumeMaker.Desktop `
  -r linux-x64 `
  -c Release `
  /p:PublishAot=true `
  --self-contained
```

خروجی: `bin/Release/net9.0/win-x64/publish/ResumeMaker.Desktop.exe` (تک باینری).

---

## 12. مدیریت فونت

### 12.1 Vazir به‌صورت Embedded Resource

فونت Vazir (فایل woff2 به‌دلیل حجم کمتر) به‌صورت embedded resource در `ResumeMaker.Infrastructure.csproj` قرارداده می‌شود:

```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\Fonts\vazir.woff2" />
  <EmbeddedResource Include="Assets\Fonts\vazir-bold.woff2" />
  <EmbeddedResource Include="Assets\Fonts\inter-regular.woff2" />
</ItemGroup>
```

### 12.2 تزریق فونت در HTML

```csharp
public sealed class EmbeddedFontProvider : IFontResourceProvider
{
    private readonly Assembly _asm = typeof(EmbeddedFontProvider).Assembly;
    
    public async Task<string> GetVazirBase64Async(string lang, CancellationToken ct)
    {
        var resourceName = lang == "fa" 
            ? "ResumeMaker.Infrastructure.Assets.Fonts.vazir.woff2"
            : "ResumeMaker.Infrastructure.Assets.Fonts.inter-regular.woff2";
        
        await using var stream = _asm.GetManifestResourceStream(resourceName)
            ?? throw new FontNotFoundException(resourceName);
        
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return Convert.ToBase64String(ms.ToArray());
    }
}
```

### 12.3 Subset Font برای کاهش حجم

با استفاده از `fonttools` (Python CLI یا زیرشاخه‌ی آن `pyftsubset`)، در زمان build، subset Vazir با glypهای فارسی + اعداد + نشانه‌های مورد نیاز تولید می‌شود. این کار حجم فونت را از ~250KB به ~50KB کاهش می‌دهد. این کار در CI/CD به‌صورت pre-build انجام می‌شود.

### 12.4 Font در خروجی HTML Static

در HTML خروجی (نه preview اپ)، فونت به‌صورت base64 inline در `@font-face` قرار می‌گیرد. این یعنی خروجی HTML تک‌فایل و portable است.

---

## 13. i18n و RTL

### 13.1 Localization Framework

به‌جای فایل‌های `.resx` که با reflection کار می‌کنند (و در AOT نیاز به special handling دارند)، از فایل‌های JSON ساده استفاده می‌شود:

```
locales/
├── fa.json
└── en.json
```

این فایل‌ها به‌صورت embedded resource در باینری قرارداده می‌شوند و در زمان اجرا با `System.Text.Json` source-gen خوانده می‌شوند.

### 13.2 RTL/LTR Switching

- در HTML خروجی، `dir` attribute روی `<html>` بر اساس `profile.language` تنظیم می‌شود.
- در UI دسکتاپ، Avalonia با `FlowDirection.RightToLeft` برای فارسی و `LeftToRight` برای انگلیسی کار می‌کند.
- در UI تحت وب، `dir` attribute روی `<html>` یا `<body>` تنظیم می‌شود و CSS از `html[dir="rtl"]` برای منطق شرطی استفاده می‌کند.

### 13.3 اعداد فارسی

برای نمایش اعداد فارسی در خروجی، تابع `ToPersianNumerals(this string)` به‌عنوان extension method در `StringExtensions` تعریف می‌شود. این تابع با `char.IsDigit` اعداد را پیدا کرده و با U+06F0..U+06F9 جایگزین می‌کند. این کار در زمان رندر Scriban اعمال می‌شود.

---

## 14. امنیت و حریم خصوصی

### 14.1 اصول

- **No telemetry** در MVP (در v1.1 Opt-in اضافه می‌شود)
- **No analytics** آنلاین
- **No crash report** بدون consent صریح کاربر
- **No outbound HTTP** به‌جز Chrome headless که با `file://` کار می‌کند (نه URL خارجی)
- **No stored credentials** چون هیچ سرویس آنلاینی برای login نیست

### 14.2 امنیت فایل

فایل‌های Profile JSON در پوشه‌ی محلی کاربر نوشته می‌شوند با permission 0600 در لینوکس (یا ACL معادل در ویندوز). اپ نباید `chmod 0777` بگذارد.

### 14.3 خطرات HTML Imported از AI

قالب‌های واردشده از AI می‌توانند حاوی کد مخرب باشند (مثلاً `<script>` یا `<iframe>` خارجی). `TemplateValidator` با AngleSharp این موارد را تشخیص داده و به‌صورت hardcoded حذف می‌کند. هیچ execution محتمل نباید رخ دهد چون HTML در Chrome headless با `--no-sandbox` اجرا نمی‌شود (یا sandboxing فعال است)، و در WebView، JavaScript disabled است (چون قالب نباید JS داشته باشد).

### 14.4 امنیت Chrome Headless

در زمان اجرای Chrome headless، موارد زیر اعمال می‌شود:

- `--no-sandbox` در سرور underprivileged لازم است (اما در دسکتاپ کاربر، sandboxing پیش‌فرض روشن است)
- `--disable-gpu` برای جلوگیری از memory leak
- `--virtual-time-budget=5000` برای جلوگیری از hang
- `--disable-extensions` برای جلوگیری از load شدن extensionها
- `--user-data-dir=temp` برای isolation session

---

## 15. استراتژی تست

### 15.1 سطوح تست

| سطح | دامنه | ابزار |
|------|-------|-------|
| Unit | Domain entities، Application services | xUnit + FluentAssertions |
| Integration | Infrastructure (JsonProfileRepository، ScribanTemplateEngine) | xUnit + TestContainers |
| Snapshot | Template render output (HTML) | Snapshot testing با `Verify` |
| E2E | Desktop UI flows | Avalonia.Headless.XUnit |
| E2E Web | API endpoints | WebApplicationFactory |
| Performance | Cold start، Export time | BenchmarkDotNet |

### 15.2 پوشش (Coverage) هدف

- Core Library: 80%+
- Infrastructure: 70%+
- Desktop UI: 40%+ (به‌دلیل سختی Avalonia testing)
- Web API: 70%+

### 15.3 تست‌های کلیدی

- **Profile round-trip**: Serialize → Deserialize باید داده‌ی یکسان بدهد.
- **Template render**: داده‌ی نمونه + قالب = HTML expected (snapshot).
- **Lead generation**: داده‌ی نمونه → پرامپت expected (snapshot).
- **HTML validation**: قالب معیوب → خطای مناسب.
- **Export pipeline**: HTML → PDF تولید می‌شود، فایل valid است.

---

## 16. Build و CI/CD

### 16.1 GitHub Actions Workflow

```yaml
name: build-and-release
on:
  push:
    tags: ['v*']
  pull_request:

jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest]
        include:
          - os: windows-latest
            rid: win-x64
            artifact: ResumeMaker.Desktop-win-x64.zip
          - os: ubuntu-latest
            rid: linux-x64
            artifact: ResumeMaker.Desktop-linux-x64.tar.gz
    
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Restore
        run: dotnet restore
      - name: Test
        run: dotnet test --no-restore --verbosity normal
      - name: Publish AOT
        run: dotnet publish src/ResumeMaker.Desktop -r ${{ matrix.rid }} -c Release /p:PublishAot=true --self-contained
      - name: Package
        run: |
          cd src/ResumeMaker.Desktop/bin/Release/net9.0/${{ matrix.rid }}/publish
          7z a -tzip ${{ matrix.artifact }} *
      - uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.artifact }}
          path: ${{ matrix.artifact }}
```

### 16.2 Font Subsetting در CI

در مرحله‌ی pre-build، اسکریپت Python با `pyftsubset` فونت Vazir را به subset فارسی + انگلیسی reduced می‌کند. این کار در build image با `pip install fonttools` انجام می‌شود.

### 16.3 Release Strategy

- **GitHub Releases** برای توزیع باینری (MIT license)
- **Winget** (ویندوز) و **Snap** (لینوکس) در v1.1
- **آپشنال: دانلود مستقیم** از GitHub Release با checksumهای SHA-256

---

## 17. ریسک‌ها و کاهش (Risks & Mitigations)

| ریسک | احتمال | اثر | راهکار کاهش |
|------|--------|------|--------------|
| Avalonia AOT شکست در edge case | متوسط | بالا | Prototype در هفته‌ی 1، fallback به R2R |
| Chrome نصب نباشد | بالا | متوسط | Auto-detect + پیام راهنما + wkhtmltopdf fallback در v1.1 |
| حجم باینری > 30MB | متوسط | پایین | ILRepack، فونت subset، trim aggressive |
| قالب AI معیوب است | بالا | متوسط | Validator قوی، error messages واضح، editor inline |
| Native AOT در macOS | متوسط | پایین | v1.1 با macOS ARM64، تست زودهنگام |
|Performance رندر ضعیف | پایین | بالا | Debounce در preview، cache HTML تولیدشده |

---

## 18. مسیر آینده (Architecture Roadmap)

### 18.1 v1.1 (3 ماه پس از MVP)

- **QR Code embedding**: با `QRCoder` library در Infrastructure.
- **Multi-Profile management**: با SQLite backend به‌جای JSON files (با EF Core 9 AOT-friendly).
- **wkhtmltopdf fallback** برای کاربران بدون Chrome.
- **Optional cloud sync** با Nextcloud WebDAV.

### 18.2 v1.5 (6 ماه پس از MVP)

- **LinkedIn PDF OCR Import** با Tesseract.NET.
- **Theme Builder** no-code palette editor با real-time preview.
- **DOCX Export** با OpenXML SDK 2.19+.
- **macOS ARM64** support.

### 18.3 v2.0 (12 ماه پس از MVP)

- **Portfolio template** با تصاویر embedded.
- **GitHub Activity embedding** با GraphQL API.
- **Public URL sharing** با GitHub Pages one-click export.
- **Theme Marketplace** (community templates) با git-based index.

### 18.4 مسیر لایه‌ی AI (آپشنال، اگر تقاضا بود)

اگر در آینده تصمیم به AI integration مستقیم گرفته شود، با الگوی **Opt-in API Key** انجام می‌شود:

- کاربر API key خود را در Settings وارد می‌کند.
- اپ با HttpClient با سرویس (OpenAI، Anthropic، Ollama) صحبت می‌کند.
- هیچ server-side proxy: تمام درای مستقیم از client.
- این فیچر در MVP نیست تا مینیمالیسم حفظ شود.

---

## 19. ضمائم

### ضمیمه A: نمونه‌ی پرامپت Lead-Based برای قالب (کامل)

```text
# System Role
You are an expert HTML/CSS resume template designer who creates beautiful, minimal, ATS-friendly resume templates.

# Project Brief
Design a single-page resume template with the following specifications:
- Style: minimal
- Primary color: #1a73e8
- Accent color: #ff6b6b
- Columns: 1
- Sidebar: none
- Language: fa (RTL)
- Font: Vazir (Persian) embedded as base64

# JSON Schema (data we will inject)
{
  "name": "مریم احمدی",
  "title": "کارشناس ارشد کامپیوتر",
  "contact": {
    "email": "maryam@example.com",
    "phone": "+98 912 345 6789",
    "location": "تهران",
    "linkedin": "linkedin.com/in/maryam",
    "github": "github.com/maryam"
  },
  "summary": "توسعه‌دهنده فول‌استک با 5 سال سابقه...",
  "sections": [
    {
      "type": "experience",
      "title": "تجربه کاری",
      "items": [
        {
          "title": "توسعه‌دهنده سنیور",
          "company": "شرکت X",
          "start": "1401",
          "end": "اکنون",
          "description": "مسئول..."
        }
      ]
    }
  ]
}

# Constraints (CRITICAL)
1. Output MUST be a single self-contained HTML file.
2. ALL CSS MUST be inline (in <style> tag, no external .css files).
3. NO JavaScript allowed (no <script> tags).
4. Use Scriban template syntax: {{ profile.name }}, {{#each profile.sections }}.
5. Embed Vazir font as @font-face with base64 (placeholder for base64 string: {{ font_base64 }}).
6. Set <html dir="rtl" lang="fa"> for Persian.
7. Use semantic HTML5: <header>, <main>, <section>, <article>.
8. Print-friendly: A4 page, no overflow, 1cm margins, no page-break inside entries.
9. Visual style: generous white space, no clutter, hierarchy via typography not decoration.
10. Maximum 2 colors (primary + accent) + black/white.

# Example: One Section in Scriban
<section class="experience">
  <h2>تجربه کاری</h2>
  {{#each profile.sections.where(s => s.type == 'experience')}}
  <article>
    <h3>{{ this.title }} — {{ this.company }}</h3>
    <span class="date">{{ this.start }} تا {{ this.end }}</span>
    <p>{{ this.description }}</p>
  </article>
  {{/each}}
</section>

# Output Requirements
- Return ONLY the HTML file content. No explanations, no markdown fences, no commentary.
- Make sure ALL placeholder variables in the JSON schema are used.
- Ensure visual hierarchy: section titles > entry titles > body text.
- Use only Vazir font (no other web fonts).
```

### ضمیمه B: نمونه‌ی پرامپت Lead-Based برای Cover Letter

```text
# System Role
You are an expert cover letter writer who creates concise, persuasive cover letters tailored to specific job postings.

# Project Brief
Write a cover letter for the following:
- Target company: [company name]
- Target position: [position]
- Tone: formal
- Length: maximum one A4 page (about 350-450 words in Persian)

# Candidate Profile (JSON)
{... profile JSON ...}

# Job Posting Keywords
- 5 years of C# experience
- ASP.NET Core
- Microservices
- Leadership

# Structure (suggested)
1. Opening paragraph: express interest, mention position source.
2. Middle paragraph 1: highlight relevant experience.
3. Middle paragraph 2: connect achievements to job requirements.
4. Closing paragraph: call to action, contact info.

# Constraints
- Output ONLY the cover letter text (no commentary, no markdown).
- Use formal Persian tone (شما، جناب، کارشناس).
- Keep sentences short and clear.
- No buzzwords without context.
- Reference specific achievements from the profile.
```

### ضمیمه C: Roadmap تجسمی

```
Q1 2026 (MVP)
─────────────────────────────────
✓ Core Library (Domain + App + Infra)
✓ 5 built-in templates (RTL/LTR)
✓ Lead-Based AI workflow
✓ Desktop (Avalonia) + Web (ASP.NET)
✓ Export: PDF, PNG, HTML, JSON
✓ Cover Letter
✓ Dark Mode (UI)

Q2 2026 (v1.1)
─────────────────────────────────
- Web Frontend release
- Multi-Profile (SQLite)
- QR Code embedding
- wkhtmltopdf fallback
- Optional cloud sync (WebDAV)

Q3 2026 (v1.5)
─────────────────────────────────
- LinkedIn OCR Import
- Theme Builder
- DOCX Export
- macOS ARM64

Q4 2026 (v2.0)
─────────────────────────────────
- Portfolio template
- GitHub Activity embedding
- Public URL sharing
- Theme Marketplace
```

---

> **پایان سند معماری** — این سند با سند دیسکاوری v1.0 همخوان است. هر تغییر در معماری باید با PR به این سند اضافه شود و tag `architecture-vN.M` در commit زده شود. مستندات فنی بیشتر (API spec، Data Model details) در زمان پیاده‌سازی هر فاز در `docs/` فولدر افزوده می‌شود.