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
        [SerializeField] ClothView cloth;
        [SerializeField] PastryViewPool bodies;
        [SerializeField] ParticleViewPool particles;
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
        [SerializeField] PointerInput pointer;
        [SerializeField] SfxPlayer sfx;

        // --- Core state ---
        public MergeSim Sim { get; private set; }
        public ShopDirector Shop { get; private set; }
        public BoostMeter Boost { get; private set; }
        public DayCycle Day { get; private set; }
        public ScoreKeeper Score { get; private set; }
        public TopOutWatch TopOut { get; private set; }

        public TuningConfig Tuning => tuning;
        public bool GameOver { get; private set; }
        public float PhysicsMs { get; private set; }

        readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        Body hovered;
        float serveTimeout = -1f;
        int flyingTier;

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            Current = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (tuning == null) tuning = ScriptableObject.CreateInstance<TuningConfig>();

            Sim = new MergeSim(tuning);
            Shop = new ShopDirector();
            Boost = new BoostMeter();
            Day = new DayCycle();
            Score = new ScoreKeeper();
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
            if (sfx != null) sfx.Muted = !tuning.SoundOn;

            float dt = Mathf.Min(0.032f, Time.deltaTime) * Mathf.Max(0.01f, tuning.TimeScale);
            bool frozen = tuning.Paused || GameOver;

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
            SyncViews();
            HandleServeTimeout(dt);
        }

        void UpdateHover()
        {
            hovered = null;
            if (pointer == null || !Shop.OrderActive || GameOver) return;
            float tol = Application.isMobilePlatform ? 16f : 6f;
            hovered = Sim.ServableAt(pointer.SimPosition.x, pointer.SimPosition.y, Shop.OrderTier, tol);
        }

        void SyncViews()
        {
            if (clothShakeRoot != null) clothShakeRoot.Sync(Sim.ShakeOffset());
            if (cloth != null) cloth.ClothColor = CurrentClothColor;

            if (bodies != null) bodies.Sync(Sim, tuning.SizeScale, Shop.OrderTier, hovered);
            if (particles != null) particles.Sync(Sim);
            if (floats != null) floats.Sync(Sim);

            bool canDrop = Day.CanDrop && Sim.CanDropNow && !GameOver;
            if (aim != null) aim.Sync(canDrop, pointer != null ? pointer.AimX : SimField.CX,
                                      Sim.CurTier, tuning.SizeScale);

            if (dangerLine != null)
                dangerLine.Sync(tuning.TopOut, tuning.ShowDangerLine, tuning.TopOutLine,
                                TopOut.Blinking, Sim.Now);

            if (fold != null) fold.Sync(Day.CloseT, CurrentClothColor);
            if (topBar != null) topBar.Sync(Shop.Served, Score.Score, Score.Best, Sim.NextTier);
            if (boostBar != null) boostBar.Sync(Boost.Charge, Boost.Ready, tuning.BoostsOn, Sim.Now);
            if (sign != null) sign.Sync(Shop.State, Shop.SecondsShown);
            if (displayCase != null) displayCase.Sync(Sim);
        }

        Color CurrentClothColor => Palette.ClothSwatches[clothSwatch % Palette.ClothSwatches.Length];
        int clothSwatch;

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
                TryShake();
                return;
            }

            // A hit on a matching pastry SERVES instead of dropping (§7.6).
            if (Shop.OrderActive && !serveFlight.Flying)
            {
                float tol = Application.isMobilePlatform ? 16f : 6f;
                Body target = Sim.ServableAt(simPos.x, simPos.y, Shop.OrderTier, tol);
                if (target != null) { Serve(target); return; }
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

        public void TryShake()
        {
            if (!tuning.BoostsOn || !Boost.Ready || !Day.CanShake || GameOver) return;
            Boost.Spend();
            Sim.DoShake();
        }

        /// <summary>Ignores charge and cooldown — for the Tweaks window's Fire shake button.</summary>
        public void ForceShake() => Sim.DoShake();

        void Serve(Body b)
        {
            flyingTier = b.tier;
            Vector2 stage = StageCoords.SimToStage(new Vector2(b.x, b.y));
            Sim.RemoveForServe(b);
            Shop.ServeInFlight = true;

            // Keep a timeout so a missed animation callback cannot strand the flyer (§7.6).
            serveTimeout = tuning.FlySec + 0.6f;

            if (serveFlight != null)
                serveFlight.Launch(flyingTier, stage, tuning.FlySec, CompleteServe);
            else
                CompleteServe();
        }

        void CompleteServe()
        {
            if (!Shop.ServeInFlight) return;
            serveTimeout = -1f;
            int orderTier = Mathf.Max(0, Shop.OrderTier);
            Shop.CompleteServe(Sim.Now, tuning);
            Score.AddServe(orderTier);
            if (sfx != null) sfx.Play("serve");
            if (customer != null) customer.Celebrate();
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

        public void CycleClothColor() => clothSwatch = (clothSwatch + 1) % Palette.ClothSwatches.Length;

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
            Day.Reset();
            Sim.ResetRun();
            Shop.Reset(tuning);
            Boost.Reset();
            Score.ResetRun();
            TopOut.Reset();
            if (gameOverCard != null) gameOverCard.Hide();
            if (bubble != null) bubble.Hide();
            if (customer != null) customer.Leave();
            if (serveFlight != null) serveFlight.Cancel();
        }

        // ---------------------------------------------------------------- events

        void OnMerged(int tier, Vector2 pos, int comboN)
        {
            Boost.AddMerge(tuning.ChargePerMerge);
            Score.AddMerge(tier, comboN);
            if (sfx != null) sfx.Play("merge", tier);
        }

        void OnTierDiscovered(int tier)
        {
            Score.AddDiscovery();
            if (sfx != null) sfx.Play("disco");
        }

        void OnShaken()
        {
            if (sfx != null) sfx.Play("shake");
        }

        void OnCustomerArrived(int orderTier)
        {
            if (sfx != null) sfx.Play("chime");
            if (customer != null) customer.Arrive(Shop.CustomerIndex, tuning.EntranceStyle, tuning.EntranceTime);
            if (bubble != null) bubble.Show(orderTier, Time.time);
        }

        void OnShopStateChanged(ShopState state)
        {
            if (state != ShopState.Closed) return;
            if (bubble != null) bubble.Hide();
            if (customer != null) customer.Leave();
        }

        // ---------------------------------------------------------------- editor hooks

        public void EditorWire(TuningConfig cfg, PastryDatabase db, Camera camera, Transform play,
                               ClothShaker shaker, ClothView clothView, PastryViewPool pastryPool,
                               ParticleViewPool particlePool, FloatingTextPool textPool,
                               AimGuideView aimGuide, DangerLineView danger, FoldView foldView,
                               TopBarView top, BoostBarView boost, OrderBubbleView orderBubble,
                               SignView signView, DisplayCaseView caseView, CustomerView customerView,
                               ServeFlightView flight, GameOverCard card, PointerInput input, SfxPlayer audio)
        {
            tuning = cfg; database = db; cam = camera; playRoot = play;
            clothShakeRoot = shaker; cloth = clothView; bodies = pastryPool;
            particles = particlePool; floats = textPool; aim = aimGuide;
            dangerLine = danger; fold = foldView; topBar = top; boostBar = boost;
            bubble = orderBubble; sign = signView; displayCase = caseView;
            customer = customerView; serveFlight = flight; gameOverCard = card;
            pointer = input; sfx = audio;
        }

        public PastryDatabase Database => database;
    }
}
