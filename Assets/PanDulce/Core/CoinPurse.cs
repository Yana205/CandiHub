using System;
using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// Run-scoped spending money: serving customers pays in, boosts draw out
    /// (spec docs/superpowers/specs/2026-08-04-purchasable-boosts-design.md).
    /// Like Score, coins reset with the run — the game deliberately has no persistence.
    /// </summary>
    public sealed class CoinPurse
    {
        public int Coins { get; private set; }

        public event Action Changed;

        /// <summary>What a serve pays: coinBase + orderTier * coinPerTier.</summary>
        public static int ServePay(ISimConfig cfg, int orderTier)
            => Mathf.Max(0, cfg.CoinBase + orderTier * cfg.CoinPerTier);

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            Changed?.Invoke();
        }

        /// <summary>Deducts and returns true only when affordable — never goes negative.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || Coins < cost) return false;
            Coins -= cost;
            Changed?.Invoke();
            return true;
        }

        public void Reset()
        {
            if (Coins == 0) return;
            Coins = 0;
            Changed?.Invoke();
        }
    }
}
