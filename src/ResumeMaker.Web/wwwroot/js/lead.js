// Lead-Based AI prompt generators (port of the C# LeadGenerator). Pure functions; DOM-free.

export const AI_LINKS = [
  { id: 'chatgpt', name: 'ChatGPT', url: 'https://chatgpt.com/' },
  { id: 'claude', name: 'Claude', url: 'https://claude.ai/new' },
  { id: 'gemini', name: 'Gemini', url: 'https://gemini.google.com/app' },
];

export const STYLES = ['minimal', 'modern', 'classic', 'dark', 'creative'];
export const TONES = ['formal', 'friendly', 'bold'];

const EXAMPLE = {
  fa: {
    name: 'مریم احمدی',
    language: 'fa',
    contact: {
      full_name: 'مریم احمدی', title: 'کارشناس ارشد کامپیوتر', email: 'maryam@example.com', phone: '+98 912 345 6789',
      location: 'تهران، ایران', links: { linkedin: 'linkedin.com/in/maryam', github: 'github.com/maryam', website: 'maryam.dev' },
    },
    summary: 'توسعه‌دهنده فول‌استک با ۵ سال سابقه...',
    sections: [
      { id: 'exp-1', type: 'experience', title: 'تجربه کاری', items: [{ title: 'توسعه‌دهنده سنیور', company: 'شرکت X', location: 'تهران', start: '1401/03', end: 'اکنون', description: 'رهبری تیم ۴ نفره...', tags: ['C#', 'Azure'] }] },
      { id: 'edu-1', type: 'education', title: 'تحصیلات', items: [{ degree: 'کارشناسی ارشد کامپیوتر', institution: 'دانشگاه شریف', start: '1399', end: '1401' }] },
      { id: 'skl-1', type: 'skills', title: 'مهارت‌ها', items: [{ category: 'Programming', entries: [{ name: 'C#', level: 'متخصص' }, { name: 'TypeScript', level: 'پیشرفته' }] }] },
      { id: 'lng-1', type: 'languages', title: 'زبان‌ها', items: [{ name: 'فارسی', level: 'زبان مادری' }, { name: 'انگلیسی', level: 'C1' }] },
    ],
  },
  en: {
    name: 'Daniel Carter',
    language: 'en',
    contact: {
      full_name: 'Daniel Carter', title: 'Senior Full-Stack Engineer', email: 'daniel@example.com', phone: '+1 415 555 0142',
      location: 'San Francisco, CA', links: { linkedin: 'linkedin.com/in/danielcarter', github: 'github.com/dcarter', website: 'danielcarter.dev' },
    },
    summary: 'Full-stack engineer with 8 years of experience...',
    sections: [
      { id: 'exp-1', type: 'experience', title: 'Experience', items: [{ title: 'Senior Software Engineer', company: 'Northwind', location: 'San Francisco', start: '2022-03', description: 'Lead a team of 5 engineers...', tags: ['C#', 'Azure'] }] },
      { id: 'edu-1', type: 'education', title: 'Education', items: [{ degree: 'BSc Computer Science', institution: 'UC Berkeley', start: '2012', end: '2016' }] },
      { id: 'skl-1', type: 'skills', title: 'Skills', items: [{ category: 'Programming', entries: [{ name: 'C#', level: 'Expert' }, { name: 'TypeScript', level: 'Advanced' }] }] },
      { id: 'lng-1', type: 'languages', title: 'Languages', items: [{ name: 'English', level: 'Native' }, { name: 'Spanish', level: 'B2' }] },
    ],
  },
};

const STYLE_HINT = {
  minimal: 'generous white space, no clutter, hierarchy through typography only, thin rules at most',
  modern: 'clean modern look, a bold header band or accent bar using the primary color, rounded tags for item tags',
  classic: 'traditional serif-free corporate layout, centered header, thin horizontal rules between sections',
  dark: 'dark header area with light text on the primary color; the page body MUST stay white and print-friendly',
  creative: 'expressive typography and accent shapes made purely with CSS, still readable and printable',
};

const json = (o) => JSON.stringify(o, null, 2);

