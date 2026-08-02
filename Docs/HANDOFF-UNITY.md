# Pan Dulce Merge — Unity build handoff

**You are building a cozy merge-drop bakery game for mobile in this Unity project.** Everything you need is in this repo. Read §0 and §1, run the mock in a browser, then work the milestones in §12.

---

## 0. References — all in-repo, no external links needed

| Path | What it is | Authority |
| --- | --- | --- |
| `Docs/mock/Pan Dulce Merge Mock.dc.html` | **The design.** A runnable single-file mock with the full game logic inline. Open it in a browser (it needs its sibling `support.js`). | **Layout, art, behaviour, tuning ranges — this wins every disagreement.** |
| `Docs/web-reference/debug.html` + `js/debug.js` + `css/debug.css` | An earlier playable build with a **live tuning side-panel**. Open it and play with the sliders. | **The model for the Editor tool in §10.** Also the only source for score / fail-state code (see §13). |
| `Docs/mock/sketches/layout-sketch.png` | Pencil layout intent — window up top, case across the middle, cloth slung below. | Layout intent |
| `Docs/mock/sketches/tweaks-panel-mockup.png` | An in-game "Tweaks" modal — sections, sliders, `Reset`, `Save as defaults`. | Styling for §10.5 |
| `Docs/mock/inspiration/` | A video + 3 frames of *Pancito Merge*, a different published game. | **Mood only.** Watercolour texture, warm palette, chunky outlines. Its layout is the old design — do not copy it. |

**Do this before writing code:** open the mock, play it for two minutes, drag every slider in its right-hand prop panel. Then open `Docs/web-reference/debug.html` and look at the panel you are going to rebuild as an `EditorWindow`.

---

## 1. The game in one page

- Pastries drop into a **furoshiki** — a cloth sling under the bakery counter. Two of the same tier that touch merge into the next, across **11 tiers**.
- The cloth floor **sags**: it curves up at the edges and gently rolls everything toward the middle, so the pile settles into a bowl instead of a stack. This is the signature feel — get it right before judging anything else.
- A **customer arrives every `customerEverySec` seconds** (a timer, not a drop count), asks for one tier, and you tap a matching pastry to hand it over.
- First merge into a tier reveals it in the **display case** — a 5-slot window that slides to follow your progress.
- Every merge charges a meter. At 100% you can **shake the furoshiki**, launching the pile to reshuffle it.
- An **end-of-day** toggle folds the cloth shut with a knot and a "Closing time" card.

**No score, no fail state, no game over.** It is a sandbox. §13 has the open question about whether that stays — it matters more now that this is a phone game.

**Two deliverables:** the game, and an **in-Editor Tweaks window** (§10) with sliders and test buttons for design work.

**Platform: portrait phones, touch only.** The design frame is a fixed 430 × 880; §5 covers fitting it to real screens and §11 covers build settings and performance.

---

## 2. Project facts and traps

| | |
| --- | --- |
| **Target** | **Mobile, portrait phones.** Everything below assumes a touch device held upright. See §11. |
| Editor | **`6000.5.6f1`, pinned.** Opening in another version reserializes everything. Do not upgrade. |
| Pipeline | **URP 17.6.0, 2D Renderer** (`Assets/Settings/`) |
| Colour space | **Linear** (`m_ActiveColorSpace: 1`) — this changes how you must specify colours. See trap 6. |
| Input | **Input System 1.20.0, `activeInputHandler: 1` — new system ONLY.** Legacy `Input.GetKeyDown` / `Input.mousePosition` throws at runtime. |
| Play mode | **`DisableDomainReload` is ON.** Statics survive between play sessions. |
| Serialization | Force Text, Unix endings, 2D mode — committed, do not change |
| Scene | `Assets/Scenes/Main.unity` — only a `Main Camera` (ortho **size 5**, blue background). No `Light2D`. |
| Android | IL2CPP + **ARM64 only** already set. Orientation is **not** locked yet — all four autorotations are still enabled (§11.1). |
| Binaries | Git LFS via `.gitattributes` (`*.png *.psd *.wav *.ttf *.otf *.mp4` …) |

Available packages: 2D Aseprite + PSD importers, 2D Animation, SpriteShape, Tilemap, **Test Framework**, TextMeshPro (inside `com.unity.ugui`), Timeline. **Newtonsoft JSON is not installed** — hand-roll the tuner's JSON (it is a flat `string → number/bool/string` map) or add the package deliberately.

**Five traps that will cost you an hour each:**

1. **Domain reload is off** — statics do *not* reset. `GameRoot.Current` will point at a destroyed object on the second play, and `EditorApplication.update += …` double-subscribes. Always:
   ```csharp
   [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
   static void ResetStatics() { Current = null; }
   ```
   and unsubscribe every event in `OnDisable`.
2. **URP 2D + Sprite-Lit = black sprites.** There is no `Light2D` in the scene. Use `Sprites-Unlit-Default` everywhere (the art is flat by design). Applies to runtime-instantiated prefabs too.
3. **The camera is wrong** — size 5, blue clear colour. Fix per §5.
4. **After writing any `.cs` via MCP, wait for the compile and read the console** before the next call. Unity API calls during compilation are dropped or throw.
5. **`Main.unity` is shared.** Build content as prefabs and keep the scene thin (§4). Never author 40 objects directly into it.
6. **The project is in Linear colour space, and every colour in this document is an sRGB hex string.** Never build a `Color` by dividing hex components by 255 — in Linear space that renders washed out and wrong. Always go through `ColorUtility.TryParseHtmlString("#cf6b5c", out var c)` or the Inspector colour picker, both of which handle the conversion. The cloth's shading multiply needs the same care — see §8.5.

Never commit `Library/`. Always commit `.meta` files. Run `git lfs install` once per machine, and add any new binary extension to `.gitattributes` *before* the first commit of such a file.

---

## 3. Unity architecture

### 3.1 The rule that keeps this clean

> **The simulation is plain C# and knows nothing about Unity objects. MonoBehaviours only tick it and mirror it.**

The mock is one 800-line class doing physics, state and painting at once. Do not port that shape. Split it in three:

```
Core        pure C# — physics, merge rules, shop timer, boost meter, day cycle
            (uses Mathf/Vector2, but no MonoBehaviour, GameObject or Transform)
Runtime     MonoBehaviours — tick Core, mirror it onto sprites, handle input and audio
Editor      the Tweaks window, gizmos, sprite baker
```

Why it matters here: the sim is the part with exact numbers to preserve, it is the part worth unit-testing without a scene, and it is the part the Tweaks window pokes at. Keeping it free of `Transform` makes all three easy.

### 3.2 Assemblies and folders

```
Assets/PanDulce/
  Core/          PanDulce.Core.asmdef          (no Unity module refs beyond the core module)
  Runtime/       PanDulce.Runtime.asmdef       (refs Core, Unity.InputSystem, Unity.TextMeshPro)
  Editor/        PanDulce.Editor.asmdef        (Editor-only; refs Core + Runtime)
  Tests/         PanDulce.Tests.asmdef         (Editor-only; refs Core)
  Art/Pastries/  11 sprites (LFS)
  Art/Shell/     window scene, case, cloth, counter pieces (LFS)
  Art/Fonts/     Baloo 2 + Quicksand TTFs + TMP assets (LFS)
  Prefabs/
  Config/        Tuning.asset, Pastries.asset (text, committed, NOT LFS)
```

Four asmdefs make the layering structural rather than a convention people forget. `Editor` being Editor-only also makes a `UnityEditor` leak into a player build impossible. Namespace everything `PanDulce.Core`, `PanDulce.Runtime`, `PanDulce.Editor`.

### 3.3 Core types

| Type | Responsibility |
| --- | --- |
| `Body` | one pastry: position, velocity, rotation, tier, `spawnT`, `squish`, `bornAt` |
| `MergeSim` | integrate, resolve collisions, merge, walls + curved cloth floor, particles, floating text, `Shake()` |
| `ShopDirector` | customer timer, open/serve/happy/closed state machine, order tier selection |
| `BoostMeter` | charge accumulation, ready state, spend |
| `DayCycle` | eases `closeT` 0↔1; answers `CanDrop`, `CanShake`, `TimerRuns` |
| `TierTable` | names + base radii, `EffectiveRadius(tier, sizeScale)` |
| `ISimConfig` | read-only knob interface the sim consumes |

`ISimConfig` is the seam that lets Core stay Unity-free while the live `TuningConfig` ScriptableObject implements it — and lets tests pass a plain struct.

```csharp
public interface ISimConfig {
    float Gravity { get; } float Bounciness { get; } float SizeScale { get; }
    float RotationAmount { get; } float MergeGrowTime { get; } float ComboDelay { get; }
    float FloorSag { get; } float CenterPull { get; } float GroundFriction { get; }
    int   Substeps { get; } /* … */
}
```

### 3.4 Runtime components — one job each

