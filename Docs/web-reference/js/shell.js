/* shell.js — builds the bakery DOM around the bag canvas and keeps it in sync
   with the game state. Everything outside the canvas (window, shutters, order
   bubble, shelf, flying pastry) lives here. */
(function (global) {
  'use strict';

  const MARKUP = `
  <div class="stage" id="stage">
    <div class="bakery">
      <div class="brick b1"></div><div class="brick b2"></div>
      <div class="brick b3"></div><div class="brick b4"></div>

      <div class="window-frame">
        <div class="window-inner">
          <div class="cust-wrap" id="custWrap">
            <canvas class="cust-canvas" id="custCanvas" width="420" height="340"></canvas>
          </div>
          <div class="shutter l"></div>
          <div class="shutter r"></div>
          <div class="countdown">
            <div class="cd-label">Opens in</div>
            <div class="cd-num" id="dropsLeft">5</div>
          </div>
        </div>
      </div>
      <div class="sill"></div>

      <div class="hud">
        <div class="chip">Customers: <span id="served">0</span></div>
        <div class="chip small">Score: <span id="score">0</span> · Best: <span id="best">0</span></div>
      </div>
    </div>

    <div class="bubble hidden" id="bubble">
      <div class="box">
        <canvas id="orderIcon" width="88" height="88"></canvas>
        <div>
          <div class="order-name">One <span id="orderName"></span>, please!</div>
          <div class="order-hint">tap it in the bag to hand it over</div>
        </div>
      </div>
      <div class="tail"></div>
    </div>

    <div class="thanks hidden" id="thanks">Thanks!</div>

    <div class="counter"></div>

    <div class="bag" id="bag">
      <canvas id="mainCanvas" width="516" height="920"></canvas>
    </div>

    <div class="rail">
      <div class="sign">
        <div class="rope l"></div><div class="rope r"></div>
        <div class="board">
          <span>Next</span>
          <canvas id="nextCanvas" width="88" height="88"></canvas>
        </div>
      </div>
      <div class="shelf">
        <div class="post l"></div><div class="post r"></div>
        <div class="grid" id="shelfGrid"></div>
        <div class="foot l"></div><div class="foot r"></div>
      </div>
    </div>

    <div class="hint">move to aim · tap to drop · match 2 alike</div>

    <div class="flyer hidden" id="flyer"><canvas width="112" height="112"></canvas></div>

    <div class="over hidden" id="over">
      <div class="card">
        <h2>The bakery closed</h2>
        <div class="big" id="overScore">0</div>
        <p>points</p>
        <p>Customers served: <span id="overServed">0</span></p>
        <p>Best: <span id="overBest">0</span></p>
        <button class="btn" id="restartBtn">Play again</button>
      </div>
    </div>
  </div>`;

  function Shell(rootEl, opts) {
    opts = opts || {};
    rootEl.innerHTML = MARKUP;
    const $ = id => rootEl.querySelector('#' + id);

    this.root = rootEl;
    this.fitEl = opts.fitEl || rootEl;
    this.el = {
      stage: $('stage'), dropsLeft: $('dropsLeft'), served: $('served'),
      score: $('score'), best: $('best'),
      custWrap: $('custWrap'), custCanvas: $('custCanvas'),
      bubble: $('bubble'), orderIcon: $('orderIcon'), orderName: $('orderName'),
      thanks: $('thanks'), bag: $('bag'), main: $('mainCanvas'),
      next: $('nextCanvas'), shelf: $('shelfGrid'),
      flyer: $('flyer'), flyerCanvas: $('flyer').querySelector('canvas'),
      over: $('over'), overScore: $('overScore'), overServed: $('overServed'),
      overBest: $('overBest'), restart: $('restartBtn'),
    };

    this.prev = { next: -1, order: null, customer: -1, bubble: false, thanks: false, over: false };
    this.tierCanvases = [];

    this.buildShelf();

    const self = this;
    this.game = new global.PanDulceGame(this.el.main, {
      onChange: () => self.render(),
      onDiscover: () => self.refreshShelf(),
      onFly: (tier, x, y) => self.fly(tier, x, y),
    });

    this.bindInput();
    this.fit();
    this._onRes = () => this.fit();
    window.addEventListener('resize', this._onRes);
    this.refreshShelf();
    this.render();
  }

  const p = Shell.prototype;

  p.buildShelf = function () {
    const frag = document.createDocumentFragment();
    global.PASTRY_NAMES.forEach((name, i) => {
      const cell = document.createElement('div');
      cell.className = 'cell';
      const cv = document.createElement('canvas');
      cv.width = 104; cv.height = 80;
      const lab = document.createElement('div');
      lab.className = 'label';
      cell.appendChild(cv); cell.appendChild(lab);
      frag.appendChild(cell);
      this.tierCanvases.push({ cv, lab });
    });
    const star = document.createElement('div');
    star.className = 'cell star';
    star.innerHTML = '<div>★</div>';
    frag.appendChild(star);
    this.el.shelf.appendChild(frag);
  };

  p.refreshShelf = function () {
    this.game.tierList.forEach((it, i) => {
      const slot = this.tierCanvases[i];
      global.Art.renderIcon(slot.cv, i, it.discovered);
      slot.lab.textContent = it.label;
    });
  };

  p.fit = function () {
    const w = this.fitEl.clientWidth || window.innerWidth;
    const h = this.fitEl.clientHeight || window.innerHeight;
    const s = Math.min(1, h / 900, w / 446);
    this.el.stage.style.transform = 'scale(' + s + ')';
  };

  p.bindInput = function () {
    const g = this.game, bag = this.el.bag;
    bag.addEventListener('pointerdown', e => {
      // Capture keeps the aim following the finger past the bag edge, but it
      // throws for pointer ids the browser doesn't consider active — never let
      // that swallow the input.
      try { bag.setPointerCapture(e.pointerId); } catch (err) {}
      g.pointerDown(e);
    });
    bag.addEventListener('pointermove', e => g.pointerMove(e));
    bag.addEventListener('pointerup', e => {
      try { bag.releasePointerCapture(e.pointerId); } catch (err) {}
      g.pointerUp(e);
    });

    this.el.restart.addEventListener('click', () => { global.Sfx.ensure(); g.reset(); });

    this._onKey = e => {
      if (e.target && /INPUT|TEXTAREA|SELECT/.test(e.target.tagName)) return;
      if (e.key === 'ArrowLeft') { g.aimBy(-9); e.preventDefault(); }
      else if (e.key === 'ArrowRight') { g.aimBy(9); e.preventDefault(); }
      else if (e.key === ' ' || e.key === 'ArrowDown') { global.Sfx.ensure(); g.drop(); e.preventDefault(); }
      else if (e.key === 'r' || e.key === 'R') { g.reset(); }
      else if (e.key === 'p' || e.key === 'P') {
        global.Config.paused = !global.Config.paused;
        if (this.onConfigExternalChange) this.onConfigExternalChange();
      }
    };
    window.addEventListener('keydown', this._onKey);
  };

  p.show = function (el, visible, restartAnim) {
    if (visible) {
      if (restartAnim && el.classList.contains('hidden')) {
        el.classList.remove('hidden');
        const a = el.style.animation;
        el.style.animation = 'none';
        void el.offsetWidth;
        el.style.animation = a || '';
      } else {
        el.classList.remove('hidden');
      }
    } else {
      el.classList.add('hidden');
    }
  };

  p.render = function () {
    // The game notifies once during its own constructor, before `this.game` is set.
    if (!this.game) return;
    const g = this.game, s = g.state, e = this.el;

    e.dropsLeft.textContent = s.dropsLeft;
    e.served.textContent = s.served;
    e.score.textContent = s.score;
    e.best.textContent = Math.max(s.best, s.score);

    e.stage.classList.toggle('open', s.mode === 'open');

    if (g.nextTier !== this.prev.next) {
      this.prev.next = g.nextTier;
      global.Art.drawSmall(e.next, g.nextTier, 17);
    }

    const custVisible = s.mode === 'open';
    e.custCanvas.style.visibility = custVisible ? 'visible' : 'hidden';
    if (custVisible && s.customer !== this.prev.customer) {
      this.prev.customer = s.customer;
      global.Art.drawCustomer(e.custCanvas, s.customer);
    }
    e.custWrap.classList.toggle('happy', s.happy);

    const bubbleOn = s.mode === 'open' && !s.happy && s.orderTier != null;
    if (bubbleOn && s.orderTier !== this.prev.order) {
      this.prev.order = s.orderTier;
      global.Art.drawSmall(e.orderIcon, s.orderTier, 16);
      e.orderName.textContent = global.PASTRY_NAMES[s.orderTier];
    }
    if (!bubbleOn) this.prev.order = null;
    if (bubbleOn !== this.prev.bubble) {
      this.prev.bubble = bubbleOn;
      this.show(e.bubble, bubbleOn, true);
    }

    if (s.happy !== this.prev.thanks) {
      this.prev.thanks = s.happy;
      this.show(e.thanks, s.happy, true);
    }

    if (s.gameOver !== this.prev.over) {
      this.prev.over = s.gameOver;
      this.show(e.over, s.gameOver, true);
      if (s.gameOver) {
        e.overScore.textContent = s.score;
        e.overServed.textContent = s.served;
        e.overBest.textContent = s.best;
      }
    }
  };

  /* Pastry leaving the bag and landing in the window. */
  p.fly = function (tier, x, y) {
    const f = this.el.flyer;
    const dur = global.Config.flySec;
    global.Art.drawSmall(this.el.flyerCanvas, tier, 21);

    f.style.transition = 'none';
    f.style.left = x + 'px';
    f.style.top = y + 'px';
    f.style.transform = 'scale(1)';
    f.classList.remove('hidden');
    void f.offsetWidth;

    f.style.transition = 'left ' + dur + 's cubic-bezier(.35,-.15,.35,1), top ' +
      dur + 's cubic-bezier(.35,-.15,.35,1), transform ' + dur + 's ease';

    const self = this;
    const done = ev => {
      if (ev.propertyName !== 'top') return;
      f.removeEventListener('transitionend', done);
      clearTimeout(self._flyGuard);
      f.classList.add('hidden');
      self.game.completeServe();
    };
    f.addEventListener('transitionend', done);
    // Safety net in case the transition never fires (tab backgrounded, etc.)
    clearTimeout(this._flyGuard);
    this._flyGuard = setTimeout(() => {
      f.removeEventListener('transitionend', done);
      f.classList.add('hidden');
      self.game.completeServe();
    }, dur * 1000 + 400);

    requestAnimationFrame(() => requestAnimationFrame(() => {
      f.style.left = '187px';
      f.style.top = '240px';
      f.style.transform = 'scale(0.55)';
    }));
  };

  global.Shell = Shell;
})(window);
