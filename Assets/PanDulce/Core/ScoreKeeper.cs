using System;
using UnityEngine;

namespace PanDulce.Core
{
    /// <summary>
    /// The score layer, ported verbatim from Docs/web-reference/js/game.js.
    /// Not in the mock — see the §13 decision. Best is in-memory only: neither existing
    /// build persists across sessions, and that was confirmed as intended.
    /// </summary>
    public sealed class ScoreKeeper
    {
        public int Score { get; private set; }
        public int Best { get; private set; }

        public event Action Changed;

        /// <summary>score += (tier + 1) * 10 * max(1, comboN)</summary>
        public void AddMerge(int newTier, int comboN)
        {
            Score += (newTier + 1) * 10 * Mathf.Max(1, comboN);
            Changed?.Invoke();
        }

        /// <summary>+250 the first time a tier is revealed.</summary>
        public void AddDiscovery()
        {
            Score += 250;
            Changed?.Invoke();
        }

        /// <summary>score += 100 + orderTier * 25</summary>
        public void AddServe(int orderTier)
        {
            Score += 100 + orderTier * 25;
            Changed?.Invoke();
        }

        /// <summary>Called when the run ends. Returns true if this run set a new best.</summary>
        public bool CommitBest()
        {
            if (Score <= Best) return false;
            Best = Score;
            Changed?.Invoke();
            return true;
        }

        public void ResetRun()
        {
            Score = 0;
            Changed?.Invoke();
        }
    }
}
