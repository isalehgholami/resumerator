# تسک‌های پروژه — Resume Maker Minimal

مبنا: `docs/001-Discovery.md` و `docs/002-Architecture.md`.
وضعیت: `[ ]` انجام نشده · `[x]` انجام شده. هر تسک به شناسه‌ی نیازمندی (FR) یا بخش معماری ارجاع دارد.

## تصمیم‌های فنی (انحراف از اسناد، با دلیل)

| موضوع | سند | پیاده‌سازی | دلیل |
|-------|-----|-----------|------|
| TFM | net9.0 | **net10.0** | SDK/Runtime نصب‌شده 10 است؛ 10 نسخه LTS فعلی است و Native AOT را کامل پشتیبانی می‌کند |
| سینتکس Scriban | `{{#each}}` | `{{ for x in y }} … {{ end }}` | `#each` سینتکس Handlebars است نه Scriban |
| Preview دسکتاپ | WebView | باز کردن preview در مرورگر پیش‌فرض + رندر زنده فایل موقت | WebView برای Avalonia رایگان/AOT-safe نیست |
| وب | ASP.NET + static | همان + نسخه‌ی کاملاً client-side روی GitHub Pages | Pages فقط static است؛ رندر با یک موتور کوچک JS که همان قالب‌ها را اجرا می‌کند |
| PNG | SkiaSharp | Chrome headless `--screenshot` | معماری خودش آن را پایدارتر می‌داند؛ وابستگی native حذف می‌شود |
| ساختار JSON | camelCase در نمونه | snake_case (مطابق بخش 9.3) | یکدستی با Source Generator |
| Persian font | Vazir | Vazirmatn (جانشین رسمی Vazir، OFL) | Vazir اصلی دیگر نگهداری نمی‌شود |

## T0 — زیرساخت مخزن
- [x] T0.1 ساخت solution، `Directory.Build.props`، `.gitignore`
- [x] T0.2 پروژه‌های Core / Infrastructure / Tests
- [x] T0.3 فونت‌های embedded (Vazirmatn Regular/Bold، Inter) — بخش 12
- [x] T0.4 README دوزبانه، LICENSE (MIT)

## T1 — Core (Domain + Application) — بخش 5
- [x] T1.1 Entities: Profile، Section، SectionItem، ContactInfo، Template، AppSettings (FR-02, FR-10..15)
- [x] T1.2 Interfaceها و DTOها (بخش 3.1)
- [x] T1.3 `ResumeMakerJsonContext` با source generator (بخش 9.3)
- [x] T1.4 `ProfileService`: ایجاد، ویرایش، حذف، کپی، Import/Export JSON (FR-01, FR-03, FR-04)
- [x] T1.5 `TemplateService`: رندر با قالب و فونت (FR-22)
- [x] T1.6 `LeadGenerator`: پرامپت قالب با 5 لایه (FR-30..35، ضمیمه A)
- [x] T1.7 `CoverLetterService`: پرامپت و مدل Cover Letter + رندر HTML/PDF (FR-50..52، ضمیمه B)
- [x] T1.8 `ExportService`: PDF / PNG / HTML / JSON + حالت ATS (FR-40..44)
- [x] T1.9 `ValidationService`: اعتبارسنجی Profile (فیلدهای الزامی، ایمیل، لینک‌ها)
- [x] T1.10 افزونه‌ی اعداد فارسی `ToPersianNumerals` (بخش 13.3)
- [x] T1.11 تست واحد Core با پوشش ≥ 70% (بخش 8.3)

