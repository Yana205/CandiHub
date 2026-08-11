using PanDulce.Core;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// Owns the five pooled ParticleSystems (instantiated once from designer-editable
    /// prefabs) and emits them on sim events. Lives under ClothShakeRoot so merge and
    /// shake effects wobble with the cloth. Coordinates arrive in sim px, y-down.
    /// </summary>
    public sealed class EffectsView : MonoBehaviour
    {
        [SerializeField] GameObject mergePrefab, sparklePrefab, servePrefab, dustPrefab, spawnPrefab;

        ParticleSystem merge, sparkle, serve, dust, spawn;
        float dustUntil;
        int dustEmitPerBurst;
        float nextDustAt;

        // The serve trail, while it is following the flyer. trail* hold the prefab's own
        // values so the designer stays in charge of the look and StopFollow can restore it.
        Transform flyer;
        float flyerScale0, trailRate, trailSize;
        Color trailColor;

        public void EditorAssign(GameObject mergeP, GameObject sparkleP, GameObject serveP,
                                 GameObject dustP, GameObject spawnP)
        {
            mergePrefab = mergeP; sparklePrefab = sparkleP; servePrefab = serveP; dustPrefab = dustP;
            spawnPrefab = spawnP;
        }

        void Awake()
        {
            merge = Spawn(mergePrefab);
            sparkle = Spawn(sparklePrefab);
            serve = Spawn(servePrefab);
            dust = Spawn(dustPrefab);
            spawn = Spawn(spawnPrefab);
            if (dust != null)
                dust.transform.localPosition = StageCoords.Stage(SimField.CX, SimField.FY);
            if (serve != null)
            {
                trailRate = serve.emission.rateOverDistanceMultiplier;
                trailSize = serve.main.startSizeMultiplier;
                trailColor = serve.main.startColor.color;
            }
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

        /// <summary>
        /// Sparkles at a WORLD position — for chrome that lives outside sim space (the case
        /// slot on a reveal, the boost button going ready). Callers hand a renderer/transform
        /// position so the burst lands on the drawn thing wherever it was dragged.
        /// </summary>
        public void SparkleWorld(Vector3 worldPos, float intensity)
        {
            if (sparkle == null) return;
            sparkle.transform.position = worldPos;
            sparkle.Emit(Count(14f, intensity));
        }

        public void ServeBurst(Vector2 simPos, int tier, float intensity)
        {
            if (serve == null) return;
            serve.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            var ep = new ParticleSystem.EmitParams { startColor = Tier(tier) };
            serve.Emit(ep, Count(8f, intensity));
        }

        /// <summary>
        /// The "here it comes" puff as the next held pastry appears above the cloth.
        /// A hint, not a celebration — a third of MergeBurst's count, tinted only part of
        /// the way toward the tier so the cue stays legible without stealing the eye.
        /// </summary>
        public void NextReady(Vector2 simPos, int tier, float intensity)
        {
            if (spawn == null) return;
            spawn.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            var ep = new ParticleSystem.EmitParams
            {
                startColor = Palette.WithAlpha(Color.Lerp(Palette.Cream, Tier(tier), 0.45f), 0.7f),
            };
            spawn.Emit(ep, Count(3f, intensity));
        }

        /// <summary>
        /// The landing plop — a small cream puff where a dropped pastry first hits the
        /// pile or the cloth. Same whisper register as NextReady: feedback, not fanfare.
        /// </summary>
        public void LandPuff(Vector2 simPos, float intensity)
        {
            if (spawn == null) return;
            spawn.transform.localPosition = StageCoords.Stage(simPos.x, simPos.y);
            var ep = new ParticleSystem.EmitParams
            {
                startColor = Palette.WithAlpha(Palette.Cream, 0.6f),
            };
            spawn.Emit(ep, Count(4f, intensity));
        }

        /// <summary>
        /// Parents the serve system to the flyer so rateOverDistance leaves a trail. The
        /// system simulates in world space, so the puffs stay where they were dropped and
        /// the flyer pulls away from them. Density scales with intensity for the same
        /// reason every Emit() count goes through <see cref="Count"/>.
        /// </summary>
        public void FollowFlyer(Transform flyerTransform, float intensity)
        {
            if (serve == null || flyerTransform == null) return;
            flyer = flyerTransform;
            flyerScale0 = Mathf.Max(1e-4f, flyer.localScale.x);
            var em = serve.emission;
            em.rateOverDistance = trailRate * Mathf.Max(0f, intensity);
            Taper();
            serve.transform.SetParent(flyer, false);
            serve.transform.localPosition = Vector3.zero;
            serve.Play();
        }

        public void StopFollow()
        {
            if (serve == null) return;
            flyer = null;
            var main = serve.main;                          // back to the prefab's own look
            main.startSizeMultiplier = trailSize;
            main.startColor = trailColor;
            serve.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            serve.transform.SetParent(transform, false);
        }

        /// <summary>
        /// The flyer shrinks 1 → 0.6 on its way to the bear (§7.6). Narrowing and thinning
        /// the puffs by the same measure makes the trail taper toward the customer instead
        /// of ending in a wall of full-size poofs.
        /// </summary>
        void Taper()
        {
            float shrink = Mathf.Clamp01(flyer.localScale.x / flyerScale0);
            float taper = shrink * shrink;                  // 1 → ~0.36 across the flight
            var main = serve.main;
            main.startSizeMultiplier = trailSize * taper;
            main.startColor = Palette.WithAlpha(trailColor, trailColor.a * Mathf.Lerp(0.4f, 1f, taper));
        }

        public void ShakeDust(float duration, float intensity)
        {
            if (dust == null) return;
            dustUntil = Time.time + Mathf.Max(0.1f, duration);
            dustEmitPerBurst = Count(4f, intensity);
        }

        void Update()
        {
            if (flyer != null) Taper();

            if (dust == null || Time.time >= dustUntil || Time.time < nextDustAt) return;
            dust.Emit(dustEmitPerBurst);
            nextDustAt = Time.time + 0.05f;
        }
    }
}
