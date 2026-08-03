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

// ---- The bear — the single customer, rising from behind the counter. ----
// Authored here (not extracted): the bear is new art from the updated mock.
// 460×400 canvas = 230×200 stage px at 2x. Pivot centre; bottom edge is the
// part that stays hidden behind the counter.
const customerDir = path.join(OUT, 'Customers');
{
  const cv = createCanvas(460, 400);
  const ctx = cv.getContext('2d');
  ctx.scale(2, 2);
  ctx.translate(115, 100);              // logical centre of the 230×200 area
  const FUR = '#a9744a', FUR_D = '#7c5231', FUR_L = '#d9a86f';

  ctx.lineWidth = 7; ctx.strokeStyle = FUR_D;

  // body shoulders (mostly hidden behind the counter)
  ctx.fillStyle = FUR;
  ctx.beginPath(); ctx.ellipse(0, 78, 62, 40, 0, 0, Math.PI * 2); ctx.fill(); ctx.stroke();

  // ears
  for (const sx of [-1, 1]) {
    ctx.fillStyle = FUR;
    ctx.beginPath(); ctx.arc(sx * 46, -52, 22, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.fillStyle = FUR_L;
    ctx.beginPath(); ctx.arc(sx * 46, -52, 11, 0, Math.PI * 2); ctx.fill();
  }

  // head
  ctx.fillStyle = FUR;
  ctx.beginPath(); ctx.arc(0, 0, 64, 0, Math.PI * 2); ctx.fill(); ctx.stroke();

  // muzzle
  ctx.fillStyle = FUR_L;
  ctx.beginPath(); ctx.ellipse(0, 22, 28, 20, 0, 0, Math.PI * 2); ctx.fill();

  // eyes, nose, smile
  ctx.fillStyle = '#3d2716';
  ctx.beginPath(); ctx.arc(-22, -6, 5.5, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath(); ctx.arc(22, -6, 5.5, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath(); ctx.ellipse(0, 14, 8, 6, 0, 0, Math.PI * 2); ctx.fill();
  ctx.lineWidth = 4; ctx.strokeStyle = '#3d2716'; ctx.lineCap = 'round';
  ctx.beginPath(); ctx.arc(-7, 22, 8, 0.15 * Math.PI, 0.85 * Math.PI); ctx.stroke();
  ctx.beginPath(); ctx.arc(7, 22, 8, 0.15 * Math.PI, 0.85 * Math.PI); ctx.stroke();

  // blush
  ctx.fillStyle = 'rgba(232,143,162,0.75)';
  ctx.beginPath(); ctx.ellipse(-40, 12, 10, 7, 0, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath(); ctx.ellipse(40, 12, 10, 7, 0, 0, Math.PI * 2); ctx.fill();

  write(customerDir, 'customer_0.png', cv);
  n++;
}

// ---- Counter opening: the red/white ring the bear pops through. ----
// 360×160 = 180×80 stage px at 2x. Drawn on the Furniture layer IN FRONT of the bear.
{
  const cv = createCanvas(360, 160);
  const ctx = cv.getContext('2d');
  ctx.scale(2, 2);
  ctx.translate(90, 40);
  ctx.lineWidth = 4; ctx.strokeStyle = '#b04531';
  ctx.fillStyle = '#d95f43';
  ctx.beginPath(); ctx.ellipse(0, 0, 86, 36, 0, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
  ctx.fillStyle = '#fff3dd';
  ctx.beginPath(); ctx.ellipse(0, 0, 66, 24, 0, 0, Math.PI * 2); ctx.fill();
  write(path.join(OUT, 'Shell'), 'counter_opening.png', cv);
  n++;
}

// ---- Window scenery: sky, clouds, skyline, lamppost, street, mullions (§8.3). ----
// One 756×412 PNG covering the 378×206 window interior at 2x. The wooden frame
// stays procedural so it can share the furniture palette.
{
  const W = 378, H = 206;
  const cv = createCanvas(W * 2, H * 2);
  const ctx = cv.getContext('2d');
  ctx.scale(2, 2);

  const sky = ctx.createLinearGradient(0, 0, 0, H);
  sky.addColorStop(0, '#d9e9f1'); sky.addColorStop(0.6, '#e9e1cd'); sky.addColorStop(1, '#efe2ce');
  ctx.fillStyle = sky; ctx.fillRect(0, 0, W, H);

  // clouds — white pills
  const pill = (x, y, w, h, a) => {
    ctx.fillStyle = `rgba(255,255,255,${a})`;
    ctx.beginPath();
    if (ctx.roundRect) ctx.roundRect(x, y, w, h, h / 2); else ctx.rect(x, y, w, h);
    ctx.fill();
  };
  pill(34, 20, 58, 14, 0.85); pill(54, 11, 30, 14, 0.85); pill(W - 46 - 46, 32, 46, 12, 0.7);

  // skyline — five striped buildings on bottom 44
  const baseY = H - 44;
  const blocks = [
    [-6, 80, 88, '#c3b39c', '#ab9880', 19],
    [66, 64, 124, '#b4a389', '#9c8b72', 21],
    [126, 88, 74, '#ccbca4', '#b4a48c', 18],
    [208, 72, 112, '#bcab92', '#a4947b', 20],
    [274, 98, 86, '#c8b8a0', '#b0a088', 19],
  ];
  for (const [x, w, h, fill, border, period] of blocks) {
    ctx.fillStyle = fill; ctx.fillRect(x, baseY - h, w, h);
    ctx.fillStyle = border; ctx.fillRect(x, baseY - h, w, 3);
    ctx.fillStyle = 'rgba(255,255,255,0.28)';
    for (let y = baseY - h + 6; y < baseY; y += period) ctx.fillRect(x, y, w, 5);
  }

  // lamppost
  ctx.fillStyle = '#8a7a66'; ctx.fillRect(100, baseY - 112, 4, 112);
  ctx.fillStyle = '#f0d79b'; ctx.strokeStyle = '#8a7a66'; ctx.lineWidth = 2;
  ctx.beginPath();
  if (ctx.roundRect) ctx.roundRect(90, baseY - 121, 24, 15, [8, 8, 3, 3]); else ctx.rect(90, baseY - 121, 24, 15);
  ctx.fill(); ctx.stroke();

  // street
  ctx.fillStyle = '#cbbca6'; ctx.fillRect(0, baseY, W, 44);
  ctx.fillStyle = '#b8a68d'; ctx.fillRect(0, baseY, W, 4);
  ctx.fillStyle = 'rgba(255,255,255,0.8)';
  for (let x = 0; x < W; x += 36) ctx.fillRect(x, H - 20, 18, 3);

  // mullions
  ctx.fillStyle = '#b5854f';
  ctx.fillRect(112, 0, 5, H);
  ctx.fillRect(W - 112 - 5, 0, 5, H);

  write(path.join(OUT, 'Shell'), 'window_scene.png', cv);
  n++;
}

// ---- Effect textures for the ParticleSystems. ----
{
  const effDir = path.join(OUT, 'Effects');

  // soft disc: radial falloff, white
  const cv = createCanvas(64, 64);
  const ctx = cv.getContext('2d');
  const g = ctx.createRadialGradient(32, 32, 4, 32, 32, 30);
  g.addColorStop(0, 'rgba(255,255,255,1)');
  g.addColorStop(0.7, 'rgba(255,255,255,0.85)');
  g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, 64, 64);
  write(effDir, 'soft_disc.png', cv);

  // 4-point star (sparkle)
  const cv2 = createCanvas(128, 128);
  const c2 = cv2.getContext('2d');
  c2.translate(64, 64);
  c2.fillStyle = '#ffffff';
  c2.beginPath();
  for (let i = 0; i < 8; i++) {
    const a = (i * Math.PI) / 4;
    const r = i % 2 === 0 ? 58 : 14;
    c2[i === 0 ? 'moveTo' : 'lineTo'](Math.cos(a) * r, Math.sin(a) * r);
  }
  c2.closePath(); c2.fill();
  write(effDir, 'spark.png', cv2);
  n += 2;
}

console.log(`baked ${n} sprites into ${OUT}`);
