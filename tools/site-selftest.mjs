// Headless self-test for the JS template engine + model builder.
// Usage: node tools/site-selftest.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const www = path.join(root, 'src/ResumeMaker.Web/wwwroot/js');
const { render, compile, TemplateSyntaxError } = await import(pathToFileURL(path.join(www, 'engine.js')));
const { buildModel } = await import(pathToFileURL(path.join(www, 'model.js')));

let failed = 0;
const ok = (cond, msg) => { if (!cond) { failed++; console.error('FAIL', msg); } else console.log('ok  ', msg); };

// ---- engine unit tests
const m = { lang: 'fa', a: { b: 'x', n: '2024 &#39;5', list: ['p', 'q'], empty: [] }, flag: false, t: 'yes' };
ok(render('{{ a.b }}', m) === 'x', 'path output');
ok(render('{{ a.zzz }}|{{ q.r.s }}', m) === '|', 'absent -> empty');
ok(render('{{ a.n | digits }}', m) === '۲۰۲۴ &#39;۵', 'digits filter (fa) keeps entities');
ok(render('{{ a.n | digits }}', { ...m, lang: 'en' }) === '2024 &#39;5', 'digits filter (en) no-op');
ok(render('{{ if a.b }}Y{{ else }}N{{ end }}', m) === 'Y', 'if true');
ok(render('{{ if a.empty }}Y{{ else }}N{{ end }}', m) === 'N', 'empty list falsy');
ok(render('{{ if flag }}Y{{ else }}N{{ end }}', m) === 'N', 'false falsy');
ok(render('{{ if !a.nope }}Y{{ end }}', m) === 'Y', 'negation of absent');
ok(render('{{ if t == "yes" }}Y{{ end }}{{ if t != "yes" }}N{{ end }}', m) === 'Y', '== / !=');
ok(render('{{ for x in a.list }}[{{ x }}{{ if x == "p" }}!{{ end }}]{{ end }}', m) === '[p!][q]', 'for + nested if');
ok(render('{{ for x in a.list }}{{ for y in a.list }}{{ x }}{{ y }}{{ end }}{{ end }}', m) === 'pppqqpqq', 'nested for');
for (const bad of ['{{ a.b && a.c }}', '{{ if a.b }}', '{{ end }}', '{{ foo(1) }}', '{{ elsif x }}', '{{~ a ~}}']) {
  let threw = false;
  try { compile(bad); } catch (e) { threw = e instanceof TemplateSyntaxError; }
  ok(threw, `rejects unsupported: ${bad}`);
}

// ---- model + templates
const fontDir = path.join(root, 'src/ResumeMaker.Infrastructure/Assets/Fonts');
const b64 = (f) => fs.readFileSync(path.join(fontDir, f)).toString('base64');
const fonts = { vazir: b64('vazir.woff2'), vazir_bold: b64('vazir-bold.woff2'), inter: b64('inter-regular.woff2') };

const tplDir = path.join(root, 'templates');
const samples = {};
for (const l of ['fa', 'en']) {
  const f = path.join(tplDir, `sample-profile.${l}.json`);
  if (fs.existsSync(f)) samples[l] = JSON.parse(fs.readFileSync(f, 'utf8'));
}
if (!Object.keys(samples).length) {
  console.warn('no sample-profile.*.json yet; using inline mini profile');
  samples.fa = {
    name: 'نمونه', language: 'fa', contact: { full_name: 'مریم <احمدی>', email: 'a@b.c', links: { github: 'https://github.com/x' } },
    summary: 'خلاصه', sections: [
      { id: 'e', type: 'experience', enabled: true, order: 2, items: [{ title: 'dev', company: 'X', start: '1401', tags: ['C#'] }, {}] },
      { id: 'k', type: 'skills', enabled: true, order: 1, items: [{ category: 'Prog', entries: [{ name: 'C#', level: 'expert' }] }] },
      { id: 'off', type: 'awards', enabled: false, order: 0, items: [{ title: 'hidden' }] },
      { id: 'emp', type: 'projects', enabled: true, order: 3, items: [] },
    ],
  };
}

const model = buildModel(samples.fa || samples.en, { fonts });
ok(model.profile.sections.length >= 1 && !model.profile.sections.some((s) => s.id === 'off'), 'disabled/empty sections dropped');
if (samples.fa?.contact?.full_name?.includes('<')) ok(model.profile.contact.full_name.includes('&lt;'), 'html-escaped');

const ids = ['minimal', 'classic', 'modern', 'compact', 'sidebar', 'ats'];
let found = 0;
for (const lang of Object.keys(samples)) {
  for (const id of ids) {
    const f = path.join(tplDir, `${id}.html`);
    if (!fs.existsSync(f)) continue;
    found++;
    const src = fs.readFileSync(f, 'utf8');
    const mdl = buildModel(samples[lang], { fonts });
    const t0 = performance.now();
    let html = '';
    try { html = render(src, mdl); } catch (e) { ok(false, `${id}/${lang} render: ${e.message}`); continue; }
    const ms = performance.now() - t0;
    ok(!/\{\{|\}\}/.test(html), `${id}/${lang} no leftover tags`);
    ok(html.includes(mdl.profile.contact.full_name), `${id}/${lang} contains full name`);
    ok(html.includes(`dir="${mdl.direction}"`), `${id}/${lang} dir attr`);
    ok(!/<script/i.test(html), `${id}/${lang} no script`);
    ok(ms < 100, `${id}/${lang} render ${ms.toFixed(1)}ms < 100ms`);
  }
}
const cl = path.join(tplDir, 'cover-letter.html');
if (fs.existsSync(cl)) {
  const lang = Object.keys(samples)[0];
  const mdl = buildModel(samples[lang], { fonts, letter: { company: 'ACME', position: 'Dev', body: 'One.\n\nTwo <b>.' } });
  const html = render(fs.readFileSync(cl, 'utf8'), mdl);
  ok(!/\{\{|\}\}/.test(html) && html.includes('ACME') && html.includes('Two &lt;b&gt;'), 'cover-letter renders');
  found++;
}
console.log(`${found} template renders checked`);
if (failed) { console.error(`${failed} FAILED`); process.exit(1); }
console.log('ALL OK');
