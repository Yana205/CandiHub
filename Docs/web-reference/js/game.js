/* game.js — physics, merging, the shop loop and everything painted inside the
   paper bag. Framework-free: the DOM shell subscribes via callbacks. */
(function (global) {
  'use strict';

  const { L, R: RIGHT, FLOOR, DROP_Y, TOP } = global.FIELD;
  const BEST_KEY = 'pandulce.best.v1';

  function PanDulceGame(canvas, hooks) {
    this.cv = canvas;
    this.ctx = canvas.getContext('2d');
    this.hooks = hooks || {};
    this.cfg = global.Config;

    this.bodies = [];
    this.parts = [];
    this.floats = [];
    this.nextId = 1;
    this.now = 0;
    this.lastT = 0;
    this.aimX = 129;
    this.px = null;
    this.py = null;
    this.canDropAt = 0;
    this.comboN = 0;
    this.lastMergeT = 0;
    this.dangerT = 0;
    this.fps = 60;
    this.stepMs = 0;

    this.tierList = global.PASTRY_NAMES.map((n, i) => ({
      name: n, label: i < 4 ? n : '?', discovered: i < 4, tier: i,
    }));

    this.state = {
      mode: 'closed',
      dropsLeft: this.cfg.customerEvery,
      served: 0,
      score: 0,
      best: Number(localStorage.getItem(BEST_KEY) || 0) || 0,
      customer: 0,
      orderTier: null,
      happy: false,
      flying: false,
      gameOver: false,
    };

    this.curTier = this.pick();
    this.nextTier = this.pick();

    this._radiiFor = -1;
    this.reset();
    this.loop = this.loop.bind(this);
    this.raf = requestAnimationFrame(this.loop);
  }

  const p = PanDulceGame.prototype;

  /* ---------- tunables read live from Config ---------- */
  Object.defineProperty(p, 'R', {
    get() {
      const s = this.cfg.sizeScale;
      if (this._radiiFor !== s) {
        this._radiiFor = s;
        this._radii = global.BASE_RADII.map(r => r * s);
      }
      return this._radii;
    }
  });
  p.rotAmt = function () { return this.cfg.rotationAmount; };
  p.every = function () { return Math.max(1, Math.round(this.cfg.customerEvery)); };

  /* Weighted pick over the low tiers — 4:3:2:1 across the first `maxTierSpawn+1`. */
  p.pick = function () {
    const top = Math.max(0, Math.min(global.BASE_RADII.length - 1, Math.round(this.cfg.maxTierSpawn)));
    const w = [];
    let sum = 0;
    for (let i = 0; i <= top; i++) { const v = Math.max(1, 4 - i); w.push(v); sum += v; }
    let s = sum * Math.random();
    for (let i = 0; i <= top; i++) { s -= w[i]; if (s <= 0) return i; }
    return 0;
  };

  p.notify = function () { if (this.hooks.onChange) this.hooks.onChange(this); };

  /* ---------- lifecycle ---------- */
  p.reset = function () {
    this.bodies = [];
    this.parts = [];
    this.floats = [];
    this.comboN = 0;
    this.dangerT = 0;
    this.canDropAt = 0;
    this.state.mode = 'closed';
    this.state.dropsLeft = this.every();
    this.state.served = 0;
    this.state.score = 0;
    this.state.orderTier = null;
    this.state.happy = false;
    this.state.flying = false;
    this.state.gameOver = false;
    for (const it of this.tierList) {
      it.discovered = it.tier < 4;
      it.label = it.discovered ? it.name : '?';
    }
    this.curTier = this.pick();
    this.nextTier = this.pick();
    const n = Math.round(this.cfg.startingBodies);
    for (let i = 0; i < n; i++) {
      this.bodies.push(this.mkBody(30 + Math.random() * 198, 300 - i * 34, this.pick(), 1));
    }
    this.notify();
  };

  p.destroy = function () { cancelAnimationFrame(this.raf); };

  p.mkBody = function (x, y, t, spawnT) {
    return {
      id: this.nextId++, x, y, vx: 0, vy: 0,
      rot: (Math.random() - 0.5) * 0.3,
      vrot: (Math.random() - 0.5) * 2.4 * this.rotAmt(),
      tier: t, spawnT: spawnT == null ? 1 : spawnT,
      squish: 0, dead: false, bornAt: this.now,
    };
  };

  /* Effective radius — back-out easing while the merge "pop" plays. */
  p.er = function (b) {
    const t = Math.min(1, b.spawnT);
    const c1 = 1.70158, c3 = c1 + 1;
    const e = 1 + c3 * Math.pow(t - 1, 3) + c1 * Math.pow(t - 1, 2);
    return this.R[b.tier] * (0.35 + 0.65 * e);
  };

  /* ---------- main loop ---------- */
  p.loop = function (t) {
    this.raf = requestAnimationFrame(this.loop);
    if (!this.ctx) return;
    const now = t / 1000;
    const raw = this.lastT ? now - this.lastT : 0.016;
    this.lastT = now;
    // Clamp to [0, 32ms]: never integrate backwards, never let a long stall
    // (tab in the background) teleport everything through the bag floor.
    let dt = Math.max(0, Math.min(0.032, raw));
    if (raw > 0) this.fps += (1 / raw - this.fps) * 0.08;

    if (!this.cfg.paused && !this.state.gameOver) {
      dt *= this.cfg.timeScale;
      this.now += dt;
      const t0 = performance.now();
      const N = Math.max(1, Math.round(this.cfg.substeps));
      for (let i = 0; i < N; i++) this.step(dt / N);
      this.checkTopOut(dt);
      this.stepMs += (performance.now() - t0 - this.stepMs) * 0.1;
    }
    this.draw();
  };

  p.step = function (dt) {
    const G = this.cfg.gravity, E = this.cfg.bounciness;
    const growTime = Math.max(0.02, this.cfg.mergeGrowTime);
    const bs = this.bodies;

    for (const b of bs) {
      b.vy += G * dt;
      b.x += b.vx * dt;
      b.y += b.vy * dt;
      b.rot += b.vrot * dt;
      const cap = 3 * this.rotAmt() + 0.15;
      if (b.vrot > cap) b.vrot = cap; else if (b.vrot < -cap) b.vrot = -cap;
      b.vrot *= 0.99;
      b.squish *= (1 - 6 * dt);
      if (b.spawnT < 1) b.spawnT = Math.min(1, b.spawnT + dt / growTime);
    }

    const merges = [];
    const maxTier = this.R.length - 1;
    const grace = this.cfg.comboDelay;

    for (let i = 0; i < bs.length; i++) {
      const a = bs[i];
      if (a.dead) continue;
      for (let j = i + 1; j < bs.length; j++) {
        const c = bs[j];
        if (c.dead) continue;
        const ra = this.er(a), rc = this.er(c);
        let dx = c.x - a.x, dy = c.y - a.y;
        let d = Math.hypot(dx, dy);
        const min = ra + rc;
        if (d >= min) continue;
        if (d < 0.01) { d = 0.01; dx = 0.01; dy = 0; }
        const nx = dx / d, ny = dy / d;

        if (a.tier === c.tier && a.tier < maxTier &&
            a.spawnT > 0.55 && c.spawnT > 0.55 &&
            this.now - a.bornAt > grace && this.now - c.bornAt > grace) {
          a.dead = true; c.dead = true;
          merges.push([a, c]);
          continue;
        }

        const ma = ra * ra, mc = rc * rc, tm = ma + mc;
        const ov = min - d;
        a.x -= nx * ov * (mc / tm); a.y -= ny * ov * (mc / tm);
        c.x += nx * ov * (ma / tm); c.y += ny * ov * (ma / tm);

        const rvx = c.vx - a.vx, rvy = c.vy - a.vy;
        const vn = rvx * nx + rvy * ny;
        if (vn < 0) {
          const jm = -(1 + E) * vn / (1 / ma + 1 / mc);
          a.vx -= jm * nx / ma; a.vy -= jm * ny / ma;
          c.vx += jm * nx / mc; c.vy += jm * ny / mc;
          const q = Math.min(0.28, Math.abs(vn) / 1500) * this.cfg.squishAmount;
          if (q > 0.05) { a.squish = Math.max(a.squish, q); c.squish = Math.max(c.squish, q); }
          const vt = rvx * -ny + rvy * nx;
          const rk = 0.15 * this.rotAmt();
          a.vrot += (vt / ra) * rk; c.vrot += (vt / rc) * rk;
        }
      }
    }

    for (const b of bs) {
      const r = this.er(b);
      if (b.x - r < L) { b.x = L + r; b.vx = Math.abs(b.vx) * E; b.vrot = -b.vy / r * 0.4 * this.rotAmt(); }
      if (b.x + r > RIGHT) { b.x = RIGHT - r; b.vx = -Math.abs(b.vx) * E; b.vrot = b.vy / r * 0.4 * this.rotAmt(); }
      if (b.y + r > FLOOR) {
        const vi = b.vy;
        b.y = FLOOR - r;
        b.vy = -Math.abs(b.vy) * E * 0.6;
        if (Math.abs(b.vy) < 20) b.vy = 0;
        b.vx *= Math.max(0, 1 - this.cfg.groundFriction * dt);
        b.vrot += (b.vx / r - b.vrot) * Math.min(0.4, 0.05 + 0.5 * this.rotAmt());
        if (vi > 180) b.squish = Math.max(b.squish, Math.min(0.3, vi / 1600) * this.cfg.squishAmount);
      }
    }

    if (merges.length) {
      for (const m of merges) this.doMerge(m[0], m[1]);
      this.bodies = this.bodies.filter(b => !b.dead);
    }

    for (const q of this.parts) {
      q.t += dt; q.x += q.vx * dt; q.y += q.vy * dt;
      q.vx *= 0.96; q.vy = q.vy * 0.96 + (q.star ? -20 : -60) * dt;
    }
    this.parts = this.parts.filter(q => q.t < q.life);
    for (const f of this.floats) { f.t += dt; f.y -= 42 * dt; }
    this.floats = this.floats.filter(f => f.t < 1);
  };

  p.doMerge = function (a, c) {
    const t2 = a.tier + 1;
    const ra = this.er(a), rc = this.er(c);
    const x = (a.x * ra + c.x * rc) / (ra + rc);
    const y = (a.y * ra + c.y * rc) / (ra + rc);
    const nb = this.mkBody(x, y, t2, 0);
    nb.vy = this.cfg.mergePopVy;
    nb.vx = (a.vx + c.vx) * 0.3;
    this.bodies.push(nb);
    this.burst(x, y, this.R[t2]);
    global.Sfx.play('merge', t2);

    if (this.now - this.lastMergeT < this.cfg.comboWindow) this.comboN++; else this.comboN = 1;
    this.lastMergeT = this.now;

    this.state.score += (t2 + 1) * 10 * Math.max(1, this.comboN);
    if (this.comboN >= 2) this.floats.push({ x, y: y - this.R[t2] - 8, t: 0, txt: 'Combo ' + this.comboN + '!' });

    const it = this.tierList[t2];
    if (!it.discovered) {
      it.discovered = true;
      it.label = it.name;
      this.floats.push({ x, y: y - this.R[t2] - 26, t: 0, txt: 'New!' });
      this.state.score += 250;
      global.Sfx.play('disco');
      if (this.hooks.onDiscover) this.hooks.onDiscover(t2);
    }
    this.notify();
  };

  p.burst = function (x, y, r) {
    const k = this.cfg.particleScale;
    const nA = Math.round(9 * k), nB = Math.round(6 * k);
    for (let i = 0; i < nA; i++) {
      const a = Math.random() * 6.28, s = 60 + Math.random() * 110;
      this.parts.push({ x: x + Math.cos(a) * r * 0.5, y: y + Math.sin(a) * r * 0.5, vx: Math.cos(a) * s, vy: Math.sin(a) * s, t: 0, life: 0.5 + Math.random() * 0.25, r: 4 + Math.random() * 5, col: '#fff3dd', star: false });
    }
    for (let i = 0; i < nB; i++) {
      const a = Math.random() * 6.28, s = 90 + Math.random() * 130;
      this.parts.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, t: 0, life: 0.6, r: 3 + Math.random() * 3, col: '#f0b64f', star: true });
    }
  };

  /* ---------- losing ---------- */
  p.checkTopOut = function (dt) {
    if (!this.cfg.topOut) { this.dangerT = 0; return; }
    const line = this.cfg.topOutLine;
    let over = false;
    for (const b of this.bodies) {
      if (b.spawnT < 1) continue;
      if (this.now - b.bornAt < 1.2) continue;
      if (Math.abs(b.vy) > 90) continue;
      if (b.y - this.er(b) < line) { over = true; break; }
    }
    if (over) {
      this.dangerT += dt;
      if (this.dangerT > this.cfg.topOutGrace) this.endGame();
    } else {
      this.dangerT = Math.max(0, this.dangerT - dt * 2);
    }
  };

  p.endGame = function () {
    if (this.state.gameOver) return;
    this.state.gameOver = true;
    this.state.flying = false;
    if (this.state.score > this.state.best) {
      this.state.best = this.state.score;
      try { localStorage.setItem(BEST_KEY, String(this.state.best)); } catch (e) {}
    }
    global.Sfx.play('over');
    this.notify();
  };

  /* ---------- shop loop ---------- */
  p.orderActive = function () {
    const s = this.state;
    return s.mode === 'open' && !s.happy && s.orderTier != null && !s.flying && !s.gameOver;
  };

  p.servableAt = function (x, y) {
    if (!this.orderActive()) return null;
    let best = null, bd = 1e9;
    for (const b of this.bodies) {
      if (b.tier !== this.state.orderTier) continue;
      const d = Math.hypot(b.x - x, b.y - y);
      if (d < this.er(b) + 6 && d < bd) { best = b; bd = d; }
    }
    return best;
  };

  p.openWindow = function () {
    const lo = Math.min(this.cfg.orderMinTier, this.cfg.orderMaxTier);
    const hi = Math.max(this.cfg.orderMinTier, this.cfg.orderMaxTier);
    const order = lo + Math.floor(Math.random() * (hi - lo + 1));
    this.state.mode = 'open';
    this.state.customer = this.state.served % 3;
    this.state.orderTier = order;
    this.state.happy = false;
    global.Sfx.play('open');
    this.notify();
  };

  p.serve = function (b) {
    const t = b.tier;
    this.bodies = this.bodies.filter(x => x !== b);
    this.burst(b.x, b.y, this.R[t]);
    this.state.flying = true;
    this.notify();
    if (this.hooks.onFly) this.hooks.onFly(t, 6 + b.x - 28, 404 + b.y - 28);
  };

  /* Called by the shell once the pastry lands in the window. */
  p.completeServe = function () {
    if (!this.state.flying) return;
    global.Sfx.play('serve');
    this.state.flying = false;
    this.state.happy = true;
    this.state.served += 1;
    this.state.score += 100 + (this.state.orderTier || 0) * 25;
    this.notify();
    clearTimeout(this._closeT);
    this._closeT = setTimeout(() => {
      this.state.mode = 'closed';
      this.state.happy = false;
      this.state.orderTier = null;
      this.state.dropsLeft = this.every();
      this.notify();
    }, this.cfg.happyMs);
  };

  /* ---------- input ---------- */
  p.canvasXY = function (e) {
    const r = this.cv.getBoundingClientRect();
    return [(e.clientX - r.left) / r.width * 258, (e.clientY - r.top) / r.height * 460];
  };

  p.pointerDown = function (e) {
    global.Sfx.ensure();
    const c = this.canvasXY(e);
    this.aimX = c[0]; this.px = c[0]; this.py = c[1];
  };
  p.pointerMove = function (e) {
    const c = this.canvasXY(e);
    this.aimX = c[0]; this.px = c[0]; this.py = c[1];
  };
  p.pointerUp = function (e) {
    if (this.state.gameOver) return;
    const c = this.canvasXY(e);
    this.aimX = c[0]; this.px = c[0]; this.py = c[1];
    const sb = this.servableAt(c[0], c[1]);
    if (sb) { this.serve(sb); return; }
    this.drop();
  };

  p.aimBy = function (dx) {
    this.aimX = Math.max(L, Math.min(RIGHT, this.aimX + dx));
  };

  p.drop = function () {
    if (this.state.gameOver) return;
    if (this.now < this.canDropAt) return;
    const t = this.curTier, r = this.R[t];
    const dropX = Math.max(L + r, Math.min(RIGHT - r, this.aimX));
    const b = this.mkBody(dropX, DROP_Y, t, 1);
    b.vy = this.cfg.dropVy;
    this.bodies.push(b);
    global.Sfx.play('drop');
    this.canDropAt = this.now + this.cfg.dropCooldown;
    this.curTier = this.nextTier;
    this.nextTier = this.pick();
    if (this.state.mode === 'closed' && !this.state.flying) {
      const d = this.state.dropsLeft - 1;
      if (d <= 0) this.openWindow(); else this.state.dropsLeft = d;
    }
    this.notify();
  };

  /* ---------- debug helpers ---------- */
  p.spawnTier = function (t, x) {
    const r = this.R[t];
    const px = x == null ? L + r + Math.random() * (RIGHT - L - 2 * r) : x;
    this.bodies.push(this.mkBody(px, DROP_Y, t, 1));
  };
  p.clearBag = function () { this.bodies = []; this.parts = []; this.floats = []; this.dangerT = 0; };
  p.discoverAll = function () {
    for (const it of this.tierList) { it.discovered = true; it.label = it.name; }
    if (this.hooks.onDiscover) this.hooks.onDiscover(-1);
    this.notify();
  };
  p.highestTier = function () {
    let h = -1;
    for (const b of this.bodies) if (b.tier > h) h = b.tier;
    return h;
  };

  /* ---------- painting the bag ---------- */
  p.draw = function () {
    const ctx = this.ctx;
    if (!ctx) return;
    ctx.setTransform(2, 0, 0, 2, 0, 0);
    ctx.clearRect(0, 0, 258, 460);

    // paper bag with a torn top edge
    ctx.beginPath();
    ctx.moveTo(4, 74);
    for (let x = 4; x < 254; x += 18) { ctx.lineTo(x + 9, 63); ctx.lineTo(x + 18, 74); }
    ctx.lineTo(254, 460); ctx.lineTo(4, 460); ctx.closePath();
    ctx.fillStyle = '#f2ddb7'; ctx.fill();
    ctx.strokeStyle = '#dfc296'; ctx.lineWidth = 2; ctx.stroke();
    ctx.save(); ctx.clip();
    ctx.globalAlpha = 0.5; ctx.fillStyle = '#e9d0a4';
    ctx.fillRect(30, 63, 26, 400); ctx.fillRect(120, 63, 18, 400); ctx.fillRect(196, 63, 30, 400);
    ctx.globalAlpha = 1; ctx.restore();

    // danger line
    const dangerBlink = this.dangerT > 0.25;
    if (this.cfg.topOut && (this.cfg.showDangerLine || dangerBlink)) {
      const line = this.cfg.topOutLine;
      const a = dangerBlink ? 0.35 + 0.45 * Math.abs(Math.sin(this.now * 7)) : 0.28;
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = '#d94f43'; ctx.lineWidth = 2.5; ctx.setLineDash([9, 7]);
      ctx.beginPath(); ctx.moveTo(6, line); ctx.lineTo(252, line); ctx.stroke();
      ctx.restore();
    }

    // aim guide + held pastry
    const canDrop = this.now >= this.canDropAt && !this.state.gameOver;
    if (canDrop) {
      const hr = this.R[this.curTier];
      const hx = Math.max(L + hr, Math.min(RIGHT - hr, this.aimX));
      if (this.cfg.aimGuide) {
        ctx.save();
        ctx.setLineDash([4, 10]); ctx.lineCap = 'round';
        ctx.strokeStyle = 'rgba(255,255,255,0.75)'; ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(hx, TOP); ctx.lineTo(hx, 436); ctx.stroke();
        ctx.restore();
      }
      ctx.save(); ctx.translate(hx, DROP_Y);
      global.Art.drawPastry(ctx, this.curTier, hr);
      ctx.restore();
    }

    // bodies
    const oa = this.orderActive();
    const hov = oa && this.px != null ? this.servableAt(this.px, this.py) : null;
    for (const b of this.bodies) {
      const r = this.er(b);
      const wanted = oa && b.tier === this.state.orderTier;
      const hovS = hov === b ? 1.14 : 1;
      if (wanted) {
        const pulse = 1 + Math.sin(this.now * 5) * 0.05;
        ctx.save(); ctx.translate(b.x, b.y);
        ctx.strokeStyle = hov === b ? '#fff3dd' : '#f0b64f';
        ctx.lineWidth = hov === b ? 5 : 3.5;
        ctx.setLineDash(hov === b ? [] : [7, 7]);
        ctx.globalAlpha = 0.95;
        ctx.beginPath(); ctx.arc(0, 0, r * hovS * pulse + 6, 0, 6.28); ctx.stroke();
        ctx.restore();
      }
      ctx.save();
      ctx.translate(b.x, b.y);
      ctx.scale(1 + b.squish * 0.6, 1 - b.squish);
      ctx.rotate(b.rot);
      global.Art.drawPastry(ctx, b.tier, r * hovS);
      ctx.restore();

      if (this.cfg.showHitbox) {
        ctx.save();
        ctx.strokeStyle = 'rgba(40,200,255,0.9)'; ctx.lineWidth = 1;
        ctx.beginPath(); ctx.arc(b.x, b.y, r, 0, 6.28); ctx.stroke();
        ctx.beginPath(); ctx.moveTo(b.x, b.y);
        ctx.lineTo(b.x + Math.cos(b.rot) * r, b.y + Math.sin(b.rot) * r); ctx.stroke();
        ctx.restore();
      }
      if (this.cfg.showIds) {
        ctx.save();
        ctx.font = '700 10px monospace'; ctx.textAlign = 'center';
        ctx.fillStyle = '#1d1108';
        ctx.fillText('t' + b.tier + '#' + b.id, b.x, b.y - r - 3);
        ctx.restore();
      }
    }

    // particles
    for (const q of this.parts) {
      const k = 1 - q.t / q.life;
      ctx.globalAlpha = k;
      if (q.star) {
        ctx.strokeStyle = q.col; ctx.lineWidth = 2; ctx.lineCap = 'round';
        const r = q.r * (0.5 + k);
        ctx.beginPath();
        ctx.moveTo(q.x - r, q.y); ctx.lineTo(q.x + r, q.y);
        ctx.moveTo(q.x, q.y - r); ctx.lineTo(q.x, q.y + r);
        ctx.stroke();
      } else {
        ctx.fillStyle = q.col;
        ctx.beginPath(); ctx.arc(q.x, q.y, q.r * (0.4 + k * 0.8), 0, 6.28); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    // floating text
    for (const f of this.floats) {
      const k = 1 - f.t;
      ctx.globalAlpha = Math.min(1, k * 2);
      ctx.font = '700 17px "Baloo 2", sans-serif'; ctx.textAlign = 'center';
      ctx.lineWidth = 4; ctx.strokeStyle = '#8a5a33'; ctx.lineJoin = 'round';
      ctx.strokeText(f.txt, f.x, f.y);
      ctx.fillStyle = '#fff3dd'; ctx.fillText(f.txt, f.x, f.y);
      ctx.globalAlpha = 1;
    }

    // woven basket in front of the bag
    ctx.fillStyle = '#a86e3c'; global.Art.rr(ctx, 0, 402, 258, 12, 6); ctx.fill();
    ctx.strokeStyle = '#8a5a33'; ctx.lineWidth = 2; global.Art.rr(ctx, 0, 402, 258, 12, 6); ctx.stroke();
    for (let row = 0; row < 3; row++) {
      const y = 422 + row * 16;
      for (let i = 0; i < 12; i++) {
        const x = i * 22 + (row % 2 ? 11 : 0) - 6;
        ctx.fillStyle = (i + row) % 2 ? '#c98f52' : '#b57a41';
        ctx.beginPath(); ctx.ellipse(x + 11, y, 12, 7.5, 0, 0, 6.28); ctx.fill();
      }
    }
    ctx.fillStyle = 'rgba(90,60,30,0.15)'; ctx.fillRect(0, 414, 258, 4);
  };

  global.PanDulceGame = PanDulceGame;
})(window);
