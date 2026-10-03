/* Rebuild the deliverable SVG and PNG assets from the approved logo.
 * Requires Node.js, sharp and pngjs. Run: node scripts/build-brand.cjs
 * Does not edit application or website source files.
 */
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const { PNG } = require('pngjs');
const ROOT = path.resolve(__dirname, '..');
const C = { accent: '#685187', secondary: '#C8BAE6', ink: '#32283E', light: '#F7F4FB', canvas: '#F1EDF7' };
const symbolSizes = [16, 24, 32, 48, 64, 96, 128, 192, 256, 512, 1024];
const logoSizes = [320, 640, 1280, 2560];
const exportedFiles = [];
const mkdir = p => fs.mkdirSync(path.dirname(p), { recursive: true });
function write(rel, text) { const p = path.join(ROOT, rel); mkdir(p); fs.writeFileSync(p, text); }
function svg(w, h, body, label = 'Unfold') {
  return '<svg xmlns="http://www.w3.org/2000/svg" width="' + w + '" height="' + h + '" viewBox="0 0 ' + w + ' ' + h + '" role="img" aria-label="' + label + '"><title>' + label + '</title>' + body + '</svg>\n';
}
function mark(left, right = left) {
  return '<rect width="101" height="222" rx="12" fill="' + left + '"/>' +
    '<path d="M140 0H216Q228 0 228 12V184C228 235 187 272 134 272C128 272 126 269 128 265C145 256 159 240 163 224C141 220 128 207 128 184V12Q128 0 140 0Z" fill="' + right + '"/>';
}
function placeMark(x, y, height, left, right = left) {
  return '<g transform="translate(' + x + ' ' + y + ') scale(' + (height / 272) + ')">' + mark(left, right) + '</g>';
}
function tileBody(rounded = true, markHeight = 320) {
  const width = markHeight * 228 / 272;
  return '<rect width="512" height="512" rx="' + (rounded ? 112 : 0) + '" fill="' + C.accent + '"/>' +
    placeMark((512 - width) / 2, (512 - markHeight) / 2, markHeight, C.light);
}

// Read the approved preview only to recover the existing wordmark silhouette.
// Raster edges become smoothed SVG outlines; no substitute font is used.
function traceWordmark() {
  const image = PNG.sync.read(fs.readFileSync(path.join(ROOT, 'source/approved-pause-v2.png')));
  if (image.width !== 1536 || image.height !== 1024) throw new Error('Unexpected approved reference dimensions');
  const box = { x: 500, y: 275, w: 860, h: 235 };
  const mask = new Uint8Array(box.w * box.h);
  for (let y = 0; y < box.h; y++) for (let x = 0; x < box.w; x++) {
    const i = ((y + box.y) * image.width + x + box.x) * 4;
    mask[y * box.w + x] = Math.max(image.data[i], image.data[i + 1], image.data[i + 2]) < 145 ? 1 : 0;
  }
  const on = (x, y) => x >= 0 && y >= 0 && x < box.w && y < box.h && mask[y * box.w + x];
  const edges = new Map();
  const key = (x, y) => y * (box.w + 1) + x;
  function edge(ax, ay, bx, by) {
    const a = key(ax, ay), b = key(bx, by);
    if (!edges.has(a)) edges.set(a, []);
    edges.get(a).push(b);
  }
  for (let y = 0; y < box.h; y++) for (let x = 0; x < box.w; x++) if (on(x, y)) {
    if (!on(x, y - 1)) edge(x, y, x + 1, y);
    if (!on(x + 1, y)) edge(x + 1, y, x + 1, y + 1);
    if (!on(x, y + 1)) edge(x + 1, y + 1, x, y + 1);
    if (!on(x - 1, y)) edge(x, y + 1, x, y);
  }
  const point = k => [k % (box.w + 1), Math.floor(k / (box.w + 1))];
  const loops = [];
  while (edges.size) {
    const start = edges.keys().next().value;
    let current = start;
    const loop = [];
    do {
      loop.push(point(current));
      const next = edges.get(current);
      if (!next || !next.length) throw new Error('Open outline');
      const value = next.pop();
      if (!next.length) edges.delete(current);
      current = value;
      if (loop.length > 100000) throw new Error('Invalid outline');
    } while (current !== start);
    const area = loop.reduce((sum, a, i) => {
      const b = loop[(i + 1) % loop.length];
      return sum + a[0] * b[1] - b[0] * a[1];
    }, 0) / 2;
    if (Math.abs(area) > 5) loops.push(loop);
  }
  function distance(p, a, b) {
    const dx = b[0] - a[0], dy = b[1] - a[1];
    const t = dx || dy ? Math.max(0, Math.min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (dx * dx + dy * dy))) : 0;
    return Math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy);
  }
  function simplify(points, tolerance = 0.55) {
    if (points.length < 3) return points;
    let max = 0, index = 0;
    for (let i = 1; i < points.length - 1; i++) {
      const d = distance(points[i], points[0], points[points.length - 1]);
      if (d > max) { max = d; index = i; }
    }
    if (max <= tolerance) return [points[0], points[points.length - 1]];
    return simplify(points.slice(0, index + 1), tolerance).slice(0, -1).concat(simplify(points.slice(index), tolerance));
  }
  const f = n => Number(n.toFixed(2));
  const paths = loops.map(loop => {
    let far = 0, farDistance = 0;
    loop.forEach((p, i) => {
      const d = Math.hypot(p[0] - loop[0][0], p[1] - loop[0][1]);
      if (d > farDistance) { far = i; farDistance = d; }
    });
    const pts = simplify(loop.slice(0, far + 1)).slice(0, -1).concat(simplify(loop.slice(far).concat([loop[0]])).slice(0, -1));
    const mid = (a, b) => [f((a[0] + b[0]) / 2), f((a[1] + b[1]) / 2)];
    let d = 'M' + mid(pts[pts.length - 1], pts[0]).join(' ');
    pts.forEach((p, i) => { d += 'Q' + p.join(' ') + ' ' + mid(p, pts[(i + 1) % pts.length]).join(' '); });
    return d + 'Z';
  }).join('');
  return { paths, loops: loops.length, mask, box };
}

