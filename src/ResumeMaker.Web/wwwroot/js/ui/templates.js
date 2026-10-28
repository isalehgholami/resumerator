// Lead Builder (AI template prompt) + Import HTML with validation + custom template library.
import { h, field, selectField, button, toast, pickFile, copyWithToast, clear, debounce, formatTime } from './dom.js';
import { t, getUiLang } from '../i18n.js';
import * as store from '../store.js';
import * as assets from '../assets.js';
import { templateLead, AI_LINKS, STYLES } from '../lead.js';
import { validateTemplate, sanitizeTemplate } from '../validate.js';
import { createPreview } from './preview.js';
import { renderReport } from './report.js';

export async function mount(root) {
  const opts = { style: 'minimal', lang: 'fa', columns: 1, sidebar: 'none', primary: '#1a73e8', accent: '#ff6b6b', font: '', notes: '' };
  const imp = { html: '', name: '', editingId: null };
  let fonts = {};
  try { fonts = await assets.loadFonts(); } catch { /* preview will report */ }
  let sampleProfile = null;
  const profiles = store.listProfiles();
  let previewProfile = profiles[0] || null;
  if (!previewProfile) { try { sampleProfile = await assets.loadSample('fa'); previewProfile = sampleProfile; } catch { /* none */ } }

  // ---------- 1. prompt generator
  const promptBox = h('textarea', { id: 'lead-out', readOnly: true, rows: 14, class: 'mono', 'aria-label': t('generated_prompt'), dir: 'ltr' });
  const regen = () => { promptBox.value = templateLead({ ...opts, font: opts.font || (opts.lang === 'fa' ? 'Vazirmatn' : 'Inter') }); };
  const upd = (k) => (v) => { opts[k] = v; regen(); sidebarSel.hidden = opts.columns !== 2; };
  const sidebarSel = selectField({
    label: t('lead_sidebar'), value: opts.sidebar, onchange: upd('sidebar'),
    options: [{ value: 'none', label: t('none') }, { value: 'left', label: t('left') }, { value: 'right', label: t('right') }],
  });
  sidebarSel.hidden = true;
  const colorField = (k, label) => {
    const id = `lead-${k}`;
    return h('div', { class: 'field' }, h('label', { for: id }, label), h('input', { id, type: 'color', value: opts[k], oninput: (e) => upd(k)(e.target.value) }));
  };
  const form = h('div', { class: 'grid' },
    selectField({ label: t('lead_style'), value: opts.style, onchange: upd('style'), options: STYLES.map((s) => ({ value: s, label: t(`style_${s}`) })) }),
    selectField({ label: t('resume_language'), value: opts.lang, onchange: upd('lang'), options: [{ value: 'fa', label: 'فارسی (RTL)' }, { value: 'en', label: 'English (LTR)' }] }),
    selectField({ label: t('lead_columns'), value: String(opts.columns), onchange: (v) => upd('columns')(Number(v)), options: [{ value: '1', label: '1' }, { value: '2', label: '2' }] }),
    sidebarSel,
    colorField('primary', t('primary_color')), colorField('accent', t('accent_color')),
    selectField({ label: t('font'), value: opts.font, onchange: upd('font'), options: [{ value: '', label: t('font_auto') }, { value: 'Vazirmatn', label: 'Vazirmatn' }, { value: 'Inter', label: 'Inter' }] }),
    field({ obj: opts, key: 'notes', label: t('lead_notes'), multiline: true, rows: 2, onchange: regen }));
  regen();

  const aiLinks = h('div', { class: 'row' }, h('span', { class: 'muted' }, t('open_in')),
    AI_LINKS.map((a) => h('a', { class: 'btn', href: a.url, target: '_blank', rel: 'noopener noreferrer' }, a.name)));
  const history = h('details', { class: 'history' }, h('summary', {}, t('prompt_history')));
  const drawHistory = () => {
    clear(history).append(h('summary', {}, t('prompt_history')));
    const list = store.listPrompts();
    if (!list.length) history.append(h('p', { class: 'muted' }, '—'));
    list.forEach((p) => history.append(h('div', { class: 'hist-row' },
      h('span', { class: 'muted' }, `${formatTime(p.ts)} · ${p.kind}`),
      button(t('copy'), () => copyWithToast(p.text), { cls: 'small' }))));
  };
  drawHistory();

  const genSection = h('section', { class: 'card-block', 'aria-labelledby': 'h-lead' },
    h('h2', { id: 'h-lead' }, `1. ${t('lead_generate')}`), h('p', { class: 'muted' }, t('lead_help')), form,
    h('label', { for: 'lead-out', class: 'lbl' }, t('generated_prompt')), promptBox,
    h('div', { class: 'row' },
      button(t('copy_prompt'), async () => { store.savePrompt('template', promptBox.value); drawHistory(); await copyWithToast(promptBox.value); }, { kind: 'primary' }),
      aiLinks),
    history);

  // ---------- 2. import
  const report = h('div', { class: 'report', 'aria-live': 'polite' });
  const htmlBox = h('textarea', { id: 'imp-html', rows: 10, class: 'mono', dir: 'ltr', placeholder: '<!DOCTYPE html> ...', 'aria-label': t('paste_html') });
  const nameBox = h('input', { id: 'imp-name', type: 'text', placeholder: t('template_name'), 'aria-label': t('template_name'), oninput: (e) => { imp.name = e.target.value; checkSave(); } });
  let lastResult = null;
  const saveBtn = button(t('save_template'), () => save(), { kind: 'primary', disabled: true });
  const sanitizeBtn = button(t('sanitize'), () => {
    htmlBox.value = sanitizeTemplate(htmlBox.value); imp.html = htmlBox.value; validate(); toast(t('sanitized'));
  });

  const pv = createPreview(() => {
    if (!imp.html.trim()) throw new Error(t('nothing_to_preview'));
    if (!previewProfile) throw new Error(t('no_profile_for_preview'));
    return assets.renderHtml(imp.html, previewProfile, fonts);
  });
  const profSel = selectField({
    label: t('preview_with'), value: previewProfile?.id || '',
    options: profiles.length ? profiles.map((p) => ({ value: p.id, label: p.name })) : [{ value: '', label: '(sample)' }],
    onchange: (v) => { previewProfile = store.getProfile(v) || previewProfile; pv.update(); },
  });

  function checkSave() { saveBtn.disabled = !(lastResult && lastResult.ok && imp.name.trim()); }
  function validate() {
    imp.html = htmlBox.value;
    lastResult = imp.html.trim() ? validateTemplate(imp.html) : null;
    renderReport(report, lastResult);
    sanitizeBtn.hidden = !lastResult || lastResult.ok;
    checkSave();
    pv.update();
  }
  const validateSoon = debounce(validate, 250);
  htmlBox.addEventListener('input', validateSoon);

  function save() {
    if (!lastResult?.ok) return;
    const rec = store.saveCustomTemplate({ id: imp.editingId, name: imp.name.trim(), html: imp.html });
    imp.editingId = null;
    toast(t('template_saved'));
    drawLibrary();
    return rec;
  }

  const impSection = h('section', { class: 'card-block', 'aria-labelledby': 'h-imp' },
    h('h2', { id: 'h-imp' }, `2. ${t('import_html')}`), h('p', { class: 'muted' }, t('import_help')),
    h('div', { class: 'row' }, button(t('choose_file'), async () => {
      const f = await pickFile('.html,.htm,text/html');
      if (!f) return;
      htmlBox.value = f.text; nameBox.value = imp.name = imp.name || f.name.replace(/\.html?$/i, ''); validate();
    })),
    h('label', { for: 'imp-html', class: 'lbl' }, t('paste_html')), htmlBox,
    h('div', { class: 'row' }, nameBox, button(t('validate'), validate), sanitizeBtn, saveBtn),
    report,
    h('div', { class: 'prev-wrap' }, profSel, pv.el));
  sanitizeBtn.hidden = true;

  // ---------- 3. library
  const lib = h('div', { class: 'cards' });
  function drawLibrary() {
    clear(lib);
    const list = store.getCustomTemplates();
    if (!list.length) lib.append(h('p', { class: 'muted' }, t('no_custom_templates')));
    list.forEach((c) => lib.append(h('article', { class: 'card' },
      h('h3', {}, c.name), h('p', { class: 'muted' }, formatTime(c.updated_at)),
      h('div', { class: 'row' },
        button(t('edit'), () => {
          imp.editingId = c.id; imp.name = c.name; nameBox.value = c.name; htmlBox.value = c.html; validate();
          htmlBox.scrollIntoView({ behavior: 'smooth', block: 'center' }); htmlBox.focus();
        }),
        button(t('delete'), () => { if (confirm(t('confirm_delete_template'))) { store.deleteCustomTemplate(c.id); drawLibrary(); } }, { kind: 'danger' })))));
  }
  drawLibrary();
  const libSection = h('section', { class: 'card-block', 'aria-labelledby': 'h-lib' }, h('h2', { id: 'h-lib' }, `3. ${t('my_templates')}`), lib);

  root.append(h('section', { class: 'page' }, h('h1', {}, t('lead_builder')), genSection, impSection, libSection));
  return () => pv.destroy();
}
