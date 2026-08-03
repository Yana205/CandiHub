# Mock parity, bear customer, Designer window, ParticleSystem effects — design

Date: 2026-08-03
Status: approved by Yana (brainstorming session)

## Goal

Bring the Unity build visually in line with the updated Claude-design mock (the two
reference images: gameplay frame and bear-customer frame), surface the already-implemented
customer mechanic with the new bear presentation, replace the 34-knob Tweaks window with a
simple curated Designer window, and replace the code-driven particles with Unity
ParticleSystem effects.

## Decisions made in this session

| Question | Decision |
| --- | --- |
| Customer design | **Single bear** rising from behind the counter through the red/white opening. Replaces the 3 side-entering regulars and their Walk/Hop/Pop/Slide entrances. |
| Top bar | **"Sweet Bakery" title centred**, per the mock. Score is not shown in the bar; it appears only on the run-end card. Score/top-out systems stay in code. |
| Editor tool | **Rebuild simpler**: a curated ~12-knob Designer window with plain-language help, replacing `TweaksWindow.cs`. |
| Particles | **Full Unity ParticleSystem** — retire the sim-owned particle list and `ParticleViewPool`. |
| Art pipeline | **Hybrid**: bake organic art (bear, window scenery, sign board) to PNGs via the existing Node canvas tool (`Docs/tools/bake-sprites.js`); keep tintable/dynamic pieces (cloth, hem, fold, bars, case chrome) procedural so cloth-colour swatches keep deriving every shade. |

## 1. Visual parity with the mock

Authority: the two updated mock images; where they don't contradict it, `Docs/HANDOFF-UNITY.md`
§8.2–§8.7, which describes the same design numerically. Work items:

- **Top bar (§8.2)** — gradient `#a97448 → #96633c`, Customers chip, centred *Sweet Bakery*
  (22 px 800 `#fff3dd`, `0 2px 0 #6f4a2c` shadow), Next chip with pastry icon.
  `TopBarView` drops the score/best labels.
- **Wall + window (§8.3)** — plank-seam wall; window scene (sky gradient, cloud pills, five
  striped buildings, lamppost, street with dashes, mullions) **baked to one PNG** by the Node
  tool. Frame border stays procedural.
- **Hanging sign (§8.4)** — new `SignView` visuals at stage (8, 56): two ropes, cream board,
  ±1.2° swing over 3.6 s. Lines: closed → `next customer in` / `{N}s`; open → `now serving` / `♥`.
- **Display case (§8.4)** — glass with 3 px white border and diagonal gradient, glare
  stripes, top knob, rail highlight, five slots (icon, ledge `#d8c6ac`, Quicksand-style
  label), sliding 5-window over discovered tiers, locked tint `#c3ae93` with `?` label.
- **Furoshiki (§8.5)** — cloth mesh gets the draped-V silhouette with the scalloped top
  curve, highlight seam, polka dots on the 42 px grid, side shadows, stitched hem band;
  desk gets plank lines and the contact-shadow ellipse. New **NextPlaqueView** at the
  cloth's top-right (64 × 50 cream rounded rect, "NEXT", next pastry at r 13) — outside
  `ClothShakeRoot` so it doesn't shake.
- **Boost bar (§8.7)** — real button: shaker icon, *Shake the furoshiki!* label, 160 × 7
  charge bar, `NN%` / `READY!` badge, 0.72→1.0 opacity states, ready pulse.
- **Lower wall / bleed** — §8.4 gradient; bleed art beyond the 430 × 880 frame per §5.1.

All colours go through `ColorUtility.TryParseHtmlString` (Linear colour space, handoff trap 6);
cloth shades go through the gamma-space `Palette.Mix`.

## 2. Bear customer

The Core mechanic (`ShopDirector`: timer → arrive → order → tap-to-serve → happy → close)
is already implemented and **does not change**. Presentation changes only:

- **Art**: new `drawBear()` in `Docs/tools/bake-sprites.js` — brown bear, round ears with
  inner tone, blush cheeks, small muzzle — baked to a PNG sprite. The red/white striped
  counter opening he pops through is baked in the same script so the two always align.
