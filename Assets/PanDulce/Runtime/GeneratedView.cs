using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Base for every view that builds its own children.
    ///
    /// The children are ordinary scene objects. Build() writes them ONCE — after that the view
    /// binds to whatever is actually in the scene, so a child you drag stays dragged and a
    /// child you delete stays deleted. That is the whole point: the hierarchy is the designer's
    /// surface, and code only supplies what serialization cannot carry.
    ///
    /// Two passes, both running Build():
    ///   - Author  — ViewFactory creates missing children and sets their initial rect.
    ///   - Bind    — ViewFactory finds existing children, re-hands them their procedural
    ///               sprite and material, and caches the view's field references. It creates
    ///               nothing, moves nothing, and returns null for anything deleted.
    ///
    /// Bind runs on every OnEnable, which is what keeps Play mode working: private field
    /// references are not serialized, so a view built only at edit time would otherwise wake
    /// up with every label null.
    ///
    /// Sim-driven views opt out via <see cref="Authorable"/> — see the note there.
    /// </summary>
    [ExecuteAlways]
    public abstract class GeneratedView : MonoBehaviour
    {
        const string ContentName = "Content";

        [SerializeField] protected PastryDatabase database;

        [Tooltip("Hand-drawn chrome art. Empty = the generated rounded rects are drawn instead.")]
        [SerializeField] protected UiSkin skin;

        [SerializeField, HideInInspector] Transform content;

        /// <summary>
        /// Set once the children have been written into the scene. From then on a name with no
        /// object behind it means the designer deleted it, not that it is missing.
        /// </summary>
        [SerializeField, HideInInspector] bool authored;

        /// <summary>
        /// Whether this view's children belong in the scene file.
        ///
        /// True for chrome, which is placed by hand. False for the sim-driven views, whose one
        /// child is repositioned from the physics every frame and whose Build() creates objects
        /// directly rather than through ViewFactory — authoring those would duplicate them on
        /// every bind. They keep the original DontSave rebuild.
        /// </summary>
        protected virtual bool Authorable => true;

        /// <summary>
        /// All generated children live under this node, never directly on the component's
        /// own GameObject. Views therefore hide themselves by disabling Content — disabling
        /// the component's GameObject would re-trigger OnEnable on the next Show and rebuild
        /// the view out from under itself.
        /// </summary>
        protected Transform Content => content;

        protected bool IsBuilt => content != null;

        /// <summary>
        /// True while Build() is writing the children out for the first time. A Build() that
        /// wants to set something layout-ish on its OWN transform — a rotation, a reset —
        /// must guard on this, or it re-applies the value on every bind and overwrites the
        /// designer.
        /// </summary>
        protected bool Authoring => !ViewFactory.BindOnly;

        protected abstract void Build();

        protected virtual void OnEnable() => Bind();

        /// <summary>Editor-time wiring. Serialized, unlike anything Build() produces.</summary>
        public void EditorAssign(PastryDatabase db)
        {
            database = db;
            Bind();
        }

        /// <summary>Editor-time wiring for the views that draw chrome plates.</summary>
        public void EditorAssignSkin(UiSkin s)
        {
            skin = s;
            Bind();
        }

        /// <summary>
        /// Re-links the view to the children already in the scene, authoring them first if it
        /// has never been written out. Creates nothing and moves nothing.
        /// </summary>
        public void Bind()
        {
            if (!Authorable) { LegacyRebuild(); return; }
            if (!authored || content == null) { Author(); return; }

            ViewFactory.BindOnly = true;
            try { Build(); }
            finally { ViewFactory.BindOnly = false; }
        }

        /// <summary>Kept for existing callers — refreshing a view means binding it.</summary>
        public void Rebuild() => Bind();

        /// <summary>Writes the children into the scene. First enable, and Re-author.</summary>
        public void Author()
        {
            if (!Authorable) { LegacyRebuild(); return; }

            content = EnsureContent(persistent: true);
            ViewFactory.BindOnly = false;
            Build();

            // A Content node left over from the old generated build is still DontSave; promote
            // the whole subtree so this pass is what finally lands in the scene file.
            Persist(content);
            authored = true;
            MarkDirty();
        }

        /// <summary>
        /// Throws the authored children away and writes them again from code — the way back
        /// from an edit gone wrong, and what Rebuild Stage does.
        /// </summary>
        [ContextMenu("Re-author From Code")]
        public void ReAuthor()
        {
            ClearGenerated();
            authored = false;
            Author();
        }

        /// <summary>
        /// Drops content that must not reach the scene file. Only the sim-driven views still
        /// have any: chrome children are authored objects now and are meant to be saved.
        /// </summary>
        public void ClearForSave()
        {
            if (!Authorable) ClearGenerated();
        }

        protected void SetVisible(bool visible)
        {
            if (content != null && content.gameObject.activeSelf != visible)
                content.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ internals

        Transform EnsureContent(bool persistent)
        {
            var t = content != null ? content : transform.Find(ContentName);
            if (t != null) return t;

            var go = new GameObject(ContentName);
            if (!persistent) go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>The original behaviour: discard everything and build it fresh each enable.</summary>
        void LegacyRebuild()
        {
            ClearGenerated();
            content = EnsureContent(persistent: false);
            ViewFactory.BindOnly = false;
            Build();
        }

        static void Persist(Transform t)
        {
            if (t == null) return;
            t.gameObject.hideFlags = HideFlags.None;
            for (int i = 0; i < t.childCount; i++) Persist(t.GetChild(i));
        }

        protected void ClearGenerated()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;

                // Ours: the Content node we author, plus anything still flagged DontSave by the
                // old build. A persistent child (e.g. OrderBubble under the customer folder)
                // belongs to the scene, and clearing it here would delete authored objects.
                bool ours = child.name == ContentName
                            || (child.hideFlags & HideFlags.DontSave) != 0;
                if (!ours) continue;

                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            content = null;
        }

        void MarkDirty()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}
