# Purchasable Boosts — Design

Date: 2026-08-04
Status: approved direction (mid-run strip, Day-old clearance first, run-scoped coins)

## Summary

Customers pay coins in addition to score. Coins buy consumable boosts from a small
strip next to the existing shake bar. First boost: **Day-old clearance** — pops all
tier-0 and tier-1 pastries from the case.

## Economy

- New `PanDulce.Core.CoinPurse`, mirroring `ScoreKeeper`: `Coins`, `Add(int)`,
  `TrySpend(int)`, `Reset()`, `Changed` event. Run-scoped; resets with the run,
  no persistence (matches existing no-save design).
- Earn on serve only: `coins = coinBase + orderTier * coinPerTier`
  (defaults 5 + tier*3; both tunable in `SimConfigData` / exposed via `ISimConfig`).
- `TopBarView` shows the coin count next to score.

## Boost: Day-old clearance

- Cost: `clearanceCost` tunable, default 30 (~2 serves).
- Effect: despawn every settled tier-0 and tier-1 body in the case. The pastry
  currently held/aiming is exempt.
- Each removed pastry plays the existing merge-pop particle effect + SFX.
- **No score and no BoostMeter charge** from removals (anti-farming): removal is a
  despawn path in `MergeSim`, never a merge.
- If no matching pastries exist, the purchase is denied (no coins spent) with the
  same wiggle feedback pattern as `BoostBarView.Deny`.

## Components

- `Core/CoinPurse.cs` (new) — currency state.
- `Core/MergeSim.cs` — add `RemoveUpToTier(int maxTier)` → returns removed count
  (or list of positions/tiers so the view can spawn effects).
- `Core/SimConfigData.cs` + `ISimConfig` — `coinBase`, `coinPerTier`, `clearanceCost`.
- `Runtime/ShopStripView.cs` (new) — one buy button with price tag; greyed when
  `Coins < cost`; exposes `ButtonRect` for GameRoot's stage hit-test.
- `Runtime/GameRoot.cs` — owns `CoinPurse`; on serve completion adds coins; on
  button hit: deny if nothing to clear, else `TrySpend` → `RemoveUpToTier(1)` →
  effects/SFX.
- `Runtime/TopBarView.cs` — coin display.
- Scene wiring for the new view (via `ViewFactory` / `StageBuilder` pattern).

## Testing

- Core tests (PanDulce.Tests): CoinPurse add/spend/deny; RemoveUpToTier removes
  only matching tiers, leaves held pastry, returns count; no boost charge on removal.
- Manual: buy with exactly enough coins; deny when broke; deny when case has no
  low tiers; effects fire per removed pastry.

## Future candidates (not in scope)

Cafecito (pause customer timer), Campana (summon/reroll order via existing
`ForceOrder`), shake refill (`BoostMeter.Fill()` already exists), Concha dorada
wildcard, Propina doble (2x payout window), Second chance anti-top-out, and an
end-of-day shop behind the `DayCycle` cloth fold.
