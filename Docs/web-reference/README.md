# Pan Dulce Merge

Playable local build of the **Pan Dulce Merge Mock** Claude Design project. Drop
pastries into the paper bag, merge two of a kind to climb the eleven tiers, and
serve whatever the customer at the window asks for.

## Run it

No build step, no dependencies. Either:

```
open index.html          # file:// works, everything is plain <script> tags
```

or, if you want localStorage to behave like a real deploy:

```
python3 -m http.server 8777
# → http://localhost:8777/index.html
```

## Pages

| Page | What it is |
| --- | --- |
| `index.html` | The game. |
| `debug.html` | The same game with a side menu for tuning game feel live. |

## Playing

- **Move** over the bag to aim, **tap/click** to drop. Arrow keys aim, space drops.
- Two identical pastries that touch merge into the next tier. Chain merges within
  the combo window for a multiplier.
- Every `customerEvery` drops the shutters open. Tap the highlighted pastry in the
  bag to hand it over — the ring pulses around anything matching the order.
- Merging into a tier for the first time unlocks it on the shelf (`New!`, +250).
- If pastries sit above the danger line for longer than the grace period, the
  bakery closes. `R` restarts, `P` pauses.

Tiers: Cookie → Muffin → Sugar Kiss → Biscuit → Empanada → Bread Roll →
Cinnamon Roll → Shell Bun → Piggy Bread → Flan → King Cake.

## Debug page

The side menu is generated from `Config.__schema` in `js/config.js`, so adding a
knob is a single entry in that array — the slider, label, help text, persistence,
JSON export and reset all follow automatically.

- **Live stats** — FPS, body count, highest tier reached, physics ms per frame.
- **Presets** — Original (the mock's values), Floaty, Snappy, Chaos, Zen.
- **Sections** — Physics, Merge, Drop, Customers, Challenge, Audio, Debug.
- **Actions** — restart, empty the bag, force the window open, unlock the whole
  shelf, spawn a specific tier or a matched pair.
- **Config JSON** — copy the current diff-from-default, or paste one in and apply.

Every change applies on the next frame; nothing restarts unless you press
*Restart run*. `startingBodies` is the one exception — it only takes effect
on restart. Double-click any value readout to restore that single knob to the
mock's original value. Tweaks persist in `localStorage` under
`pandulce.config.v1`; the high score lives in `pandulce.best.v1`.

## Layout

```
index.html      game page
debug.html      game + tuning panel
css/game.css    the 430x880 bakery stage
css/debug.css   side menu
js/config.js    tunables + schema the panel builds itself from
js/art.js       canvas drawing for pastries, customers, shelf icons
js/audio.js     WebAudio blips (no asset files)
js/game.js      physics, merging, shop loop, bag rendering
js/shell.js     bakery DOM around the canvas, kept in sync with game state
js/debug.js     the side menu
```

The bakery front, shutters, order bubble, sign, shelf and the flying pastry are
DOM/CSS; only the inside of the paper bag is canvas. Game coordinates inside the
bag are the canvas's 258×460 CSS space (backing store is 2×).

## Differences from the mock

The mock is an endless sandbox with no fail state. To make it a game that can be
finished and replayed this build adds:

- a score (merge value × combo, +250 per new tier, +100 and up per customer),
  a high score in `localStorage`, and a game-over card with restart;
- a top-out rule — pastries resting above the danger line for `topOutGrace`
  seconds close the bakery. Turn it off with **Challenge → Can lose** for the
  mock's original endless behaviour;
- keyboard controls.

Everything else — physics constants, easing, merge rules, the customer cadence,
the artwork and the sound design — is ported straight from the mock, and the
**Original** preset reproduces its exact tuning.
