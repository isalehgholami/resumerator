# Template contract (shared by Scriban [C#] and the JS engine [GitHub Pages])

Templates are single-file HTML with inline CSS, no JavaScript, rendered by a **strict subset of Scriban**.
Both engines MUST produce equivalent output for any template using only this subset.

## Syntax subset
| Construct | Example |
|-----------|---------|
| Output | `{{ profile.contact.full_name }}` |
| Filter (only one allowed) | `{{ item.start \| digits }}` — converts 0-9 to Persian digits (U+06F0..) when `lang == "fa"`, else unchanged |
| If / else / end | `{{ if item.company }} … {{ else }} … {{ end }}` |
| Negation | `{{ if !item.tags }}` |
| Equality with a string literal | `{{ if section.type == "experience" }}`, `!=` also allowed |
| For loop | `{{ for section in profile.sections }} … {{ end }}` |
| Nested paths | `a.b.c`; loop variables are plain identifiers |

Not allowed (so the JS engine stays tiny): `&&`, `||`, `elsif`, function calls, arithmetic, `else if`, whitespace-control `{{~ ~}}`, comments, `include`, `capture`, `while`, `for` modifiers, `forloop.*`.
Every tag is one of the forms above, written with spaces inside the braces. Nesting of `if`/`for` is allowed. `end` closes the innermost block.

Truthiness: **absent key, null, empty string, empty list, false** are all falsy. (The model builder omits null/empty values so both engines agree.)

## Model (root variables)
- `profile` — the Profile in snake_case JSON with: `sections` = only enabled sections sorted by `order`; every section/item has null and empty values removed.
  - `profile.contact.{full_name,title,email,phone,location,links.{linkedin,github,website,...}}`, `profile.summary`, `profile.language`, `profile.name`
  - `section.{id,type,title,items}` where `type` ∈ experience|education|skills|languages|projects|awards|interests|custom
  - item fields (subset per type): `title company location start end description tags[] degree institution category entries[{name,level}] name level url issuer date`
  - `tags` is a list of strings; iterate with `{{ for tag in item.tags }}{{ tag }}{{ end }}`
- `direction` — `rtl` or `ltr`; `lang` — `fa` or `en`
- `font_base64` — base64 woff2 of the body font (NOT escaped); `font_bold_base64` — bold weight (Vazirmatn Bold for fa, same as regular for en)
- `font_family` — CSS family name, `Vazirmatn` for fa, `Inter` for en (profile.theme.font_family overrides)
- `primary_color`, `accent_color` — hex colors with defaults `#1a73e8` / `#ff6b6b` when theme overrides absent
- `labels` — localized strings: `labels.summary labels.experience labels.education labels.skills labels.languages labels.projects labels.awards labels.interests labels.contact labels.present labels.to labels.cover_letter labels.dear labels.sincerely`
  (`present` = «اکنون»/«Present», `to` = «تا»/«to»)
- Cover-letter template only: `letter.{company,position,body}` and `paragraphs` (list of strings; body split on blank lines)

All string values (except `font_base64`, `font_bold_base64`) are **HTML-escaped by the model builder**, so templates never need an escape filter.

## Template file requirements
- Starts with `<!DOCTYPE html>` and `<html dir="{{ direction }}" lang="{{ lang }}">`
- One `<style>` block containing `@font-face` using `url(data:font/woff2;base64,{{ font_base64 }})`
- Semantic HTML5: `<header>`, `<main>`, `<section>`, `<article>`
- A4 print CSS: `@page { size: A4; margin: 18mm 20mm }`, `break-inside: avoid` on entries
- `html[dir="rtl"]` / `html[dir="ltr"]` selectors for direction-specific CSS (prefer logical properties)
- Must contain `{{ profile.contact.full_name }}` and a `{{ for section in profile.sections }}` loop (the validator checks)
- No `<script>`, no external URLs

## Built-in template ids
`minimal`, `classic`, `modern`, `compact`, `sidebar`, `ats`, and the non-listed `cover-letter`. Files: `templates/<id>.html`.
