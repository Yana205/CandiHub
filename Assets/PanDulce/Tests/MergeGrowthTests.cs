using NUnit.Framework;
using PanDulce.Core;
using PanDulce.Runtime;
using UnityEditor;

namespace PanDulce.Tests
{
    /// <summary>
    /// Per-asset audit of the merge chain's growth. Unlike CoreSimTests this suite loads
    /// the REAL Pastries.asset, so it fails on data, not code: if a designer sizes a
    /// dessert so a merge shrinks or drifts outside the authored growth band, the failure
    /// names the exact pair. The band itself (target ± tolerance) lives on the asset too —
    /// Studio ▸ Merge growth is the control, this is the enforcement.
    /// </summary>
    public class MergeGrowthTests
    {
        const string AssetPath = "Assets/PanDulce/Config/Pastries.asset";

        static PastryDatabase Db()
        {
            var db = AssetDatabase.LoadAssetAtPath<PastryDatabase>(AssetPath);
            Assert.That(db, Is.Not.Null, $"No database at {AssetPath} — run Pan Dulce ▸ Rebuild Stage.");
            return db;
        }

        [Test]
        public void EveryMerge_HandsBackABiggerDessert()
        {
            var db = Db();
            for (int i = 1; i < TierTable.Count; i++)
                Assert.That(db.MergeGrowth(i), Is.GreaterThan(0f),
                            $"{db.Name(i - 1)} → {db.Name(i)}: merging must never shrink " +
                            "(Play size makes this tier smaller than the one below it).");
        }

        [Test]
        public void EveryMerge_GrowsWithinTheAuthoredBand()
        {
            var db = Db();
            float target = db.MergeGrowthTarget, tol = db.MergeGrowthTolerance;
            for (int i = 1; i < TierTable.Count; i++)
                Assert.That(db.MergeGrowth(i), Is.InRange(target - tol, target + tol),
                            $"{db.Name(i - 1)} → {db.Name(i)}: growth is outside " +
                            $"+{(target - tol) * 100f:F0}%..+{(target + tol) * 100f:F0}% — " +
                            "fix the Play size or press Studio ▸ Even out growth.");
        }
    }
}
