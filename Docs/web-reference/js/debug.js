/* debug.js — builds the tweak side menu from Config.__schema and wires it to
   the live game. Every change applies on the next frame; nothing restarts
   unless you ask it to. */
(function (global) {
  'use strict';

  const Cfg = global.Config;

  function el(tag, cls, txt) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (txt != null) n.textContent = txt;
    return n;
  }

  function fmt(item, v) {
    if (item.type === 'bool') return v ? 'yes' : 'no';
    const dec = item.int ? 0 : Math.max(0, -Math.floor(Math.log10(item.step)));
    return v.toFixed(dec) + (item.unit ? ' ' + item.unit : '');
  }

  function DebugPanel(container, shell) {
    this.shell = shell;
    this.game = shell.game;
    this.rows = {};
    this.container = container;
    this.build();
    this.syncAll();
    const self = this;
    shell.onConfigExternalChange = () => self.syncAll();
    setInterval(() => self.updateStats(), 120);
  }

  const p = DebugPanel.prototype;

  p.build = function () {
    const c = this.container;

    const head = el('header');
    head.appendChild(el('h1', null, 'Game feel'));
    const sub = el('div', 'sub');
    sub.innerHTML = 'Live changes · <a href="index.html" style="color:#d9b98c">back to the game</a> · P pause · R restart';
    head.appendChild(sub);
    c.appendChild(head);

    // live stats
    const stats = el('div', 'stats');
    this.statEls = {};
    [['fps', 'FPS'], ['bodies', 'bodies'], ['top', 'top tier'], ['ms', 'physics ms']].forEach(pair => {
      const s = el('div', 'stat');
      const v = el('div', 'v', '–');
      s.appendChild(v);
      s.appendChild(el('div', 'k', pair[1]));
      stats.appendChild(s);
      this.statEls[pair[0]] = { box: s, v };
    });
    c.appendChild(stats);

    // presets
    const pres = el('div', 'presets');
    this.presetBtns = {};
    Object.keys(Cfg.__presets).forEach(name => {
      const b = el('button', 'pill', name);
      b.addEventListener('click', () => {
        Cfg.applyPreset(name);
        this.syncAll();
        this.toast('Preset: ' + name);
      });
      pres.appendChild(b);
      this.presetBtns[name] = b;
    });
    const rb = el('button', 'pill', '↺ Reset');
    rb.addEventListener('click', () => { Cfg.reset(); this.syncAll(); this.toast('Back to defaults'); });
    pres.appendChild(rb);
    c.appendChild(pres);

    // scrollable body
    const scroll = el('div', 'scroll');
    c.appendChild(scroll);

    Cfg.__schema.forEach((group, gi) => {
      const d = el('details', 'group');
      d.open = gi < 4;
      const sum = el('summary');
      sum.appendChild(el('span', null, group.icon || '•'));
      sum.appendChild(el('span', null, group.section));
      d.appendChild(sum);
      const body = el('div', 'body');
      group.items.forEach(item => body.appendChild(this.makeRow(item)));
      d.appendChild(body);
      scroll.appendChild(d);
    });

    // actions
    const act = el('details', 'group');
    act.open = true;
    const asum = el('summary');
    asum.appendChild(el('span', null, '🎬'));
    asum.appendChild(el('span', null, 'Actions'));
    act.appendChild(asum);
    const abody = el('div', 'body');
    const wrap = el('div', 'actions');

    const mk = (label, fn) => { const b = el('button', null, label); b.addEventListener('click', fn); wrap.appendChild(b); return b; };
    mk('Restart run', () => { this.game.reset(); this.shell.refreshShelf(); this.toast('Run restarted'); });
    mk('Empty bag', () => { this.game.clearBag(); this.toast('Bag emptied'); });
    mk('Open window', () => {
      if (this.game.state.mode === 'closed') this.game.openWindow();
      else this.toast('Already open');
    });
    mk('Unlock all', () => { this.game.discoverAll(); this.shell.refreshShelf(); this.toast('Shelf unlocked'); });
    mk('Fill bag (×8)', () => {
      for (let i = 0; i < 8; i++) this.game.spawnTier(this.game.pick());
      this.toast('8 pastries dropped');
    });
    abody.appendChild(wrap);

    const spawn = el('div', 'actions');
    spawn.style.marginTop = '8px';
    const sel = el('select');
    global.PASTRY_NAMES.forEach((n, i) => {
      const o = el('option', null, i + ' · ' + n);
      o.value = String(i);
      sel.appendChild(o);
    });
    sel.value = '4';
    spawn.appendChild(sel);
    const sb = el('button', null, 'Drop this');
    sb.addEventListener('click', () => { this.game.spawnTier(Number(sel.value)); });
    spawn.appendChild(sb);
    const pair = el('button', null, 'Drop pair');
    pair.addEventListener('click', () => {
      const t = Number(sel.value);
      this.game.spawnTier(t, 80);
      this.game.spawnTier(t, 180);
    });
    spawn.appendChild(pair);
    abody.appendChild(spawn);
    act.appendChild(abody);
    scroll.appendChild(act);

    // json io
    const io = el('details', 'group');
    const isum = el('summary');
    isum.appendChild(el('span', null, '📋'));
    isum.appendChild(el('span', null, 'Config JSON'));
    io.appendChild(isum);
    const ibody = el('div', 'io');
    const ta = el('textarea');
    ta.spellcheck = false;
    this.ta = ta;
    ibody.appendChild(ta);
    const ibtns = el('div', 'actions');
    const copy = el('button', null, 'Copy');
    copy.addEventListener('click', () => {
      ta.select();
      try { document.execCommand('copy'); } catch (e) {}
      if (navigator.clipboard) navigator.clipboard.writeText(ta.value).catch(() => {});
      this.toast('Copied');
    });
    const apply = el('button', null, 'Apply');
    apply.addEventListener('click', () => {
      try {
        Cfg.apply(JSON.parse(ta.value || '{}'));
        this.syncAll();
        this.toast('Config applied');
      } catch (e) { this.toast('Invalid JSON'); }
    });
    ibtns.appendChild(copy); ibtns.appendChild(apply);
    ibody.appendChild(ibtns);
    io.appendChild(ibody);
    scroll.appendChild(io);
  };

  p.makeRow = function (item) {
    const row = el('div', item.type === 'bool' ? 'check-wrap' : 'row');

    if (item.type === 'bool') {
      const lab = el('label', 'check');
      const inp = document.createElement('input');
      inp.type = 'checkbox';
      lab.appendChild(inp);
      lab.appendChild(el('span', null, item.label));
      inp.addEventListener('change', () => {
        Cfg[item.key] = inp.checked;
        Cfg.save();
        this.afterChange(item);
      });
      row.appendChild(lab);
      if (item.help) row.appendChild(el('div', 'help', item.help));
      row.style.marginBottom = '9px';
      this.rows[item.key] = { item, input: inp };
      return row;
    }

    const top = el('div', 'top');
    top.appendChild(el('label', null, item.label));
    const val = el('div', 'val');
    top.appendChild(val);
    row.appendChild(top);

    const inp = document.createElement('input');
    inp.type = 'range';
    inp.min = item.min; inp.max = item.max; inp.step = item.step;
    row.appendChild(inp);
    if (item.help) row.appendChild(el('div', 'help', item.help));

    inp.addEventListener('input', () => {
      Cfg[item.key] = item.int ? Math.round(Number(inp.value)) : Number(inp.value);
      val.textContent = fmt(item, Cfg[item.key]);
      val.classList.toggle('dirty', Cfg[item.key] !== item.def);
      this.afterChange(item);
    });
    inp.addEventListener('change', () => Cfg.save());

    // double-click the readout to go back to the mock's original value
    val.title = 'double-click to restore (' + fmt(item, item.def) + ')';
    val.addEventListener('dblclick', () => {
      Cfg[item.key] = item.def;
      Cfg.save();
      this.sync(item.key);
      this.afterChange(item);
    });

    this.rows[item.key] = { item, input: inp, val };
    return row;
  };

  p.afterChange = function (item) {
    if (item.key === 'customerEvery' && this.game.state.mode === 'closed') {
      this.game.state.dropsLeft = Math.min(this.game.state.dropsLeft, this.game.every());
      this.game.notify();
    }
    if (item.key === 'volume' || item.key === 'soundOn') global.Sfx.ensure();
    this.markPreset();
    this.refreshJson();
  };

  p.sync = function (key) {
    const r = this.rows[key];
    if (!r) return;
    const v = Cfg[key];
    if (r.item.type === 'bool') { r.input.checked = !!v; return; }
    r.input.value = v;
    r.val.textContent = fmt(r.item, v);
    r.val.classList.toggle('dirty', v !== r.item.def);
  };

  p.syncAll = function () {
    Object.keys(this.rows).forEach(k => this.sync(k));
    this.markPreset();
    this.refreshJson();
  };

  p.markPreset = function () {
    const cur = JSON.stringify(sortedKeys(Cfg.diff()));
    Object.keys(this.presetBtns).forEach(name => {
      const want = JSON.stringify(sortedKeys(Cfg.__presets[name]));
      this.presetBtns[name].classList.toggle('active', want === cur);
    });
  };

  function sortedKeys(o) {
    const out = {};
    Object.keys(o).sort().forEach(k => { out[k] = o[k]; });
    return out;
  }

  p.refreshJson = function () {
    if (document.activeElement === this.ta) return;
    this.ta.value = JSON.stringify(Cfg.diff(), null, 2);
  };

  p.updateStats = function () {
    const g = this.game;
    this.statEls.fps.v.textContent = Math.round(g.fps);
    this.statEls.fps.box.classList.toggle('warn', g.fps < 45);
    this.statEls.bodies.v.textContent = g.bodies.length;
    const h = g.highestTier();
    this.statEls.top.v.textContent = h < 0 ? '–' : h;
    this.statEls.ms.v.textContent = g.stepMs.toFixed(1);
    this.statEls.ms.box.classList.toggle('warn', g.stepMs > 8);
  };

  p.toast = function (msg) {
    const n = document.getElementById('notice');
    if (!n) return;
    n.textContent = msg;
    n.classList.add('on');
    clearTimeout(this._toastT);
    this._toastT = setTimeout(() => n.classList.remove('on'), 1300);
  };

  global.DebugPanel = DebugPanel;
})(window);
