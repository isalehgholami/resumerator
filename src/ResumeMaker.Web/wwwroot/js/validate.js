// Template validator (port of the C# TemplateValidator). DOMParser-based.
// Returns { errors: [{code, text}], warnings: [{code, text}], ok }. Codes are i18n keys ("v_*").
import { compile, referencedRoots, TemplateSyntaxError } from './engine.js';

const KNOWN_ROOTS = new Set(['profile', 'direction', 'lang', 'font_base64', 'font_bold_base64', 'font_family',
  'primary_color', 'accent_color', 'labels', 'letter', 'paragraphs']);
const SEMANTIC = ['header', 'main', 'section', 'article'];
const URL_ATTRS = ['src', 'href', 'srcset', 'action', 'poster', 'data', 'formaction', 'xlink:href'];
const EXTERNAL = /^\s*(?:https?:)?\/\//i;

function externalInCss(css) {
  const out = [];
  const re = /url\(\s*(['"]?)([^'")]+)\1\s*\)|@import\s+(?:url\(\s*)?(['"])([^'"]+)\3/gi;
  let m;
  while ((m = re.exec(css))) {
    const u = (m[2] ?? m[4] ?? '').trim();
    if (/^(?:https?:)?\/\//i.test(u)) out.push(u);
  }
  return out;
}

export function validateTemplate(html, { cover = false } = {}) {
  const errors = [];
  const warnings = [];
  const err = (code, text) => errors.push({ code, text });
  const warn = (code, text) => warnings.push({ code, text });

  if (!html || !html.trim()) { err('v_empty'); return { errors, warnings, ok: false }; }
  if (html.length > 3 * 1024 * 1024) err('v_too_large');

  // --- syntax of the template subset
  try { compile(html); } catch (e) {
    if (e instanceof TemplateSyntaxError) err('v_syntax', e.message); else throw e;
  }
  try {
    const { roots, loopVars } = referencedRoots(html);
    const unknown = [...roots].filter((r) => !KNOWN_ROOTS.has(r) && !loopVars.has(r));
    if (unknown.length) warn('v_unknown_vars', unknown.join(', '));
  } catch { /* syntax error already reported */ }

  // --- required placeholders
  if (!/\{\{\s*profile\.contact\.full_name\s*\}\}/.test(html)) err('v_no_name');
  if (!cover && !/\{\{\s*for\s+section\s+in\s+profile\.sections\s*\}\}/.test(html)) err('v_no_sections_loop');
  if (cover && !/\{\{\s*for\s+\w+\s+in\s+paragraphs\s*\}\}|\{\{\s*letter\.body\s*\}\}/.test(html)) err('v_no_paragraphs');
  if (!/\{\{\s*direction\s*\}\}/.test(html)) warn('v_no_dir');
  if (!/\{\{\s*font_base64\s*\}\}/.test(html)) warn('v_no_font_placeholder');

  // --- structure via DOMParser
  const doc = new DOMParser().parseFromString(html, 'text/html');
  if (!/^\s*<!doctype html/i.test(html)) warn('v_no_doctype');

  if (doc.querySelector('script')) err('v_script');
  if (doc.querySelector('iframe, frame, frameset')) err('v_iframe');
  if (doc.querySelector('object, embed, applet')) err('v_object');
  if (doc.querySelector('base')) err('v_base');
  if (doc.querySelector('meta[http-equiv="refresh" i]')) err('v_refresh');
  if (doc.querySelector('form')) warn('v_form');

  let inline = false;
  let bad = null;
  for (const el of doc.querySelectorAll('*')) {
    for (const a of el.attributes) {
      if (/^on/i.test(a.name)) inline = a.name;
      if (/^\s*javascript:/i.test(a.value)) bad = a.name;
      if (URL_ATTRS.includes(a.name.toLowerCase()) && EXTERNAL.test(a.value)) err('v_external', a.value.slice(0, 80));
      if (a.name === 'style') externalInCss(a.value).forEach((u) => err('v_external', u.slice(0, 80)));
    }
  }
  if (inline) err('v_inline_handler', inline);
  if (bad) err('v_js_url', bad);

  const styles = [...doc.querySelectorAll('style')].map((s) => s.textContent).join('\n');
  externalInCss(styles).forEach((u) => err('v_external', u.slice(0, 80)));
  if (doc.querySelector('link[rel~="stylesheet" i], link[rel~="preload" i]')) err('v_external', '<link>');
  if (!doc.querySelector('style')) warn('v_no_style');
  if (!/@font-face/i.test(styles)) err('v_no_fontface');
  else if (!/url\(\s*['"]?data:font\/woff2;base64,\s*\{\{\s*font_base64\s*\}\}/i.test(styles)) warn('v_fontface_form');

  for (const tag of SEMANTIC) {
    if (!cover && !doc.querySelector(tag)) warn('v_semantic', tag);
  }
  if (!/@page/i.test(styles)) warn('v_no_page');
  if (!/break-inside|page-break-inside/i.test(styles)) warn('v_no_break');

  return { errors, warnings, ok: errors.length === 0 };
}

/** Remove dangerous constructs (scripts, iframes, handlers, external resources). Keeps {{ }} tags. */
export function sanitizeTemplate(html) {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  doc.querySelectorAll('script, iframe, frame, frameset, object, embed, applet, base, link, form').forEach((n) => n.remove());
  doc.querySelectorAll('meta[http-equiv]').forEach((n) => n.remove());
  for (const el of doc.querySelectorAll('*')) {
    for (const a of [...el.attributes]) {
      const n = a.name.toLowerCase();
      if (n.startsWith('on') || /^\s*javascript:/i.test(a.value) || (URL_ATTRS.includes(n) && EXTERNAL.test(a.value))) el.removeAttribute(a.name);
    }
  }
  for (const s of doc.querySelectorAll('style')) {
    s.textContent = s.textContent
      .replace(/@import\s+[^;]+;/gi, '')
      .replace(/url\(\s*(['"]?)(?:https?:)?\/\/[^)]*\1\s*\)/gi, 'none');
  }
  return '<!DOCTYPE html>\n' + doc.documentElement.outerHTML;
}