- **Placement**: bear anchored at the counter opening (mock frame 2: centred ~x 215,
  between window bottom and case top). Sorting: bear on the `Customer` layer — in front of
  the backdrop, behind the counter opening ring, case and bubble.
- **Entrance**: replaces the four side entrances with a single **rise**: he slides up from
  behind the counter with a small overshoot bounce, `entranceTime` long, strong ease-out.
  The `EntranceStyle` enum, its knob, and the four old entrance code paths are deleted —
  Rise is the only entrance.
- **Order bubble**: mock styling — cream box, tail toward the bear, 44 px pastry icon,
  `{Name}, please!` headline, `tap it in the cloth to hand it over` hint, pop-in with
  back-ease after 0.5 s.
- Serve flight target and happy double-bounce stay as coded.

## 3. Designer window (replaces TweaksWindow)

New `Assets/PanDulce/Editor/DesignerWindow.cs`, menu **Window ▸ Pan Dulce ▸ Designer**,
dockable IMGUI EditorWindow. `TweaksWindow.cs` is deleted. Kept properties: schema-driven
rows, edits apply next frame via `TuningConfig`, `Undo.RecordObject` + `SetDirty`,
`EditorApplication.update` subscribe/unsubscribe in OnEnable/OnDisable (domain reload off).

**Curated knobs (~12), each with a one-line plain-language help text:**
gravity · bounciness · pastry size (`sizeScale`) · merge grow time · customer every N s ·
entrance time · cloth colour (four swatches) · shake power · charge per merge ·
particle intensity (drives ParticleSystem emission/scale) · sound on/off · time scale
(editor only).

**Test buttons**, each with a one-line description of what it does:
Restart run · Drop a merge pair (two same-tier pastries that roll into each other) ·
Fill the cloth (×8 random) · Summon the bear now · Fill charge & fire shake ·
Toggle closing time · Screenshot the stage.

Dropped from the old window: presets, JSON round-trip, Tier-B "beyond the mock" foldout,
live stats block, per-knob reset. A single **Reset all to defaults** button remains in the
footer. (The old capabilities stay recoverable from git history if ever wanted.)

## 4. ParticleSystem effects

Retire `MergeSim`'s particle list and `ParticleViewPool`. Floating text pool stays.

New `EffectsView` (Runtime) owning four pre-created, pooled ParticleSystems — emitted via
`Emit()`/`Play()`, never instantiated per event — parented so merge/shake effects sit under
`ClothShakeRoot` (they shake with the cloth) and serve effects on the `Overlay` layer:

| Effect | Trigger (existing event) | Look |
| --- | --- | --- |
| Merge burst | `MergeSim.Merged` | cream `#fff3dd` sugar-dust puffs + golden `#f0b64f` star sparks, count/size scaled by new tier's radius, secondary tint from the pastry's fill colour |
| New-tier sparkle | `MergeSim.TierDiscovered` | larger celebratory sparkle ring |
| Serve poof + trail | serve launch / flyer | puff at pickup, faint sparkle trail following the flyer |
| Shake dust | `MergeSim.Shaken` | dust kicked along the cloth floor for `shakeDuration` |

Constraints: `Sprites-Unlit-Default` material on every renderer (no `Light2D` in scene —
lit particles render black); sorting layers `PlayArea`/40 and `Overlay`; `particleScale`
knob becomes the intensity multiplier the Designer window exposes. Core's `Merged` event
already carries tier + position, so Core does not change for effects.

Tests referencing removed Core particle code are updated/removed accordingly.

## 5. Verification

- 28 EditMode tests stay green (minus any that tested the removed Core particle list —
  those are updated).
- After each visual chunk: `screenshot-game-view` via the Unity MCP, compared side by side
  against the mock images.
- Full customer cycle observed in play mode: countdown sign → bear rises → bubble → tap to
  serve → flight → happy bounce → "Thank you" → countdown resumes.
- Zero console errors on load, rebuild, and play; enter/exit play mode three times with
  identical behaviour (domain-reload trap).
- `Pan Dulce ▸ Rebuild Stage` still regenerates `Main.unity` cleanly.

## Out of scope

Real hand-drawn art, fonts (Baloo 2 / Quicksand import), Sprite Atlas creation, on-device
mobile pass, in-game tweaks modal, persistence — unchanged from the skeleton's open items.