## T2 — Infrastructure — بخش 5.1
- [x] T2.1 `JsonProfileRepository` + `index.json` + مجوز 0600 در لینوکس (FR-03، بخش 14.2)
- [x] T2.2 `JsonSettingsStore`، `AppPaths` (مسیرهای Windows / Linux / macOS، بخش 8.4)
- [x] T2.3 `FileBackupService` (هر 5 دقیقه، حداکثر 10 نسخه، بخش 9.4)
- [x] T2.4 `ScribanTemplateEngine` سازگار با AOT (ScriptObject دستی، بدون reflection) + فیلتر `digits`
- [x] T2.5 `TemplateValidator` با AngleSharp: well-formed، placeholderها، font-face، حذف script/iframe/منابع خارجی (FR-24، بخش 6.3)
- [x] T2.6 `TemplateRepository`: قالب‌های built-in (embedded) + custom در `templates/custom/` (FR-23)
- [x] T2.7 `EmbeddedFontProvider` (بخش 12.2)
- [x] T2.8 `ChromeHeadlessExporter` (PDF + PNG) با تشخیص خودکار Chrome/Edge/Chromium و آرگومان‌های امن (بخش 10.2, 14.4)
- [x] T2.9 `HtmlStaticExporter` (فونت base64 inline) — merged into ExportService
- [x] T2.10 تست‌های Infrastructure (round-trip، snapshot رندر، validator)

## T3 — قالب‌ها — بخش 6.4
- [x] T3.1 قالب‌های `minimal`، `classic`، `modern`، `compact`، `sidebar` — هر کدام RTL و LTR (FR-20, FR-21)
- [x] T3.2 قالب `ats` (بدون جدول/ستون/رنگ، Arial) (FR-44)
- [x] T3.3 قالب Cover Letter با استایل قالب
- [x] T3.4 بخش مشترک سینتکس قابل اجرا هم در Scriban هم در موتور JS

## T4 — Web (ASP.NET Minimal API + Static) — بخش 8
- [x] T4.1 Endpointها مطابق بخش 8.2 (profiles، templates، lead، render، export، settings)
- [x] T4.2 هدرهای CSP و امنیتی (بخش 8.4)
- [x] T4.3 Native AOT برای سرور
- [x] T4.4 تست‌های API با `WebApplicationFactory`

## T5 — Static Frontend + GitHub Pages — بخش 8.3
- [x] T5.1 موتور رندر JS سازگار با قالب‌های Scriban
- [x] T5.2 ویرایشگر Profile (سکشن‌ها، drag & drop، فعال/غیرفعال) (FR-11)
- [x] T5.3 انتخاب قالب + preview زنده در iframe (≤ 100ms)
- [x] T5.4 Lead-Based AI: فرم، پرامپت، Copy، لینک ChatGPT/Claude/Gemini، Import HTML + اعتبارسنجی (FR-30..34)
- [x] T5.5 Cover Letter Builder (FR-50..52)
- [x] T5.6 Export: HTML، JSON، چاپ/PDF مرورگر، PNG (FR-42, 43)
- [x] T5.7 i18n فارسی/انگلیسی، RTL/LTR، Dark Mode (FR-60..72)
- [x] T5.8 ذخیره در localStorage، Import/Export JSON
- [x] T5.9 Workflow دیپلوی GitHub Pages

## T6 — Desktop (Avalonia 11 + Native AOT) — بخش 7
- [x] T6.1 اسکلت MVVM با CommunityToolkit.Mvvm و Composition Root بدون reflection
- [x] T6.2 صفحات: لیست پروفایل، ویرایشگر، قالب‌ها، Lead Builder، Cover Letter، Export، Settings
- [x] T6.3 Preview در مرورگر با فایل موقت و بازآفرینی با تغییر
- [x] T6.4 RTL/LTR، Dark/Light، i18n با JSON (بخش 13)
- [x] T6.5 Backup خودکار (بخش 9.4)

## T7 — CI/CD — بخش 16
- [x] T7.1 Workflow بیلد و تست روی Windows و Linux
- [x] T7.2 Publish Native AOT: `win-x64` و `linux-x64` برای Desktop (و Server)
- [x] T7.3 آپلود artifact و GitHub Release با SHA-256 روی تگ `v*`
- [x] T7.4 Workflow دیپلوی GitHub Pages

## T8 — کیفیت و انتشار
- [x] T8.1 `dotnet build` و `dotnet test` سبز
- [x] T8.2 مرور معیارهای پذیرش (بخش 13 Discovery)
- [x] T8.3 Push به `github.com/isalehgholami/resumerator`
- [x] T8.4 تأیید سبز بودن CI و Pages
