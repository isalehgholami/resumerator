#!/usr/bin/env node
// Builds the static GitHub Pages site into ./_site (no dependencies).
//   node tools/build-site.mjs           build only
//   node tools/build-site.mjs --serve   build, then serve _site on http://localhost:8080 (PORT env overrides)
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const out = path.join(root, '_site');
const wwwroot = path.join(root, 'src/ResumeMaker.Web/wwwroot');
const fontsSrc = path.join(root, 'src/ResumeMaker.Infrastructure/Assets/Fonts');
const FONTS = ['vazir.woff2', 'vazir-bold.woff2', 'inter-regular.woff2'];

function copyDir(src, dst, filter = () => true) {
  fs.mkdirSync(dst, { recursive: true });
  for (const e of fs.readdirSync(src, { withFileTypes: true })) {
    const s = path.join(src, e.name);
    const d = path.join(dst, e.name);
    if (e.isDirectory()) copyDir(s, d, filter);
    else if (filter(e.name)) fs.copyFileSync(s, d);
  }
}

function build() {
  fs.rmSync(out, { recursive: true, force: true });
  copyDir(wwwroot, out);
  // templates: *.html + sample jsons (SYNTAX.md is documentation, not shipped)
  copyDir(path.join(root, 'templates'), path.join(out, 'templates'), (n) => /\.(html|json)$/i.test(n));
  fs.mkdirSync(path.join(out, 'fonts'), { recursive: true });
  for (const f of FONTS) fs.copyFileSync(path.join(fontsSrc, f), path.join(out, 'fonts', f));
  fs.writeFileSync(path.join(out, '.nojekyll'), '');
  const n = fs.readdirSync(path.join(out, 'templates')).length;
  console.log(`Built ${out} (${n} template files, ${FONTS.length} fonts)`);
}

const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.woff2': 'font/woff2',
  '.svg': 'image/svg+xml', '.png': 'image/png', '.ico': 'image/x-icon', '.txt': 'text/plain; charset=utf-8',
};

function serve(port) {
  const server = http.createServer((req, res) => {
    let p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    if (p.endsWith('/')) p += 'index.html';
    const file = path.normalize(path.join(out, p));
    if (!file.startsWith(out)) { res.writeHead(403).end('Forbidden'); return; }
    fs.readFile(file, (err, data) => {
      if (err) { res.writeHead(404, { 'Content-Type': 'text/plain' }).end('Not found'); return; }
      res.writeHead(200, {
        'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream',
        'Cache-Control': 'no-store',
        'X-Content-Type-Options': 'nosniff',
      });
      res.end(data);
    });
  });
  server.listen(port, () => console.log(`Serving _site at http://localhost:${port}/`));
}

build();
if (process.argv.includes('--serve')) serve(Number(process.env.PORT) || 8080);
