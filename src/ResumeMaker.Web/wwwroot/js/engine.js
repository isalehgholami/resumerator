// Mini renderer for the strict Scriban subset defined in templates/SYNTAX.md.
// DOM-free: runs in browsers and in node.
//
// Supported: {{ a.b.c }}, {{ a.b | digits }}, {{ if cond }} {{ else }} {{ end }},
//            {{ for x in a.b }} {{ end }}, cond := [!]path | path (==|!=) "literal"

const PATH = '[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*';
const RE_OUT = new RegExp(`^(${PATH})(?:\\s*\\|\\s*(digits))?$`);
const RE_FOR = new RegExp(`^for\\s+([A-Za-z_][A-Za-z0-9_]*)\\s+in\\s+(${PATH})$`);
const RE_IF = /^if\s+(.+)$/;
const RE_COND = new RegExp(`^(?:(!)\\s*(${PATH})|(${PATH})(?:\\s*(==|!=)\\s*(?:"((?:[^"\\\\]|\\\\.)*)"|'((?:[^'\\\\]|\\\\.)*)'))?)$`);

export class TemplateSyntaxError extends Error {
  constructor(message, tag) {
    super(tag ? `${message}: {{ ${tag} }}` : message);
    this.name = 'TemplateSyntaxError';
    this.tag = tag;
  }
}

function unescapeLit(s) { return s.replace(/\\(.)/g, '$1'); }

/** Tokenize + parse into an AST. Throws TemplateSyntaxError. */
export function compile(src) {
  const re = /\{\{\s*([\s\S]*?)\s*\}\}/g;
  const root = [];
  const stack = [{ kind: 'root', body: root }];
  const cur = () => stack[stack.length - 1];
  const push = (n) => (cur().kind === 'if' && cur().inElse ? cur().node.else : cur().body).push(n);
  let last = 0;
  let m;
  while ((m = re.exec(src))) {
    if (m.index > last) push({ t: 'text', v: src.slice(last, m.index) });
    last = re.lastIndex;
    const tag = m[1].trim();
    let r;
    if (tag === 'end') {
      if (stack.length === 1) throw new TemplateSyntaxError('Unexpected end', tag);
      stack.pop();
    } else if (tag === 'else') {
      const c = cur();
      if (c.kind !== 'if' || c.inElse) throw new TemplateSyntaxError('Unexpected else', tag);
      c.inElse = true;
    } else if ((r = RE_FOR.exec(tag))) {
      const node = { t: 'for', v: r[1], path: r[2].split('.'), body: [] };
      push(node);
      stack.push({ kind: 'for', body: node.body });
    } else if ((r = RE_IF.exec(tag))) {
      const c = RE_COND.exec(r[1].trim());
      if (!c) throw new TemplateSyntaxError('Unsupported condition', tag);
      let cond;
      if (c[1]) cond = { not: true, path: c[2].split('.') };
      else if (c[4]) cond = { op: c[4], path: c[3].split('.'), lit: unescapeLit(c[5] ?? c[6] ?? '') };
      else cond = { path: c[3].split('.') };
      const node = { t: 'if', cond, then: [], else: [] };
      push(node);
      stack.push({ kind: 'if', body: node.then, node, inElse: false });
    } else if ((r = RE_OUT.exec(tag))) {
      push({ t: 'out', path: r[1].split('.'), digits: !!r[2] });
    } else {
      throw new TemplateSyntaxError('Unsupported tag', tag);
    }
  }
  if (last < src.length) push({ t: 'text', v: src.slice(last) });
  if (stack.length > 1) throw new TemplateSyntaxError('Missing {{ end }} (unclosed block)');
  return root;
}

const hasOwn = (o, k) => Object.prototype.hasOwnProperty.call(o, k);

function lookup(path, scope) {
  let s = scope;
  let v;
  let found = false;
  while (s) {
    if (hasOwn(s.vars, path[0])) { v = s.vars[path[0]]; found = true; break; }
    s = s.parent;
  }
  if (!found) return undefined;
  for (let i = 1; i < path.length; i++) {
    if (v == null || typeof v !== 'object' || !hasOwn(v, path[i])) return undefined;
    v = v[path[i]];
  }
  return v;
}

export function isTruthy(v) {
  if (v === undefined || v === null || v === false || v === '') return false;
  if (Array.isArray(v) && v.length === 0) return false;
  return true;
}

const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹';
/** Convert 0-9 to Persian digits, leaving HTML entities (&#39; etc.) intact. */
export function toPersianDigits(s) {
  return s.replace(/&[#A-Za-z0-9]+;|[0-9]/g, (m) => (m.length > 1 ? m : FA_DIGITS[+m]));
}

function str(v) {
  if (v === undefined || v === null) return '';
  if (typeof v === 'string') return v;
  if (typeof v === 'number' || v === true) return String(v);
  return '';
}

function run(nodes, scope, root, out) {
  for (const n of nodes) {
    switch (n.t) {
      case 'text': out.push(n.v); break;
      case 'out': {
        let s = str(lookup(n.path, scope));
        if (n.digits && root.lang === 'fa') s = toPersianDigits(s);
        out.push(s);
        break;
      }
      case 'if': {
        const c = n.cond;
        const v = lookup(c.path, scope);
        let ok;
        if (c.not) ok = !isTruthy(v);
        else if (c.op) ok = (str(v) === c.lit) === (c.op === '==');
        else ok = isTruthy(v);
        run(ok ? n.then : n.else, scope, root, out);
        break;
      }
      case 'for': {
        const list = lookup(n.path, scope);
        if (Array.isArray(list)) {
          for (const item of list) run(n.body, { vars: { [n.v]: item }, parent: scope }, root, out);
        }
        break;
      }
    }
  }
}

const cache = new Map();

/** Render template source with the given model object. */
export function render(src, model) {
  let ast = cache.get(src);
  if (!ast) {
    ast = compile(src);
    if (cache.size > 30) cache.clear();
    cache.set(src, ast);
  }
  const out = [];
  run(ast, { vars: model, parent: null }, model, out);
  return out.join('');
}

/** Collect every root identifier referenced by a template (for the validator). */
export function referencedRoots(src) {
  const roots = new Set();
  const loopVars = new Set();
  (function walk(nodes) {
    for (const n of nodes) {
      if (n.t === 'out') roots.add(n.path[0]);
      else if (n.t === 'if') { roots.add(n.cond.path[0]); walk(n.then); walk(n.else); }
      else if (n.t === 'for') { roots.add(n.path[0]); loopVars.add(n.v); walk(n.body); }
    }
  })(compile(src));
  return { roots, loopVars };
}
