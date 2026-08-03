/* art.js — canvas drawing for every pastry, the customers and the shelf icons.
   All draw calls are centred on (0,0) and take an explicit radius so the same
   routine serves the physics bodies, the "next" sign, the order bubble and the
   collection shelf. */
(function (global) {
  'use strict';

  function rr(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  function drawPastry(ctx, t, R) {
    const O = Math.max(2, R * 0.09);
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';
    const P = (fill, stroke) => {
      ctx.fillStyle = fill;
      ctx.fill();
      if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = O; ctx.stroke(); }
    };

    if (t === 0) { // happy cookie
      ctx.beginPath(); ctx.arc(0, 0, R, 0, 6.28); P('#f4d9a8', '#c9a26b');
      ctx.fillStyle = '#6b4a2e';
      ctx.beginPath(); ctx.arc(-R * 0.34, -R * 0.12, R * 0.1, 0, 6.28); ctx.fill();
      ctx.beginPath(); ctx.arc(R * 0.34, -R * 0.12, R * 0.1, 0, 6.28); ctx.fill();
      ctx.strokeStyle = '#6b4a2e'; ctx.lineWidth = O * 0.8;
      ctx.beginPath(); ctx.arc(0, R * 0.08, R * 0.32, 0.35, 2.79); ctx.stroke();
      ctx.fillStyle = 'rgba(232,143,162,0.7)';
      ctx.beginPath(); ctx.arc(-R * 0.58, R * 0.18, R * 0.13, 0, 6.28); ctx.fill();
      ctx.beginPath(); ctx.arc(R * 0.58, R * 0.18, R * 0.13, 0, 6.28); ctx.fill();

    } else if (t === 1) { // muffin
      ctx.beginPath();
      ctx.moveTo(-R * 0.72, -R * 0.05); ctx.lineTo(R * 0.72, -R * 0.05);
      ctx.lineTo(R * 0.55, R * 0.92); ctx.lineTo(-R * 0.55, R * 0.92);
      ctx.closePath(); P('#d95f43', '#b04531');
      ctx.strokeStyle = '#b04531'; ctx.lineWidth = O * 0.7;
      for (let i = -1; i <= 1; i++) {
        ctx.beginPath(); ctx.moveTo(i * R * 0.3, R * 0.05); ctx.lineTo(i * R * 0.24, R * 0.85); ctx.stroke();
      }
      ctx.beginPath(); ctx.ellipse(0, -R * 0.32, R * 0.8, R * 0.62, 0, 3.14, 6.28); ctx.closePath(); P('#f0c26a', '#cf9b4a');
      ctx.fillStyle = '#b97f33';
      ctx.beginPath(); ctx.arc(-R * 0.3, -R * 0.5, R * 0.06, 0, 6.28); ctx.fill();
      ctx.beginPath(); ctx.arc(R * 0.2, -R * 0.62, R * 0.06, 0, 6.28); ctx.fill();

    } else if (t === 2) { // sugar kiss
      ctx.beginPath(); ctx.moveTo(0, -R * 0.95);
      ctx.quadraticCurveTo(R * 0.85, -R * 0.1, R * 0.8, R * 0.55);
      ctx.quadraticCurveTo(R * 0.5, R * 0.9, 0, R * 0.9);
      ctx.quadraticCurveTo(-R * 0.5, R * 0.9, -R * 0.8, R * 0.55);
      ctx.quadraticCurveTo(-R * 0.85, -R * 0.1, 0, -R * 0.95);
      ctx.closePath(); P('#e88fa2', '#c96b82');
      ctx.beginPath(); ctx.arc(0, -R * 0.55, R * 0.28, 0, 6.28); P('#d95f77');
      ctx.fillStyle = 'rgba(255,255,255,0.8)';
      for (const d of [[-0.35, 0.1], [0.3, 0.25], [0, 0.55], [-0.15, -0.15], [0.45, -0.1]]) {
        ctx.beginPath(); ctx.arc(d[0] * R, d[1] * R, R * 0.06, 0, 6.28); ctx.fill();
      }

    } else if (t === 3) { // biscuit with jam
      rr(ctx, -R * 0.85, -R * 0.05, R * 1.7, R * 0.75, R * 0.3); P('#f2d9ae', '#cfa870');
      ctx.beginPath(); ctx.ellipse(0, -R * 0.15, R * 0.82, R * 0.62, 0, 3.14, 6.28); ctx.closePath(); P('#f6e3bd', '#cfa870');
      ctx.strokeStyle = '#c94f4f'; ctx.lineWidth = R * 0.22; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(-R * 0.72, R * 0.08);
      ctx.quadraticCurveTo(-R * 0.3, R * 0.28, 0, R * 0.1);
      ctx.quadraticCurveTo(R * 0.35, -R * 0.05, R * 0.72, R * 0.12); ctx.stroke();
      ctx.fillStyle = 'rgba(255,255,255,0.9)';
      for (const d of [[-0.4, -0.5], [-0.05, -0.62], [0.35, -0.45]]) {
        ctx.beginPath(); ctx.arc(d[0] * R, d[1] * R, R * 0.055, 0, 6.28); ctx.fill();
      }

    } else if (t === 4) { // empanada
      ctx.beginPath(); ctx.arc(0, R * 0.25, R * 0.92, 3.14, 6.28);
      ctx.quadraticCurveTo(0, R * 0.45, -R * 0.92, R * 0.25); ctx.closePath(); P('#e8a24b', '#bf7c2f');
      ctx.fillStyle = '#d18a35';
      for (let i = -3; i <= 3; i++) {
        ctx.beginPath(); ctx.arc(i * R * 0.26, R * 0.28 + Math.abs(i) * R * 0.008, R * 0.11, 0, 6.28); ctx.fill();
      }
      ctx.fillStyle = '#bf7c2f';
      for (const dx of [-0.3, 0, 0.3]) { ctx.beginPath(); ctx.arc(dx * R, -R * 0.15, R * 0.05, 0, 6.28); ctx.fill(); }

    } else if (t === 5) { // bread roll
      ctx.beginPath(); ctx.ellipse(0, 0, R * 0.98, R * 0.6, 0, 0, 6.28); P('#efb96b', '#c2884a');
      ctx.beginPath(); ctx.ellipse(0, 0, R * 0.6, R * 0.22, 0, 0, 6.28); P('#f7dca8', '#c2884a');
      ctx.strokeStyle = '#c2884a'; ctx.lineWidth = O * 0.7;
      ctx.beginPath(); ctx.moveTo(-R * 0.5, 0); ctx.quadraticCurveTo(0, -R * 0.12, R * 0.5, 0); ctx.stroke();

    } else if (t === 6) { // cinnamon roll
      ctx.beginPath(); ctx.arc(0, 0, R, 0, 6.28); P('#e0a055', '#b97a3a');
      ctx.strokeStyle = '#b97a3a'; ctx.lineWidth = R * 0.16;
      ctx.beginPath();
      for (let a = 0; a < 12.5; a += 0.2) {
        const rad = R * 0.08 + a * R * 0.072;
        const x = Math.cos(a) * rad, y = Math.sin(a) * rad;
        if (a === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.stroke();
      ctx.strokeStyle = 'rgba(255,250,240,0.85)'; ctx.lineWidth = R * 0.14; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(-R * 0.7, -R * 0.3);
      ctx.quadraticCurveTo(-R * 0.2, -R * 0.55, R * 0.15, -R * 0.35);
      ctx.quadraticCurveTo(R * 0.5, -R * 0.18, R * 0.72, -R * 0.3); ctx.stroke();

    } else if (t === 7) { // shell bun
      ctx.beginPath(); ctx.arc(0, 0, R, 0, 6.28); P('#efb3bd', '#cf8b96');
      ctx.strokeStyle = '#fae3e6'; ctx.lineWidth = R * 0.11; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.arc(0, 0, R * 0.3, 0, 6.28); ctx.stroke();
      for (let i = 0; i < 8; i++) {
        const a = i * 0.785;
        ctx.beginPath(); ctx.moveTo(Math.cos(a) * R * 0.34, Math.sin(a) * R * 0.34);
        ctx.quadraticCurveTo(Math.cos(a + 0.25) * R * 0.68, Math.sin(a + 0.25) * R * 0.68,
                             Math.cos(a + 0.12) * R * 0.94, Math.sin(a + 0.12) * R * 0.94);
        ctx.stroke();
      }

    } else if (t === 8) { // piggy bread
      ctx.beginPath(); ctx.ellipse(-R * 0.12, R * 0.08, R * 0.78, R * 0.58, 0, 0, 6.28); P('#b5793f', '#8f5a2a');
      ctx.fillStyle = '#b5793f';
      for (const dx of [-0.55, -0.15, 0.25]) {
        rr(ctx, dx * R, R * 0.45, R * 0.22, R * 0.32, R * 0.08);
        ctx.fill(); ctx.strokeStyle = '#8f5a2a'; ctx.lineWidth = O * 0.7; ctx.stroke();
      }
      ctx.beginPath(); ctx.moveTo(R * 0.28, -R * 0.62); ctx.lineTo(R * 0.55, -R * 0.85); ctx.lineTo(R * 0.66, -R * 0.5);
      ctx.closePath(); P('#b5793f', '#8f5a2a');
      ctx.beginPath(); ctx.arc(R * 0.45, -R * 0.18, R * 0.44, 0, 6.28); P('#b5793f', '#8f5a2a');
      ctx.beginPath(); ctx.ellipse(R * 0.78, -R * 0.1, R * 0.18, R * 0.14, 0, 0, 6.28); P('#d9a86f', '#8f5a2a');
      ctx.fillStyle = '#8f5a2a';
      ctx.beginPath(); ctx.arc(R * 0.73, -R * 0.12, R * 0.035, 0, 6.28); ctx.fill();
      ctx.beginPath(); ctx.arc(R * 0.84, -R * 0.12, R * 0.035, 0, 6.28); ctx.fill();
      ctx.beginPath(); ctx.arc(R * 0.42, -R * 0.3, R * 0.05, 0, 6.28); ctx.fill();
      ctx.strokeStyle = '#8f5a2a'; ctx.lineWidth = O * 0.7;
      ctx.beginPath(); ctx.arc(-R * 0.88, -R * 0.05, R * 0.14, 1.5, 5.2); ctx.stroke();

    } else if (t === 9) { // flan
      ctx.beginPath(); ctx.moveTo(-R * 0.62, -R * 0.6); ctx.lineTo(R * 0.62, -R * 0.6);
      ctx.quadraticCurveTo(R * 0.72, -R * 0.6, R * 0.75, -R * 0.45);
      ctx.lineTo(R * 0.92, R * 0.55); ctx.quadraticCurveTo(R * 0.95, R * 0.72, R * 0.75, R * 0.72);
      ctx.lineTo(-R * 0.75, R * 0.72); ctx.quadraticCurveTo(-R * 0.95, R * 0.72, -R * 0.92, R * 0.55);
      ctx.lineTo(-R * 0.75, -R * 0.45); ctx.quadraticCurveTo(-R * 0.72, -R * 0.6, -R * 0.62, -R * 0.6);
      ctx.closePath(); P('#f2c464', '#d19a3c');
      ctx.beginPath(); ctx.moveTo(-R * 0.68, -R * 0.35);
      ctx.lineTo(-R * 0.62, -R * 0.58); ctx.quadraticCurveTo(0, -R * 0.72, R * 0.62, -R * 0.58);
      ctx.lineTo(R * 0.68, -R * 0.35);
      ctx.quadraticCurveTo(R * 0.45, -R * 0.18, R * 0.3, -R * 0.38);
      ctx.quadraticCurveTo(R * 0.15, -R * 0.1, -R * 0.05, -R * 0.34);
      ctx.quadraticCurveTo(-R * 0.25, -R * 0.05, -R * 0.42, -R * 0.3);
      ctx.quadraticCurveTo(-R * 0.55, -R * 0.15, -R * 0.68, -R * 0.35);
      ctx.closePath(); P('#b06a2a', '#96591f');
      ctx.strokeStyle = 'rgba(255,255,255,0.6)'; ctx.lineWidth = R * 0.09; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(-R * 0.55, R * 0.3); ctx.quadraticCurveTo(-R * 0.5, R * 0.5, -R * 0.3, R * 0.55); ctx.stroke();

    } else { // king cake
      ctx.beginPath(); ctx.arc(0, 0, R * 0.95, 0, 6.28); ctx.arc(0, 0, R * 0.4, 0, 6.28, true); P('#eec27a', '#c8964d');
      ctx.strokeStyle = '#c8964d'; ctx.lineWidth = O;
      ctx.beginPath(); ctx.arc(0, 0, R * 0.4, 0, 6.28); ctx.stroke();
      const cols = ['#d95f43', '#7fa864', '#e88fa2', '#f0b64f'];
      for (let i = 0; i < 8; i++) {
        const a = i * 0.785 + 0.2;
        ctx.strokeStyle = cols[i % 4]; ctx.lineWidth = R * 0.16; ctx.lineCap = 'round';
        ctx.beginPath(); ctx.arc(0, 0, R * 0.68, a, a + 0.3); ctx.stroke();
      }
      ctx.strokeStyle = 'rgba(255,255,255,0.75)'; ctx.lineWidth = R * 0.07;
      for (let i = 0; i < 8; i++) {
        const a = i * 0.785 + 0.6;
        ctx.beginPath(); ctx.arc(0, 0, R * 0.68, a, a + 0.16); ctx.stroke();
      }
    }
  }

  /* Paint one pastry centred in a small hi-dpi canvas (next sign, order bubble, flyer). */
  function drawSmall(cv, tier, targetR) {
    const ctx = cv.getContext('2d');
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    const w = cv.width / 2, h = cv.height / 2;
    ctx.clearRect(0, 0, w, h);
    ctx.save();
    ctx.translate(w / 2, h / 2);
    drawPastry(ctx, tier, targetR);
    ctx.restore();
  }

  /* Shelf icon — silhouetted while the tier is still undiscovered. */
  function renderIcon(cv, tier, discovered) {
    const ctx = cv.getContext('2d');
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    ctx.clearRect(0, 0, 52, 40);
    const tmp = document.createElement('canvas');
    tmp.width = 104; tmp.height = 80;
    const tc = tmp.getContext('2d');
    tc.setTransform(2, 0, 0, 2, 0, 0);
    tc.translate(26, 20);
    drawPastry(tc, tier, 12 + tier * 0.9);
    if (!discovered) {
      tc.setTransform(1, 0, 0, 1, 0, 0);
      tc.globalCompositeOperation = 'source-in';
      tc.fillStyle = '#3a2a1c';
      tc.fillRect(0, 0, 104, 80);
    }
    ctx.drawImage(tmp, 0, 0, 52, 40);
  }

  /* The three regulars that show up at the window. */
  function drawCustomer(cv, idx) {
    const ctx = cv.getContext('2d');
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    ctx.clearRect(0, 0, 210, 170);
    ctx.lineJoin = 'round'; ctx.lineCap = 'round';
    const shirt = ['#d95f43', '#7fa864', '#7f9fc9'][idx];
    const skin = ['#b5793f', '#cdc5ba', '#f0c25e'][idx];
    const dark = ['#8f5a2a', '#9a9186', '#c99436'][idx];
    const P = (f, s) => { ctx.fillStyle = f; ctx.fill(); if (s) { ctx.strokeStyle = s; ctx.lineWidth = 3; ctx.stroke(); } };

    rr(ctx, 45, 116, 120, 58, 26); P(shirt, 'rgba(0,0,0,0.15)');
    rr(ctx, 63, 128, 84, 46, 14); P('#fff3dd', '#e0cba6');
    ctx.strokeStyle = '#e0cba6'; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(75, 128); ctx.lineTo(80, 118); ctx.moveTo(135, 128); ctx.lineTo(130, 118); ctx.stroke();

    if (idx === 0) {
      ctx.beginPath(); ctx.arc(70, 38, 15, 0, 6.28); P(skin, dark);
      ctx.beginPath(); ctx.arc(140, 38, 15, 0, 6.28); P(skin, dark);
      ctx.beginPath(); ctx.arc(70, 38, 7, 0, 6.28); P('#d9a86f');
      ctx.beginPath(); ctx.arc(140, 38, 7, 0, 6.28); P('#d9a86f');
    } else if (idx === 1) {
      ctx.beginPath(); ctx.moveTo(58, 52); ctx.lineTo(66, 20); ctx.lineTo(88, 40); ctx.closePath(); P(skin, dark);
      ctx.beginPath(); ctx.moveTo(152, 52); ctx.lineTo(144, 20); ctx.lineTo(122, 40); ctx.closePath(); P(skin, dark);
      ctx.beginPath(); ctx.moveTo(64, 47); ctx.lineTo(68, 30); ctx.lineTo(80, 41); ctx.closePath(); P('#e8a7b5');
      ctx.beginPath(); ctx.moveTo(146, 47); ctx.lineTo(142, 30); ctx.lineTo(130, 41); ctx.closePath(); P('#e8a7b5');
    } else {
      ctx.strokeStyle = dark; ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(105, 32); ctx.quadraticCurveTo(100, 18, 92, 16); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(105, 32); ctx.quadraticCurveTo(106, 16, 114, 12); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(105, 32); ctx.quadraticCurveTo(114, 22, 122, 20); ctx.stroke();
    }

    ctx.beginPath(); ctx.arc(105, 78, 44, 0, 6.28); P(skin, dark);
    ctx.fillStyle = '#4a3324';
    ctx.beginPath(); ctx.arc(89, 74, 4.5, 0, 6.28); ctx.fill();
    ctx.beginPath(); ctx.arc(121, 74, 4.5, 0, 6.28); ctx.fill();
    ctx.fillStyle = 'rgba(232,143,162,0.75)';
    ctx.beginPath(); ctx.arc(78, 90, 7, 0, 6.28); ctx.fill();
    ctx.beginPath(); ctx.arc(132, 90, 7, 0, 6.28); ctx.fill();

    if (idx === 0) {
      ctx.beginPath(); ctx.ellipse(105, 94, 16, 12, 0, 0, 6.28); P('#e8c191', dark);
      ctx.fillStyle = '#6f4a2c'; ctx.beginPath(); ctx.ellipse(105, 90, 5, 3.5, 0, 0, 6.28); ctx.fill();
      ctx.strokeStyle = '#6f4a2c'; ctx.lineWidth = 2.5;
      ctx.beginPath(); ctx.arc(100, 97, 4, 0.3, 2.6); ctx.stroke();
      ctx.beginPath(); ctx.arc(110, 97, 4, 0.5, 2.8); ctx.stroke();
    } else if (idx === 1) {
      ctx.fillStyle = '#e8a7b5';
      ctx.beginPath(); ctx.moveTo(101, 88); ctx.lineTo(109, 88); ctx.lineTo(105, 93); ctx.closePath(); ctx.fill();
      ctx.strokeStyle = '#4a3324'; ctx.lineWidth = 2.5;
      ctx.beginPath(); ctx.arc(100, 96, 4.5, 0.4, 2.6); ctx.stroke();
      ctx.beginPath(); ctx.arc(110, 96, 4.5, 0.5, 2.7); ctx.stroke();
      ctx.strokeStyle = 'rgba(74,51,36,0.6)'; ctx.lineWidth = 2;
      ctx.beginPath(); ctx.moveTo(58, 84); ctx.lineTo(74, 86); ctx.moveTo(58, 94); ctx.lineTo(74, 92); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(152, 84); ctx.lineTo(136, 86); ctx.moveTo(152, 94); ctx.lineTo(136, 92); ctx.stroke();
    } else {
      ctx.beginPath(); ctx.moveTo(97, 86); ctx.lineTo(113, 86); ctx.lineTo(105, 98); ctx.closePath(); P('#e07b3a', '#c2611f');
      ctx.strokeStyle = '#4a3324'; ctx.lineWidth = 2.5;
      ctx.beginPath(); ctx.arc(89, 74, 6.5, 3.5, 5.9); ctx.stroke();
    }
  }

  global.Art = { rr, drawPastry, drawSmall, renderIcon, drawCustomer };
})(window);
