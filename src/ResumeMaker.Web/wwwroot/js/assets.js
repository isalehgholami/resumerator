// Runtime asset loading (same-origin, relative URLs so it works under /resumerator/).
import { render } from './engine.js';
import { buildModel } from './model.js';
import * as store from './store.js';

export const BUILTIN_TEMPLATES = ['minimal', 'classic', 'modern', 'compact', 'sidebar'];
export const ATS_TEMPLATE = 'ats';

const cache = new Map();
async function cached(key, fn) {
  if (!cache.has(key)) {
    const p = fn().catch((e) => { cache.delete(key); throw e; });
    cache.set(key, p);
  }
  return cache.get(key);
}

export const fetchText = (path) => cached('t:' + path, async () => {
  const r = await fetch(path, { cache: 'no-cache' });
  if (!r.ok) throw new Error(`${path}: HTTP ${r.status}`);
  return r.text();
});

export const fetchJson = async (path) => JSON.parse(await fetchText(path));

function toBase64(buf) {
  const bytes = new Uint8Array(buf);
  let bin = '';
  for (let i = 0; i < bytes.length; i += 0x8000) bin += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
  return btoa(bin);
}
const fetchB64 = (path) => cached('b:' + path, async () => {
  const r = await fetch(path);
  if (!r.ok) throw new Error(`${path}: HTTP ${r.status}`);
  return toBase64(await r.arrayBuffer());
});

/** base64 woff2 fonts used inside rendered resumes. */
export async function loadFonts() {
  const [vazir, vazir_bold, inter] = await Promise.all([
    fetchB64('fonts/vazir.woff2'), fetchB64('fonts/vazir-bold.woff2'), fetchB64('fonts/inter-regular.woff2'),
  ]);
  return { vazir, vazir_bold, inter };
}

/** Template source by id (built-in via fetch, custom via localStorage). */
export async function getTemplateSource(id) {
  if (id && id.startsWith('custom-')) {
    const c = store.getCustomTemplate(id);
    if (!c) throw new Error(`Custom template not found: ${id}`);
    return c.html;
  }
  return fetchText(`templates/${id}.html`);
}

/** Which template id actually renders for a profile (ATS mode overrides). */
export const effectiveTemplateId = (profile) => (profile.ats_mode ? ATS_TEMPLATE : profile.template_id || 'minimal');

/** Synchronous render once source + fonts are loaded. */
export function renderHtml(source, profile, fonts, extra = {}) {
  return render(source, buildModel(profile, { fonts, ...extra }));
}

export async function loadSample(lang) {
  return fetchJson(`templates/sample-profile.${lang === 'en' ? 'en' : 'fa'}.json`);
}
