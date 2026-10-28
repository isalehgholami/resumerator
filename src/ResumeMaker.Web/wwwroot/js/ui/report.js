// Renders a validateTemplate() result as an accessible list.
import { h, clear } from './dom.js';
import { t } from '../i18n.js';

export function renderReport(host, result) {
  clear(host);
  if (!result) return;
  const row = (kind, item) => h('li', { class: kind },
    h('span', { class: 'ico', 'aria-hidden': 'true' }, kind === 'err' ? '✕' : '!'),
    h('span', {}, h('span', { class: 'sr-only' }, kind === 'err' ? `${t('error')}: ` : `${t('warning')}: `),
      t(item.code) + (item.text ? ` — ${item.text}` : '')));
  host.append(
    h('p', { class: `verdict ${result.ok ? 'ok' : 'bad'}` }, result.ok ? `✓ ${t('v_ok')}` : `✕ ${t('v_fail')}`),
    result.errors.length ? h('ul', { class: 'issues' }, result.errors.map((e) => row('err', e))) : null,
    result.warnings.length ? h('ul', { class: 'issues' }, result.warnings.map((e) => row('warn', e))) : null);
}
