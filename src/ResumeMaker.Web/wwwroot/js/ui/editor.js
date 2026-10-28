// Profile editor: content / sections / design / export tabs + live preview.
import { h, field, selectField, checkbox, button, toast, clear, formatTime } from './dom.js';
import { t } from '../i18n.js';
import * as store from '../store.js';
import * as assets from '../assets.js';
import { createPreview } from './preview.js';
import { createSectionsEditor } from './sections.js';
import { exportHtml, exportJson, printPdf, exportPng } from '../export.js';

const TABS = ['content', 'sections', 'design', 'export', 'preview'];

export async function mount(root, { params, navigate }) {
  const profile = store.getProfile(params[0]);
  if (!profile) {
    root.append(h('section', { class: 'page' }, h('h1', {}, t('not_found')), h('a', { class: 'btn primary', href: '#/' }, t('back_to_list'))));
    return;
  }
  root.append(h('p', { class: 'muted loading' }, t('loading')));

  let fonts;
  let source = '';
  let sourceError = null;
  async function loadSource() {
    sourceError = null;
    try { source = await assets.getTemplateSource(assets.effectiveTemplateId(profile)); } catch (e) { sourceError = e; }
  }
  try { fonts = await assets.loadFonts(); } catch (e) { fonts = {}; sourceError = e; }
  await loadSource();
  clear(root);

  const currentHtml = () => {
    if (sourceError) throw sourceError;
    return assets.renderHtml(source, profile, fonts);
  };
  const preview = createPreview(currentHtml);

  // ---- save status
  const status = h('span', { class: 'save-status', role: 'status', 'aria-live': 'polite' }, `${t('saved')} ${formatTime(profile.updated_at)}`);
  const offStore = store.subscribe((type, d) => {
    if (type === 'saving') status.textContent = t('saving');
    if (type === 'saved') status.textContent = d.ok ? `${t('saved')} ${formatTime(d.at)}` : t('save_failed');
  });
  const changed = () => { store.scheduleSave(profile); preview.schedule(); };

  // ---- top bar
  const nameInput = h('input', {
    class: 'name-input', type: 'text', value: profile.name, 'aria-label': t('profile_name'),
    oninput: (e) => { profile.name = e.target.value; changed(); },
  });
  const top = h('div', { class: 'ed-top' },
    h('a', { class: 'btn', href: '#/' }, `← ${t('profiles')}`), nameInput, status,
    h('a', { class: 'btn', href: `#/cover/${profile.id}` }, t('cover_letter')),
    button(t('export_pdf'), () => doPdf(), { kind: 'primary' }));

  // ---- tabs
  const tablist = h('div', { class: 'tabs', role: 'tablist', 'aria-label': t('editor') });
  const panels = {};
  const tabBtns = {};
  const wrap = h('div', { class: 'editor', dataset: { tab: 'content' } });
  function selectTab(name, focus) {
    wrap.dataset.tab = name;
    for (const k of TABS) {
      tabBtns[k].setAttribute('aria-selected', String(k === name));
      tabBtns[k].tabIndex = k === name ? 0 : -1;
      panels[k].hidden = k !== name;
    }
    if (name === 'preview') preview.update();
    if (focus) tabBtns[name].focus();
  }
  TABS.forEach((k, i) => {
    tabBtns[k] = h('button', {
      type: 'button', role: 'tab', id: `tab-${k}`, 'aria-controls': `panel-${k}`, class: k === 'preview' ? 'tab only-narrow' : 'tab',
      onclick: () => selectTab(k),
      onkeydown: (e) => {
        const vis = TABS.filter((x) => tabBtns[x].offsetParent !== null);
        const j = vis.indexOf(k);
        const dir = document.documentElement.dir === 'rtl' ? -1 : 1;
        let n = null;
        if (e.key === 'ArrowRight') n = vis[(j + dir + vis.length) % vis.length];
        if (e.key === 'ArrowLeft') n = vis[(j - dir + vis.length) % vis.length];
        if (e.key === 'Home') n = vis[0];
        if (e.key === 'End') n = vis[vis.length - 1];
        if (n) { e.preventDefault(); selectTab(n, true); }
      },
    }, t(`tab_${k}`));
    tablist.append(tabBtns[k]);
    panels[k] = h('div', { role: 'tabpanel', id: `panel-${k}`, 'aria-labelledby': `tab-${k}`, class: 'panel', tabindex: '0' });
  });

  // ---- content panel
  const c = profile.contact;
  panels.content.append(
    h('h2', {}, t('contact')),
    h('div', { class: 'grid' },
      field({ obj: c, key: 'full_name', label: t('f_full_name'), onchange: changed, autocomplete: 'name' }),
      field({ obj: c, key: 'title', label: t('f_job_title'), onchange: changed }),
      field({ obj: c, key: 'email', label: t('f_email'), type: 'email', dir: 'ltr', onchange: changed, autocomplete: 'email' }),
      field({ obj: c, key: 'phone', label: t('f_phone'), type: 'tel', dir: 'ltr', onchange: changed, autocomplete: 'tel' }),
      field({ obj: c, key: 'location', label: t('f_location'), onchange: changed })),
    h('h2', {}, t('links')),
    h('div', { class: 'grid' },
      ...['linkedin', 'github', 'website', 'twitter'].map((k) =>
        field({ obj: c.links, key: k, label: t(`f_${k}`), dir: 'ltr', onchange: changed }))),
    h('h2', {}, t('summary')),
    field({ obj: profile, key: 'summary', label: t('summary'), multiline: true, rows: 5, onchange: changed }),
  );

  // ---- sections panel
  panels.sections.append(h('p', { class: 'muted' }, t('sections_help')), createSectionsEditor(profile, changed));

  // ---- design panel
  const tplBox = h('fieldset', { class: 'tpl-grid' }, h('legend', {}, t('template')));
  async function setTemplate(id) {
    profile.template_id = id;
    await loadSource();
    changed(); drawTemplates();
  }
  function drawTemplates() {
    clear(tplBox).append(h('legend', {}, t('template')));
    const all = [
      ...assets.BUILTIN_TEMPLATES.map((id) => ({ id, name: t(`tpl_${id}`), desc: t(`tpl_${id}_d`) })),
      ...store.getCustomTemplates().map((x) => ({ id: x.id, name: x.name, desc: t('custom_template'), custom: true })),
    ];
    for (const tp of all) {
      const rid = `tpl-${tp.id}`;
      tplBox.append(h('div', { class: `tpl${tp.id === profile.template_id ? ' sel' : ''}` },
        h('input', { type: 'radio', name: 'tpl', id: rid, value: tp.id, checked: tp.id === profile.template_id, disabled: profile.ats_mode, onchange: () => setTemplate(tp.id) }),
        h('label', { for: rid }, h('strong', {}, tp.name), h('small', {}, tp.desc)),
        tp.custom ? button('✕', () => {
          if (!confirm(t('confirm_delete_template'))) return;
          store.deleteCustomTemplate(tp.id);
          if (profile.template_id === tp.id) setTemplate('minimal'); else drawTemplates();
        }, { cls: 'icon danger', ariaLabel: `${t('remove')}: ${tp.name}` }) : null));
    }
  }
  drawTemplates();
  const th = profile.theme;
  const colorRow = (key, label, def) => {
    const id = `col-${key}`;
    const inp = h('input', { type: 'color', id, value: th[key] || def, oninput: (e) => { th[key] = e.target.value; changed(); } });
    return h('div', { class: 'field color' }, h('label', { for: id }, label), inp,
      button(t('reset'), () => { th[key] = def; inp.value = def; changed(); }, { cls: 'small' }));
  };
  panels.design.append(
    selectField({
      label: t('resume_language'), value: profile.language,
      options: [{ value: 'fa', label: 'فارسی (RTL)' }, { value: 'en', label: 'English (LTR)' }],
      onchange: (v) => { profile.language = v; changed(); },
    }),
    h('p', { class: 'hint' }, t('resume_language_hint')),
    tplBox,
    checkbox({ label: t('ats_mode'), checked: profile.ats_mode, onchange: async (v) => { profile.ats_mode = v; await loadSource(); drawTemplates(); changed(); } }),
    h('p', { class: 'hint' }, t('ats_hint')),
    h('h2', {}, t('colors')),
    h('div', { class: 'grid' }, colorRow('primary_color', t('primary_color'), '#1a73e8'), colorRow('accent_color', t('accent_color'), '#ff6b6b')),
    selectField({
      label: t('font'), value: th.font_family || '',
      options: [{ value: '', label: t('font_auto') }, { value: 'Vazirmatn', label: 'Vazirmatn' }, { value: 'Inter', label: 'Inter' }],
      onchange: (v) => { th.font_family = v || null; changed(); },
    }),
    h('p', { class: 'hint' }, t('theme_note')),
    h('p', {}, h('a', { href: '#/templates' }, t('open_lead_builder'))),
  );

  // ---- export panel
  const exportMsg = h('p', { class: 'export-msg', role: 'status', 'aria-live': 'polite' });
  const safeHtml = () => { try { return currentHtml(); } catch (e) { toast(e.message, 'error'); return null; } };
  async function doPdf() { const html = safeHtml(); if (html) { toast(t('print_hint')); await printPdf(html); } }
  async function doPng(dpi) {
    const html = safeHtml();
    if (!html) return;
    exportMsg.textContent = t('png_working');
    const r = await exportPng(html, dpi, profile.name);
    exportMsg.textContent = r.ok ? t('png_done') : t('png_unsupported');
    if (!r.ok) console.warn('PNG export failed:', r.reason);
  }
  const col = (title, ...btns) => h('div', { class: 'exp-card' }, h('h3', {}, title), ...btns);
  panels.export.append(
    h('div', { class: 'exp-grid' },
      col('PDF', h('p', { class: 'muted' }, t('pdf_desc')), button(t('export_pdf'), doPdf, { kind: 'primary' })),
      col('HTML', h('p', { class: 'muted' }, t('html_desc')), button(t('download_html'), () => { const x = safeHtml(); x && exportHtml(x, profile.name); })),
      col('PNG', h('p', { class: 'muted' }, t('png_desc')),
        h('div', { class: 'row' }, button('150 DPI', () => doPng(150)), button('300 DPI', () => doPng(300)))),
      col('JSON', h('p', { class: 'muted' }, t('json_desc')), button(t('download_json'), () => { store.flush(); exportJson(store.exportProfileJson(profile), profile.name); }))),
    exportMsg);

  // ---- assemble
  const formCol = h('div', { class: 'ed-form' }, tablist, ...TABS.map((k) => panels[k]));
  const prevCol = h('div', { class: 'ed-preview' }, preview.el);
  panels.preview.append(h('p', { class: 'muted' }, t('preview_tab_hint')));
  wrap.append(top, h('div', { class: 'ed-main' }, formCol, prevCol));
  root.append(wrap);
  // The mobile "preview" tab reuses the always-present preview column; the panel itself stays empty.
  selectTab('content');
  preview.update();

  return () => { store.flush(); offStore(); preview.destroy(); };
}
