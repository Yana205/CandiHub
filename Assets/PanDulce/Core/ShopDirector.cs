using System;
using UnityEngine;

namespace PanDulce.Core
{
    public enum ShopState { Closed, Open, Happy }

    /// <summary>
    /// The customer loop: a timer (not a drop count) opens the window, a regular asks for
    /// one tier, serving them holds a "Thank you!" then closes and resets the timer (§7.6).
    /// </summary>
    public sealed class ShopDirector
    {
        public ShopState State { get; private set; } = ShopState.Closed;

        /// <summary>Tier the current customer wants, or -1 when there is no order.</summary>
        public int OrderTier { get; private set; } = -1;

        /// <summary>Which of the three regulars is at the counter — served % 3.</summary>
        public int CustomerIndex { get; private set; }

        public int Served { get; private set; }
        public float TimeLeft { get; private set; }
        public int SecondsShown { get; private set; }
        public bool ServeInFlight { get; set; }

        public event Action<ShopState> StateChanged;
        public event Action<int> CustomerArrived;   // orderTier

        /// <summary>
        /// Which tiers a customer may ask for — wired to MergeSim.IsDiscovered by GameRoot,
        /// so the bear never orders a dessert the player has never seen. Discovery, not the
        /// current pile: an order for something not on the cloth yet is the ask to go build it.
        /// Null (tests, bare setups) keeps an unfiltered roll over the whole chain.
        /// </summary>
        public Func<int, bool> Orderable;

        /// <summary>
        /// Tier the shop craves right now, or -1 for none — wired by GameRoot to "a roll
        /// cake is sitting in the box". The top of the chain cannot merge further, so a
        /// finished cake is pure dead weight until someone buys it; while one exists, the
        /// next customer orders it, every time. Deliberately bypasses the no-repeat guard.
        /// Null (tests, bare setups) means no cravings.
        /// </summary>
        public Func<int> Craving;

        readonly System.Random rng;
        float happyUntil;
        int lastOrder = -1;

        public ShopDirector(System.Random rng = null)
        {
            this.rng = rng ?? new System.Random();
        }

        public static int CustomerEverySec(ISimConfig cfg)
            => Mathf.Max(3, Mathf.RoundToInt(cfg.CustomerEverySec));

        public bool OrderActive => State == ShopState.Open && OrderTier >= 0 && !ServeInFlight;

        public void Tick(float dt, float now, ISimConfig cfg, bool timerRuns)
        {
            if (State == ShopState.Happy)
            {
                if (now >= happyUntil) Close(cfg);
                return;
            }

            if (State != ShopState.Closed || ServeInFlight || !timerRuns) return;

            TimeLeft -= dt;
            SecondsShown = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
            if (TimeLeft <= 0f)
            {
                TimeLeft = CustomerEverySec(cfg);
                OpenWindow();
            }
        }

        /// <summary>A customer arrives and asks for one of the revealed desserts.</summary>
        public void OpenWindow()
        {
            OrderTier = PickOrder();
            lastOrder = OrderTier;
            CustomerIndex = Served % 3;
            SetState(ShopState.Open);
            CustomerArrived?.Invoke(OrderTier);
        }

        /// <summary>
        /// An even roll over EVERY revealed dessert, simple ones included (Yana, 2026-08-10).
        /// The band used to start at tier 2, so the bear only ever wanted the hard half and a
        /// pile full of mochi was never worth anything. Revealed — not "sitting in the box":
        /// a colour in the glass case is a promise the player can build it, so asking for a
        /// dessert that is not on the cloth yet is the order doing its job, not a bug.
        /// If somehow nothing is revealed, ask for the best dessert the player CAN make.
        /// </summary>
        int PickOrder()
        {
            // The craving trumps the roll — see Craving. Discovery still gates it (a
            // debug-spawned cake in an early run must not conjure impossible orders).
            int crave = Craving != null ? Craving() : -1;
            if (crave >= 0 && crave <= TierTable.Max && (Orderable == null || Orderable(crave)))
                return crave;

            int n = 0;
            Span<int> band = stackalloc int[TierTable.Count];
            for (int t = 0; t <= TierTable.Max; t++)
                if (Orderable == null || Orderable(t)) band[n++] = t;

            if (n == 0)
            {
                for (int t = TierTable.Max; t >= 0; t--)
                    if (Orderable(t)) return t;
                return 0;
            }

            // No instant repeats: the same dessert twice running reads as a stuck shop,
            // so the previous order sits this roll out whenever there is any choice.
            if (n > 1 && lastOrder >= 0)
                for (int i = 0; i < n; i++)
                    if (band[i] == lastOrder) { band[i] = band[--n]; break; }

            return band[rng.Next(0, n)];
        }

        public void ForceOrder(int tier)
        {
            OrderTier = Mathf.Clamp(tier, 0, TierTable.Max);
            lastOrder = OrderTier;
            if (State != ShopState.Open) { CustomerIndex = Served % 3; SetState(ShopState.Open); }
        }

        /// <summary>The flying pastry landed: thank the customer and hold it.</summary>
        public void CompleteServe(float now, ISimConfig cfg)
        {
            ServeInFlight = false;
            Served++;
            happyUntil = now + cfg.HappyMs / 1000f;
            SetState(ShopState.Happy);
        }

        public void SkipCustomer(ISimConfig cfg) => Close(cfg);

        void Close(ISimConfig cfg)
        {
            OrderTier = -1;
            TimeLeft = CustomerEverySec(cfg);
            SecondsShown = Mathf.CeilToInt(TimeLeft);
            SetState(ShopState.Closed);
        }

        void SetState(ShopState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        public void Reset(ISimConfig cfg)
        {
            State = ShopState.Closed;
            OrderTier = -1;
            lastOrder = -1;
            CustomerIndex = 0;
            Served = 0;
            ServeInFlight = false;
            happyUntil = 0f;
            // The start delay pads ONLY this first countdown — a few calm moments to read
            // the shop before the bear shows up. Every later visit uses the plain cadence.
            TimeLeft = CustomerEverySec(cfg) + Mathf.Max(0f, cfg.StartDelaySec);
            SecondsShown = Mathf.CeilToInt(TimeLeft);
            StateChanged?.Invoke(State);
        }
    }
}
