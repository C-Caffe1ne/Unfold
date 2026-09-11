#!/usr/bin/env node
// Convert generated 4x3 pose atlases into Unfold packages. No poses are painted
// or morphed here: this only crops, registers, scales and encodes the source art.
// Usage: SHARP_MODULE=/path/to/sharp node Scripts/assemble-cat-assets.cjs
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(__dirname, '..');
const authoring = path.join(root, 'docs/assets/cat-expansion');
const assets = path.join(root, 'Sources/Unfold/Resources/Characters');
const size = 384;
const cats = [
  { id: 'miso-tabby', name: 'Miso', breed: 'Ginger tabby',
    // The generated in-between cells swapped the tail/paw side. Exclude them
    // from playback and replace their atlas slots with approved source poses.
    replacements: { 1: 0, 10: 11 },
    clips: {
      idle: { poses: [0, 3, 2, 3, 0], delay: [1400, 850, 120, 600, 500], loop: true },
      click: { poses: [0, 9, 11, 9, 0], delay: [180, 300, 650, 350, 330], loop: false },
    } },
  { id: 'luna-blue', name: 'Luna', breed: 'Russian Blue' },
  { id: 'coco-siamese', name: 'Coco', breed: 'Siamese' },
];
// The neutral source frame is reused at both ends, including between actions.
// Slow idle holds and brief blink timing keep the companion unobtrusive.
const clips = {
  idle: { poses: [0, 1, 3, 2, 3, 1, 0], delay: [900, 350, 650, 120, 550, 350, 550], loop: true },
  stretch: { poses: [0, 5, 6, 7, 6, 5, 0], delay: [260, 240, 320, 900, 260, 240, 360], loop: false },
  click: { poses: [0, 9, 10, 11, 10, 9, 0], delay: [180, 220, 180, 550, 180, 200, 300], loop: false },
};

function bounds(data, width, height) {
  let left = width, top = height, right = -1, bottom = -1;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (data[(y * width + x) * 4 + 3] < 128) continue;
    left = Math.min(left, x); top = Math.min(top, y);
    right = Math.max(right, x); bottom = Math.max(bottom, y);
  }
  if (right < 0) throw new Error('Empty pose');
  return { left, top, right, bottom, width: right - left + 1, height: bottom - top + 1 };
}

function footCenter(data, width, bbox) {
  let total = 0, count = 0;
  for (let y = bbox.bottom - 5; y <= bbox.bottom; y++) for (let x = bbox.left; x <= bbox.right; x++) {
    if (data[(y * width + x) * 4 + 3] < 200) continue;
    total += x; count++;
  }
  return count ? total / count : (bbox.left + bbox.right) / 2;
}

async function makeGif(frames, delays, loop, destination) {
  await sharp(Buffer.concat(frames), {
    raw: { width: size, height: size * frames.length, channels: 4, pageHeight: size },
  }).gif({
    loop: loop ? 0 : 1, delay: delays, colours: 256, dither: 0,
    effort: 10, reuse: false, interFrameMaxError: 0, interPaletteMaxError: 0,
    keepDuplicateFrames: true,
  }).toFile(destination);
}

