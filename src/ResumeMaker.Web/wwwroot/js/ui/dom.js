// Tiny DOM helpers (no framework).
import { t } from '../i18n.js';

const PROPS = new Set(['value', 'checked', 'disabled', 'selected', 'hidden', 'textContent', 'htmlFor', 'draggable', 'readOnly', 'multiple']);

export function h(tag, props, ...kids) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(props || {})) {
    if (v === null || v === undefined || v === false) {
      if (k === 'checked' || k === 'disabled' || k === 'hidden' || k === 'draggable' || k === 'readOnly') el[k] = false;
      continue;
    }
    if (k === 'class') el.className = v;
    else if (k === 'dataset') Object.assign(el.dataset, v);
    else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
    else if (PROPS.has(k)) el[k] = v;
    else el.setAttribute(k, v === true ? '' : String(v));
  }
  append(el, kids);
  return el;
}

export function append(el, kids) {
  for (const k of kids.flat(Infinity)) {
    if (k === null || k === undefined || k === false) continue;
    el.append(k.nodeType ? k : document.createTextNode(String(k)));
  }
  return el;
}

export const clear = (el) => { while (el.firstChild) el.removeChild(el.firstChild); return el; };
export const $ = (sel, root = document) => root.querySelector(sel);

let uidN = 0;
export const nextId = (p = 'f') => `${p}${++uidN}`;

export function debounce(fn, ms) {
  let tm;
  const d = (...a) => { clearTimeout(tm); tm = setTimeout(() => fn(...a), ms); };
  d.cancel = () => clearTimeout(tm);
  return d;
}

/** Labeled text input / textarea bound to obj[key]. */
export function field({ obj, key, label, type = 'text', multiline = false, rows = 3, placeholder = '', onchange, dir, hint, list, autocomplete }) {
  const id = nextId();
  const common = {
    id, value: obj[key] ?? '', placeholder,
    dir: dir || null, autocomplete: autocomplete || 'off', list: list || null,
    oninput: (e) => { obj[key] = e.target.value; onchange && onchange(); },
  };
  const control = multiline ? h('textarea', { ...common, rows }) : h('input', { ...common, type });
  return h('div', { class: 'field' }, h('label', { for: id }, label), control, hint ? h('small', { class: 'hint' }, hint) : null);
}

export function selectField({ label, value, options, onchange, id }) {
  id = id || nextId();
  const sel = h('select', { id, onchange: (e) => onchange(e.target.value) },
    options.map((o) => (o.group
      ? h('optgroup', { label: o.group }, o.options.map((x) => h('option', { value: x.value, selected: x.value === value }, x.label)))
      : h('option', { value: o.value, selected: o.value === value }, o.label))));
  sel.value = value ?? '';
  return h('div', { class: 'field' }, label ? h('label', { for: id }, label) : null, sel);
}

export function checkbox({ label, checked, onchange }) {
  const id = nextId();
  return h('div', { class: 'check' },
    h('input', { id, type: 'checkbox', checked: !!checked, onchange: (e) => onchange(e.target.checked) }),
    h('label', { for: id }, label));
}

export function button(label, onclick, { kind = '', title, ariaLabel, disabled, type = 'button', cls = '' } = {}) {
  return h('button', {
    type, class: `btn ${kind} ${cls}`.trim(), onclick, title: title || null, 'aria-label': ariaLabel || null, disabled,
  }, label);
}

// ---------- toast / live region
let toastHost;
export function toast(msg, kind = 'info') {
  if (!toastHost) {
    toastHost = h('div', { class: 'toasts', role: 'status', 'aria-live': 'polite' });
    document.body.append(toastHost);
  }
  const el = h('div', { class: `toast ${kind}` }, msg);
  toastHost.append(el);
  setTimeout(() => el.remove(), kind === 'error' ? 6000 : 2800);
}

export async function copyText(text) {
  try {
    if (navigator.clipboard && window.isSecureContext) { await navigator.clipboard.writeText(text); return true; }
  } catch { /* fallback below */ }
  const ta = h('textarea', { 'aria-hidden': 'true', style: 'position:fixed;inset-block-start:0;inset-inline-start:0;opacity:0' }, '');
  ta.value = text;
  document.body.append(ta);
  ta.focus(); ta.select();
  let ok = false;
  try { ok = document.execCommand('copy'); } catch { ok = false; }
  ta.remove();
  return ok;
}

export async function copyWithToast(text) {
  const ok = await copyText(text);
  toast(ok ? t('copied') : t('copy_failed'), ok ? 'info' : 'error');
  return ok;
}

export function pickFile(accept) {
  return new Promise((resolve) => {
    const inp = h('input', { type: 'file', accept, class: 'sr-only', tabindex: '-1' });
    inp.addEventListener('change', async () => {
      const f = inp.files[0];
      inp.remove();
      resolve(f ? { name: f.name, text: await f.text() } : null);
    });
    inp.addEventListener('cancel', () => { inp.remove(); resolve(null); });
    document.body.append(inp);
    inp.click();
  });
}

export function formatTime(iso) {
  try { return new Date(iso).toLocaleString(document.documentElement.lang === 'fa' ? 'fa-IR' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }); }
  catch { return iso; }
}
