/* audio.js — tiny WebAudio blip synth. No files, no loading. */
(function (global) {
  'use strict';

  const Sfx = {
    ac: null,
    master: null,

    ensure() {
      if (this.ac) {
        if (this.ac.state === 'suspended') this.ac.resume();
        return;
      }
      try {
        this.ac = new (window.AudioContext || window.webkitAudioContext)();
        this.master = this.ac.createGain();
        this.master.gain.value = 1;
        this.master.connect(this.ac.destination);
      } catch (e) { this.ac = null; }
    },

    tone(f, t0, dur, type, vol) {
      const ac = this.ac;
      if (!ac) return;
      const o = ac.createOscillator(), g = ac.createGain();
      o.type = type;
      o.frequency.setValueAtTime(f, ac.currentTime + t0);
      g.gain.setValueAtTime(0, ac.currentTime + t0);
      g.gain.linearRampToValueAtTime(vol, ac.currentTime + t0 + 0.015);
      g.gain.exponentialRampToValueAtTime(0.001, ac.currentTime + t0 + dur);
      o.connect(g); g.connect(this.master);
      o.start(ac.currentTime + t0);
      o.stop(ac.currentTime + t0 + dur + 0.05);
    },

    play(kind, n) {
      const cfg = global.Config;
      if (!cfg.soundOn || !this.ac) return;
      this.master.gain.value = cfg.volume;
      const T = (f, t0, d, ty, v) => this.tone(f, t0, d, ty, v);
      if (kind === 'drop') T(180, 0, 0.1, 'sine', 0.12);
      else if (kind === 'merge') { const f = 260 + (n || 0) * 55; T(f, 0, 0.12, 'triangle', 0.16); T(f * 1.5, 0.06, 0.14, 'triangle', 0.12); }
      else if (kind === 'disco') { T(660, 0, 0.12, 'sine', 0.12); T(880, 0.09, 0.16, 'sine', 0.12); T(1100, 0.18, 0.2, 'sine', 0.1); }
      else if (kind === 'open') { T(330, 0, 0.14, 'triangle', 0.1); T(440, 0.1, 0.18, 'triangle', 0.1); }
      else if (kind === 'serve') { T(523, 0, 0.12, 'sine', 0.13); T(659, 0.09, 0.12, 'sine', 0.13); T(784, 0.18, 0.22, 'sine', 0.13); }
      else if (kind === 'over') { T(392, 0, 0.18, 'triangle', 0.14); T(311, 0.16, 0.2, 'triangle', 0.14); T(233, 0.34, 0.5, 'triangle', 0.14); }
    },
  };

  global.Sfx = Sfx;
})(window);
