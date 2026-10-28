// Live preview: sandboxed srcdoc iframe (sandbox="" => no scripts, unique origin), scaled to fit.
import { h } from './dom.js';
import { t } from '../i18n.js';

const PAGE_W = 794; // A4 @96dpi
const PAGE_H = 1123;

/**
 * @param getHtml  () => string (sync, may throw)
 * @returns {{el: HTMLElement, schedule: Function, update: Function, destroy: Function}}
 */
export function createPreview(getHtml, { delay = 30 } = {}) {
  const frame = h('iframe', {
    class: 'pv-frame', title: t('preview'), sandbox: '', width: PAGE_W, height: PAGE_H, referrerpolicy: 'no-referrer',
  });
  const error = h('div', { class: 'pv-error', role: 'alert', hidden: true });
  const sizer = h('div', { class: 'pv-sizer' }, frame);
  const el = h('div', { class: 'pv' }, error, sizer);

  const fit = () => {
    const w = el.clientWidth;
    if (!w) return;
    const s = Math.min(1, (w - 2) / PAGE_W);
    frame.style.transform = `scale(${s})`;
    sizer.style.width = `${PAGE_W * s}px`;
    sizer.style.height = `${PAGE_H * s}px`;
  };
  const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(fit) : null;
  ro && ro.observe(el);

  let timer;
  function update() {
    const t0 = performance.now();
    try {
      const html = getHtml();
      if (frame.srcdoc !== html) frame.srcdoc = html;
      error.hidden = true;
    } catch (e) {
      error.textContent = `${t('preview_error')}: ${e.message}`;
      error.hidden = false;
    }
    el.dataset.renderMs = (performance.now() - t0).toFixed(1);
    fit();
  }
  return {
    el, update,
    schedule() { clearTimeout(timer); timer = setTimeout(update, delay); },
    destroy() { clearTimeout(timer); ro && ro.disconnect(); },
  };
}
