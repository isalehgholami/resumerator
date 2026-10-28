// localStorage-backed store: profiles, custom templates, settings, backups. Every access is guarded.

const K = {
  profiles: 'rm.profiles', settings: 'rm.settings', templates: 'rm.templates',
  backups: 'rm.backups', prompts: 'rm.prompts', seeded: 'rm.seeded',
};
const mem = new Map();
export const MAX_BACKUPS = 10;
const BACKUP_INTERVAL_MS = 5 * 60 * 1000;

function rd(key, def) {
  if (mem.has(key)) return mem.get(key);
  let v = def;
  try {
    const raw = localStorage.getItem(key);
    if (raw != null) v = JSON.parse(raw);
  } catch { /* storage unavailable or corrupt: use default */ }
  mem.set(key, v);
  return v;
}
let storageOk = true;
function wr(key, val) {
  mem.set(key, val);
  try { localStorage.setItem(key, JSON.stringify(val)); storageOk = true; return true; } catch { storageOk = false; return false; }
}
export const isPersistent = () => storageOk;

// ---------- events
const listeners = new Set();
export const subscribe = (fn) => { listeners.add(fn); return () => listeners.delete(fn); };
const emit = (type, detail) => listeners.forEach((fn) => { try { fn(type, detail); } catch { /* ignore */ } });

// ---------- ids / defaults
export function uid() {
  try { if (crypto?.randomUUID) return crypto.randomUUID(); } catch { /* fall through */ }
  return 'id-' + Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
}
const now = () => new Date().toISOString();

export const DEFAULT_SETTINGS = { ui_lang: null, theme: 'system' };
export const getSettings = () => ({ ...DEFAULT_SETTINGS, ...rd(K.settings, {}) });
export function setSettings(patch) { const s = { ...getSettings(), ...patch }; wr(K.settings, s); emit('settings', s); return s; }

export function newSection(type, order = 0) {
  return { id: `${type.slice(0, 3)}-${uid().slice(0, 6)}`, type, title: '', enabled: true, order, items: [] };
}

export function newProfile(lang = 'fa', name) {
  const types = ['experience', 'education', 'skills', 'languages'];
  return {
    id: uid(),
    name: name || (lang === 'fa' ? 'رزومه جدید' : 'New resume'),
    language: lang,
    template_id: 'minimal',
    ats_mode: false,
    created_at: now(),
    updated_at: now(),
    contact: { full_name: '', title: '', email: '', phone: '', location: '', links: { linkedin: '', github: '', website: '' } },
    summary: '',
    sections: types.map((t, i) => newSection(t, i + 1)),
    theme: { primary_color: '#1a73e8', accent_color: '#ff6b6b', font_family: null },
  };
}

const snake = (k) => k.replace(/([a-z0-9])([A-Z])/g, '$1_$2').toLowerCase();
function snakeKeys(v) {
  if (Array.isArray(v)) return v.map(snakeKeys);
  if (v && typeof v === 'object') return Object.fromEntries(Object.entries(v).map(([k, x]) => [snake(k), snakeKeys(x)]));
  return v;
}

/** Accept camelCase or snake_case profiles; fill defaults. Throws on non-profile input. */
export function normalizeProfile(raw) {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) throw new Error('not a profile');
  const p = snakeKeys(raw);
  if (!p.contact && !p.sections) throw new Error('not a profile');
  const base = newProfile(p.language === 'en' ? 'en' : 'fa', p.name);
  const out = { ...base, ...p };
  out.language = p.language === 'en' ? 'en' : 'fa';
  out.contact = { ...base.contact, ...(p.contact || {}) };
  out.contact.links = { ...base.contact.links, ...((p.contact || {}).links || {}) };
  out.theme = { ...base.theme, ...(p.theme || {}) };
  out.summary = typeof p.summary === 'string' ? p.summary : '';
  out.id = typeof p.id === 'string' && p.id ? p.id : uid();
  out.name = typeof p.name === 'string' && p.name ? p.name : base.name;
  out.ats_mode = !!p.ats_mode;
  out.template_id = typeof p.template_id === 'string' && p.template_id ? p.template_id : 'minimal';
  const secs = Array.isArray(p.sections) ? p.sections : [];
  out.sections = secs
    .filter((s) => s && typeof s === 'object')
    .map((s, i) => ({
      ...s,
      id: s.id || `sec-${uid().slice(0, 6)}`,
      type: s.type || 'custom',
      title: s.title || '',
      enabled: s.enabled !== false,
      order: Number.isFinite(Number(s.order)) ? Number(s.order) : i + 1,
      items: Array.isArray(s.items) ? s.items : [],
    }))
    .sort((a, b) => a.order - b.order)
    .map((s, i) => ({ ...s, order: i + 1 }));
  return out;
}

