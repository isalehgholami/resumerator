// Sections list editor: add/remove/enable/reorder (drag & drop + buttons) and per-type item editors.
import { h, field, selectField, button, clear } from './dom.js';
import { t } from '../i18n.js';
import { newSection } from '../store.js';
import { SECTION_TYPES } from '../model.js';

const TEXT = (k, extra = {}) => ({ k, ...extra });
// Field definitions per section type. `ml` multiline, `tags` comma list, `ltr` force ltr direction (urls, dates).
const FIELDS = {
  experience: [TEXT('title'), TEXT('company'), TEXT('location'), TEXT('start', { ltr: 1 }), TEXT('end', { ltr: 1 }), TEXT('description', { ml: 1 }), TEXT('tags', { tags: 1 })],
  education: [TEXT('degree'), TEXT('institution'), TEXT('location'), TEXT('start', { ltr: 1 }), TEXT('end', { ltr: 1 }), TEXT('description', { ml: 1 })],
  projects: [TEXT('title'), TEXT('url', { ltr: 1 }), TEXT('start', { ltr: 1 }), TEXT('end', { ltr: 1 }), TEXT('description', { ml: 1 }), TEXT('tags', { tags: 1 })],
  awards: [TEXT('title'), TEXT('issuer'), TEXT('date', { ltr: 1 }), TEXT('description', { ml: 1 })],
  interests: [TEXT('name')],
  custom: [TEXT('title'), TEXT('description', { ml: 1 })],
};

export const SKILL_LEVELS = ['beginner', 'intermediate', 'advanced', 'expert'];
export const LANG_LEVELS_DESC = ['native', 'fluent', 'advanced', 'intermediate', 'basic'];
export const CEFR = ['a1', 'a2', 'b1', 'b2', 'c1', 'c2'];

const move = (arr, i, d) => {
  const j = i + d;
  if (j < 0 || j >= arr.length) return false;
  [arr[i], arr[j]] = [arr[j], arr[i]];
  return true;
};

