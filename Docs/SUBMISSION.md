# Pan Dulce 🍮

*A cozy bakery merge game — Unity 6 (URP 2D)*

## The game in one paragraph

Pan Dulce is a physics merge game set in a tiny Japanese-style bakery. You drop desserts into a box on the counter; when two identical desserts touch, they merge into the next dessert in the chain — **Mochi → Purin → Melon Pan → Choco Donut → Roll Cake**. While you build the pile, bear customers walk up to the counter and order a dessert. Press and hold the dessert they asked for and it flies out of the box into their paws, earning you coins and freeing space. The run ends when the pile overflows the box — so the game is a constant negotiation between *growing* desserts (merging up) and *selling* them (keeping the box breathable).

## The story ("why am I playing?")

You inherited the bakery from your grandmother — along with her famous recipe
book, whose pages are missing. Grandma never wrote her recipes down; you
recover them the way she made them: by baking. Every dessert you discover by
merging is a **recipe found**, and it lights up its silhouette in the glass
display case — the case *is* the recipe book, rebuilt in sugar. The bears are
her old regulars, still asking for their favorites; an order for a dessert you
haven't rediscovered yet is grandma's menu telling you what to bake next. Full
player-facing copy lives in `Docs/ITCH_PAGE.md`.

## Core loop

1. **Drop** — tap to drop the queued dessert into the box (a "next" plaque previews what's coming).
2. **Merge** — two identical desserts that touch combine into the next tier, with a pop animation and score.
3. **Serve** — a customer arrives on a timer and orders a dessert you've already discovered. Hold your finger on a matching one for 0.3 s and it flies to the counter. Serving pays coins (higher tiers pay more) and the shop briefly closes with a "Thank you!" before the next regular arrives.
4. **Survive** — desserts grow ~1.35× per merge, so the box fills fast. Cross the danger line at the top and stay there: game over, shown as a score card over the shop.

## Mechanics

- **Merge chain with skins.** Five gameplay tiers, but each tier has color variants (e.g. Matcha Mochi vs. Original Mochi). Variants are *skin tracks*, not tiers — only same-color desserts merge, which adds a matching constraint on top of the size puzzle without changing the physics.
- **Customers as the pressure valve.** Orders are rolled only from tiers the player has *discovered* this run, so a customer can ask for something not currently in the box — that order is a goal ("go build a Roll Cake"), not just a pickup. Serving is the only way to remove desserts, tying survival directly to the shop fantasy.
- **Economy.** Serving pays `base + tier × perTier` coins. Coins and score are run-scoped (no persistence, by design — it's an arcade run).
- **Two boosts.**
  - *Shake* — charged by merging (a meter fills per merge); spend it to shake the box and settle the pile.
  - *Clearance* — bought with coins; a paid escape hatch when the pile gets dangerous. This creates the first real economic decision: bank coins for score, or spend them to extend the run.
- **Day cycle.** The shop visually folds closed and open (cloth flaps over the box) around end-of-day moments, gating drops and the customer timer while the animation plays.

## Technical notes

The code is split into two assemblies: `PanDulce.Core` is a pure-C# deterministic simulation (physics, merge rules, customer director, economy, boost meter — all plain classes with events, covered by EditMode tests), and `PanDulce.Runtime` is the Unity view layer that renders and feeds input into it. All sizes flow through a single `TierTable` so art and collision can never disagree about a dessert's radius. The layout is authored in-scene over hand-drawn art (`LayoutArt` prefab as the designer surface), so the artist's Photoshop file and the physics bounds stay in lockstep.

## Roadmap — from prototype to store in 3 months

This prototype was built in 2–3 weeks. The program gives us roughly 3 months to take it to a store release; the pace so far makes that scope credible, because the hard part — a stable, tested merge sim with customers and an economy wired through it — already exists.

### Month 1 — depth & retention

Turn one good run into a reason to come back.

- **Persistent coins + skin store.** Skins already exist as data (variant tracks in `PastryDatabase`); coins start carrying across runs and buy cosmetic sets (matcha, berry, seasonal) in a between-run store. This is the game's first retention hook and its future monetization surface.
- **Combo scoring.** Chain merges within a short window multiply score/coins — the merge events already exist, this is a timer and a multiplier.
- **Named regulars with tastes.** The three bears get names, a favorite dessert, and a tip bonus for serving it — cheap content that makes the shop feel alive.

### Month 2 — progression & polish

Give the game a shape beyond the endless run.

- **Days as levels.** The day-cycle fold becomes real structure: each day has a quota (serve N customers, earn X coins), difficulty ramps by speeding the customer timer or tightening the box, and the fold closes the day with a results card.
- **Shop upgrades.** Persistent coins buy permanent upgrades between days: a wider box, a second "next" preview slot, new boost types (swap the queued dessert, freeze the customer timer).
- **Juice pass.** Music, fuller SFX, merge/serve feel, tutorialized first run — the polish that decides store reviews.

### Month 3 — release

- **Mobile build hardening.** Performance on low-end devices, safe-area and aspect-ratio coverage (the `StageFitter`/`SafeAreaInset` groundwork is already in), input edge cases.
- **Store readiness.** Icons, screenshots, store page copy, privacy compliance, basic analytics, closed test round → fix cycle → submission.

### Post-launch dream

- **Shop decoration.** Spend coins on furniture, wallpaper, and counter art — pure cosmetics that make the bakery feel *yours*, in the Animal Crossing tradition.
- **Rush hours & events.** Timed events where customers queue two at a time, or a "wedding order" asking for the max-tier dessert against the clock — the live-ops layer that keeps a merge game breathing.
- **Second chains.** A drinks chain (tea → latte → parfait) sharing the box with pastries, where cross-chain customers order a *set*.
