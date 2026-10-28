// Cover Letter Builder: form -> lead prompt -> paste AI text -> edit -> live preview -> export.
import { h, field, selectField, button, toast, copyWithToast } from './dom.js';
import { t } from '../i18n.js';
import * as store from '../store.js';
import * as assets from '../assets.js';
import { coverLetterLead, AI_LINKS, TONES } from '../lead.js';
import { createPreview } from './preview.js';
import { exportHtml, printPdf } from '../export.js';

const FALLBACK = `<!DOCTYPE html><html dir="{{ direction }}" lang="{{ lang }}"><head><meta charset="utf-8"><title>{{ labels.cover_letter }}</title>
<style>@font-face{font-family:"{{ font_family }}";src:url(data:font/woff2;base64,{{ font_base64 }}) format("woff2")}
@page{size:A4;margin:1cm}body{font-family:"{{ font_family }}",sans-serif;line-height:1.8;padding:1cm;color:#222}h1{color:{{ primary_color }}}</style></head>
<body><header><h1>{{ profile.contact.full_name }}</h1></header><main>{{ for p in paragraphs }}<p>{{ p }}</p>{{ end }}</main></body></html>`;

export async function mount(root, { params }) {
  const profile = store.getProfile(params[0]);
  if (!profile) {
    root.append(h('section', { class: 'page' }, h('h1', {}, t('not_found')), h('a', { class: 'btn primary', href: '#/' }, t('back_to_list'))));
    return;
  }
  const cl = (profile.cover_letter = { company: '', position: '', keywords: '', tone: 'formal', lang: profile.language, body: '', ...(profile.cover_letter || {}) });
  let fonts = {};
  let source = FALLBACK;
  try { fonts = await assets.loadFonts(); } catch { /* ignore */ }
  try { source = await assets.fetchText('templates/cover-letter.html'); } catch { toast(t('cover_template_missing'), 'error'); }

  const html = () => assets.renderHtml(source, { ...profile, language: cl.lang }, fonts, { letter: { company: cl.company, position: cl.position, body: cl.body } });
  const pv = createPreview(html);
  const promptBox = h('textarea', { id: 'cl-prompt', readOnly: true, rows: 10, class: 'mono', dir: 'ltr', 'aria-label': t('generated_prompt') });
  const regen = () => { promptBox.value = coverLetterLead({ profile, company: cl.company, position: cl.position, keywords: cl.keywords, tone: cl.tone, lang: cl.lang }); };
  const changed = () => { store.scheduleSave(profile); regen(); pv.schedule(); };
  regen();

  const form = h('div', { class: 'grid' },
    field({ obj: cl, key: 'company', label: t('cl_company'), onchange: changed }),
    field({ obj: cl, key: 'position', label: t('cl_position'), onchange: changed }),
    selectField({ label: t('cl_tone'), value: cl.tone, onchange: (v) => { cl.tone = v; changed(); }, options: TONES.map((x) => ({ value: x, label: t(`tone_${x}`) })) }),
    selectField({ label: t('resume_language'), value: cl.lang, onchange: (v) => { cl.lang = v; changed(); }, options: [{ value: 'fa', label: 'فارسی (RTL)' }, { value: 'en', label: 'English (LTR)' }] }),
    Object.assign(field({ obj: cl, key: 'keywords', label: t('cl_keywords'), multiline: true, rows: 4, hint: t('cl_keywords_hint'), onchange: changed }), {}));
  form.lastChild.classList.add('wide');

  const body = field({ obj: cl, key: 'body', label: t('cl_body'), multiline: true, rows: 14, hint: t('cl_body_hint'), onchange: changed, dir: cl.lang === 'fa' ? 'rtl' : 'ltr' });

  const doHtml = () => { try { exportHtml(html(), `${profile.name}-cover-letter`); } catch (e) { toast(e.message, 'error'); } };
  const doPdf = async () => { try { toast(t('print_hint')); await printPdf(html()); } catch (e) { toast(e.message, 'error'); } };

  const left = h('div', { class: 'cl-left' },
    h('section', { class: 'card-block' }, h('h2', {}, `1. ${t('cl_details')}`), form),
    h('section', { class: 'card-block' }, h('h2', {}, `2. ${t('lead_generate')}`),
      h('label', { for: 'cl-prompt', class: 'lbl' }, t('generated_prompt')), promptBox,
      h('div', { class: 'row' },
        button(t('copy_prompt'), async () => { store.savePrompt('cover-letter', promptBox.value); await copyWithToast(promptBox.value); }, { kind: 'primary' }),
        h('span', { class: 'muted' }, t('open_in')),
        AI_LINKS.map((a) => h('a', { class: 'btn', href: a.url, target: '_blank', rel: 'noopener noreferrer' }, a.name)))),
    h('section', { class: 'card-block' }, h('h2', {}, `3. ${t('cl_paste')}`), body,
      h('div', { class: 'row' }, button(t('export_pdf'), doPdf, { kind: 'primary' }), button(t('download_html'), doHtml))));

  root.append(h('section', { class: 'page wide-page' },
    h('div', { class: 'page-head' }, h('h1', {}, t('cover_letter')), h('a', { class: 'btn', href: `#/edit/${profile.id}` }, `← ${t('back_to_resume')}`)),
    h('div', { class: 'cl-layout' }, left, h('div', { class: 'cl-preview' }, pv.el))));
  pv.update();
  return () => { store.flush(); pv.destroy(); };
}
