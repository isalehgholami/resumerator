# Resumerator — Resume Maker Minimal

[فارسی](#فارسی) · [English](#english)

**Live web app:** https://isalehgholami.github.io/resumerator/ — **Downloads:** [Releases](https://github.com/isalehgholami/resumerator/releases) (Windows x64 · Linux x64)

## English

A minimal, offline-first, open-source resume maker. Write your data once, pick one of five templates (or an ATS-friendly plain one), and export a static HTML / PDF / PNG file. Persian (RTL, Vazirmatn) and English (LTR) are first-class.

**Lead-Based AI Workflow** — instead of embedding an AI API, the app generates a structured prompt. Paste it into ChatGPT / Claude / Gemini, import the HTML it returns, and the app validates and stores it as your own template. No API keys, no data leaves your machine.

### Components
| Part | Tech | Notes |
|------|------|-------|
| `src/ResumeMaker.Core` | C# / .NET 10 | Domain + application services, AOT-compatible |
| `src/ResumeMaker.Infrastructure` | Scriban, AngleSharp | JSON storage, template engine, validator, Chrome headless export |
| `src/ResumeMaker.Desktop` | Avalonia 11, Native AOT | Windows x64 / Linux x64 binary |
| `src/ResumeMaker.Web` | ASP.NET Minimal API, Native AOT | Self-host server + the static frontend in `wwwroot` |
| `templates/` | HTML + Scriban subset | 5 templates + ATS + cover letter; contract in `templates/SYNTAX.md` |

The same static frontend runs fully client-side on GitHub Pages (profiles are stored in your browser's `localStorage`).

### Build
```bash
dotnet test
dotnet publish src/ResumeMaker.Desktop -c Release -r win-x64     # or linux-x64 (Native AOT)
dotnet run --project src/ResumeMaker.Web                         # API + web UI on http://localhost:5000
node tools/build-site.mjs --serve                                # static site only, http://localhost:8080
```
PDF/PNG export uses an installed Chrome, Edge or Chromium.

See [`TASKS.md`](TASKS.md) for the task breakdown and [`docs/`](docs) for the discovery and architecture documents.

## فارسی

رزومه‌ساز مینیمال، متن‌باز و آفلاین. اطلاعات را یک‌بار وارد کنید، یکی از ۵ قالب (یا قالب ساده‌ی سازگار با ATS) را انتخاب کنید و خروجی HTML / PDF / PNG بگیرید. فارسی (راست‌به‌چپ با فونت وزیرمتن) و انگلیسی هر دو پشتیبانی می‌شوند.

**الگوی Lead-Based AI** — برنامه به‌جای اتصال مستقیم به AI، یک پرامپت ساختاریافته می‌سازد. آن را در ChatGPT / Claude / Gemini بچسبانید، HTML خروجی را وارد کنید و برنامه آن را اعتبارسنجی و به‌عنوان قالب شخصی ذخیره می‌کند. نه API key می‌خواهد و نه داده‌ای از دستگاه شما خارج می‌شود.

نسخه‌ی وب: https://isalehgholami.github.io/resumerator/ — دانلود باینری ویندوز/لینوکس: [Releases](https://github.com/isalehgholami/resumerator/releases)

خروجی PDF/PNG به Chrome، Edge یا Chromium نصب‌شده نیاز دارد. فونت‌ها: Vazirmatn و Inter (مجوز OFL).

## License
MIT
