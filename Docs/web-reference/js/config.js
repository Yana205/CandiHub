/* config.js — every knob the game reads, plus the schema the debug side menu
   builds itself from. Values live in one flat object so tweaking is just
   `Config.gravity = 900` and the running game picks it up next frame. */
(function (global) {
  'use strict';

  const PASTRY_NAMES = ['Cookie', 'Muffin', 'Sugar Kiss', 'Biscuit', 'Empanada',
    'Bread Roll', 'Cinnamon Roll', 'Shell Bun', 'Piggy Bread', 'Flan', 'King Cake'];

  const BASE_RADII = [13, 17, 22, 27, 33, 40, 48, 57, 67, 78, 90];

  /* Play field, in bag-canvas units (the canvas is 258x460 CSS px). */
  const FIELD = { L: 12, R: 246, FLOOR: 442, DROP_Y: 36, TOP: 56 };

  const SCHEMA = [
    {
      section: 'Physics', icon: '🍩', items: [
        { key: 'gravity', label: 'Gravity', min: 200, max: 4000, step: 25, def: 1500, unit: 'px/s²',
          help: 'How fast the pastries fall.' },
        { key: 'bounciness', label: 'Bounce', min: 0, max: 0.6, step: 0.01, def: 0.08,
          help: 'Restitution in collisions and against the walls.' },
        { key: 'sizeScale', label: 'Size', min: 0.6, max: 1.8, step: 0.05, def: 1.3, unit: '×',
          help: 'Global scale applied to every radius.' },
        { key: 'rotationAmount', label: 'Spin', min: 0, max: 1, step: 0.05, def: 0.2,
          help: 'How much they turn when they hit and roll.' },
        { key: 'groundFriction', label: 'Floor friction', min: 0, max: 25, step: 0.5, def: 9,
          help: 'Slows sliding along the bottom of the bag.' },
        { key: 'substeps', label: 'Substeps', min: 1, max: 8, step: 1, def: 3, int: true,
          help: 'Physics iterations per frame. More = steadier, costlier.' },
        { key: 'squishAmount', label: 'Squash', min: 0, max: 2, step: 0.05, def: 1, unit: '×',
          help: 'How much they flatten on impact.' },
      ]
    },
    {
      section: 'Merge', icon: '✨', items: [
        { key: 'mergeGrowTime', label: 'Grow time', min: 0.05, max: 2.5, step: 0.05, def: 0.85, unit: 's',
          help: 'Length of the pop the merged pastry appears with.' },
        { key: 'comboDelay', label: 'Post-merge grace', min: 0, max: 2, step: 0.05, def: 0.5, unit: 's',
          help: 'Minimum lifetime before it can merge again.' },
        { key: 'comboWindow', label: 'Combo window', min: 0.2, max: 4, step: 0.1, def: 1.4, unit: 's',
          help: 'Gap between merges that still chains a combo.' },
        { key: 'mergePopVy', label: 'Merge kick', min: -400, max: 100, step: 5, def: -70, unit: 'px/s',
          help: 'Upward hop of the newborn pastry.' },
        { key: 'particleScale', label: 'Particles', min: 0, max: 3, step: 0.1, def: 1, unit: '×',
          help: 'How many sparks the burst throws.' },
      ]
    },
    {
      section: 'Drop', icon: '👆', items: [
        { key: 'dropCooldown', label: 'Cooldown', min: 0, max: 2, step: 0.05, def: 0.5, unit: 's',
          help: 'Wait between one pastry and the next.' },
        { key: 'dropVy', label: 'Launch speed', min: 0, max: 600, step: 10, def: 60, unit: 'px/s' },
        { key: 'maxTierSpawn', label: 'Max spawn tier', min: 0, max: 10, step: 1, def: 3, int: true,
          help: 'Highest tier that can land in your hand.' },
        { key: 'aimGuide', label: 'Aim line', def: true, type: 'bool' },
      ]
    },
    {
      section: 'Customers', icon: '🧡', items: [
        { key: 'customerEvery', label: 'Drops per customer', min: 1, max: 20, step: 1, def: 5, int: true,
          help: 'How many you drop before the window opens.' },
        { key: 'orderMinTier', label: 'Min order tier', min: 0, max: 10, step: 1, def: 2, int: true },
        { key: 'orderMaxTier', label: 'Max order tier', min: 0, max: 10, step: 1, def: 5, int: true },
        { key: 'flySec', label: 'Flight time', min: 0.2, max: 2.5, step: 0.05, def: 0.9, unit: 's' },
        { key: 'happyMs', label: 'Thanks! hold', min: 200, max: 4000, step: 100, def: 1400, unit: 'ms' },
      ]
    },
    {
      section: 'Challenge', icon: '⚠️', items: [
        { key: 'topOut', label: 'Can lose', def: true, type: 'bool',
          help: 'If the bag overflows out the top, the run ends.' },
        { key: 'topOutLine', label: 'Danger line', min: 60, max: 220, step: 2, def: 92, unit: 'px',
          help: 'Height inside the bag that must not be crossed.' },
        { key: 'topOutGrace', label: 'Overflow grace', min: 0.5, max: 8, step: 0.1, def: 2.2, unit: 's' },
        { key: 'startingBodies', label: 'Starting pastries', min: 0, max: 24, step: 1, def: 9, int: true,
          help: 'Applies on restart.' },
      ]
    },
    {
      section: 'Audio', icon: '🔊', items: [
        { key: 'soundOn', label: 'Sound', def: true, type: 'bool' },
        { key: 'volume', label: 'Volume', min: 0, max: 1, step: 0.05, def: 0.8, unit: '×' },
      ]
    },
    {
      section: 'Debug', icon: '🔧', items: [
        { key: 'timeScale', label: 'Slow motion', min: 0.05, max: 2, step: 0.05, def: 1, unit: '×' },
        { key: 'paused', label: 'Pause', def: false, type: 'bool' },
        { key: 'showHitbox', label: 'Show colliders', def: false, type: 'bool' },
        { key: 'showIds', label: 'Show tier / id', def: false, type: 'bool' },
        { key: 'showDangerLine', label: 'Show danger line', def: false, type: 'bool' },
      ]
    },
  ];

  const PRESETS = {
    'Original': {},
    'Floaty': { gravity: 650, bounciness: 0.24, mergeGrowTime: 1.3, rotationAmount: 0.45, dropVy: 20, groundFriction: 4 },
    'Snappy': { gravity: 2600, bounciness: 0.02, mergeGrowTime: 0.28, dropCooldown: 0.18, dropVy: 220, groundFriction: 16, comboDelay: 0.2 },
    'Chaos': { gravity: 2200, bounciness: 0.45, rotationAmount: 1, sizeScale: 1.5, particleScale: 2.4, squishAmount: 1.8, groundFriction: 2 },
    'Zen': { gravity: 900, bounciness: 0.05, customerEvery: 8, topOut: false, dropCooldown: 0.3, mergeGrowTime: 1.0 },
  };

  const defaults = {};
  for (const g of SCHEMA) for (const it of g.items) defaults[it.key] = it.def;

  const STORE_KEY = 'pandulce.config.v1';
  const Config = Object.assign({}, defaults);

  Config.__defaults = defaults;
  Config.__schema = SCHEMA;
  Config.__presets = PRESETS;

  function itemFor(key) {
    for (const g of SCHEMA) for (const it of g.items) if (it.key === key) return it;
    return null;
  }

  function sanitize(key, v) {
    const it = itemFor(key);
    if (!it) return null;
    if (it.type === 'bool') return !!v;
    let n = Number(v);
    if (!isFinite(n)) return null;
    if (it.int) n = Math.round(n);
    return Math.min(it.max, Math.max(it.min, n));
  }

  Config.load = function () {
    let raw;
    try { raw = JSON.parse(localStorage.getItem(STORE_KEY) || 'null'); } catch (e) { raw = null; }
    if (!raw || typeof raw !== 'object') return;
    for (const k of Object.keys(raw)) {
      const v = sanitize(k, raw[k]);
      if (v !== null) Config[k] = v;
    }
    // Never restore a paused session — that reads as a broken game.
    Config.paused = false;
  };

  Config.save = function () {
    const out = {};
    for (const k of Object.keys(defaults)) if (Config[k] !== defaults[k]) out[k] = Config[k];
    try { localStorage.setItem(STORE_KEY, JSON.stringify(out)); } catch (e) {}
  };

  Config.reset = function () {
    Object.assign(Config, defaults);
    Config.save();
  };

  Config.apply = function (patch) {
    for (const k of Object.keys(patch || {})) {
      const v = sanitize(k, patch[k]);
      if (v !== null) Config[k] = v;
    }
    Config.save();
  };

  Config.applyPreset = function (name) {
    Object.assign(Config, defaults, PRESETS[name] || {});
    Config.save();
  };

  Config.diff = function () {
    const out = {};
    for (const k of Object.keys(defaults)) if (Config[k] !== defaults[k]) out[k] = Config[k];
    return out;
  };

  Config.itemFor = itemFor;

  global.PASTRY_NAMES = PASTRY_NAMES;
  global.BASE_RADII = BASE_RADII;
  global.FIELD = FIELD;
  global.Config = Config;
})(window);
