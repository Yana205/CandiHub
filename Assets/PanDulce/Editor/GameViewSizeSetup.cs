using System;
using System.Reflection;
using PanDulce.Runtime;
using UnityEditor;
using UnityEngine;

namespace PanDulce.Editor
{
    /// <summary>
    /// Registers the project's Game view sizes so both developers test against the same shapes.
    ///
    /// Game view sizes live in Library/GameViewSizes.asset, which is machine-local and ignored
    /// by git — so without this they would have to be re-added by hand on every clone and after
    /// every Library rebuild. Registering them from code is the only way to share them.
    ///
    /// This reaches into UnityEditor internals (GameViewSizes has no public API). Every failure
    /// path degrades to a one-line warning rather than an exception, because a Unity upgrade
    /// renaming something must not break the Editor.
    /// </summary>
    [InitializeOnLoad]
    static class GameViewSizeSetup
    {
        /// <summary>
        /// Sized against §5.1. The phone entry is the one that matters day to day: it is the
        /// only preset where the design frame does not fill the screen, so it is the only one
        /// that proves the backdrop bleed is covering the slack.
        /// </summary>
        static readonly (string label, int w, int h)[] Sizes =
        {
            ("PanDulce Safe 446x900",    446,  900),   // zero slack — matches the HTML mock
            ("PanDulce iPhone 15",      1170, 2532),   // 226 px vertical slack, bleed visible
            ("PanDulce Pixel 7",        1080, 2400),   // a wider phone, less slack
            ("PanDulce iPad 11",        1668, 2388),   // height-limited: slack goes sideways
        };

        static GameViewSizeSetup()
        {
            // Deferred: touching the size groups during a domain reload can race the Game view.
            EditorApplication.delayCall += Register;
        }

        /// <summary>Manual re-run, for when a Library rebuild drops the sizes.</summary>
        [MenuItem("Pan Dulce/Register Game View Sizes")]
        internal static void Register()
        {
            try
            {
                foreach (var group in new[] { GameViewSizeGroupType.Standalone,
                                              GameViewSizeGroupType.Android,
                                              GameViewSizeGroupType.iOS })
                    foreach (var s in Sizes)
                        AddIfMissing(group, s.label, s.w, s.h);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PanDulce] Could not register Game view sizes: {e.Message}. " +
                                 $"Add {StageCoords.SafeW}x{StageCoords.SafeH} and 1170x2532 by hand.");
            }
        }

        static void AddIfMissing(GameViewSizeGroupType groupType, string label, int w, int h)
        {
            var editorAsm = typeof(UnityEditor.Editor).Assembly;

            var sizesType = editorAsm.GetType("UnityEditor.GameViewSizes");
            var sizeType = editorAsm.GetType("UnityEditor.GameViewSize");
            var kindType = editorAsm.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || kindType == null) return;

            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)
                                   ?.GetValue(null);
            if (instance == null) return;

            var group = sizesType.GetMethod("GetGroup")?.Invoke(instance, new object[] { (int)groupType });
            if (group == null) return;

            if (Contains(group, sizeType, label)) return;

            var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
            if (ctor == null) return;

            var size = ctor.Invoke(new[]
            {
                Enum.Parse(kindType, "FixedResolution"), (object)w, h, label
            });

            group.GetType().GetMethod("AddCustomSize")?.Invoke(group, new[] { size });
        }

        static bool Contains(object group, Type sizeType, string label)
        {
            var groupType = group.GetType();
            var count = groupType.GetMethod("GetTotalCount")?.Invoke(group, null) as int?;
            var getAt = groupType.GetMethod("GetGameViewSize");
            var baseText = sizeType.GetProperty("baseText");
            if (count == null || getAt == null || baseText == null) return true;  // fail closed

            for (int i = 0; i < count.Value; i++)
            {
                var entry = getAt.Invoke(group, new object[] { i });
                if (entry != null && (baseText.GetValue(entry) as string) == label) return true;
            }
            return false;
        }
    }
}