async function assemble(cat) {
  const source = path.join(authoring, 'sources', `${cat.id}.png`);
  const metadata = await sharp(source).metadata();
  if (!metadata.hasAlpha || metadata.width % 4 || metadata.height % 3) {
    throw new Error(`${cat.id}: expected transparent, equally divided 4x3 atlas`);
  }
  const cellWidth = metadata.width / 4, cellHeight = metadata.height / 3;
  const cells = [];
  for (let i = 0; i < 12; i++) {
    const { data } = await sharp(source).extract({
      left: i % 4 * cellWidth, top: Math.floor(i / 4) * cellHeight,
      width: cellWidth, height: cellHeight,
    }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    cells.push({ data, bounds: bounds(data, cellWidth, cellHeight) });
  }
  const anchorX = footCenter(cells[0].data, cellWidth, cells[0].bounds);
  // A single scale for the entire character avoids stretching-related size pops.
  const extent = Math.max(...cells.map(c => Math.max(anchorX - c.bounds.left, c.bounds.right - anchorX)));
  const scale = Math.min(284 / cells[0].bounds.height, 166 / extent,
    310 / Math.max(...cells.map(c => c.bounds.height)));
  const normalized = [];
  const registration = [];
  for (let i = 0; i < cells.length; i++) {
    const cell = cells[i], b = cell.bounds;
    const crop = {
      left: Math.max(0, b.left - 4), top: Math.max(0, b.top - 4),
      width: Math.min(cellWidth - 1, b.right + 4) - Math.max(0, b.left - 4) + 1,
      height: Math.min(cellHeight - 1, b.bottom + 4) - Math.max(0, b.top - 4) + 1,
    };
    const width = Math.round(crop.width * scale), height = Math.round(crop.height * scale);
    const left = Math.round(size / 2 + (crop.left - anchorX) * scale);
    const top = Math.round(342 - (b.bottom - crop.top + 1) * scale);
    if (left < 12 || top < 12 || left + width > size - 12 || top + height > size - 12) {
      throw new Error(`${cat.id} pose ${i}: insufficient padding after registration`);
    }
    const input = await sharp(cell.data, { raw: { width: cellWidth, height: cellHeight, channels: 4 } })
      .extract(crop).resize(width, height).png().toBuffer();
    const rgba = await sharp({ create: { width: size, height: size, channels: 4, background: '#00000000' } })
      .composite([{ input, left, top }]).raw().toBuffer();
    normalized.push(rgba);
    registration.push({ index: i, sourceBounds: b, left, top, width, height });
  }
  const destination = path.join(assets, cat.id);
  await fs.mkdir(destination, { recursive: true });
  for (const [target, source] of Object.entries(cat.replacements || {})) normalized[target] = normalized[source];
  // Retain all authored key poses in the PNG for later asset editing.
  await sharp({ create: { width: size * 4, height: size * 3, channels: 4, background: '#00000000' } })
    .composite(normalized.map((input, i) => ({ input, raw: { width: size, height: size, channels: 4 },
      left: i % 4 * size, top: Math.floor(i / 4) * size })))
    .png().toFile(path.join(destination, 'spritesheet.png'));
  const report = { ...cat, scale, registration, clips: {} };
  const catClips = { ...clips, ...cat.clips };
  for (const [key, clip] of Object.entries(catClips)) {
    const file = path.join(destination, `${key}.gif`);
    await makeGif(clip.poses.map(i => normalized[i]), clip.delay, clip.loop, file);
    const encoded = await sharp(file, { animated: true }).metadata();
    report.clips[key] = { frames: encoded.pages, width: encoded.width, height: encoded.pageHeight,
      delays: encoded.delay, durationMs: encoded.delay.reduce((a, b) => a + b, 0),
      loop: encoded.loop, bytes: (await fs.stat(file)).size };
  }
  await fs.writeFile(path.join(destination, 'character.json'), JSON.stringify({
    id: cat.id, name: cat.name, version: 1, thumbnailSymbol: 'cat.fill',
    spriteSheet: { file: 'spritesheet.png', columns: 4, rows: 3, frameWidth: size, frameHeight: size },
    animations: Object.fromEntries(Object.entries(catClips).map(([key, clip]) => [key, { gif: `${key}.gif`, loop: clip.loop }])),
  }, null, 2) + '\n');
  return report;
}

(async () => {
  const report = [];
  const selected = process.argv.slice(2);
  for (const cat of cats.filter(cat => !selected.length || selected.includes(cat.id))) {
    report.push(await assemble(cat));
    console.log(`${cat.name}: idle.gif, stretch.gif, click.gif, spritesheet.png, character.json`);
  }
  await fs.writeFile(path.join(authoring, 'asset-validation.json'), JSON.stringify(report, null, 2) + '\n');
})().catch(error => { console.error(error); process.exitCode = 1; });