// ---------- profiles
let profiles = null;
function load() { if (!profiles) profiles = rd(K.profiles, []).map((p) => { try { return normalizeProfile(p); } catch { return null; } }).filter(Boolean); return profiles; }
const persistProfiles = () => wr(K.profiles, load());

export const listProfiles = () => [...load()].sort((a, b) => (b.updated_at || '').localeCompare(a.updated_at || ''));
export const getProfile = (id) => load().find((p) => p.id === id) || null;

export function addProfile(p) { const n = normalizeProfile(p); n.updated_at = now(); load().push(n); persistProfiles(); emit('profiles'); return n; }

export function duplicateProfile(id, suffix = ' (copy)') {
  const p = getProfile(id);
  if (!p) return null;
  const c = JSON.parse(JSON.stringify(p));
  c.id = uid(); c.name += suffix; c.created_at = c.updated_at = now();
  load().push(c); persistProfiles(); emit('profiles');
  return c;
}

export function deleteProfile(id) {
  snapshot(true);
  profiles = load().filter((p) => p.id !== id);
  persistProfiles(); emit('profiles');
}

let saveTimer = null;
let pending = null;
/** Autosave: debounce 400ms. */
export function scheduleSave(profile) {
  pending = profile;
  emit('saving');
  clearTimeout(saveTimer);
  saveTimer = setTimeout(flush, 400);
}
export function flush() {
  clearTimeout(saveTimer);
  if (!pending) return;
  const p = pending; pending = null;
  p.updated_at = now();
  const list = load();
  const i = list.findIndex((x) => x.id === p.id);
  if (i >= 0) list[i] = p; else list.push(p);
  const ok = persistProfiles();
  snapshot(false);
  emit('saved', { at: p.updated_at, ok });
}

// ---------- import / export
export function exportProfileJson(profile) { return JSON.stringify(profile, null, 2); }

/** Parse JSON text of a profile, an array of profiles, or a full backup ({profiles:[...]}). Returns added profiles. */
export function importJsonText(text) {
  let data;
  try { data = JSON.parse(text); } catch { throw new Error('invalid-json'); }
  const arr = Array.isArray(data) ? data : Array.isArray(data?.profiles) ? data.profiles : [data];
  const added = [];
  const existing = new Set(load().map((p) => p.id));
  for (const raw of arr) {
    const p = normalizeProfile(raw);
    if (existing.has(p.id)) { p.id = uid(); }
    existing.add(p.id);
    added.push(addProfile(p));
  }
  if (!added.length) throw new Error('not a profile');
  return added;
}

export function exportAllJson() {
  return JSON.stringify({ app: 'resumerator', exported_at: now(), profiles: load(), templates: getCustomTemplates() }, null, 2);
}

// ---------- backups (max 10 snapshots)
export const listBackups = () => rd(K.backups, []);
let lastSnap = 0;
export function snapshot(force) {
  const t = Date.now();
  if (!force && t - lastSnap < BACKUP_INTERVAL_MS) return false;
  const list = [...listBackups()];
  const snap = { ts: new Date(t).toISOString(), profiles: JSON.parse(JSON.stringify(load())) };
  list.unshift(snap);
  while (list.length > MAX_BACKUPS) list.pop();
  let ok = wr(K.backups, list);
  while (!ok && list.length > 1) { list.pop(); ok = wr(K.backups, list); }
  lastSnap = t;
  return ok;
}
export function restoreBackup(index) {
  const b = listBackups()[index];
  if (!b) return false;
  snapshot(true);
  profiles = b.profiles.map(normalizeProfile);
  persistProfiles(); emit('profiles');
  return true;
}

// ---------- custom templates
export const getCustomTemplates = () => rd(K.templates, []);
export function saveCustomTemplate({ id, name, html }) {
  const list = [...getCustomTemplates()];
  const i = id ? list.findIndex((t) => t.id === id) : -1;
  const rec = { id: id || `custom-${uid().slice(0, 8)}`, name, html, updated_at: now() };
  if (i >= 0) list[i] = rec; else list.push(rec);
  wr(K.templates, list); emit('templates');
  return rec;
}
export function deleteCustomTemplate(id) { wr(K.templates, getCustomTemplates().filter((t) => t.id !== id)); emit('templates'); }
export const getCustomTemplate = (id) => getCustomTemplates().find((t) => t.id === id) || null;

// ---------- prompt history (debug aid, FR-35)
export const listPrompts = () => rd(K.prompts, []);
export function savePrompt(kind, text) {
  const list = listPrompts();
  if (list[0] && list[0].text === text) return;
  wr(K.prompts, [{ ts: now(), kind, text }, ...list].slice(0, 10));
}

// ---------- first run
export const isSeeded = () => rd(K.seeded, false) === true;
export const markSeeded = () => wr(K.seeded, true);

export function clearAll() {
  for (const k of [K.profiles, K.templates, K.prompts]) { mem.delete(k); try { localStorage.removeItem(k); } catch { /* ignore */ } }
  profiles = null;
  emit('profiles');
}