/** Section 8.3 / Appendix A, adapted to the real template syntax subset and model variable list. */
export function templateLead(o = {}) {
  const lang = o.lang === 'en' ? 'en' : 'fa';
  const style = STYLES.includes(o.style) ? o.style : 'minimal';
  const columns = o.columns === 2 ? 2 : 1;
  const sidebar = columns === 2 && (o.sidebar === 'left' || o.sidebar === 'right') ? o.sidebar : 'none';
  const font = o.font || (lang === 'fa' ? 'Vazirmatn' : 'Inter');
  const primary = o.primary || '#1a73e8';
  const accent = o.accent || '#ff6b6b';
  const dir = lang === 'fa' ? 'rtl' : 'ltr';
  const order = (o.sections && o.sections.length ? o.sections : ['experience', 'education', 'skills', 'languages', 'projects', 'awards', 'interests']).join(' > ');
  const notes = (o.notes || '').trim();

  return `# System Role
You are an expert HTML/CSS resume template designer who creates beautiful, minimal, ATS-friendly, print-ready resume templates.

# Project Brief
Design a single-document resume template with the following specifications:
- Style: ${style} (${STYLE_HINT[style]})
- Primary color: ${primary}
- Accent color: ${accent}
- Columns: ${columns}
- Sidebar: ${sidebar}
- Language: ${lang} (${dir.toUpperCase()}) — the same file must also work for the other language, so use \`html[dir="rtl"]\` / \`html[dir="ltr"]\` selectors and CSS logical properties (margin-inline-start, padding-inline-end, text-align: start ...)
- Font: ${font}, embedded as base64 (placeholder below)
- Section priority (most important first): ${order}${notes ? `\n- Extra notes from the user: ${notes}` : ''}

# Template Syntax (STRICT — a tiny engine renders it; anything else breaks)
Allowed tags, always written with a space inside the braces:
- Output:            {{ profile.contact.full_name }}
- Persian digits:    {{ item.start | digits }}      (the ONLY filter; converts 0-9 to Persian digits when lang == "fa")
- Condition:         {{ if item.company }} ... {{ else }} ... {{ end }}
- Negation:          {{ if !item.tags }} ... {{ end }}
- String equality:   {{ if section.type == "experience" }} ... {{ end }}   (also !=)
- Loop:              {{ for section in profile.sections }} ... {{ end }}
- Nested paths:      a.b.c   — loop variables are plain identifiers
- {{ end }} closes the innermost if/for. if/for may be nested.
Falsy values: missing key, null, "", empty list, false.
FORBIDDEN: &&, ||, elsif, "else if", function calls, arithmetic, {{~ ~}}, comments, include, capture, while, forloop.*, any other filter.
All strings are already HTML-escaped by the app — never try to escape them.

# Data Model (root variables available to the template)
- profile.name
- profile.language, lang ("fa" | "en"), direction ("rtl" | "ltr")
- profile.contact.full_name, .title, .email, .phone, .location, .links.linkedin, .links.github, .links.website  (any of them may be missing)
- profile.summary (may be missing)
- profile.sections — ONLY enabled, non-empty sections, already sorted. Each section has: id, type, title, items
  - type is one of: experience | education | skills | languages | projects | awards | interests | custom
  - item fields by type (all optional, check with {{ if }}):
    - experience: title, company, location, start, end, description, tags[]
    - education: degree, institution, location, start, end, description
    - skills: category, entries[] (each: name, level)
    - languages: name, level
    - projects: title, url, description, tags[], start, end
    - awards: title, issuer, date, description
    - interests: name
    - custom: title, description
  - tags is a list of strings: {{ for tag in item.tags }}{{ tag }}{{ end }}
- font_base64 (regular woff2, NOT escaped), font_bold_base64 (bold woff2), font_family (CSS family name)
- primary_color, accent_color (hex)
- labels.summary labels.experience labels.education labels.skills labels.languages labels.projects labels.awards labels.interests labels.contact labels.present labels.to  (localized UI words)

# Example Data
${json(EXAMPLE[lang])}

# Constraints (CRITICAL)
1. Output MUST be ONE self-contained HTML file starting with \`<!DOCTYPE html>\` and \`<html dir="{{ direction }}" lang="{{ lang }}">\`.
2. ALL CSS inline in a single <style> block. NO external stylesheets, fonts, images, CDNs or any http(s):// URL.
3. NO JavaScript: no <script>, no on* attributes, no <iframe>/<object>/<embed>.
4. Embed the font exactly like this (also add a bold @font-face with {{ font_bold_base64 }} and font-weight: 700):
   @font-face { font-family: "{{ font_family }}"; src: url(data:font/woff2;base64,{{ font_base64 }}) format("woff2"); font-weight: 400; }
5. Must contain \`{{ profile.contact.full_name }}\` and \`{{ for section in profile.sections }}\` (render each section generically; branch on \`section.type\` with == "..." when layouts differ).
6. Use colors via the values {{ primary_color }} and {{ accent_color }} inside the CSS. Max 2 colors + black/white/grays.
7. Semantic HTML5: <header>, <main>, <section>, <article>.
8. Print-friendly A4: \`@page { size: A4; margin: 18mm 20mm }\`, \`break-inside: avoid\` on entries, no overflow, 1 page for typical data.
9. Hide every optional part with {{ if ... }} so no empty labels, dashes or separators appear when data is missing.
10. Visual style: ${STYLE_HINT[style]}.

# Example: one section in the allowed syntax
{{ for section in profile.sections }}
<section class="s-{{ section.type }}">
  <h2>{{ section.title }}</h2>
  {{ for item in section.items }}
  <article>
    {{ if item.title }}<h3>{{ item.title }}{{ if item.company }} — {{ item.company }}{{ end }}</h3>{{ end }}
    {{ if item.start }}<span class="date">{{ item.start | digits }} {{ labels.to }} {{ if item.end }}{{ item.end | digits }}{{ else }}{{ labels.present }}{{ end }}</span>{{ end }}
    {{ if item.description }}<p>{{ item.description }}</p>{{ end }}
    {{ if item.tags }}<ul class="tags">{{ for tag in item.tags }}<li>{{ tag }}</li>{{ end }}</ul>{{ end }}
  </article>
  {{ end }}
</section>
{{ end }}

# Output Requirements
- Return ONLY the HTML file content. No explanations, no markdown fences, no commentary.
- Make sure ALL data fields above are used somewhere.
- Visual hierarchy: name > section titles > entry titles > body text.
- Use only the embedded font (no other web fonts; generic fallbacks like sans-serif are fine).`;
}

