using System.Collections.Generic;
using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Composition root. Creates the Core objects, ticks them, and mirrors them onto views.
    /// Nothing else owns sim state.
    ///
    /// Domain reload is OFF in this project, so statics survive between play sessions —
    /// Current is explicitly reset via RuntimeInitializeOnLoadMethod, and every event is
    /// unsubscribed in OnDisable. Missing either produces a second run that behaves subtly
    /// differently from the first (handoff §2, trap 1).
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        public static GameRoot Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [Header("Config")]
        [SerializeField] TuningConfig tuning;
        [SerializeField] PastryDatabase database;

        [Header("Scene wiring")]
        [SerializeField] Camera cam;
        [SerializeField] Transform playRoot;
        [SerializeField] ClothShaker clothShakeRoot;
        [SerializeField] PastryViewPool bodies;
        [SerializeField] FloatingTextPool floats;
        [SerializeField] AimGuideView aim;
        [SerializeField] DangerLineView dangerLine;
        [SerializeField] FoldView fold;
        [SerializeField] TopBarView topBar;
        [SerializeField] BoostBarView boostBar;
        [SerializeField] OrderBubbleView bubble;
        [SerializeField] SignView sign;
        [SerializeField] DisplayCaseView displayCase;
        [SerializeField] CustomerView customer;
        [SerializeField] ServeFlightView serveFlight;
        [SerializeField] GameOverCard gameOverCard;
        [SerializeField] EffectsView effects;
        [SerializeField] NextPlaqueView nextPlaque;
        [SerializeField] PointerInput pointer;
        [SerializeField] SfxPlayer sfx;

        // --- Core state ---
        public MergeSim Sim { get; private set; }
        public ShopDirector Shop { get; private set; }
        public BoostMeter Boost { get; private set; }
        public DayCycle Day { get; private set; }
        public ScoreKeeper Score { get; private set; }
        public CoinPurse Purse { get; private set; }
        public TopOutWatch TopOut { get; private set; }

        /// <summary>Day-old clearance pops tiers 0..this (spec 2026-08-04).</summary>
        public const int ClearanceMaxTier = 1;

        public TuningConfig Tuning => tuning;
        public bool GameOver { get; private set; }
        public float PhysicsMs { get; private set; }

        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        readonly List<(Vector2 pos, int tier)> clearedBuffer = new List<(Vector2, int)>(32);
        Body hovered;
        float serveTimeout = -1f;
        int flyingTier;
        int pendingBubbleTier = -1;     // bubble waits for the walk-in to finish
        float pendingBubbleAt;
        bool heldShown;                 // last frame's aim-guide visibility, for the spawn cue
        float runClock;                 // real seconds since run start — feeds the opening calm

        // Hold-to-serve (Yana, 2026-08-08): a press on the wanted dessert swells it for
        // HoldServeSec, then it flies. Release early = cancel; and any press that BEGAN on
        // the dessert never falls through to a drop, so a mistimed tap can neither serve
        // nor dump a pastry onto the pile.
        const float HoldServeSec = 0.3f;
        Body holdTarget;
        float holdT;
        bool pressOnDessert;
        bool wasPointerDown;

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            Current = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (tuning == null) tuning = ScriptableObject.CreateInstance<TuningConfig>();

            Sim = new MergeSim(tuning);
            Shop = new ShopDirector();
            // Orders only ever ask for desserts the pile can produce right now.
            Shop.Orderable = t => Sim.IsDiscovered(t);
            // Which color tracks exist, and which tiers each has art for, is the painted
            // sprites' call — the sim just asks. Reaching the donut opens the color rolls.
            Sim.SkinTrackCount = database != null ? database.SkinCount : 1;
            Sim.SkinHasArt = (tier, track) => database != null && database.HasVariant(tier, track);
            Boost = new BoostMeter();
            Day = new DayCycle();
            Score = new ScoreKeeper();
            Purse = new CoinPurse();
            TopOut = new TopOutWatch();

            Shop.Reset(tuning);
        }

        void OnEnable()
        {
            Sim.Merged += OnMerged;
            Sim.TierDiscovered += OnTierDiscovered;
            Sim.Shaken += OnShaken;
            Shop.CustomerArrived += OnCustomerArrived;
            Shop.StateChanged += OnShopStateChanged;
            if (pointer != null) pointer.Released += OnPointerReleased;
        }

        void OnDisable()
        {
            // Mandatory: a missed unsubscribe survives into the next play session.
            Sim.Merged -= OnMerged;
            Sim.TierDiscovered -= OnTierDiscovered;
            Sim.Shaken -= OnShaken;
            Shop.CustomerArrived -= OnCustomerArrived;
            Shop.StateChanged -= OnShopStateChanged;
            if (pointer != null) pointer.Released -= OnPointerReleased;
            if (Current == this) Current = null;
        }

        // ---------------------------------------------------------------- frame

        void Update()
        {
            if (tuning == null) return;
            Sim.SetConfig(tuning);
            // The authored case order doubles as the spawn menu; progress mode keeps the
            // classic tier 0–3 pick. Re-pushed each frame so Studio edits apply live.
            Sim.SetSpawnPool(displayCase != null && !displayCase.FollowProgress
                             ? displayCase.SeatTiers : null);
            if (sfx != null) sfx.Muted = !tuning.SoundOn;

            // The opening calm: the run's first seconds play slowed and ease up to full
            // tempo. It scales the same dt everything ticks on, so the pile, merges and
            // the customer clock all breathe together — input stays real-time.
            float rawDt = Mathf.Min(0.032f, Time.deltaTime);
            float dt = rawDt * Mathf.Max(0.01f, tuning.TimeScale)
                     * StartCalm.Scale(runClock, tuning.StartCalmSec, tuning.StartCalmScale);
            bool frozen = tuning.Paused || GameOver;
            if (!frozen) runClock += rawDt;

            Day.Tick(dt, tuning.EndOfDay);

            if (!frozen)
            {
                watch.Restart();
                Sim.Tick(dt);
                watch.Stop();
                float sample = (float)watch.Elapsed.TotalMilliseconds;
                PhysicsMs += (sample - PhysicsMs) * 0.1f;

                Shop.Tick(dt, Sim.Now, tuning, Day.TimerRuns);

                if (TopOut.Tick(dt, Sim.Bodies, Sim.Now, tuning)) EndRun();
            }

            UpdateHover();
            UpdateServeHold(rawDt);
            SyncViews();
            HandleServeTimeout(dt);
        }

        /// <summary>
        /// The serve press: began on the wanted dessert, held on it for HoldServeSec →
        /// serve. Runs on REAL time — the opening calm slows the pile, not the player's
        /// hands. hovered is this frame's servable hit, so "slid off" and "it merged
        /// away mid-press" are both just hovered != holdTarget.
        /// </summary>
        void UpdateServeHold(float rawDt)
        {
            bool down = pointer != null && pointer.IsDown;
            if (down && !wasPointerDown)
            {
                holdTarget = hovered;
                holdT = 0f;
                pressOnDessert = holdTarget != null;
            }
            wasPointerDown = down;

            if (holdTarget == null) return;
            if (!down || GameOver || hovered != holdTarget) { holdTarget = null; return; }

            holdT += rawDt;
            if (holdT < HoldServeSec) return;
            Body target = holdTarget;
            holdTarget = null;
            Serve(target);
        }

        /// <summary>
        /// The bell announces the visit, but the ORDER stays secret until the bear reaches
        /// the counter: rings, hover and tap-to-serve all reveal with the bubble, not the
        /// chime. pendingBubbleTier doubles as the walk-in tracker, so everything unlocks
        /// on the exact frame the bubble pops.
        /// </summary>
        bool OrderRevealed => Shop.OrderActive && pendingBubbleTier < 0;

        void UpdateHover()
        {
            hovered = null;
            if (pointer == null || !OrderRevealed || GameOver) return;
            float tol = Application.isMobilePlatform ? 16f : 6f;
            hovered = Sim.ServableAt(pointer.SimPosition.x, pointer.SimPosition.y, Shop.OrderTier, tol);
        }

        void SyncViews()
        {
            if (pendingBubbleTier >= 0 && Time.time >= pendingBubbleAt)
            {
                if (bubble != null) bubble.Show(pendingBubbleTier, Time.time);
                if (sfx != null) sfx.Play("pop");   // the reveal gets its own beat
                pendingBubbleTier = -1;
            }

            if (clothShakeRoot != null) clothShakeRoot.Sync(Sim.ShakeOffset());

            // Rings only once the bear has arrived, and they drop when the serve launches.
            if (bodies != null) bodies.Sync(Sim, tuning.SizeScale,
                                            OrderRevealed ? Shop.OrderTier : -1, hovered,
                                            holdTarget, holdT / HoldServeSec);
            if (floats != null) floats.Sync(Sim);

            bool canDrop = Day.CanDrop && Sim.CanDropNow && !GameOver;
            if (aim != null) aim.Sync(canDrop, pointer != null ? pointer.AimX : SimField.CX,
                                      Sim.CurTier, Sim.CurSkin, tuning.SizeScale,
                                      tuning.WallLeft, tuning.WallRight);

            // "Here it comes": the held pastry is HIDDEN for the whole drop cooldown, so the
            // moment it visually appears above the cloth is this rising edge — not the Drop()
            // that queued it. Firing from the edge also covers the start-of-day grace, the
            // fold reopening and Restart, which Drop() never sees.
            if (canDrop && !heldShown && aim != null && effects != null)
                effects.NextReady(aim.HeldSimPos, Sim.CurTier, tuning.ParticleScale);
            heldShown = canDrop;

            if (dangerLine != null)
                dangerLine.Sync(tuning.TopOut, tuning.ShowDangerLine, tuning.TopOutLine,
                                TopOut.Blinking, Sim.Now);

            if (fold != null) fold.Sync(Day.CloseT, CurrentClothColor);
            if (topBar != null) topBar.Sync(Shop.Served, Purse.Coins);
            if (boostBar != null)
            {
                boostBar.Sync(Boost.Charge, Boost.Ready, tuning.BoostsOn, Sim.Now);
                boostBar.SyncClearance(Purse.Coins, tuning.ClearanceCost,
                                       Sim.HasAnyUpToTier(ClearanceMaxTier), Sim.Now);
            }
            // arriving == the walk-in is still pending; NOT !OrderRevealed, which also
            // covers the serve flight — the sign must keep reading "now serving" then.
            if (sign != null) sign.Sync(Shop.State, Shop.SecondsShown, pendingBubbleTier >= 0);
            if (displayCase != null) displayCase.Sync(Sim);
            if (nextPlaque != null) nextPlaque.Sync(Sim.NextTier, Sim.NextSkin);
        }

        Color CurrentClothColor
            => Palette.ClothSwatches[Mathf.Abs(tuning.ClothColorIndex) % Palette.ClothSwatches.Length];

        // ---------------------------------------------------------------- input

        void OnPointerReleased(Vector2 simPos)
        {
            if (GameOver)
            {
                // The card's only control is Play again.
                if (gameOverCard != null && HitStage(simPos, gameOverCard.ButtonRect)) Restart();
                return;
            }

            if (boostBar != null && tuning.BoostsOn && HitStage(simPos, boostBar.ButtonRect))
            {
                if (!TryShake()) boostBar.Deny(Sim.Now);
                return;
            }

            if (boostBar != null && tuning.BoostsOn && HitStage(simPos, boostBar.ClearanceRect))
            {
                if (!TryClearance()) boostBar.DenyClearance(Sim.Now);
                return;
            }

            // Serving is HOLD-based (UpdateServeHold) — a release never serves. Any press
            // that began on the wanted dessert is fully consumed here, and so is a release
            // over it, so neither a too-short hold nor a stray tap dumps a pastry onto the
            // pile the player was aiming at.
            if (pressOnDessert)
            {
                pressOnDessert = false;
                return;
            }
            if (OrderRevealed)
            {
                float tol = Application.isMobilePlatform ? 16f : 6f;
                if (Sim.ServableAt(simPos.x, simPos.y, Shop.OrderTier, tol) != null) return;
            }

            if (Day.CanDrop && Sim.Drop(pointer.AimX, true) && sfx != null) sfx.Play("drop");
        }

        /// <summary>Stage-space hit test — the bars live in stage px, not sim px.</summary>
        static bool HitStage(Vector2 simPos, Rect stageRect)
        {
            Vector2 stage = StageCoords.SimToStage(simPos);
            return stageRect.Contains(stage);
        }

        // ---------------------------------------------------------------- actions

        public bool TryShake()
        {
            if (!tuning.BoostsOn || !Boost.Ready || !Day.CanShake || GameOver) return false;
            Boost.Spend();
            Sim.DoShake();
            return true;
        }

        /// <summary>Ignores charge and cooldown — for the Tweaks window's Fire shake button.</summary>
        public void ForceShake() => Sim.DoShake();

        /// <summary>
        /// Day-old clearance: pay coins, pop every tier-0/1 pastry. Denies without
        /// spending when broke or when there is nothing to clear (spec 2026-08-04).
        /// </summary>
        public bool TryClearance()
        {
            if (!tuning.BoostsOn || !Day.CanShake || GameOver) return false;
            if (!Sim.HasAnyUpToTier(ClearanceMaxTier)) return false;
            if (!Purse.TrySpend(tuning.ClearanceCost)) return false;

            clearedBuffer.Clear();
            Sim.RemoveUpToTier(ClearanceMaxTier, clearedBuffer);
            if (effects != null)
                for (int i = 0; i < clearedBuffer.Count; i++)
                    effects.MergeBurst(clearedBuffer[i].pos, clearedBuffer[i].tier,
                                       TierTable.EffectiveRadius(clearedBuffer[i].tier, tuning),
                                       tuning.ParticleScale);
            Sim.AddFloat(SimField.CX, 200f, "Day-old clearance!");
            if (sfx != null) sfx.Play("serve");
            return true;
        }

        void Serve(Body b)
        {
            flyingTier = b.tier;
            int flyingSkin = b.skin;
            Vector2 stage = StageCoords.SimToStage(new Vector2(b.x, b.y));
            Sim.AddFloat(b.x, b.y - TierTable.Er(b, tuning) - 8f,
                         $"+${CoinPurse.ServePay(tuning, b.tier)}");
            if (effects != null) effects.ServeBurst(new Vector2(b.x, b.y), b.tier, tuning.ParticleScale);
            Sim.RemoveForServe(b);
            Shop.ServeInFlight = true;

            // Keep a timeout so a missed animation callback cannot strand the flyer (§7.6).
            serveTimeout = tuning.FlySec + 0.6f;

            if (serveFlight != null)
            {
                serveFlight.Launch(flyingTier, flyingSkin, stage, tuning.FlySec, CompleteServe);
                if (effects != null) effects.FollowFlyer(serveFlight.FlyerTransform, tuning.ParticleScale);
            }
            else
                CompleteServe();
        }

        void CompleteServe()
        {
            if (!Shop.ServeInFlight) return;
            serveTimeout = -1f;
            if (effects != null) effects.StopFollow();
            int orderTier = Mathf.Max(0, Shop.OrderTier);
            Shop.CompleteServe(Sim.Now, tuning);
            Score.AddServe(orderTier);
            Purse.Add(CoinPurse.ServePay(tuning, orderTier));
            if (sfx != null) sfx.Play("serve");
            if (customer != null) customer.Celebrate();
            pendingBubbleTier = -1;
            if (bubble != null) bubble.Hide();
        }

        void HandleServeTimeout(float dt)
        {
            if (serveTimeout < 0f) return;
            serveTimeout -= dt;
            if (serveTimeout > 0f) return;
            if (serveFlight != null) serveFlight.Cancel();
            CompleteServe();
        }


        // ---------------------------------------------------------------- run lifecycle

        void EndRun()
        {
            if (GameOver) return;
            GameOver = true;
            Day.ForcedClosed = true;                 // the bakery closes; the fold does the talking
            bool newBest = Score.CommitBest();
            if (serveFlight != null) serveFlight.Cancel();
            Shop.ServeInFlight = false;
            if (gameOverCard != null) gameOverCard.Show(Score.Score, Score.Best, newBest);
        }

        public void Restart()
        {
            GameOver = false;
            serveTimeout = -1f;
            pendingBubbleTier = -1;
            heldShown = false;          // the first pastry of the new run gets its cue
            runClock = 0f;              // the new run opens calm again
            holdTarget = null;
            pressOnDessert = false;
            Day.Reset();
            Sim.ResetRun();
            Shop.Reset(tuning);
            Boost.Reset();
            Score.ResetRun();
            Purse.Reset();
            TopOut.Reset();
            if (gameOverCard != null) gameOverCard.Hide();
            if (bubble != null) bubble.Hide();
            if (customer != null) customer.Leave();
            if (serveFlight != null) serveFlight.Cancel();
            if (effects != null) effects.StopFollow();
        }

        // ---------------------------------------------------------------- events

        void OnMerged(int tier, Vector2 pos, int comboN)
        {
            Boost.AddMerge(tuning.ChargePerMerge);
            Score.AddMerge(tier, comboN);
            // Burst radius follows the dessert's real size, so a 200% purin bursts 200% wide.
            if (effects != null)
                effects.MergeBurst(pos, tier, TierTable.EffectiveRadius(tier, tuning), tuning.ParticleScale);
            if (sfx != null) sfx.Play("merge", tier);
        }

        void OnTierDiscovered(int tier, Vector2 pos)
        {
            Score.AddDiscovery();
            if (effects != null) effects.Discovery(pos, tuning.ParticleScale);
            if (sfx != null) sfx.Play("disco");
        }

        void OnShaken()
        {
            if (effects != null) effects.ShakeDust(tuning.ShakeDuration, tuning.ParticleScale);
            if (sfx != null) sfx.Play("shake");
        }

        void OnCustomerArrived(int orderTier)
        {
            if (sfx != null) sfx.Play("chime");
            if (customer != null) customer.Arrive(tuning.EntranceTime);
            pendingBubbleTier = orderTier;
            pendingBubbleAt = Time.time + tuning.EntranceTime;
        }

        void OnShopStateChanged(ShopState state)
        {
            if (state != ShopState.Closed) return;
            pendingBubbleTier = -1;
            if (bubble != null) bubble.Hide();
            // Waddle out, don't vanish — Restart still hard-Leaves after this fires.
            if (customer != null) customer.Depart(tuning.EntranceTime);
        }

        // ---------------------------------------------------------------- editor hooks

        public void EditorWire(TuningConfig cfg, PastryDatabase db, Camera camera, Transform play,
                               ClothShaker shaker, PastryViewPool pastryPool,
                               FloatingTextPool textPool,
                               AimGuideView aimGuide, DangerLineView danger, FoldView foldView,
                               TopBarView top, BoostBarView boost, OrderBubbleView orderBubble,
                               SignView signView, DisplayCaseView caseView, CustomerView customerView,
                               ServeFlightView flight, GameOverCard card, NextPlaqueView plaque,
                               EffectsView effectsView, PointerInput input, SfxPlayer audio)
        {
            tuning = cfg; database = db; cam = camera; playRoot = play;
            clothShakeRoot = shaker; bodies = pastryPool;
            floats = textPool; aim = aimGuide;
            dangerLine = danger; fold = foldView; topBar = top; boostBar = boost;
            bubble = orderBubble; sign = signView; displayCase = caseView;
            customer = customerView; serveFlight = flight; gameOverCard = card;
            nextPlaque = plaque; effects = effectsView; pointer = input; sfx = audio;
        }

        public PastryDatabase Database => database;
    }
}
