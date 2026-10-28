// Builds the template model from a profile, exactly as described in templates/SYNTAX.md.
// DOM-free: runs in browsers and in node.

export const SECTION_TYPES = ['experience', 'education', 'skills', 'languages', 'projects', 'awards', 'interests', 'custom'];
export const DEFAULT_PRIMARY = '#1a73e8';
export const DEFAULT_ACCENT = '#ff6b6b';

export const LABELS = {
  fa: {
    summary: 'درباره من', experience: 'تجربه کاری', education: 'تحصیلات', skills: 'مهارت‌ها',
    languages: 'زبان‌ها', projects: 'پروژه‌ها', awards: 'جوایز و افتخارات', interests: 'علاقه‌مندی‌ها',
    contact: 'اطلاعات تماس', present: 'اکنون', to: 'تا', cover_letter: 'نامه پوششی',
    dear: 'با سلام و احترام', sincerely: 'با احترام',
  },
  en: {
    summary: 'Summary', experience: 'Experience', education: 'Education', skills: 'Skills',
    languages: 'Languages', projects: 'Projects', awards: 'Awards', interests: 'Interests',
    contact: 'Contact', present: 'Present', to: 'to', cover_letter: 'Cover Letter',
    dear: 'Dear Hiring Manager,', sincerely: 'Sincerely,',
  },
};

const LEVELS = {
  fa: {
    beginner: 'مبتدی', intermediate: 'متوسط', advanced: 'پیشرفته', expert: 'متخصص',
    native: 'زبان مادری', fluent: 'مسلط', basic: 'مقدماتی',
  },
  en: {
    beginner: 'Beginner', intermediate: 'Intermediate', advanced: 'Advanced', expert: 'Expert',
    native: 'Native', fluent: 'Fluent', basic: 'Basic',
  },
};

export function escapeHtml(s) {
  return String(s)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

/** Remove null/empty values and HTML-escape strings. Returns undefined when nothing is left. */
function clean(v) {
  if (v === null || v === undefined || v === false) return undefined;
  if (typeof v === 'string') return v.trim() === '' ? undefined : escapeHtml(v.trim());
  if (typeof v === 'number') return escapeHtml(String(v));
  if (v === true) return true;
  if (Array.isArray(v)) {
    const a = v.map(clean).filter((x) => x !== undefined);
    return a.length ? a : undefined;
  }
  if (typeof v === 'object') {
    const o = {};
    for (const [k, x] of Object.entries(v)) {
      const c = clean(x);
      if (c !== undefined) o[k] = c;
    }
    return Object.keys(o).length ? o : undefined;
  }
  return undefined;
}

function localizeLevel(level, lang) {
  if (typeof level !== 'string') return level;
  const k = level.trim().toLowerCase();
  if (/^[abc][12]$/.test(k)) return k.toUpperCase();
  return LEVELS[lang][k] ?? level;
}

const HEX = /^#[0-9a-fA-F]{3,8}$/;
const FAMILY = /^[A-Za-z0-9 _-]{1,40}$/;

/**
 * @param profile  snake_case profile object
 * @param opts     { fonts: { vazir, vazir_bold, inter } (base64), letter: {company, position, body} }
 */
export function buildModel(profile, opts = {}) {
  const lang = profile.language === 'en' ? 'en' : 'fa';
  const fonts = opts.fonts || {};
  const theme = profile.theme || {};
  const wantFamily = typeof theme.font_family === 'string' && FAMILY.test(theme.font_family) ? theme.font_family : null;
  const family = wantFamily || (lang === 'fa' ? 'Vazirmatn' : 'Inter');
  const useVazir = /^vazir/i.test(family) ? true : /^inter$/i.test(family) ? false : lang === 'fa';
  const fontB64 = (useVazir ? fonts.vazir : fonts.inter) || '';
  const fontBoldB64 = (useVazir ? fonts.vazir_bold : fonts.inter) || fontB64;
  const labels = LABELS[lang];

  const sections = (profile.sections || [])
    .filter((s) => s && s.enabled !== false)
    .map((s, i) => ({ s, i }))
    .sort((a, b) => (Number(a.s.order ?? a.i) - Number(b.s.order ?? b.i)) || a.i - b.i)
    .map(({ s }) => {
      const type = SECTION_TYPES.includes(s.type) ? s.type : 'custom';
      const items = (s.items || []).map((it) => {
        const item = clean(it);
        if (!item) return undefined;
        if (type === 'skills' && Array.isArray(item.entries)) {
          item.entries = item.entries.map((e) => (e.level ? { ...e, level: localizeLevel(e.level, lang) } : e));
        }
        if (type === 'languages' && item.level) item.level = localizeLevel(item.level, lang);
        return item;
      }).filter(Boolean);
      if (!items.length) return undefined;
      const title = clean(s.title) ?? (type === 'custom' ? undefined : escapeHtml(labels[type]));
      return clean({ id: s.id, type, title, items });
    })
    .filter(Boolean);

  const prof = clean({
    name: profile.name,
    language: lang,
    contact: profile.contact,
    summary: profile.summary,
  }) || {};
  prof.sections = sections;

  const model = {
    profile: prof,
    direction: lang === 'fa' ? 'rtl' : 'ltr',
    lang,
    font_base64: fontB64,
    font_bold_base64: fontBoldB64,
    font_family: family,
    primary_color: HEX.test(theme.primary_color || '') ? theme.primary_color : DEFAULT_PRIMARY,
    accent_color: HEX.test(theme.accent_color || '') ? theme.accent_color : DEFAULT_ACCENT,
    labels: { ...labels },
  };

  if (opts.letter) {
    const l = opts.letter;
    const body = String(l.body || '');
    model.letter = clean({ company: l.company, position: l.position, body }) || {};
    model.paragraphs = body.split(/\r?\n[ \t]*\r?\n/).map((p) => p.trim()).filter(Boolean).map(escapeHtml);
  }
  return model;
}