function profileForLetter(p) {
  const clean = (v) => {
    if (v === null || v === undefined || v === '' || v === false) return undefined;
    if (Array.isArray(v)) { const a = v.map(clean).filter((x) => x !== undefined); return a.length ? a : undefined; }
    if (typeof v === 'object') {
      const o = {};
      for (const [k, x] of Object.entries(v)) { const c = clean(x); if (c !== undefined) o[k] = c; }
      return Object.keys(o).length ? o : undefined;
    }
    return v;
  };
  return clean({
    name: p.contact?.full_name, title: p.contact?.title, location: p.contact?.location, summary: p.summary,
    sections: (p.sections || []).filter((s) => s.enabled !== false).map((s) => ({ type: s.type, title: s.title, items: s.items })),
  }) || {};
}

/** Appendix B. */
export function coverLetterLead({ profile, company, position, keywords, tone, lang }) {
  const l = lang === 'en' ? 'en' : 'fa';
  const tn = TONES.includes(tone) ? tone : 'formal';
  const kw = (Array.isArray(keywords) ? keywords : String(keywords || '').split(/\r?\n|,/))
    .map((k) => k.trim()).filter(Boolean);
  const words = l === 'fa' ? 'about 350-450 words in Persian' : 'about 300-400 words in English';
  const toneNote = {
    formal: l === 'fa' ? 'Use formal Persian tone (شما، جناب، سرکار).' : 'Use a formal, professional tone.',
    friendly: 'Use a warm, friendly but still professional tone.',
    bold: 'Use a confident, bold, energetic tone — direct claims backed by facts.',
  }[tn];
  return `# System Role
You are an expert cover letter writer who creates concise, persuasive cover letters tailored to specific job postings.

# Project Brief
Write a cover letter for the following:
- Target company: ${company || '[company name]'}
- Target position: ${position || '[position]'}
- Tone: ${tn}
- Language: ${l === 'fa' ? 'Persian (fa)' : 'English (en)'}
- Length: maximum one A4 page (${words})

# Candidate Profile (JSON)
${json(profileForLetter(profile))}

# Job Posting Keywords
${kw.length ? kw.map((k) => `- ${k}`).join('\n') : '- (none provided — infer from the position title)'}

# Structure (suggested)
1. Opening paragraph: express interest, mention the position${l === 'fa' ? ' و منبع آگهی' : ' and where you found it'}.
2. Middle paragraph 1: highlight the most relevant experience.
3. Middle paragraph 2: connect concrete achievements to the job requirements and keywords.
4. Closing paragraph: call to action, availability for an interview.

# Constraints
- Output ONLY the body text of the letter as plain text (no markdown, no commentary, no subject line).
- Do NOT include a salutation ("Dear ...") or a sign-off/signature — the template adds them.
- Separate paragraphs with ONE blank line.
- ${toneNote}
- Keep sentences short and clear.
- No buzzwords without context.
- Reference specific achievements from the profile; never invent facts that are not in the profile.`;
}
