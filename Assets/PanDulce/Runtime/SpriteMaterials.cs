using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// One shared unlit material for every sprite in the game.
    ///
    /// The scene has no Light2D and the art is flat by design, so anything using URP's
    /// Sprite-LIT-Default renders black (handoff §2, trap 2). Routing every renderer
    /// through here makes that impossible to get wrong — including for objects built at
    /// runtime, which is where the trap usually bites.
    ///
    /// A single shared material is also what lets SpriteRenderer batching work, which is
    /// the difference between single-digit and triple-digit draw calls (§8.1).
    /// </summary>
    public static class SpriteMaterials
    {
        static Material unlit;

        public static Material Unlit
        {
            get
            {
                if (unlit != null) return unlit;

                Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Sprites/Default");

                unlit = new Material(shader)
                {
                    name = "PanDulceSpriteUnlit",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                return unlit;
            }
        }

        /// <summary>Domain reload is off, so a stale material would survive into the next run.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => unlit = null;
    }
}