| Component | Job |
| --- | --- |
| `GameRoot` | **Composition root.** Creates Core objects, ticks them in `Update`, holds `static GameRoot Current` for the Editor tool. Nothing else owns sim state. |
| `PastryViewPool` | Pooled `SpriteRenderer`s; each frame, mirrors sim bodies by id — position, rotation, squish scale |
| `ParticleViewPool` | Same, for the sim's particle list (the sim owns particle motion, not Unity's `ParticleSystem`) |
| `FloatingTextPool` | Pooled TMP labels for "Combo 3!", "New in the case!", "Shake!" |
| `ClothView` | Draws the furoshiki; derives every highlight/shadow from `clothColor` |
| `ClothShaker` | Offsets `ClothShakeRoot` during a shake |
| `FoldView` | The two flaps, the knot, the "Closing time" label, driven by `DayCycle.CloseT` |
| `CustomerView` | Sprite for the current regular + the four entrance animations |
| `SignView` | The hanging sign's two lines |
| `OrderBubbleView` | Bubble show/hide + icon + name |
| `DisplayCaseView` | Five slots, sliding window, discovered/silhouette state |
| `BoostBarView` | Charge fill, badge text, ready pulse, button click |
| `TopBarView` | Customer count, next-pastry chip |
| `ServeFlightView` | The pastry's arc from cloth to customer |
| `PointerInput` | New Input System → sim coordinates; aim, drop, serve |
| `SfxPlayer` | Bakes the six procedural cues once, plays them |
| `StageFitter` | Scales the stage root to fit the screen — **the only object that scales** |

### 3.5 Events, not polling

Core raises events; views subscribe. Views never reach into the sim to ask "did something merge?".

```csharp
public event Action<int, Vector2, int> Merged;      // tier, position, comboN
public event Action<int> TierDiscovered;
public event Action<int, Vector2> ServeStarted;
public event Action ServeCompleted;
public event Action Shaken;
public event Action<ShopState> ShopStateChanged;
```

**Subscribe in `OnEnable`, unsubscribe in `OnDisable`** — mandatory here, because domain reload is off and a missed unsubscribe survives into the next play session (trap 1).

### 3.6 Pooling

Bodies, particles and floating text churn constantly. Pool all three; never `Instantiate`/`Destroy` per merge. Pool parents live under `ClothShakeRoot` (§4) so they inherit the shake for free. Pre-warm ~48 pastries, ~120 particles, ~8 labels.

### 3.7 Depth: sorting layers, not Z

Everything sits at z = 0. Create these **Sorting Layers** in order and use *Order in Layer* within each:

| Sorting layer | Order | Contents |
| --- | --- | --- |
| `Background` | 0–30 | shop wall, window scene, lower wall |
| `Customer` | 0 | the customer |
| `Furniture` | 10 / 20 / 30 | hanging sign / counter lip / drawer band |
| `Case` | 0–30 | display case glass, ledges, icons, labels |
| `PlayArea` | 0 desk · 10 cloth · 20 aim · 30 bodies · 40 particles · 50 floats · 60 hem · 70 fold · 80 plaque | everything inside the cloth |
| `Overlay` | 0 top bar · 10 bubble · 20 thanks · 30 boost bar · 40 flyer | world-space Canvas + the serve flight |

The customer sits **behind** the sign, case and bubble — that ordering is deliberate, it puts them behind the counter.

### 3.8 UI approach

Build the whole 430 × 880 stage in **world space** so one fitter scales it uniformly. For the text-and-button parts (top bar, order bubble, "Thank you!", boost bar) use a **world-space `Canvas`** parented under the stage root, sorting layer `Overlay`. That gives you TMP, layout groups and a real `Button` for the shake control without fighting screen-space scaling. Add an `EventSystem` under systems.

---

## 4. Scene hierarchy

Keep `Main.unity` thin: folder objects, the camera, and prefab instances. **Use empty GameObjects as folders**, named so they sort and read as dividers:

```
Main.unity
│
├─ [ 00 · SYSTEMS ]
│   ├─ GameRoot                       ← GameRoot, SfxPlayer, PointerInput
│   └─ EventSystem
│
├─ [ 10 · CAMERA ]
│   └─ Main Camera                    ← ortho 4.4, solid #cfa06b
│
├─ [ 20 · STAGE ]                     ← StageFitter (the ONLY object that scales)
│   │
│   ├─ [ 21 · BACKDROP ]
│   │   ├─ ShopWall
│   │   ├─ Window
│   │   │   ├─ Sky
│   │   │   ├─ Clouds
│   │   │   ├─ Skyline                ← 5 buildings
│   │   │   ├─ Lamppost
│   │   │   ├─ Street
│   │   │   └─ Mullions
│   │   └─ LowerWall
│   │
│   ├─ [ 22 · CUSTOMER ]
│   │   └─ CustomerAnchor             ← CustomerView; entrance animation drives this
│   │       └─ CustomerSprite
│   │
│   ├─ [ 23 · FURNITURE ]
│   │   ├─ HangingSign                ← SignView (swing animation on this node)
│   │   │   ├─ Ropes
│   │   │   └─ Board → TopLabel, BigLabel
│   │   ├─ CounterLip
│   │   └─ DrawerBand
│   │
│   ├─ [ 24 · DISPLAY CASE ]
│   │   └─ CaseFrame                  ← DisplayCaseView
│   │       ├─ Glass, Rail, Glare, Knob
│   │       └─ Slots → Slot_0 … Slot_4  (Icon, Ledge, Label)
│   │
│   ├─ [ 25 · PLAY AREA ]
│   │   ├─ Desk                       ← does NOT shake
│   │   ├─ ClothShakeRoot             ← ClothShaker  (everything below shakes together)
│   │   │   ├─ Cloth                  ← ClothView
│   │   │   ├─ AimGuide
│   │   │   ├─ Bodies                 ← PastryViewPool  (pooled children)
│   │   │   ├─ Particles              ← ParticleViewPool
│   │   │   └─ FloatingText           ← FloatingTextPool
│   │   ├─ Hem
│   │   ├─ FoldFlaps                  ← FoldView → FlapLeft, FlapRight, Knot, ClosingLabel
│   │   └─ NextPlaque                 ← does NOT shake
│   │
│   └─ [ 26 · UI ]                    ← world-space Canvas
│       ├─ TopBar → CustomersChip, Title, NextChip
│       ├─ OrderBubble
│       ├─ ThanksLabel
│       ├─ BoostBar → ShakeButton (Icon, Label, ChargeBar, Badge)
│       └─ ServeFlight
│
└─ [ 90 · DEBUG ]                     ← gizmo drawers; disabled in builds
```

**Folder-object rules:**

- Identity transform always — position 0, rotation 0, **scale 1**. Children inherit, so a stray scale on a folder silently breaks layout.
- No components on folders, except an optional empty `HierarchyFolder` marker used to grey them out in the hierarchy.
- Numeric prefixes (`00`, `10`, `20`…) so ordering survives Unity's alphabetical sorting in some views, with gaps to insert later.
- Never reference a folder from code. Code holds direct references to the leaf it needs, wired in the Inspector or resolved by the prefab.
- Do not add folders under pooled parents (`Bodies`, `Particles`) — pool children should be flat.

**Prefabs:** `ShopFront`, `DisplayCase`, `Furoshiki`, `UIRoot`, `CustomerView`, `PastryView`, `ParticleView`, `FloatingText`. The scene instantiates them; edits happen in prefab mode, which is what keeps `Main.unity` merge-safe.

---

## 5. Coordinates, camera and the mobile frame

The mock simulates in **canvas pixels, y-down**, in a **418 × 400** play area:

```
CW 418   CH 400            play canvas
WL 34    WR 384            left / right walls
FY 372                     nominal floor line
BL 26    BR 392            cloth edges → centre cx = 209, half-width hw = 183
DROP_Y 36                  spawn height (the held pastry is drawn at y = 42)
aim line spans y 66 → 372
```

Keep the sim in these units. Every constant below — gravity 1500 px/s², radii 13…90, the 26 px sag — is expressed in them; converting to metres silently invalidates all of them.

```csharp
const float PX = 0.01f;          // 1 sim px = 0.01 world units (PPU 100)
Vector3 World(Vector2 sim) => new Vector3(sim.x * PX, -sim.y * PX, 0f);
// Canvas +rot is clockwise on screen (y-down); Unity +z is counter-clockwise:
transform.localEulerAngles = new Vector3(0, 0, -body.rot * Mathf.Rad2Deg);
```

**Simulate y-down, flip only at render time.** Gravity stays `+1500`. Sprites are authored as they appear on screen — the flip applies to positions, not art.

Stage frame **430 × 880** px → 4.30 × 8.80 units. The play canvas sits at stage `(6, 424)`, so the sim origin is stage-local `(6, 424)`.

**Camera fixes:** orthographic **size 4.4**, background **solid `#cfa06b`**, position `(2.15, -4.40, -10)` with the stage root's top-left at world origin.

### 5.1 Fitting a fixed 430 × 880 frame onto real phones

`StageFitter` reproduces the mock's rule — scale the stage root by `min(1, screenH/900, screenW/446)` — but on a device you never hit the `1` case, so in Unity it is simply:

```csharp
float scale = Mathf.Min(Screen.height / 900f, Screen.width / 446f);
```

The design frame is 430 × 880 inside a 446 × 900 safe box — an aspect of **0.495**. Real phones are taller than that, so **width is the limiting dimension and you get horizontal bars top and bottom**:

| Device | Screen | Limited by | Stage renders | Slack |
| --- | --- | --- | --- | --- |
| iPhone 14/15 | 1170 × 2532 (0.462) | width | 1127 × 2306 | 226 px vertical |
| Pixel 7/8 | 1080 × 2400 (0.450) | width | 1041 × 2130 | 270 px vertical |
| Older 16:9 | 1080 × 1920 (0.563) | height | 917 × 1877 | 143 px horizontal |
| iPad 4:3 | 1536 × 2048 (0.750) | height | 977 × 2000 | 559 px horizontal |

**Fill the slack with bleed art, not flat colour.** Extend the shop wall's plank texture above the top bar and the lower wall's wood gradient below the boost bar by ~150 stage px each, and widen both by ~120 px per side. Everything gameplay-relevant stays inside 430 × 880; the bleed only ever shows on devices whose aspect differs from the design frame. Flat `#cfa06b` bars would read as a bug on a phone.

### 5.2 Safe area — the top bar and boost bar both sit in danger zones

The top bar occupies stage y 0–56, exactly where the status bar, notch and Dynamic Island live. The boost bar occupies y 824–880, exactly where the iOS home indicator and Android gesture bar live. **Both must be inset or the shake button will fight the system's swipe-up gesture.**

```csharp
// SafeAreaInset — runs on the TopBar and BoostBar nodes, not on the stage root
Rect safe = Screen.safeArea;
float topInsetPx    = (Screen.height - (safe.y + safe.height)) / stageScale;   // → stage px
float bottomInsetPx = safe.y / stageScale;
```

- Push `TopBar` **down** by `topInsetPx` and extend its background upward by the same amount so the bar's colour runs under the notch rather than leaving a gap.
- Push `BoostBar` **up** by `max(bottomInsetPx, 24)` — a 24 stage-px floor keeps the button clear of the gesture bar even on devices reporting no inset.
- **Do not inset the play area.** The cloth stays centred in the frame; only the two chrome bars move. Re-evaluate on orientation/resolution change, and recompute after `StageFitter` so the conversion to stage px uses the final scale.

Test this in the **Simulator** view (Game view dropdown → Simulator) against an iPhone 15 Pro and a Pixel, not just a free-aspect Game view — the notch only appears there.

---

## 6. Tiers and tuning knobs

### 6.1 Tiers

| # | Name | Radius | | # | Name | Radius |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Cookie | 13 | | 6 | Cinnamon Roll | 48 |
| 1 | Muffin | 17 | | 7 | Shell Bun | 57 |
| 2 | Kiss Cookie | 22 | | 8 | Piggy Cookie | 67 |
| 3 | Biscuit | 27 | | 9 | Flan | 78 |
| 4 | Turnover | 33 | | 10 | Ring Cake | 90 |
| 5 | Bread Roll | 40 | | | | |

Four names changed in this revision — *Sugar Kiss → Kiss Cookie*, *Empanada → Turnover*, *Piggy Bread → Piggy Cookie*, *King Cake → Ring Cake*. If you see the old names anywhere, they are stale.

Effective radius = `radius × sizeScale`; recompute only when `sizeScale` changes. Tiers 0–3 start discovered. **Spawn pick is always weighted 4:3:2:1 over tiers 0–3 only.**

Store this table in a `PastryDatabase` ScriptableObject (`Config/Pastries.asset`): name, base radius, sprite. One asset, no magic numbers scattered through code.

### 6.2 Tier A knobs — the mock's props, reproduce exactly

| key | control | default | min | max | step | unit | section |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `gravity` | slider | 1500 | 600 | 3000 | 50 | px/s² | Physics |
| `bounciness` | slider | 0.08 | 0 | 0.5 | 0.01 | | Physics |
| `sizeScale` | slider | 1.3 | 0.7 | 1.6 | 0.05 | × | Physics |
| `rotationAmount` | slider | 0.2 | 0 | 1 | 0.05 | | Physics |
| `mergeGrowTime` | slider | 0.85 | 0.2 | 2 | 0.05 | s | Merge |
| `comboDelay` | slider | 0.5 | 0 | 2 | 0.1 | s | Merge |
| `customerEverySec` | int | **18** | 5 | 60 | 1 | s | Customers |
| `entranceStyle` | enum | `Walk` | `Walk` `Hop` `Pop` `Slide` | | | | Customers |
| `entranceTime` | slider | 0.7 | 0.3 | 2 | 0.05 | s | Customers |
| `clothColor` | colour | `#cf6b5c` | swatches `#cf6b5c` `#6a7fb0` `#7fa864` `#d9a441` | | | | Furoshiki |
| `endOfDay` | toggle | false | | | | | Furoshiki |
| `boostsOn` | toggle | true | | | | | Boost |
| `shakePower` | slider | 1 | 0.3 | 2.2 | 0.1 | × | Boost |
| `chargePerMerge` | slider | 0.14 | 0.02 | 1 | 0.02 | | Boost |
| `soundOn` | toggle | true | | | | | Audio |

### 6.3 Tier B knobs — hard-coded in the mock, worth exposing

Defaults **must** equal the mock's constants so a fresh config reproduces it exactly. Put these behind a collapsed "Beyond the mock" foldout so nobody mistakes them for approved design.

| key | default | range | |
| --- | --- | --- | --- |
| `substeps` | 3 | 1–8 int | physics iterations per frame |
| `floorSag` | 26 | 0–80 px | how high the cloth rises at its edges |
| `centerPull` | 34 | 0–120 px/s² | inward roll while touching the cloth |
| `groundFriction` | 9 | 0–25 | `vx *= (1 − k·dt)` on contact |
| `comboWindow` | 1.4 | 0.2–4 s | gap that still chains a combo |
| `mergePopVy` | −70 | −400–100 px/s | upward hop of a newborn |
| `squishAmount` | 1 | 0–2 × | impact flattening |
| `particleScale` | 1 | 0–3 × | multiplies 9 puffs + 6 sparks |
| `dropCooldown` | 0.5 | 0–2 s | |
| `dropVy` | 60 | 0–600 px/s | |
| `flySec` | 0.9 | 0.2–2.5 s | serve flight time |
| `happyMs` | 1400 | 200–4000 ms | "Thank you!" hold |
| `startingBodies` | 9 | 0–24 int | **applies on restart only** |
| `shakeDuration` | 0.6 | 0.1–2 s | cloth wobble length |
| `timeScale` | 1 | 0.05–2 × | Editor-only |
| `paused` | false | | Editor-only |
| `showColliders` / `showIds` / `showFloorCurve` | false | | Editor-only debug draw |

---

## 7. Simulation — port literally

Do **not** use `Rigidbody2D`/`CircleCollider2D`. The mock uses a bespoke positional solver (mass = r², fixed substeps, custom squish/spin coupling, a curved floor with inward pull). Unity 2D physics will not reproduce it and half the knobs would stop meaning anything.

### 7.1 Effective radius — back-out easing during the merge pop

```csharp
float Er(Body b) {
    float t = Mathf.Min(1f, b.spawnT);
    const float c1 = 1.70158f, c3 = c1 + 1f;
    float e = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    return R[b.tier] * (0.35f + 0.65f * e);
}
```

### 7.2 Frame loop — `Update`, not `FixedUpdate`

```csharp
float dt = Mathf.Min(0.032f, Time.deltaTime);   // leave Time.timeScale at 1
now += dt;

float target = cfg.EndOfDay ? 1f : 0f;          // day cycle eases over 1.1 s
float d = target - closeT;
if (Mathf.Abs(d) > 0.001f) closeT += Mathf.Sign(d) * Mathf.Min(Mathf.Abs(d), dt / 1.1f);

if (shop.Closed && !serveInFlight && closeT < 0.4f) {   // customer countdown
    timeLeft -= dt;
    secondsShown = Mathf.Max(0, Mathf.CeilToInt(timeLeft));
    if (timeLeft <= 0f) { timeLeft = CustomerEverySec; shop.OpenWindow(); }
}

for (int i = 0; i < cfg.Substeps; i++) Step(dt / cfg.Substeps);
```

`CustomerEverySec => Mathf.Max(3, Mathf.RoundToInt(cfg.CustomerEverySec))`.

### 7.3 `Step(dt)`

