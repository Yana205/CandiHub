# Pan Dulce Merge — mobile skeleton

The playable structure with placeholder art, built against `HANDOFF-UNITY.md`. Real art
drops into `Assets/PanDulce/Art/` and `Config/Pastries.asset` without touching game code.

## Deltas from the handoff

These are decisions taken deliberately, not drift. §13 left them open; they were answered
before implementation.

| Area | Handoff | This build |
| --- | --- | --- |
| Game loop | Pure sandbox — no score, no fail state | **Top-out + score**, ported verbatim from `Docs/web-reference/js/game.js` |
| Danger line | Does not exist | **y = 82**, always visible at α 0.28, blinks when threatened |
| Top bar centre | `Sweet Bakery` title | **Score**, with `best NNNN` beneath it |
| Persistence | Open question | **None at runtime.** Best score and case discovery reset on launch, matching both existing builds. Only `Tuning.asset` persists |
| Run end | n/a | A top-out **closes the bakery**: `closeT` is forced to 1 so the existing fold and knot play, then a `Sold out!` card reports score/best |

`topOutLine` was re-anchored from the reference's `92`: that sat 14% down a `DROP_Y 36 →
FLOOR 442` column, and this frame's column is `36 → 372`.

## Layout

```
Assets/PanDulce/
  Core/      pure C# — no MonoBehaviour, GameObject or Transform. 11 files
  Runtime/   MonoBehaviours that tick Core and mirror it. 16 files
  Editor/    Tweaks window, stage builder, sprite import, knob schema
  Tests/     28 EditMode tests over Core (no scene required)
  Art/       baked placeholder sprites (LFS)
  Config/    Tuning.asset, Pastries.asset (text, committed)
```

## Two commands you will actually use

- **`Pan Dulce ▸ Rebuild Stage`** (`Cmd+Shift+B`) — regenerates `Main.unity` from
  `StageBuilder.cs`. The scene is authored by script, so layout changes are a code diff and
  the scene stays merge-safe.
- **`Window ▸ Pan Dulce ▸ Tweaks`** — 34 knobs across 8 sections, 5 presets, live stats,
  test buttons, JSON round-trip. Edits apply on the next frame with no restart.

## Placeholder art

`node Docs/tools/bake-sprites.js` (after `npm i` in `Docs/tools/`) regenerates the 11
pastries and 3 customers. It **extracts `drawPastry`/`drawCustomer` from the mock HTML by
brace-matching and runs them against a native canvas**, so the sprites cannot drift from the
design authority. Every pastry is drawn in multiples of its radius, so one sprite per tier at
a canonical R = 200 is identical to the mock at every size (§8.1).

Shell art (wall, window, case, bars) is generated in C# by `Shapes.cs` — §8.2–8.4 are almost
entirely rounded rects, gradients and stripes, which are trivial to rasterise and produce no
throwaway assets.

## Architectural notes worth knowing

- **Views are self-building.** Each `GeneratedView` creates its children under a `Content`
  node flagged `DontSave`, rebuilt on every `OnEnable`. Private field references are not
  serialized, so a view built only at edit time would wake up in play mode with every label
  null. This also keeps `Main.unity` thin — pools contribute three empty parents, not ~176
  objects.
- **`StageBuilder` strips generated content before saving** and rebuilds after, because Unity
  warns when `DontSave` objects are parented under saved ones at save time.
- **Mesh vertex colours need explicit `.linear`.** `SpriteRenderer.color` is a gamma-space
  property Unity converts on assignment; mesh vertex colours are uploaded raw. The cloth is a
  mesh, so it needs the conversion its own hem does not.
- **Everything routes through `SpriteMaterials.Unlit`**, including runtime-instantiated
  objects — the scene has no `Light2D`, so a lit material renders black (§2, trap 2).

## Verified

- 28/28 EditMode tests green, including the `Er()` curve, the 4:3:2:1 spawn distribution over
  10k samples, the merge gate, the floor curve, and the challenge layer's three settle filters.
- Play mode: 21 bodies merged to 6 across 15 merges reaching tier 4; resting bodies sat at
  `y = 355` (floor 372 − radius) with a mean offset of 74px from centre against a 183px
  half-width — i.e. **the pile forms a bowl, not a stack**.
- Zero console errors on load, rebuild and play.

## Not done — needs hardware or a decision

- Real art (M5) — this is the skeleton's whole premise.
- On-device verification (M6): safe-area insets, touch tolerance, ASTC, 60fps thermals,
  draw-call count. All are implemented but only testable on a phone.
- Sprite Atlas — the code is atlas-ready (one shared unlit material) but no atlas asset exists.
- The in-game Tweaks modal (§10.5), an explicit stretch goal.
- Fonts: TMP defaults are in use. Baloo 2 / Quicksand are not yet imported.
