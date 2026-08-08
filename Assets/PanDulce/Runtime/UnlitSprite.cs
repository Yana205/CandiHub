using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Re-links the shared unlit sprite material on load.
    ///
    /// Prefab-saved renderers cannot reference SpriteMaterials.Unlit directly — it is a
    /// HideAndDontSave in-memory material, so a prefab would serialize a dead reference
    /// and render magenta. Asset-backed art that lives in prefabs (LayoutArt) carries this
    /// component instead; generated views keep assigning the material in code.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class UnlitSprite : MonoBehaviour
    {
        void OnEnable() => GetComponent<SpriteRenderer>().sharedMaterial = SpriteMaterials.Unlit;
    }
}