```csharp
// 1 — integrate
foreach (var b in bodies) {
    b.vy += cfg.Gravity * dt;
    b.x += b.vx * dt;  b.y += b.vy * dt;  b.rot += b.vrot * dt;
    float cap = 3f * cfg.RotationAmount + 0.15f;
    b.vrot = Mathf.Clamp(b.vrot, -cap, cap) * 0.99f;
    b.squish *= (1f - 6f * dt);
    if (b.spawnT < 1f) b.spawnT = Mathf.Min(1f, b.spawnT + dt / cfg.MergeGrowTime);
}

// 2 — pairwise; O(n²) is fine, counts stay under ~40
for i, for j > i:
    if (a.dead || c.dead) continue;
    float ra = Er(a), rc = Er(c);
    float dx = c.x - a.x, dy = c.y - a.y, d = Mathf.Sqrt(dx*dx + dy*dy);
    float min = ra + rc;
    if (d >= min) continue;
    if (d < 0.01f) { d = 0.01f; dx = 0.01f; dy = 0f; }
    float nx = dx / d, ny = dy / d;

    // merge gate — BOTH grown past 0.55 AND older than comboDelay
    if (a.tier == c.tier && a.tier < 10 &&
        a.spawnT > 0.55f && c.spawnT > 0.55f &&
        now - a.bornAt > cfg.ComboDelay && now - c.bornAt > cfg.ComboDelay) {
        a.dead = c.dead = true; merges.Add((a, c)); continue;
    }

    float ma = ra*ra, mc = rc*rc, tm = ma + mc, ov = min - d;   // mass = r²
    a.x -= nx*ov*(mc/tm);  a.y -= ny*ov*(mc/tm);
    c.x += nx*ov*(ma/tm);  c.y += ny*ov*(ma/tm);

    float rvx = c.vx - a.vx, rvy = c.vy - a.vy;
    float vn = rvx*nx + rvy*ny;
    if (vn < 0) {
        float jm = -(1f + cfg.Bounciness) * vn / (1f/ma + 1f/mc);
        a.vx -= jm*nx/ma; a.vy -= jm*ny/ma;
        c.vx += jm*nx/mc; c.vy += jm*ny/mc;
        float q = Mathf.Min(0.28f, Mathf.Abs(vn) / 1500f) * cfg.SquishAmount;
        if (q > 0.05f) { a.squish = Mathf.Max(a.squish, q); c.squish = Mathf.Max(c.squish, q); }
        float vt = rvx * -ny + rvy * nx, rk = 0.15f * cfg.RotationAmount;
        a.vrot += (vt/ra) * rk;  c.vrot += (vt/rc) * rk;
    }

// 3 — walls, and THE CURVED CLOTH FLOOR (the signature of this design)
const float cx = 209f, hw = 183f;               // (BL+BR)/2, (BR-BL)/2
foreach (var b in bodies) {
    float r = Er(b);
    if (b.x - r < WL) { b.x = WL + r; b.vx =  Mathf.Abs(b.vx) * E; b.vrot = -b.vy/r * 0.4f * rotAmt; }
    if (b.x + r > WR) { b.x = WR - r; b.vx = -Mathf.Abs(b.vx) * E; b.vrot =  b.vy/r * 0.4f * rotAmt; }

    float k  = Mathf.Min(1f, Mathf.Abs(b.x - cx) / hw);
    float fy = FY - cfg.FloorSag * k * k;                    // 26 px higher at the edges
    if (b.y + r > fy) {
        float vi = b.vy;
        b.y  = fy - r;
        b.vy = -Mathf.Abs(b.vy) * E * 0.6f;
        if (Mathf.Abs(b.vy) < 20f) b.vy = 0f;
        b.vx *= (1f - cfg.GroundFriction * dt);              // friction
        b.vx -= ((b.x - cx) / hw) * cfg.CenterPull * dt;     // roll toward the middle
        b.vrot += (b.vx/r - b.vrot) * Mathf.Min(0.4f, 0.05f + 0.5f * rotAmt);
        if (vi > 180f) b.squish = Mathf.Max(b.squish, Mathf.Min(0.3f, vi/1600f) * cfg.SquishAmount);
    }
}

// 4 — apply merges, drop dead bodies, age particles and floating text
```

### 7.4 Merge

```csharp
int t2 = a.tier + 1;
float ra = Er(a), rc = Er(c);
float x = (a.x*ra + c.x*rc) / (ra+rc);          // radius-weighted midpoint
float y = (a.y*ra + c.y*rc) / (ra+rc);
var nb = MakeBody(x, y, t2, spawnT: 0);
nb.vy = cfg.MergePopVy;                          // -70
nb.vx = (a.vx + c.vx) * 0.3f;
Burst(x, y, R[t2]);  Sfx("merge", t2);

charge = Mathf.Min(1f, charge + cfg.ChargePerMerge);

comboN = (now - lastMergeT < cfg.ComboWindow) ? comboN + 1 : 1;
lastMergeT = now;
if (comboN >= 2) FloatText(x, y - R[t2] - 8, $"Combo {comboN}!");

if (!discovered[t2]) {
    discovered[t2] = true;
    FloatText(x, y - R[t2] - 26, "New in the case!");
    Sfx("disco");
    TierDiscovered?.Invoke(t2);                  // the case may slide
}
```

New body defaults: `rot = Random.Range(-0.15f, 0.15f)`, `vrot = (Random.value - 0.5f) * 2.4f * rotationAmount`, `spawnT = 1` for drops, `0` for merge products.

### 7.5 Particles and floating text

Per merge **and** per serve, from `(x, y)` with the new tier's radius `r`:

- **9 puffs** — spawned on a circle of `r*0.5`, speed 60–170, life 0.5–0.75 s, radius 4–9, `#fff3dd`.
- **6 sparks** — cross-shaped, from the centre, speed 90–220, life 0.6 s, radius 3–6, `#f0b64f`.
- Both: `v *= 0.96` per frame plus lift (−20 px/s² sparks, −60 puffs); alpha fades with remaining life; a spark draws as a `+` of arm length `r*(0.5+k)`.

Floating text rises 42 px/s over 1 s: `#fff3dd` fill, `#8a5a33` 4 px outline, 17 px bold, alpha `min(1, 2k)`.

### 7.6 Drop, serve, customers

```csharp
// drop
if (closeT > 0.12f || now < canDropAt) return;         // no dropping while folding shut
float r = R[curTier];
var b = MakeBody(Mathf.Clamp(aimX, WL + r, WR - r), 36f, curTier, spawnT: 1);
b.vy = cfg.DropVy;                                      // 60
canDropAt = now + cfg.DropCooldown;                     // 0.5
curTier = nextTier;  nextTier = Pick();

// customer arrives
orderTier = Random.Range(2, 6);                         // tiers 2..5 inclusive
customer  = served % 3;                                 // three regulars, cycled
Sfx("chime");
```

- Servable while open, not happy, an order exists, nothing in flight.
- Pointer-up hit-test: nearest body with `tier == orderTier` and `dist < Er(b) + 6`. **A hit serves instead of dropping.**
- `Serve(b)`: remove, burst, then fly from stage `(6 + b.x − 28, 424 + b.y − 28)` to **`(182, 224)`**, scaling 1 → 0.6 over `flySec` (0.9 s), position eased `cubic-bezier(0.35, −0.15, 0.35, 1)` (a curve with a slight undershoot), scale eased smoothly.
- On arrival: `Sfx("serve")`, happy, `served++`. After `happyMs` (1400): close, clear the order, and **reset `timeLeft = customerEverySec`**.
- Keep a timeout fallback so a missed animation callback cannot strand the flyer.

The hanging sign reads:

| State | Top line | Big line |
| --- | --- | --- |
| Closed | `next customer in` | `{ceil(timeLeft)}s` |
| Open | `now serving` | `♥` |

### 7.7 Shake boost

```csharp
void OnShakePressed() {
    if (charge < 1f || closeT > 0.12f) return;
    charge = 0f;
    DoShake();
}

void DoShake() {
    float p = cfg.ShakePower;                     // 1
    foreach (var b in bodies) {
        b.vy = -(190f + Random.value * 190f) * p;
        b.vx = (Random.value - 0.5f) * 350f * p;
        b.vrot += (Random.value - 0.5f) * 3.2f * cfg.RotationAmount;
        b.squish = 0.2f;
    }
    shakeUntil = now + cfg.ShakeDuration;         // 0.6
    FloatText(209f, 176f, "Shake!");
    Sfx("shake");
}
```

**Cloth wobble** — while `now < shakeUntil`, offset `ClothShakeRoot`:

```csharp
float sh = (shakeUntil - now) / cfg.ShakeDuration;      // 1 → 0
float ox = Mathf.Sin(now * 47f) * sh * 13f;             // sim px
float oy = Mathf.Cos(now * 39f) * sh * 6f;
```

> ⚠️ The mock writes `26` and `12` into a canvas transform whose scale is 2, so the on-screen amplitude is **half** those numbers. Use **13 and 6**. A literal port shakes twice as hard as designed.

Only the cloth and its contents move — `Desk` and `NextPlaque` are siblings outside `ClothShakeRoot` (§4).

### 7.8 End of day

`endOfDay` eases `closeT` 0 → 1 over 1.1 s (and back). `closeT > 0.12` blocks dropping and shaking; `closeT ≥ 0.4` pauses the customer countdown; `closeT > 0.002` draws the fold (§8.5).

### 7.9 Reset

Clear bodies, particles, floats; reset charge, combo, timer, cooldown; re-lock tiers 4–10; re-pick current and next; spawn `startingBodies` (9) at `x = 60 + rand*300`, `y = 300 − i*34`, tier via `Pick()`.

---

## 8. Layout and art

All coordinates are stage-local, y-down from the top-left of the 430 × 880 frame.

### 8.1 Pastry sprites — the cheap path

Every pastry is drawn purely in multiples of its radius `R`, **including the outline width** (`O = max(2, R*0.09)`). So **one sprite per tier, uniformly scaled, is identical to the mock.**

