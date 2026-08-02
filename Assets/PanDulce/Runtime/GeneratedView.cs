using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Base for every view that generates its own child objects.
    ///
    /// The contract: children are created fresh on each OnEnable and flagged DontSave, so
    /// they never enter the scene file. That gives three things at once —
    ///   1. Main.unity stays thin (§4): it holds components and named parents, not leaves.
    ///   2. Play mode works. Private field references are NOT serialized, so a view built
    ///      only at edit time would wake up with every label null.
    ///   3. The Editor still previews the layout, because [ExecuteAlways] builds there too.
    ///
    /// Rebuilding is idempotent — ClearGenerated runs first — so enabling twice cannot
    /// duplicate anything.
    /// </summary>
    [ExecuteAlways]
    public abstract class GeneratedView : MonoBehaviour
    {
        [SerializeField] protected PastryDatabase database;

        /// <summary>Editor-time wiring. Serialized, unlike anything Build() produces.</summary>
        public void EditorAssign(PastryDatabase db)
        {
            database = db;
            Rebuild();
        }

        /// <summary>
        /// All generated children live under this node, never directly on the component's
        /// own GameObject. Views therefore hide themselves by disabling Content — disabling
        /// the component's GameObject would re-trigger OnEnable on the next Show and rebuild
        /// the view out from under itself.
        /// </summary>
        protected Transform Content { get; private set; }

        protected virtual void OnEnable() => Rebuild();

        public void Rebuild()
        {
            ClearGenerated();
            var go = new GameObject("Content") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            Content = go.transform;
            Build();
        }

        /// <summary>
        /// Drops generated content so the scene can be written without it. Unity warns
        /// ("references runtime script in scene file. Fixing!") if DontSave objects are
        /// parented under saved ones at save time, so the builder strips before saving and
        /// rebuilds afterwards.
        /// </summary>
        public void ClearForSave() => ClearGenerated();

        protected void SetVisible(bool visible)
        {
            if (Content != null && Content.gameObject.activeSelf != visible)
                Content.gameObject.SetActive(visible);
        }

        protected bool IsBuilt => Content != null;

        protected abstract void Build();

        protected void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
