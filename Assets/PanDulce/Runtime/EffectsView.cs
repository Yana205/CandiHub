using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Owns the four pooled ParticleSystems (instantiated once from designer-editable
    /// prefabs) and emits them on sim events. Lives under ClothShakeRoot so merge and
    /// shake effects wobble with the cloth. Coordinates arrive in sim px, y-down.
    /// </summary>
    public sealed class EffectsView : MonoBehaviour
    {
        [SerializeField] GameObject mergePrefab, sparklePrefab, servePrefab, dustPrefab;

        ParticleSystem merge, sparkle, serve, dust;
        float dustUntil;
        int dustEmitPerBurst;
        float nextDustAt;

        public void EditorAssign(GameObject mergeP, GameObject sparkleP, GameObject serveP, GameObject dustP)
        {
            mergePrefab = mergeP; sparklePrefab = sparkleP; servePrefab = serveP; dustPrefab = dustP;
        }

        void Awake()
        {
            merge = Spawn(mergePrefab);
            sparkle = Spawn(sparklePrefab);
            serve = Spawn(servePrefab);
            dust = Spawn(dustPrefab);
            if (dust != null)
                dust.transform.localPosition = StageCoords.Stage(SimField.CX, SimField.FY);
        }

        ParticleSystem Spawn(GameObject prefab)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform);
            return go.GetComponent<ParticleSystem>();
        }

        static int Count(float baseCount, float intensity)
            => Mathf.Max(0, Mathf.RoundToInt(baseCount * Mathf.Max(0f, intensity)));

        static Color Tier(int tier)
            => Palette.TierFill[Mathf.Clamp(tier, 0, Palette.TierFill.Length - 1)];

        public void MergeBurst(Vector2 simPos, int tier, float radius, float intensity)
        {
            if (merge == null) return;
            merge.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            var shape = merge.shape;
            shape.radius = Mathf.Max(0.05f, radius * 0.5f * StageCoords.PX);

            var ep = new ParticleSystem.EmitParams
            {
                startColor = Color.Lerp(Palette.Cream, Tier(tier), 0.35f),
            };
            merge.Emit(ep, Count(9f + radius * 0.06f, intensity));

            if (sparkle != null)
            {
                sparkle.transform.localPosition = merge.transform.localPosition;
                sparkle.Emit(Count(6f, intensity));
            }
        }

        public void Discovery(Vector2 simPos, float intensity)
        {
            if (sparkle == null) return;
            sparkle.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            sparkle.Emit(Count(18f, intensity));
        }

        public void ServeBurst(Vector2 simPos, int tier, float intensity)
        {
            if (serve == null) return;
            serve.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            var ep = new ParticleSystem.EmitParams { startColor = Tier(tier) };
            serve.Emit(ep, Count(8f, intensity));
        }

        /// <summary>Parents the serve system to the flyer so rateOverDistance leaves a trail.</summary>
        public void FollowFlyer(Transform flyer)
        {
            if (serve == null || flyer == null) return;
            serve.transform.SetParent(flyer, false);
            serve.transform.localPosition = Vector3.zero;
            serve.Play();
        }

        public void StopFollow()
        {
            if (serve == null) return;
            serve.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            serve.transform.SetParent(transform, false);
        }

        public void ShakeDust(float duration, float intensity)
        {
            if (dust == null) return;
            dustUntil = Time.time + Mathf.Max(0.1f, duration);
            dustEmitPerBurst = Count(4f, intensity);
        }

        void Update()
        {
            if (dust == null || Time.time >= dustUntil || Time.time < nextDustAt) return;
            dust.Emit(dustEmitPerBurst);
            nextDustAt = Time.time + 0.05f;
        }
    }
}
