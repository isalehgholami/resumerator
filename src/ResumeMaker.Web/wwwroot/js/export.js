// Client-side exports: single-file HTML, JSON, PDF (browser print), PNG (SVG foreignObject -> canvas).

export function slug(s) {
  return (String(s || 'resume').trim().replace(/[\\/:*?"<>|\s]+/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '') || 'resume').slice(0, 60);
}

export function download(filename, data, mime) {
  const blob = data instanceof Blob ? data : new Blob([data], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = filename; a.rel = 'noopener';
  document.body.append(a);
  a.click();
  setTimeout(() => { a.remove(); URL.revokeObjectURL(url); }, 1500);
}

/** HTML is already self-contained (fonts are base64 inside the rendered template). */
export const exportHtml = (html, name) => download(`${slug(name)}.html`, html, 'text/html;charset=utf-8');
export const exportJson = (text, name) => download(`${slug(name)}.json`, text, 'application/json;charset=utf-8');

/**
 * Hidden same-origin iframe without allow-scripts (page CSP already blocks inline scripts):
 * allow-same-origin lets us call print(); allow-modals lets the print dialog open.
 */
function hiddenFrame(html) {
  return new Promise((resolve) => {
    const f = document.createElement('iframe');
    f.setAttribute('sandbox', 'allow-same-origin allow-modals');
    f.setAttribute('aria-hidden', 'true');
    f.tabIndex = -1;
    f.style.cssText = 'position:fixed;inset-inline-start:-10000px;top:0;width:794px;height:1123px;border:0;visibility:hidden';
    f.addEventListener('load', () => resolve(f), { once: true });
    f.srcdoc = html;
    document.body.append(f);
  });
}

export async function printPdf(html) {
  const f = await hiddenFrame(html);
  try { await f.contentDocument.fonts.ready; } catch { /* ignore */ }
  const w = f.contentWindow;
  w.addEventListener('afterprint', () => setTimeout(() => f.remove(), 500), { once: true });
  w.focus();
  w.print();
  setTimeout(() => f.remove(), 120000);
}

/** @returns {Promise<{ok:true}|{ok:false,reason:string}>} */
export async function exportPng(html, dpi, name) {
  let frame;
  try {
    frame = await hiddenFrame(html);
    const doc = frame.contentDocument;
    try { await doc.fonts.ready; } catch { /* ignore */ }
    const width = 794;
    const height = Math.max(1123, Math.ceil(doc.documentElement.scrollHeight));
    const scale = dpi / 96;
    if (width * scale * height * scale > 16000 * 16000 / 4) return { ok: false, reason: 'too-large' };
    const xml = new XMLSerializer().serializeToString(doc.documentElement);
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">` +
      `<foreignObject x="0" y="0" width="100%" height="100%">${xml}</foreignObject></svg>`;
    const img = new Image();
    img.decoding = 'sync';
    const loaded = new Promise((res, rej) => { img.onload = res; img.onerror = () => rej(new Error('svg-load')); });
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    await loaded;
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(width * scale);
    canvas.height = Math.round(height * scale);
    const ctx = canvas.getContext('2d');
    if (!ctx) return { ok: false, reason: 'no-canvas' };
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise((res) => {
      try { canvas.toBlob(res, 'image/png'); } catch { res(null); }
    });
    if (!blob) return { ok: false, reason: 'tainted' };
    download(`${slug(name)}-${dpi}dpi.png`, blob);
    return { ok: true };
  } catch (e) {
    return { ok: false, reason: String(e && e.message || e) };
  } finally {
    frame && frame.remove();
  }
}
