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
            OrderTier = rng.Next(2, 6);
            CustomerIndex = Served % 3;
            SetState(ShopState.Open);
            CustomerArrived?.Invoke(OrderTier);
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
            TimeLeft = CustomerEverySec(cfg);
            SecondsShown = Mathf.CeilToInt(TimeLeft);
            StateChanged?.Invoke(State);
        }
    }
}