export function createSectionsEditor(profile, changed) {
  const root = h('div', { class: 'sections' });
  const open = new Set();
  let focusKey = null;
  let dragFrom = -1;

  const renumber = () => profile.sections.forEach((s, i) => { s.order = i + 1; });

  function render() {
    clear(root);
    renumber();
    const list = h('ol', { class: 'sec-list', 'aria-label': t('sections') });
    profile.sections.forEach((sec, i) => list.append(sectionCard(sec, i)));
    root.append(addBar(), list);
    if (!profile.sections.length) root.append(h('p', { class: 'muted' }, t('no_sections')));
    if (focusKey) {
      const el = root.querySelector(`[data-fk="${focusKey}"]`);
      el && el.focus();
      focusKey = null;
    }
  }

  function addBar() {
    const sel = h('select', { id: 'add-sec-type', 'aria-label': t('section_type') },
      SECTION_TYPES.map((x) => h('option', { value: x }, t(`type_${x}`))));
    return h('div', { class: 'add-bar' },
      h('label', { for: 'add-sec-type' }, t('add_section')), sel,
      button(`+ ${t('add')}`, () => {
        const s = newSection(sel.value, profile.sections.length + 1);
        profile.sections.push(s);
        open.add(s.id);
        focusKey = `${s.id}:title`;
        changed(); render();
      }, { kind: 'primary' }));
  }

  function sectionCard(sec, i) {
    const label = sec.title || t(`type_${sec.type}`);
    const fk = (n) => `${sec.id}:${n}`;
    const card = h('li', { class: `sec-card${sec.enabled ? '' : ' off'}`, dataset: { index: i } });

    const handle = h('span', {
      class: 'handle', draggable: true, 'aria-hidden': 'true', title: t('drag_hint'),
      ondragstart: (e) => {
        dragFrom = i;
        e.dataTransfer.effectAllowed = 'move';
        try { e.dataTransfer.setData('text/plain', String(i)); e.dataTransfer.setDragImage(card, 12, 12); } catch { /* ignore */ }
        card.classList.add('dragging');
      },
      ondragend: () => { card.classList.remove('dragging'); dragFrom = -1; root.querySelectorAll('.drop').forEach((x) => x.classList.remove('drop')); },
    }, '⋮⋮');
    card.addEventListener('dragover', (e) => { if (dragFrom >= 0) { e.preventDefault(); card.classList.add('drop'); } });
    card.addEventListener('dragleave', () => card.classList.remove('drop'));
    card.addEventListener('drop', (e) => {
      e.preventDefault();
      if (dragFrom < 0 || dragFrom === i) return;
      const [m] = profile.sections.splice(dragFrom, 1);
      profile.sections.splice(i, 0, m);
      dragFrom = -1; changed(); render();
    });

    const enable = h('input', {
      type: 'checkbox', checked: sec.enabled, 'aria-label': `${t('enabled')}: ${label}`,
      onchange: (e) => { sec.enabled = e.target.checked; card.classList.toggle('off', !sec.enabled); changed(); },
    });

    const title = h('input', {
      type: 'text', value: sec.title || '', placeholder: t(`type_${sec.type}`), 'aria-label': t('section_title'),
      'data-fk': fk('title'), class: 'sec-title',
      oninput: (e) => { sec.title = e.target.value; changed(); },
    });

    const mv = (d, name, glyph) => button(glyph, () => {
      if (move(profile.sections, i, d)) { focusKey = fk(name); changed(); render(); }
    }, { ariaLabel: `${t(name === 'up' ? 'move_up' : 'move_down')}: ${label}`, title: t(name === 'up' ? 'move_up' : 'move_down'), disabled: d < 0 ? i === 0 : i === profile.sections.length - 1, cls: 'icon' });
    const up = mv(-1, 'up', '↑'); up.dataset.fk = fk('up');
    const down = mv(1, 'down', '↓'); down.dataset.fk = fk('down');

    const del = button('✕', () => {
      if (sec.items.length && !confirm(t('confirm_delete_section'))) return;
      profile.sections.splice(i, 1);
      changed(); render();
    }, { ariaLabel: `${t('remove')}: ${label}`, title: t('remove'), cls: 'icon danger' });

    const details = h('details', { class: 'sec-body', open: open.has(sec.id), ontoggle: (e) => { e.target.open ? open.add(sec.id) : open.delete(sec.id); } },
      h('summary', {}, h('span', { class: 'badge' }, t(`type_${sec.type}`)), ' ', h('span', { class: 'count' }, `${sec.items.length} ${t('items')}`)),
      h('div', { class: 'sec-content' }, itemsEditor(sec, details => details)));
    card.append(h('div', { class: 'sec-head' }, handle, enable, title, up, down, del), details);
    return card;
  }

  function itemsEditor(sec) {
    const wrap = h('div', { class: 'items' });
    const rerender = () => {
      clear(wrap);
      sec.items.forEach((it, idx) => wrap.append(itemCard(sec, it, idx, rerender)));
      wrap.append(button(`+ ${t('add_item')}`, () => {
        sec.items.push(blankItem(sec.type));
        changed(); rerender();
        const cnt = wrap.closest('.sec-card')?.querySelector('.count');
        cnt && (cnt.textContent = `${sec.items.length} ${t('items')}`);
        const last = wrap.querySelectorAll('.item-card');
        last[last.length - 1]?.querySelector('input,textarea,select')?.focus();
      }, { cls: 'add-item' }));
      const cnt = wrap.closest('.sec-card')?.querySelector('.count');
      cnt && (cnt.textContent = `${sec.items.length} ${t('items')}`);
    };
    rerender();
    return wrap;
  }

  function itemCard(sec, it, idx, rerender) {
    const n = `${t('item')} ${idx + 1}`;
    const bar = h('div', { class: 'item-bar' },
      h('strong', {}, n),
      h('span', { class: 'spacer' }),
      button('↑', () => { if (move(sec.items, idx, -1)) { changed(); rerender(); } }, { cls: 'icon', ariaLabel: `${t('move_up')}: ${n}`, disabled: idx === 0 }),
      button('↓', () => { if (move(sec.items, idx, 1)) { changed(); rerender(); } }, { cls: 'icon', ariaLabel: `${t('move_down')}: ${n}`, disabled: idx === sec.items.length - 1 }),
      button('✕', () => { sec.items.splice(idx, 1); changed(); rerender(); }, { cls: 'icon danger', ariaLabel: `${t('remove')}: ${n}`, title: t('remove') }));
    const body = h('div', { class: 'grid' });
    if (sec.type === 'skills') skillFields(body, it);
    else if (sec.type === 'languages') langFields(body, it);
    else for (const f of FIELDS[sec.type] || FIELDS.custom) body.append(itemField(it, f));
    return h('div', { class: 'item-card' }, bar, body);
  }

  function itemField(it, f) {
    const label = t(`f_${f.k}`);
    if (f.tags) {
      const tmp = { v: (it.tags || []).join(', ') };
      return field({ obj: tmp, key: 'v', label, hint: t('tags_hint'), onchange: () => { it.tags = tmp.v.split(/[,،]/).map((x) => x.trim()).filter(Boolean); changed(); } });
    }
    const wrap = field({ obj: it, key: f.k, label, multiline: !!f.ml, rows: 3, dir: f.ltr ? 'ltr' : null, onchange: changed });
    if (f.ml) wrap.classList.add('wide');
    return wrap;
  }

  function skillFields(body, it) {
    it.entries = it.entries || [];
    body.append(field({ obj: it, key: 'category', label: t('f_category'), onchange: changed }));
    const entries = h('div', { class: 'wide entries' });
    const draw = () => {
      clear(entries);
      it.entries.forEach((e, i) => entries.append(h('div', { class: 'entry-row' },
        field({ obj: e, key: 'name', label: t('f_name'), onchange: changed }),
        selectField({
          label: t('f_level'), value: e.level || '', onchange: (v) => { e.level = v; changed(); },
          options: [{ value: '', label: '—' }, ...SKILL_LEVELS.map((l) => ({ value: l, label: t(`lvl_${l}`) }))],
        }),
        button('✕', () => { it.entries.splice(i, 1); changed(); draw(); }, { cls: 'icon danger', ariaLabel: `${t('remove')}: ${e.name || i + 1}` }))));
      entries.append(button(`+ ${t('add_entry')}`, () => { it.entries.push({ name: '', level: 'intermediate' }); changed(); draw(); entries.querySelector('.entry-row:last-of-type input')?.focus(); }));
    };
    draw();
    body.append(entries);
  }

  function langFields(body, it) {
    body.append(field({ obj: it, key: 'name', label: t('f_name'), onchange: changed }));
    body.append(selectField({
      label: t('f_level'), value: it.level || '', onchange: (v) => { it.level = v; changed(); },
      options: [
        { value: '', label: '—' },
        { group: t('lvl_group_desc'), options: LANG_LEVELS_DESC.map((l) => ({ value: l, label: t(`lvl_${l}`) })) },
        { group: t('lvl_group_cefr'), options: CEFR.map((l) => ({ value: l, label: l.toUpperCase() })) },
      ],
    }));
  }

  function blankItem(type) {
    if (type === 'skills') return { category: '', entries: [{ name: '', level: 'intermediate' }] };
    if (type === 'languages') return { name: '', level: 'intermediate' };
    return Object.fromEntries((FIELDS[type] || FIELDS.custom).map((f) => [f.k, f.tags ? [] : '']));
  }

  render();
  return root;
}
