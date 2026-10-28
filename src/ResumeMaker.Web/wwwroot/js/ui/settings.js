// Settings: UI language, theme (UI only), backups, data.
import { h, selectField, button, toast, clear, pickFile, formatTime } from './dom.js';
import { t, getUiLang, setUiLang } from '../i18n.js';
import * as store from '../store.js';
import { applyTheme } from '../theme.js';
import { download } from '../export.js';

export function mount(root, { rerender }) {
  const s = store.getSettings();
  const backups = h('ul', { class: 'backups' });
  const drawBackups = () => {
    clear(backups);
    const list = store.listBackups();
    if (!list.length) backups.append(h('li', { class: 'muted' }, t('no_backups')));
    list.forEach((b, i) => backups.append(h('li', {},
      h('span', {}, `${formatTime(b.ts)} — ${b.profiles.length} ${t('profiles_n')}`),
      button(t('restore'), () => {
        if (!confirm(t('confirm_restore'))) return;
        store.restoreBackup(i); drawBackups(); toast(t('restored'));
      }, { cls: 'small' }))));
  };
  drawBackups();

  root.append(h('section', { class: 'page narrow' },
    h('h1', {}, t('settings')),
    h('div', { class: 'card-block' },
      selectField({
        label: t('ui_language'), value: getUiLang(),
        options: [{ value: 'fa', label: 'فارسی' }, { value: 'en', label: 'English' }],
        onchange: (v) => { setUiLang(v); store.setSettings({ ui_lang: v }); rerender(); },
      }),
      selectField({
        label: t('theme'), value: s.theme || 'system',
        options: [{ value: 'system', label: t('theme_system') }, { value: 'light', label: t('theme_light') }, { value: 'dark', label: t('theme_dark') }],
        onchange: (v) => { store.setSettings({ theme: v }); applyTheme(v); },
      }),
      h('p', { class: 'hint' }, t('theme_ui_only'))),
    h('div', { class: 'card-block' },
      h('h2', {}, t('data')), h('p', { class: 'muted' }, store.isPersistent() ? t('data_local') : t('storage_unavailable')),
      h('div', { class: 'row' },
        button(t('export_all'), () => download('resumerator-backup.json', store.exportAllJson(), 'application/json;charset=utf-8')),
        button(t('import_json'), async () => {
          const f = await pickFile('.json,application/json');
          if (!f) return;
          try { toast(t('imported_n').replace('{n}', store.importJsonText(f.text).length)); drawBackups(); } catch (e) { toast(`${t('import_failed')}: ${e.message}`, 'error'); }
        }),
        button(t('clear_all'), () => { if (confirm(t('confirm_clear_all'))) { store.snapshot(true); store.clearAll(); toast(t('cleared')); } }, { kind: 'danger' }))),
    h('div', { class: 'card-block' },
      h('h2', {}, t('backups')), h('p', { class: 'muted' }, t('backups_help')),
      button(t('backup_now'), () => { store.snapshot(true); drawBackups(); toast(t('backup_done')); }),
      backups)));
}
