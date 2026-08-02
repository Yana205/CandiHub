#!/usr/bin/env node
/**
 * Bakes the placeholder pastry and customer sprites straight out of the mock.
 *
 * The mock's drawPastry()/drawCustomer() are the design authority for the art, and they
 * are pure Canvas2D path code. Rather than re-implementing them (and drifting), this
 * script EXTRACTS those methods from the mock HTML by brace-matching and runs them against
 * a native canvas. Re-run it after any art change in the mock and the sprites follow.
 *
 * Every pastry is drawn in multiples of its radius R — including the outline width — so
 * one sprite per tier authored at a canonical R is identical to the mock at every size
 * (handoff §8.1). That is why all 11 bake at R = 200 into a 512² texture.
 *
 * Usage:  node Docs/tools/bake-sprites.js [outDir]
 * Needs:  npm i @napi-rs/canvas
 */
const fs = require('fs');
const path = require('path');
const { createCanvas } = require('@napi-rs/canvas');

const REPO = path.resolve(__dirname, '..', '..');
const MOCK = path.join(REPO, 'Docs', 'mock', 'Pan Dulce Merge Mock.dc.html');
const OUT = process.argv[2] || path.join(REPO, 'Assets', 'PanDulce', 'Art');

const CANONICAL_R = 200;   // §8.1 — max on-screen radius is 90*1.6=144, so this never upscales
const PASTRY_SIZE = 512;   // padding covers the piggy's snout and ears

/** Pull `name(...) { ... }` out of the mock by matching braces from its opening line. */
function extractMethod(src, name) {
  const start = src.indexOf(`\n  ${name}(`);
  if (start < 0) throw new Error(`method ${name} not found in mock`);
  let i = src.indexOf('{', start);
  if (i < 0) throw new Error(`no body for ${name}`);
  let depth = 0;
  for (let j = i; j < src.length; j++) {
    const ch = src[j];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return src.slice(start + 1, j + 1);
    }
  }
  throw new Error(`unterminated body for ${name}`);
}

const src = fs.readFileSync(MOCK, 'utf8');
const painterSrc = `({
  ${extractMethod(src, 'rr')},
  ${extractMethod(src, 'drawPastry')},
  ${extractMethod(src, 'drawCustomer')},
  R: R
})`;

// Uniform radius: bakes every tier at the canonical size (see header).
const R = new Array(11).fill(CANONICAL_R);

// eval() is deliberate and safe here: this is an offline build tool, and the only input is
// a version-controlled design document in this repo — never user or network data. Evaluating
// the mock's own painting code is the entire point, since re-implementing it would let the
// sprites silently drift from the design authority. Nothing in the game ships this file.
const painter = eval(painterSrc); // eslint-disable-line no-eval

const TIERS = [
  'cookie', 'muffin', 'kiss-cookie', 'biscuit', 'turnover', 'bread-roll',
  'cinnamon-roll', 'shell-bun', 'piggy-cookie', 'flan', 'ring-cake',
];

function write(dir, file, canvas) {
  fs.mkdirSync(dir, { recursive: true });
  const p = path.join(dir, file);
  fs.writeFileSync(p, canvas.toBuffer('image/png'));
  return p;
}

let n = 0;

// ---- 11 pastries, 512², pivot centre ----
const pastryDir = path.join(OUT, 'Pastries');
TIERS.forEach((slug, t) => {
  const cv = createCanvas(PASTRY_SIZE, PASTRY_SIZE);
  const ctx = cv.getContext('2d');
  ctx.translate(PASTRY_SIZE / 2, PASTRY_SIZE / 2);
  painter.drawPastry(ctx, t);
  write(pastryDir, `pastry_${String(t).padStart(2, '0')}_${slug}.png`, cv);
  n++;
});

// ---- 3 regulars. drawCustomer sets its own 2x transform over a 210x170 logical area. ----
const customerDir = path.join(OUT, 'Customers');
for (let i = 0; i < 3; i++) {
  const cv = createCanvas(210 * 2, 170 * 2);
  painter.drawCustomer(cv, i);
  write(customerDir, `customer_${i}.png`, cv);
  n++;
}

console.log(`baked ${n} sprites into ${OUT}`);
