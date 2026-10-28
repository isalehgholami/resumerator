// Bootstrap + hash router.
import { t, setUiLang, detectLang, getUiLang } from './i18n.js';
import * as store from './store.js';
import { applyTheme } from './theme.js';
import { h, clear, toast } from './ui/dom.js';
import * as profilesView from './ui/profiles.js';
import * as editorView from './ui/editor.js';
import * as templatesView from './ui/templates.js';
import * as coverView from './ui/cover.js';
import * as settingsView from './ui/settings.js';

const GITHUB = 'https://github.com/isalehgholami/resumerator';
const ROUTES = [
  { re: /^$/, nav: 'profiles', view: profilesView },
  { re: /^edit\/([^/]+)$/, nav: 'profiles', view: editorView },
  { re: /^cover\/([^/]+)$/, nav: 'profiles', view: coverView },
  { re: /^templates$/, nav: 'templates', view: templatesView },
  { re: /^settings$/, nav: 'settings', view: settingsView },
];

const main = document.getElementById('main');
let cleanup = null;
let token = 0;
let first = true;

export function navigate(hash) { if (location.hash === hash) route(); else location.hash = hash; }

function drawShell(current) {
  const top = document.getElementById('topbar');
  clear(top).append(
    h('a', { class: 'brand', href: '#/' }, h('span', { class: 'logo', 'aria-hidden': 'true' }, '▤'), t('app_name')),
    h('nav', { 'aria-label': t('nav') }, h('ul', {},
      [['profiles', '#/', t('nav_profiles')], ['templates', '#/templates', t('nav_templates')], ['settings', '#/settings', t('nav_settings')]]
        .map(([id, href, label]) => h('li', {}, h('a', { href, 'aria-current': id === current ? 'page' : null }, label))))));
  clear(document.getElementById('footer')).append(
    h('span', {}, t('footer_open_source')), ' · ',
    h('a', { href: GITHUB, target: '_blank', rel: 'noopener noreferrer' }, `${t('github')}: isalehgholami/resumerator`));
  document.getElementById('skip-link').textContent = t('skip');
  document.title = t('app_name');
}

async function route() {
  const my = ++token;
  if (cleanup) { try { cleanup(); } catch { /* ignore */ } cleanup = null; }
  store.flush();
  const path = location.hash.replace(/^#\/?/, '');
  let match = null;
  for (const r of ROUTES) { const m = r.re.exec(path); if (m) { match = { r, params: m.slice(1) }; break; } }
  if (!match) match = { r: ROUTES[0], params: [] };
  drawShell(match.r.nav);
  clear(main);
  try {
    const c = await match.r.view.mount(main, { params: match.params, navigate, rerender: route });
    if (my !== token) { c && c(); return; }
    cleanup = typeof c === 'function' ? c : null;
    if (!first) main.focus({ preventScroll: true });
    first = false;
  } catch (e) {
    console.error(e);
    main.append(h('p', { class: 'error-box', role: 'alert' }, `${e.message}`));
  }
}

async function firstRun() {
  if (store.isSeeded() || store.listProfiles().length) { store.markSeeded(); return; }
  try { await profilesView.addSample(getUiLang()); } catch { /* sample optional */ }
  store.markSeeded();
}

async function boot() {
  const s = store.getSettings();
  setUiLang(detectLang(s.ui_lang));
  applyTheme(s.theme);
  window.addEventListener('hashchange', route);
  window.addEventListener('beforeunload', () => store.flush());
  document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'hidden') store.flush(); });
  window.addEventListener('unhandledrejection', (e) => toast(String(e.reason && e.reason.message || e.reason), 'error'));
  await firstRun();
  route();
}
boot();