- Author at canonical radius **Rc = 200 px** in a **512 × 512** texture (art reaches ~1.0 R; padding covers the piggy's snout and ears), pivot centre.
- Import: Sprite (2D and UI), Single, **PPU 100**, Pivot Center, Full Rect, Bilinear, Generate Physics Shape **off**, material **`Sprites-Unlit-Default`**.
- Runtime: `localScale = new Vector3(1 + squish*0.6f, 1 - squish, 1) * (Er(b) / 200f)`.
- Max on-screen radius is `90 × 1.6 = 144`, so 200 px canonical never upscales.

**Mobile texture settings.** Uncompressed 512² RGBA is 1 MB per sprite — 11 pastries plus shell art would burn ~20 MB of texture memory for no benefit. Use a **platform override**: `Compression: None` for Editor/Standalone (so you can compare against the mock pixel-for-pixel) and **ASTC 6×6** for Android and iOS. Flat art with hard outlines compresses cleanly at 6×6; if you see ringing on the thin outlines, step to 5×5 rather than turning compression off.

**Pack everything into a Sprite Atlas.** With ~40 bodies plus the shell on screen, `SpriteRenderer` batching is what keeps the draw call count near single digits — and it only batches sprites sharing a texture and material. One atlas for pastries, one for shell art, both with the same unlit material. This is the single highest-value performance decision in the project.

The per-tier recipe is `drawPastry(ctx, t)` in the mock. Port it once into an editor-time texture baker, or hand-author to this palette:

| Tier | Fill | Outline | Accents |
| --- | --- | --- | --- |
| 0 Cookie | `#f4d9a8` | `#c9a26b` | face `#6b4a2e`, blush `rgba(232,143,162,.7)` |
| 1 Muffin | cup `#d95f43` | `#b04531` | top `#f0c26a`/`#cf9b4a`, chips `#b97f33` |
| 2 Kiss Cookie | `#e88fa2` | `#c96b82` | centre `#d95f77`, white sprinkles |
| 3 Biscuit | `#f2d9ae` | `#cfa870` | top `#f6e3bd`, jam `#c94f4f` |
| 4 Turnover | `#e8a24b` | `#bf7c2f` | crimp `#d18a35`, vents `#bf7c2f` |
| 5 Bread Roll | `#efb96b` | `#c2884a` | slash `#f7dca8` |
| 6 Cinnamon Roll | `#e0a055` | `#b97a3a` | spiral `#b97a3a`, icing `rgba(255,250,240,.85)` |
| 7 Shell Bun | `#efb3bd` | `#cf8b96` | shell lines `#fae3e6` |
| 8 Piggy Cookie | `#b5793f` | `#8f5a2a` | snout `#d9a86f` |
| 9 Flan | `#f2c464` | `#d19a3c` | caramel `#b06a2a`/`#96591f` |
| 10 Ring Cake | `#eec27a` | `#c8964d` | ribbons `#d95f43` `#7fa864` `#e88fa2` `#f0b64f` |

### 8.2 Top bar — `(0, 0)` 430 × 56

Gradient `#a97448 → #96633c`, 4 px `#7c5231` bottom border, padding 0 10, space-between.
**Left** `Customers: {served}` chip — `#7c5231` fill, 2 px `#6f4a2c`, radius 10, `#fff3dd` 700 14 px.
**Centre** `Sweet Bakery` — 22 px 800 `#fff3dd`, letter-spacing 0.5, shadow `0 2px 0 #6f4a2c`.
**Right** `Next` chip — same fill, gap 5, label 13 px 800, then a 36 × 36 pastry at radius 14.

### 8.3 Wall and window — wall `(0, 56)` 430 × 340

Fill `#e6c194`, vertical plank seams `repeating-linear-gradient(90deg, rgba(140,96,54,.16) 0 2px, transparent 2px 46px)`; a `rgba(160,110,60,.13)` band over the bottom 60 px.

**Window** at `(26, 72)`, 378 × 206 — 7 px `#a97448` border, radius 8, shadow `0 5px 0 #8a5a33` + `inset 0 0 0 3px #c08a55`, sky `#d9e9f1 → #e9e1cd (60%) → #efe2ce`. Inside (window-relative):

- **Clouds:** `(34,20)` 58×14 @85% · `(54,11)` 30×14 @85% · `(right 46, 32)` 46×12 @70%, all white pills.
- **Skyline** — all on `bottom 44`, striped `repeating-linear-gradient(180deg, rgba(255,255,255,.26–.30) 0 5px, transparent 5px <period>)`, 3 px top border:

  | left | size | fill | border | period |
  | --- | --- | --- | --- | --- |
  | −6 | 80 × 88 | `#c3b39c` | `#ab9880` | 19 |
  | 66 | 64 × 124 | `#b4a389` | `#9c8b72` | 21 |
  | 126 | 88 × 74 | `#ccbca4` | `#b4a48c` | 18 |
  | 208 | 72 × 112 | `#bcab92` | `#a4947b` | 20 |
  | 274 | 98 × 86 | `#c8b8a0` | `#b0a088` | 19 |

- **Lamppost:** 4 × 112 `#8a7a66` at `left 100, bottom 44`; head `left 90, bottom 150`, 24 × 15, radius `8 8 3 3`, `#f0d79b`, 2 px `#8a7a66`.
- **Street:** full-width 44 px `#cbbca6`, 4 px `#b8a68d` top edge; dashes at `bottom 20`, 3 px, `repeating-linear-gradient(90deg, rgba(255,255,255,.8) 0 18px, transparent 18px 36px)`.
- **Mullions:** 5 px `#b5854f` bars at `left 112` and `right 112`, full height.

### 8.4 Furniture, customer, case

| Element | Rect | Style |
| --- | --- | --- |
| Hanging sign | `(8, 56)` 118 × 100 | swing ±1.2° over 3.6 s ease-in-out, origin `59, 0`; two 3 × 24 `#7c5231` ropes at ±16°; board at `top 20` 118 × 76, `#fffaf0 → #f2e3c8`, 3 px `#a97448`, radius 10, shadow `0 4px 0 #8a5a33`. Top line Quicksand 10 px 700 `#a58358`; big line 34 px 800 `#8a5a33` |
| Customer | `(105, 172)` 210 × 170 | origin bottom-centre; entrance per §8.6 |
| Order bubble | `(132, 136)` | pop `.35s cubic-bezier(.34,1.56,.64,1)` after `.5s`; box `#fffaf0`, 3 px `#e0cba6`, radius 18, padding 8/14/8/10, shadow `0 4px 0 rgba(122,84,49,.25)`; 44 × 44 icon at radius 16; `{Name}, please!` 17 px 700 `#6b4a2e`; hint `tap it in the cloth to hand it over` Quicksand 10 px 600 `#a58358`; tail 18 × 18 rotated 45°, offset `-11, 40` |
| "Thank you!" | `(0, 150)` 430 wide, centred | pop `.4s cubic-bezier(.34,1.56,.64,1)`, 38 px 800 `#fff3dd`, shadow `0 4px 0 #b26a3c` + ±2 px |
| Counter lip | `(0, 392)` 430 × 12 | `#e8bb80 → #c98f52`, 2 px `#ab7742` bottom |
| Drawer band | `(0, 404)` 430 × 20 | `#a86e3c`, 3 px `#7c5231` bottom, `inset 0 3px 0 rgba(0,0,0,.12)`; three equal 9 px outlines, 2 px `#8f5a2a`, radius 2, gap 10, padding 0 12 |
| Lower wall | `(0, 424)` 430 × 456 | `#6b4527 0% → #7c5231 5% → #c08a4f 7% → #b98244 32% → #a97139 100%` |

**Display case** — `(9, 300)` 412 × 96. Replaces the old 11-cell shelf.

- Knob on top `(191, −11)` 30 × 11, radius `6 6 0 0`, `#e0c079`, 2 px `#b18f4c`, no bottom border.
- Glass: 3 px `rgba(255,255,255,.92)`, radius `14 14 5 5`, fill `linear-gradient(158deg, rgba(255,255,255,.5) 0%, rgba(255,255,255,.1) 38%, rgba(255,255,255,.06) 62%, rgba(255,255,255,.3) 100%)`, `inset 0 -8px 14px rgba(255,255,255,.35)`, shadow `0 4px 0 rgba(120,84,48,.18)`, clipped.
- Rail highlight: full-width 2 px `rgba(255,255,255,.75)` at `top 15`. Two glare stripes rotated 20°: `(−40,−30)` 44 × 190 @40%, `(22,−30)` 16 × 190 @30%.
- **Five slots** across the bottom 74 px, bottom-aligned. Each: 48 × 48 icon (pastry at radius ≈ 21, nudged +1 px down), a 52 × 5 ledge `#d8c6ac` with shadow `0 2px 0 #b99f7c`, and a Quicksand 9.5 px 700 `#7a5735` label.
- **Sliding window:** with `maxD` = highest discovered tier, show tiers `[start, start+5)` where `start = clamp(maxD − 3, 0, 6)`. Undiscovered entries render the sprite tinted flat **`#c3ae93`**, label `?`.

### 8.5 The furoshiki

Painted back to front, in sim coordinates. `C = clothColor`; every shade is a channel multiply `mix(C, f)`.

> **Do the multiply in gamma space.** The mock multiplies sRGB channels, but this project renders in Linear (§2, trap 6), where multiplying linear values by the same factor gives a visibly different result. Match the mock with:
> ```csharp
> static Color Mix(Color c, float f) {
>     Color g = c.gamma;
>     return new Color(Mathf.Min(1f, g.r*f), Mathf.Min(1f, g.g*f), Mathf.Min(1f, g.b*f), c.a).linear;
> }
> ```
> Every `mix(C, …)` below goes through this. Getting it wrong makes all four cloth colours look muddy in shadow and blown out in highlight.

1. **Desk (does not shake).** Top band `rgba(70,44,22,.45)` 418 × 14; `rgba(255,226,182,.55)` 3 px line at y 16; plank lines `rgba(92,58,26,.3)` 2 px at y 58, 150, 268, 390; contact shadow ellipse at `(209, 92)` radii 206 × 30, `rgba(64,38,16,.22)`.
2. **Cloth body** (start of `ClothShakeRoot`). A draped V: from `(BL−16, 40)` via quadratics `(BL+4,88)→(BL+36,100)`, `(cx−62,124)→(cx,110)`, `(cx+62,124)→(BR−36,100)`, `(BR−4,88)→(BR+16,40)`, then straight down. Fill `C`, stroke `mix(C,.72)` 2.5 px.
3. **Detailing**, clipped: highlight seam `rgba(255,246,232,.55)` 2.5 px tracing the same curve ~12 px lower; polka dots `rgba(255,246,232,.34)` radius 4 on a 42 × 42 grid from y 140, alternate rows offset 21; side shadows `rgba(0,0,0,.12)` over the outer panels.
4. **Aim guide + held pastry** — only while `now ≥ canDropAt && closeT < 0.12`. Dashed `rgba(255,255,255,.8)` 3 px, dash `[4,10]`, y 66 → 372 at the clamped aim x; held pastry at `(hx, 42)`.
5. **Bodies → particles → floating text.** Order-matching bodies get a pulsing ring at `Er·pulse + 6`, `pulse = 1 + sin(now*5)*0.05`, `#f0b64f` 3.5 px dashed `[7,7]`; the hovered one becomes `#fff3dd` 5 px solid and the body scales ×1.14.
6. **Hem.** Solid `mix(C,.88)` band from y 372 down, a `mix(C,.68)` 2.5 px line at y 373, a dashed `rgba(255,246,232,.5)` 2 px stitch at y 386, dash `[9,8]`.
7. **NEXT plaque (does not shake).** At `(BR−34, 34)`: 64 × 50 rounded rect (radius 12) `rgba(255,243,221,.95)`, 2.5 px `#a97448`, the word `NEXT` 11 px 800 `#a58358`, next pastry at radius 13. This is *in addition to* the top-bar Next chip.

**Fold-closed** (`closeT > 0.002`), with `p = closeT`, `e = p < .5 ? 2p² : 1 − (−2p+2)²/2`:

- Two flaps pivoting at y 352 — one at `BL+12` rotating `+e·1.42` rad, one at `BR−12` rotating `−e·1.42`. Each is a curved triangle `(0,0) → quad(±34,−190 → ±4,−318) → quad(±150,−178 → ±214,−8)`, filled `mix(C,1.06)` / `mix(C,.9)`, stroked `mix(C,.66)` 2.5 px, with a clipped highlight curve and seven `rgba(255,246,232,.3)` dots.
- Past `p > 0.55` a knot scales in with `k = (p−.55)/.45` at `(cx, 108)`: two ellipses 30 × 17 at ∓0.45 rad `mix(C,1.1)`, a centre ellipse 26 × 21 `mix(C,1.18)`, all stroked `mix(C,.66)`, plus a `rgba(255,246,232,.5)` arc highlight.
- **"Closing time"** 20 px 800 `#fff3dd` with a `mix(C,.6)` 5 px outline at `(cx, 190)`, fading in with `k`.

### 8.6 Customer entrances

Four styles, `entranceTime` long (0.7 s), eased `cubic-bezier(0.22, 0.9, 0.3, 1)`, on the customer anchor (origin bottom-centre). Reproduce as `AnimationCurve`s:

| Style | Keyframes |
| --- | --- |
| `Walk` | x −200 → −128 (30%) → −74 (50%) → −30 (70%) → +4 (88%) → 0; y bobs −9 / 0 / −7 / 0; fades in by 12% |
| `Hop` | y +130 → −20 (48%) → +6 (72%) → 0 with squash-stretch `.92/1.06 → 1.06/.94 → .97/1.03 → 1/1`; fades in by 30% |
| `Pop` | scale `.18` rot −12° → scale 1.14 rot +5° (55%) → scale .96 rot −2° (78%) → scale 1 rot 0 |
| `Slide` | y +96 → 0, fading in by 60% |

When happy, the entrance is replaced by a bounce played **twice** over 0.45 s: `y 0 → −14 (30%, scale 1.02/0.98) → 0 (60%, scale .99/1.01) → 0`.

Three regulars, cycled `served % 3`: shirts `#d95f43` / `#7fa864` / `#7f9fc9`, skin `#b5793f` / `#cdc5ba` / `#f0c25e`, with ears / cat ears / antenna respectively. Bake to three sprites like the pastries.

### 8.7 Boost bar — `(0, 824)` 430 × 56

Shown only when `boostsOn`. Gradient `#a97448 → #8f6039`, 3 px `#7c5231` top border, `inset 0 3px 0 rgba(255,225,180,.22)`.

Button 252 × 46, radius 15, `#f0b64f → #dd9a2e`, 3 px `#7c5231`, shadow `0 3px 0 #6f4a2c`, gap 9:

- **Shaker icon** 26 × 24 — a 17 × 17 radius-4 `#fff3dd` square rotated 12° at `(4,6)`; a 10 × 10 `#fff3dd` circle with 2.5 px `#c07f1c` at `(8,0)`; two 7 × 2.5 motion dashes `rgba(255,243,221,.85)` at `(−4,8)` and `(right −4,15)`.
- **Label** `Shake the furoshiki!` 15 px 800 `#6b4a2e`.
- **Charge bar** 160 × 7, radius 4, track `rgba(111,74,44,.4)`, fill `#fff3dd`, width eased over 0.35 s.
- **Badge** `(right −8, top −9)`, min 23 × 23, radius 12, `#7c5231`, 2.5 px `#6f4a2c`, `#fff3dd` 11 px 800 — `NN%`, or `READY!` at full.
- **States:** opacity 0.72 charging, 1.0 ready; when ready it pulses over 1.15 s — `translateY 0, scale 1` ↔ `translateY −2, scale 1.035`, with a halo ring expanding 0 → 7 px of `rgba(255,243,221,.6) → transparent`.

### 8.8 Fonts and palette

Baloo 2 (600/700/800) for headings, HUD and floating text; Quicksand (500/600/700) for small hints and case labels. TTFs into `Art/Fonts/` (LFS), generate TMP assets. Cream-on-brown outlines are TMP outline/underlay settings, not baked textures. Do not fall back to Arial.

Palette: `#f2ddb7` masa · `#8a5a33` crust · `#6f4a2c` dark crust · `#fff3dd` cream · `#a97448` wood · `#cfa06b` page background · cloth `#cf6b5c` `#6a7fb0` `#7fa864` `#d9a441`.

---

## 9. Audio and input

### 9.1 Six procedural cues

Bake `AudioClip`s once at startup with `AudioClip.Create`, or author six clips. Envelope: 15 ms linear attack to `vol`, then exponential decay to 0.001 over `dur`.

| Cue | Tones (freq Hz, start s, dur s, wave, vol) |
| --- | --- |
| `drop` | 180, 0, 0.10, sine, 0.12 |
| `merge` | `f = 260 + tier*55` → (f, 0, 0.12, tri, 0.16) + (f×1.5, 0.06, 0.14, tri, 0.12) |
| `disco` (new tier) | 660/0/0.12 · 880/0.09/0.16 · 1100/0.18/0.20, sine, 0.12/0.12/0.10 |
| `chime` (customer arrives) | 1318/0/0.55 · 1760/0.02/0.60 · 2637/0.04/0.30 · 1568/0.24/0.55 · 2093/0.26/0.50, sine, 0.09/0.055/0.03/0.07/0.04 |
| `serve` | 523/0/0.12 · 659/0.09/0.12 · 784/0.18/0.22, sine, 0.13 |
| `shake` | six tones at `300 + rand*560` Hz staggered 0.04 s apart, 0.08 s, tri, 0.045 — plus 170 Hz sine, 0.02 s in, 0.32 s, 0.07 |

Mute everything when `soundOn` is false. There is no game-over cue in this design.

### 9.2 Input — new Input System only

```csharp
using UnityEngine.InputSystem;
var ptr = Pointer.current;                  // covers mouse AND touch — do not special-case Touchscreen
if (ptr != null) {
    Vector2 screen = ptr.position.ReadValue();
    bool down = ptr.press.wasPressedThisFrame;
    bool up   = ptr.press.wasReleasedThisFrame;
}
```

- Screen → sim: `ScreenToWorldPoint`, subtract the play-area root, divide by `PX`, flip Y back to y-down. Clamp `aimX` to `[WL, WR]`.
- Down and move set `aimX`; **up** serves if released over a servable body, else drops. Aim must keep tracking past the play area's edge while held — polling the device (rather than raycasting a collider) gives that for free.
- Multi-touch is out of scope: use `Pointer.current`, ignore extra contacts.
- The mock has no keyboard controls. If you add any for desk testing, use `Keyboard.current` and keep them Editor-only.

**Touch sizing — this needs a deliberate change from the mock.** The serve hit test is `dist < Er(b) + 6` sim px. A tier-0 pastry at default `sizeScale` has `Er ≈ 17`, so the target is ~23 sim px across — roughly **4 mm on a phone**, well under the 7–9 mm minimum for a reliable tap.

Only bodies of the ordered tier are candidates and the test already picks the *nearest* one, so widening the tolerance cannot cause a mis-serve — it can only make the intended tap land. Use:

```csharp
float tolerance = Application.isMobilePlatform ? 16f : 6f;
if (dist < Er(b) + tolerance) { … }
```

Everything else is already finger-sized: the shake button is 252 × 46 stage px (≈ 610 × 111 device px on a 1080p phone) and aiming is a drag, not a tap.

Two more touch behaviours worth getting right:

- **The finger hides the pastry.** Aiming only uses the pointer's x, so this is cosmetic — but keep the aim guide and the held pastry drawn at the top of the cloth (y 42, per §8.5) rather than under the finger, which is what the mock already does. Do not add a follow-the-finger preview.
- **Reject taps that begin on the boost bar or top bar** so a mistimed press near the bottom edge does not also drop a pastry. The world-space Canvas handles this if the play area's pointer handler ignores events the UI consumed.

---

## 10. The Tweaks window — the design tool

**Model it on `Docs/web-reference/debug.html`.** Open that page: a collapsible side panel next to the live game, with live stats, preset pills, collapsible slider sections, an actions block, a JSON block, and toast messages. Rebuild that as an `EditorWindow` so the game can be tuned while it plays in the Game view. `Docs/mock/sketches/tweaks-panel-mockup.png` shows the visual treatment wanted (sections, right-aligned values, `Reset` / `Save as defaults` footer).

Menu path **Window ▸ Pan Dulce ▸ Tweaks**. IMGUI (`OnGUI`) is recommended for v1 — schema-driven rows are trivial in it and it survives domain reloads without UXML plumbing.

### 10.1 Schema-driven, always

The web panel builds itself from one array; adding a knob there is a single entry. Keep that property:

```csharp
public sealed class Knob {
    public string key, label, unit, help;
    public float min, max, step, def;
    public KnobKind kind;                      // Range, Int, Bool, Enum, Color
    public string[] options;                   // enum values / colour swatches
    public bool beyondMock;                    // Tier B (§6.3)
    public Func<TuningConfig, float> Get;
    public Action<TuningConfig, float> Set;    // bools 0/1, enums by index
}
public sealed class Section { public string title, icon; public Knob[] knobs; }
```

Explicit delegates, not reflection — compile-time safe, no GC while dragging. `GameRoot` reads the `TuningConfig` asset **every frame**, which is what makes edits apply on the next frame with no restart.

### 10.2 Panel blocks, top to bottom

1. **Header** — "Pan Dulce · Tweaks", a note that edits persist after exiting play mode, and a "Ping config asset" button.
2. **Live stats** (play mode only, repaint ~8 Hz via `EditorApplication.update`):
   FPS (warn < 45) · bodies · highest tier · physics ms/frame (warn > 8) · **charge %** · **next customer in** · current combo · tiers discovered.
   Measure sim ms with a `Stopwatch` around the substep loop, smoothed `ms += (sample − ms) * 0.1f`.
3. **Preset pills** — highlight the one matching the current diff-from-default:

   | Preset | Diff |
   | --- | --- |
   | Mock default | `{}` |
   | Floaty | gravity 650, bounciness 0.24, mergeGrowTime 1.3, rotationAmount 0.45 |
   | Snappy | gravity 2600, bounciness 0.02, mergeGrowTime 0.28, comboDelay 0.2 |
   | Chaos | gravity 2200, bounciness 0.45, rotationAmount 1, sizeScale 1.5, shakePower 2.2, chargePerMerge 0.5 |
   | Zen | gravity 900, bounciness 0.05, customerEverySec 40, mergeGrowTime 1.0 |

4. **Sections**, foldouts in mock order, first four open: **Physics · Merge · Customers · Furoshiki · Boost · Audio**, then a collapsed **Beyond the mock** holding every Tier B knob.
   Each row: label · right-aligned value with unit · control · help text in small grey. Ints snap; bools are toggles; `entranceStyle` is a popup; `clothColor` is four swatch buttons plus a free colour field. **Mark values that differ from default** and give each row a **↺** to restore just that knob.
5. **Actions** — see §10.3.
6. **Config JSON** — text area with the diff-from-default, `Copy` (`EditorGUIUtility.systemCopyBuffer`) and `Apply` (parse, clamp through the schema, ignore unknown keys). **Keep key names identical to the mock's props** so a config pastes straight into the mock's panel and back.
7. **Footer** — `Reset` (back to mock defaults) and `Save as defaults` (store the current values as a named preset slot). Toast line for "Preset applied", "Cloth emptied", etc.

### 10.3 Test buttons — the point of the tool

All disabled outside play mode, all routed through `GameRoot.Current`.

| Group | Buttons |
| --- | --- |
| **Run** | `Restart run` · `Pause` / `Resume` · **`Step one frame`** (invaluable for physics debugging) · `Time scale` slider |
| **Cloth** | `Empty the cloth` · `Fill ×8` · tier dropdown + `Drop this` · `Drop pair` (spawns at x = 80 and x = 300 so they roll down the curve into each other) · `Drop ×5 of tier` |
| **Progression** | `Reveal next tier` · `Reveal whole case` · `Re-lock case` |
| **Customers** | `Summon customer now` · order-tier dropdown + `Force this order` · `Complete order instantly` · `Skip customer` |
| **Boost** | `Fill charge` · `Fire shake` (ignores charge and cooldown) · `Empty charge` |
| **Day** | `Toggle end of day` · `Cycle cloth colour` |
| **Capture** | `Screenshot stage` (writes a PNG next to the project for design review) |

`Drop pair` and `Step one frame` are the two you will use most — the first to test merge behaviour on the curved floor without waiting, the second to inspect a single collision.

### 10.4 Editor correctness

- `Undo.RecordObject(cfg, "Tune " + knob.label)` then `EditorUtility.SetDirty(cfg)`; save on control release, not every drag frame.
- ScriptableObject edits **persist past play mode** — desirable here, and it means a tuning session is a committable diff on `Tuning.asset`. Say so in the header, keep `Reset` prominent.
- **Domain reload is off:** subscribe to `EditorApplication.update` in `OnEnable`, unsubscribe in `OnDisable`, never from a static constructor. Resolve `GameRoot.Current` fresh on each repaint instead of caching it.
- Repaint on `EditorApplication.playModeStateChanged` so buttons enable/disable correctly.
- **Scene-view gizmos** (in `[ 90 · DEBUG ]`): the walls, the drop line, and — most useful — the **curved floor** drawn as a polyline sampling `FY − floorSag·k²`, so the sag can be tuned visually. Plus body colliders and ids when `showColliders` / `showIds` are on.

### 10.5 Optional: the in-game Tweaks modal

`tweaks-panel-mockup.png` shows the same controls as a modal **inside the running game** — title, ✕, section headers, slider rows, `Reset` / `Save as defaults`. Build it from the **same schema** behind `#if DEVELOPMENT_BUILD || UNITY_EDITOR` so the game can be tuned on a phone. Treat it as a stretch goal after §10.2 works. Note the mockup mixes Spanish section headers with English keys — confirm the intended language before shipping that.

---

## 11. Mobile build settings and performance

### 11.1 Player settings to change — do this at M0

**Orientation (the one that is actually wrong right now).** All four autorotations are still enabled. Lock to portrait:

| Setting | Value |
| --- | --- |
| Default Orientation | **Portrait** |
| Allowed: Portrait | ✅ |
| Allowed: Portrait Upside Down | ❌ (upside-down portrait on a phone is a misfeature) |
| Allowed: Landscape Left / Right | ❌ |

A fixed portrait orientation also means `StageFitter` and `SafeAreaInset` only recompute on resolution change, not on every rotation.

**Android** — IL2CPP and ARM64-only are already set, which is what the Play Store requires. Remaining:

| Setting | Value | Why |
| --- | --- | --- |
| Minimum API Level | 24 (Android 7.0) | Linear colour space needs ES 3.0+; API 24 is a safe modern floor |
| Target API Level | Highest installed | Play Store enforces a recent target |
| Graphics APIs | **Vulkan**, then OpenGLES3 | Auto Graphics API off; Vulkan first is faster for 2D batching |
| Package name | real reverse-DNS | `com.DefaultCompany.candihub` will not ship |
| Managed Stripping | Low | Medium+ can strip code the Input System reflects over |

**iOS** — needs a Mac with Xcode, which you have.

| Setting | Value |
| --- | --- |
| Target minimum iOS | 13.0 |
| Target Device | iPhone + iPad (already `Universal`) — iPad gets side bars per §5.1 |
| Architecture | ARM64 |
| Requires ARKit / Persistent WiFi | off |

Also set the company name and product name, and supply an app icon and a solid-colour splash (`#cfa06b`) so the default Unity splash does not ship.

### 11.2 Performance — the budget is generous, don't waste it

The sim is cheap: 40 bodies is 780 pairs × 3 substeps ≈ 2 340 checks per frame. A mid-range phone handles that with room to spare. The risks are allocation and draw calls, not maths.

- `Application.targetFrameRate = 60` in a `[RuntimeInitializeOnLoadMethod]`, and `QualitySettings.vSyncCount = 0` (vSync is ignored on mobile; `targetFrameRate` is what governs).
- **Zero allocations in `Step()`.** No LINQ, no `List` created per frame, no lambdas capturing locals in the inner loop. Use a persistent merge buffer and a swap-remove for dead bodies rather than `bodies.Where(...).ToList()`. Profile with the Deep Profiler off and GC Alloc showing 0 B/frame in steady state.
- Pool bodies, particles and text (§3.6). No `Instantiate`/`Destroy` during play.
- Sprite Atlas + one unlit material (§8.1) — target **under 15 draw calls** for the whole stage.
- URP asset: MSAA already off (correct for 2D), Render Scale 1, HDR off, no post-processing volume, no shadows. Do not add a `Light2D` — unlit sprites skip the 2D lighting passes entirely.
- Cap resolution on very high-DPI devices if you see fill-rate cost: `Screen.SetResolution(1080, Mathf.RoundToInt(1080f * Screen.height / Screen.width), true)` when `Screen.width > 1200`. Measure before adding it.
- The 11 `AudioClip`s are generated procedurally at startup — a few ms, no files, no load hitch. Keep it that way rather than shipping WAVs.

### 11.3 Device testing loop

- **In-Editor:** the **Simulator** view (Game view dropdown → Simulator) for notch, safe area and aspect. This is the only way to catch §5.2 problems without a build.
- **On device:** Android over USB is the fast loop — `Build And Run`, then `adb logcat -s Unity` for console output. iOS needs an Xcode round-trip, so do layout work on Android and verify on iOS at milestone boundaries.
- **Profile on the weakest device you intend to support**, not in the Editor. Editor frame timings are meaningless for mobile budgets.
- Check thermals: play for 10 minutes and confirm the frame rate holds. A merge game gets long sessions.

---

## 12. Milestones and acceptance

| # | Milestone | Done when |
| --- | --- | --- |
| M0 | Environment | Project opens in `6000.5.6f1`; **orientation locked to portrait** and platform switched to Android (§11.1); camera at size 4.4 / `#cfa06b`; four asmdefs; sorting layers created; hierarchy folders in place (§4) |
| M1 | Config + Tweaks window | Every knob renders, JSON round-trips, ↺ and presets work — with no game yet |
| M2 | Sim core + cloth | Drop, collide, merge, walls, **curved floor + centre pull**, spawn pop, squish, spin. EditMode tests green |
| M3 | Shop loop | Timer, sign countdown, customer entrance, order bubble, serve flight, case reveal with the sliding window, "Thank you!" |
| M4 | Boost + end of day | Charge meter, shake with cloth wobble, fold-closed with knot and "Closing time" |
| M5 | Art + juice | 11 pastry sprites, 3 customers, window scene, display case, particles, floating text, six sounds |
| M6 | Mobile pass | Safe-area insets, bleed art, touch tolerance, atlas + ASTC, portrait lock — verified on a real device |
| M7 | Verification | Checklist below passes |

Commit at every milestone, on a branch. Keep `Main.unity` thin.

**EditMode tests** (Test Framework is installed — Core has no Unity object dependencies, so these need no scene): `Er()` at `spawnT` 0 / 0.55 / 1 · the 4:3:2:1 spawn distribution over 10 k samples · the merge gate rejecting a body younger than `comboDelay` · the floor curve returning `FY − 26` at the cloth edges and `FY` at the centre · `chargePerMerge` reaching exactly 1.0 after 8 merges.

**Feel**
- [ ] At defaults, pastries settle into a **bowl** — rolling toward the centre, sitting higher at the edges. Compare side by side with the mock in a browser.
- [ ] Merge products pop in over `mergeGrowTime` and cannot re-merge until `spawnT > 0.55` **and** age `> comboDelay`.
- [ ] A customer arrives every 18 s; the sign counts down whole seconds then switches to `now serving` / `♥`; the entrance matches the selected style.
- [ ] Serving flies the pastry to `(182, 224)` at scale 0.6, plays `serve`, bounces the customer twice, holds "Thank you!" 1.4 s, then resets the timer.
- [ ] First merge into a new tier shows "New in the case!"; past tier 4 the case **slides** so the newest is visible.
- [ ] Charge fills 14% per merge; at 100% the button reads `READY!` and pulses; shaking launches the pile, wobbles **only the cloth and its contents** (desk and NEXT plaque stay still), and empties the meter.
- [ ] `endOfDay` folds both flaps, ties the knot, shows "Closing time", and blocks dropping and shaking.
- [ ] All four cloth colours shade correctly — every highlight and shadow derives from the base colour, none hard-coded red.

**Tool**
- [ ] Every knob applies on the **next frame** in play mode with no restart — except `startingBodies`.
- [ ] Copy the JSON out, paste those values into the mock's prop panel, and both behave identically. Strongest end-to-end check there is.
- [ ] All test buttons work in play mode and are greyed out outside it.

**Mobile** — all checked on a real device, not the Editor
- [ ] Portrait-locked; rotating the phone does nothing.
- [ ] On a notched phone, the top bar sits **below** the status bar with its colour running under the notch, and the boost bar sits **above** the gesture bar. Verified in the Simulator view for an iPhone 15 Pro and a Pixel, then on hardware.
- [ ] Swiping up from the bottom does not fight the shake button.
- [ ] The letterbox slack shows **bleed art**, never flat `#cfa06b` bars. Check a 16:9 device and a tablet.
- [ ] Tapping a tier-0 pastry to serve it succeeds reliably with a thumb, not just a stylus-precise tap.
- [ ] 60 fps held for 10 minutes with ~40 bodies; no thermal throttle; **0 B/frame GC alloc** in steady state.
- [ ] Under 15 draw calls for the whole stage (Frame Debugger).
- [ ] No default Unity splash; real package name; app icon set.

**Hygiene**
- [ ] Zero console errors/warnings on load, entering play mode, and after 3 minutes of play.
- [ ] **Enter and exit play mode three times** — runs 2 and 3 behave exactly like run 1 (no doubled handlers, no `MissingReferenceException`, stats not ticking at 3×).
- [ ] No sprite renders black, including runtime-instantiated prefabs.
- [ ] `grep -rn "Input\.\(GetKey\|GetMouse\|mousePosition\|touches\)" Assets/PanDulce` returns nothing.
- [ ] Player build compiles and runs at 60 fps with ~40 bodies; physics ms under 8.
- [ ] `git status` clean of `Library/`; every asset has its `.meta`; `git lfs ls-files` lists sprites and fonts.

---

## 13. Decisions already made, and one real question

**Settled — don't re-litigate:**

- **Mobile, portrait-locked phones, touch only.** Tablets are supported by the fitter but not designed for.
- Editor `6000.5.6f1`, URP 2D, new Input System.
- Custom C# solver, not `Rigidbody2D`. Raise it before writing code if you disagree, not after.
- Sim in canvas pixels, y-down.
- Core is Unity-free; MonoBehaviours only tick and mirror it.
- One sprite per tier, uniformly scaled — no procedural drawing in Unity.
- Depth via sorting layers, never z offsets.
- Empty GameObjects as hierarchy folders, identity transform, no components.
- The Tweaks window is schema-driven; do not hand-write rows.
- Tuning lives in a committed ScriptableObject, not `EditorPrefs`.
- Content in prefabs; `Main.unity` stays thin.

**⚠️ Ask before M3.** `Docs/web-reference/` contains a **game layer the current design does not have**: a score (merge value × combo, +250 per new tier, +100 and up per customer), a stored high score, a **top-out fail state** (pastries resting above a danger line for a grace period end the run), a game-over card, and keyboard controls.

None of that is in the mock. The new `endOfDay` fold hints at a *closing time* framing instead — a day that ends rather than a bag that overflows. Don't silently port either version. Ask which:

1. **Pure sandbox** — mock as-is, no score, no fail state, `endOfDay` just a toggle.
2. **Closing-time run** — the day ends on a timer, `endOfDay` becomes the run-end trigger, a summary card replaces the game-over card.
3. **Keep top-out and score** on the new layout — needs a new danger line, which the cloth has no visual for yet.

Mobile makes this question sharper rather than softer: a sandbox with no end condition is a fine desk toy but a weak phone game — sessions need a natural stopping point, and options 2 and 3 both provide one. Option 2 (closing time) also fits the art that already exists.

One smaller question remains: whether **case discovery persists** across sessions. Both builds re-lock every run, which on mobile means a player who closes the app loses all progress — almost certainly wrong for the platform, but it is a design call, so confirm before adding `PlayerPrefs` persistence.

**Which platform first?** Android is the faster loop (`Build And Run` over USB, `adb logcat`) and its settings are already half-configured. Do layout and touch work there, verify on iOS at milestone boundaries. Nothing in the architecture depends on the choice.
