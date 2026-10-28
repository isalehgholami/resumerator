// Profile list: new / duplicate / delete / import / export, "try with sample".
import { h, button, toast, pickFile, formatTime, clear } from './dom.js';
import { t, getUiLang } from '../i18n.js';
import * as store from '../store.js';
import * as assets from '../assets.js';
import { exportJson } from '../export.js';

export async function addSample(lang) {
  const raw = await assets.loadSample(lang);
  raw.id = store.uid();
  return store.addProfile(raw);
}

export function mount(root, { navigate }) {
  const page = h('section', { class: 'page' });
  const listHost = h('div', { class: 'cards' });

  async function doImport() {
    const f = await pickFile('.json,application/json');
    if (!f) return;
    try {
      const added = store.importJsonText(f.text);
      toast(t('imported_n').replace('{n}', added.length));
      draw();
    } catch (e) { toast(`${t('import_failed')}: ${e.message}`, 'error'); }
  }
  async function doSample(lang) {
    try { const p = await addSample(lang); navigate(`#/edit/${p.id}`); }
    catch (e) { toast(`${t('sample_failed')}: ${e.message}`, 'error'); }
  }

  function draw() {
    clear(listHost);
    const list = store.listProfiles();
    if (!list.length) {
      listHost.append(h('div', { class: 'empty' },
        h('h2', {}, t('no_profiles')), h('p', { class: 'muted' }, t('no_profiles_hint')),
        h('div', { class: 'row' }, button(t('try_sample_fa'), () => doSample('fa'), { kind: 'primary' }), button(t('try_sample_en'), () => doSample('en')))));
      return;
    }
    for (const p of list) {
      const title = p.contact?.full_name || '—';
      listHost.append(h('article', { class: 'card', lang: p.language, dir: p.language === 'fa' ? 'rtl' : 'ltr' },
        h('h2', {}, h('a', { href: `#/edit/${p.id}` }, p.name)),
        h('p', { class: 'muted' }, [title, p.contact?.title].filter(Boolean).join(' · ')),
        h('p', { class: 'meta' },
          h('span', { class: 'badge' }, p.language === 'fa' ? 'FA' : 'EN'), ' ',
          h('span', { class: 'badge' }, p.ats_mode ? 'ATS' : p.template_id), ' ',
          h('span', { class: 'muted' }, `${t('updated')}: ${formatTime(p.updated_at)}`)),
        h('div', { class: 'row', dir: document.documentElement.dir },
          h('a', { class: 'btn primary', href: `#/edit/${p.id}` }, t('edit')),
          button(t('duplicate'), () => { store.duplicateProfile(p.id, ` (${t('copy')})`); draw(); toast(t('duplicated')); }),
          button(t('export_json'), () => exportJson(store.exportProfileJson(p), p.name)),
          button(t('delete'), () => {
            if (!confirm(`${t('confirm_delete_profile')}\n\n${p.name}`)) return;
            store.deleteProfile(p.id); draw(); toast(t('deleted'));
          }, { kind: 'danger' }))));
    }
  }

  page.append(
    h('div', { class: 'page-head' }, h('h1', {}, t('profiles')),
      h('div', { class: 'row' },
        button(`+ ${t('new_profile')}`, () => {
          const p = store.addProfile(store.newProfile(getUiLang()));
          navigate(`#/edit/${p.id}`);
        }, { kind: 'primary' }),
        button(t('import_json'), doImport),
        button(t('try_sample_fa'), () => doSample('fa')),
        button(t('try_sample_en'), () => doSample('en')))),
    listHost);
  root.append(page);
  draw();
  return store.subscribe((type) => { if (type === 'profiles') draw(); });
}
