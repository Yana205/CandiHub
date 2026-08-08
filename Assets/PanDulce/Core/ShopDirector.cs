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
        /// so the bear never orders a dessert the player has not merged into existence yet.
        /// Null (tests, bare setups) keeps the classic unfiltered 2..5 roll.
        /// </summary>
        public Func<int, bool> Orderable;

        readonly System.Random rng;
        float happyUntil;

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

        /// <summary>A customer arrives and asks for a tier in 2..5 inclusive.</summary>
        public void OpenWindow()
        {
            OrderTier = PickOrder();
            CustomerIndex = Served % 3;
            SetState(ShopState.Open);
            CustomerArrived?.Invoke(OrderTier);
        }

        /// <summary>
        /// A roll over the upper half of the chain (2..Max), filtered to discovered tiers so
        /// every order is servable. If the whole band is still silhouettes, ask for the best
        /// dessert the player CAN make — never an impossible one.
        /// </summary>
        int PickOrder()
        {
            if (Orderable == null) return rng.Next(2, TierTable.Max + 1);

            int n = 0;
            Span<int> band = stackalloc int[TierTable.Count];
            for (int t = 2; t <= TierTable.Max; t++)
                if (Orderable(t)) band[n++] = t;
            if (n > 0) return band[rng.Next(0, n)];

            for (int t = TierTable.Max; t >= 0; t--)
                if (Orderable(t)) return t;
            return rng.Next(2, TierTable.Max + 1);
        }

        public void ForceOrder(int tier)
        {
            OrderTier = Mathf.Clamp(tier, 0, TierTable.Max);
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