async function png(rel, source, width, height) {
  const sourceWidth = Number(source.match(/width="([\d.]+)"/)[1]);
  const density = Math.max(72, Math.ceil(72 * width / sourceWidth * 3));
  const buffer = await sharp(Buffer.from(source), { density }).resize(width, height || null).png({ compressionLevel: 9 }).toBuffer();
  const target = path.join(ROOT, rel); mkdir(target); fs.writeFileSync(target, buffer);
  const meta = await sharp(buffer).metadata();
  exportedFiles.push({ file: rel, width: meta.width, height: meta.height, format: 'png', alpha: meta.hasAlpha });
}
async function main() {
  const word = traceWordmark();
  if (word.loops !== 8) throw new Error('Expected 6 letters and 2 counters; found ' + word.loops + ' contours');
  function lettering(fill) { return '<path fill="' + fill + '" fill-rule="evenodd" d="' + word.paths + '"/>'; }
  function logoBody(light = false) {
    return placeMark(24, 24, 272, light ? C.light : C.accent, light ? C.light : C.secondary) +
      '<g transform="translate(298 50)">' + lettering(light ? C.light : C.ink) + '</g>';
  }
  const symbol = svg(512, 512, placeMark((512 - 400 * 228 / 272) / 2, 56, 400, C.accent, C.secondary));
  const symbolInk = svg(512, 512, placeMark((512 - 400 * 228 / 272) / 2, 56, 400, C.ink));
  const symbolLight = svg(512, 512, placeMark((512 - 400 * 228 / 272) / 2, 56, 400, C.light));
  const logo = svg(1162, 320, logoBody());
  const logoLight = svg(1162, 320, logoBody(true));
  const app = svg(512, 512, tileBody());
  const favicon = svg(512, 512, tileBody(true, 368));
  const touch = svg(512, 512, tileBody(false, 320));
  const maskable = svg(512, 512, tileBody(false, 304));
  const pinned = svg(512, 512, placeMark((512 - 400 * 228 / 272) / 2, 56, 400, '#000000'));
  const svgFiles = {
    'symbol-lilac': symbol, 'symbol-ink': symbolInk, 'symbol-light': symbolLight,
    'logo-lilac': logo, 'logo-light': logoLight, 'app-icon-lilac': app,
    'favicon': favicon, 'maskable-icon': maskable, 'safari-pinned-tab': pinned
  };
  for (const [name, data] of Object.entries(svgFiles)) write('svg/' + name + '.svg', data);
  for (const size of symbolSizes) {
    await png('png/symbol/symbol-lilac-' + size + '.png', symbol, size, size);
    await png('png/app-icon/unfold-' + size + '.png', app, size, size);
  }
  for (const size of logoSizes) {
    await png('png/logo/logo-lilac-' + size + '.png', logo, size);
    await png('png/logo/logo-light-' + size + '.png', logoLight, size);
  }
  for (const size of [16, 24, 32, 48, 64, 128, 256]) await png('web/favicon-' + size + 'x' + size + '.png', favicon, size, size);
  await png('web/apple-touch-icon.png', touch, 180, 180);
  await png('web/android-chrome-192x192.png', app, 192, 192);
  await png('web/android-chrome-512x512.png', app, 512, 512);
  await png('web/maskable-icon-512x512.png', maskable, 512, 512);
  for (const [name, data] of Object.entries({ favicon, 'logo-lilac': logo, 'logo-light': logoLight, 'safari-pinned-tab': pinned })) write('web/' + name + '.svg', data);
  write('web/site.webmanifest', JSON.stringify({
    name: 'Unfold', short_name: 'Unfold', start_url: '/', scope: '/', display: 'standalone',
    background_color: C.light, theme_color: C.accent,
    icons: [
      { src: 'android-chrome-192x192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
      { src: 'android-chrome-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
      { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
    ]
  }, null, 2) + '\n');
  write('web/head-snippet.html', '<!-- Copy the contents of web/ to your website public root. -->\n' +
    '<link rel="icon" href="/favicon.ico" sizes="any">\n' +
    '<link rel="icon" href="/favicon.svg" type="image/svg+xml">\n' +
    '<link rel="icon" href="/favicon-32x32.png" sizes="32x32" type="image/png">\n' +
    '<link rel="apple-touch-icon" href="/apple-touch-icon.png" sizes="180x180">\n' +
    '<link rel="mask-icon" href="/safari-pinned-tab.svg" color="#685187">\n' +
    '<link rel="manifest" href="/site.webmanifest">\n' +
    '<meta name="theme-color" content="#685187">\n');
  const raw = await sharp(Buffer.from(svg(word.box.w, word.box.h, lettering('#000000')))).ensureAlpha().raw().toBuffer();
  let intersection = 0, union = 0;
  for (let i = 0; i < word.mask.length; i++) {
    const actual = raw[i * 4 + 3] >= 128, expected = !!word.mask[i];
    if (actual && expected) intersection++;
    if (actual || expected) union++;
  }
  const iou = intersection / union;
  if (iou < 0.985) throw new Error('Wordmark outline match too low: ' + iou);
  const preview = svg(1600, 1050,
    '<rect width="1600" height="1050" fill="' + C.canvas + '"/>' +
    '<g font-family="Arial, sans-serif" fill="' + C.ink + '">' +
    '<text x="88" y="87" font-size="23" letter-spacing="4">UNFOLD / LILAC BRAND KIT</text>' +
    '<text x="1512" y="87" text-anchor="end" font-size="19" fill="#70627F">GENTLE PAUSE · V1</text>' +
    '<rect x="72" y="125" width="1456" height="350" rx="28" fill="' + C.light + '"/>' +
    '<g transform="translate(219 148)">' + logoBody() + '</g>' +
    '<rect x="72" y="507" width="424" height="344" rx="28" fill="' + C.light + '"/>' +
    '<rect x="520" y="507" width="424" height="344" rx="28" fill="' + C.light + '"/>' +
    '<rect x="968" y="507" width="560" height="344" rx="28" fill="' + C.light + '"/>' +
    '<text x="108" y="554" font-size="16" letter-spacing="2">SYMBOL / TRANSPARENT</text>' +
    '<g transform="translate(170 574) scale(0.45)">' + placeMark((512 - 400 * 228 / 272) / 2, 56, 400, C.accent, C.secondary) + '</g>' +
    '<text x="556" y="554" font-size="16" letter-spacing="2">APP ICON</text>' +
    '<g transform="translate(629 592) scale(0.4)">' + tileBody() + '</g>' +
    '<text x="1004" y="554" font-size="16" letter-spacing="2">FAVICON / ACTUAL SIZES</text>' +
    [16, 24, 32, 48, 64].map((size, i) =>
      '<g transform="translate(' + (1020 + i * 93) + ' ' + (666 - size / 2) + ') scale(' + (size / 512) + ')">' + tileBody(true, 368) + '</g>' +
      '<text x="' + (1020 + i * 93 + size / 2) + '" y="732" text-anchor="middle" font-size="16" fill="#70627F">' + size + 'px</text>'
    ).join('') +
    '<text x="1004" y="797" font-size="17" fill="#70627F">SVG · PNG · ICO · ICNS</text>' +
    [C.accent, C.secondary, C.ink, C.light].map((color, i) =>
      '<rect x="' + (90 + i * 366) + '" y="910" width="54" height="54" rx="12" fill="' + color + '" stroke="#D5CADE"/>' +
      '<text x="' + (161 + i * 366) + '" y="944" font-size="20">' + color + '</text>'
    ).join('') + '</g>');
  write('preview.svg', preview);
  await png('preview.png', preview, 1600, 1050);
  write('assets.json', JSON.stringify({ version: 1, palette: C, wordmark: { contours: word.loops, referenceMaskIoU: iou }, png: exportedFiles }, null, 2) + '\n');
  console.log(JSON.stringify({ pngCount: exportedFiles.length, svgCount: Object.keys(svgFiles).length + 5, wordmarkIoU: iou, root: ROOT }));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
