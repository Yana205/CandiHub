using System.Collections.Generic;
using UnityEngine;

namespace PanDulce.Runtime
{
    /// <summary>
    /// The procedural cues (§9.1), baked once at startup with AudioClip.Create.
    /// A few ms of maths, no files, no load hitch — deliberately kept that way rather than
    /// shipping WAVs. There is no game-over cue in this design.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        const int SampleRate = 44100;

        struct Tone
        {
            public float freq, start, dur, vol;
            public int wave;   // 0 sine, 1 triangle
            public Tone(float f, float s, float d, int w, float v) { freq = f; start = s; dur = d; wave = w; vol = v; }
        }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource source;
        bool built;

        public bool Muted { get; set; }

        void Awake() => Build();

        void Build()
        {
            if (built) return;
            built = true;

            source = gameObject.GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;

            clips["drop"] = Bake("drop", new[] { new Tone(180f, 0f, 0.10f, 0, 0.12f) });

            // merge is pitched per tier: f = 260 + tier*55
            for (int t = 0; t < Core.TierTable.Count; t++)
            {
                float f = 260f + t * 55f;
                clips["merge" + t] = Bake("merge" + t, new[]
                {
                    new Tone(f, 0f, 0.12f, 1, 0.16f),
                    new Tone(f * 1.5f, 0.06f, 0.14f, 1, 0.12f),
                });
            }

            clips["disco"] = Bake("disco", new[]
            {
                new Tone(660f, 0f, 0.12f, 0, 0.12f),
                new Tone(880f, 0.09f, 0.16f, 0, 0.12f),
                new Tone(1100f, 0.18f, 0.20f, 0, 0.10f),
            });

            clips["chime"] = Bake("chime", new[]
            {
                new Tone(1318f, 0f, 0.55f, 0, 0.09f),
                new Tone(1760f, 0.02f, 0.60f, 0, 0.055f),
                new Tone(2637f, 0.04f, 0.30f, 0, 0.03f),
                new Tone(1568f, 0.24f, 0.55f, 0, 0.07f),
                new Tone(2093f, 0.26f, 0.50f, 0, 0.04f),
            });

            // the order bubble popping in at the counter — softer and rounder than the
            // door chime, so bell = "someone's coming", pop = "here's what they'd like"
            clips["pop"] = Bake("pop", new[]
            {
                new Tone(880f, 0f, 0.07f, 0, 0.10f),
                new Tone(1175f, 0.05f, 0.13f, 0, 0.08f),
            });

            clips["serve"] = Bake("serve", new[]
            {
                new Tone(523f, 0f, 0.12f, 0, 0.13f),
                new Tone(659f, 0.09f, 0.12f, 0, 0.13f),
                new Tone(784f, 0.18f, 0.22f, 0, 0.13f),
            });

            var shake = new List<Tone>(7);
            var rng = new System.Random(4242);   // stable across sessions; the cue is baked once
            for (int i = 0; i < 6; i++)
                shake.Add(new Tone(300f + (float)rng.NextDouble() * 560f, i * 0.04f, 0.08f, 1, 0.045f));
            shake.Add(new Tone(170f, 0.02f, 0.32f, 0, 0.07f));
            clips["shake"] = Bake("shake", shake.ToArray());
        }

        /// <summary>15 ms linear attack to vol, then exponential decay to 0.001 over dur.</summary>
        static AudioClip Bake(string name, Tone[] tones)
        {
            float total = 0f;
            foreach (var t in tones) total = Mathf.Max(total, t.start + t.dur);
            int samples = Mathf.Max(1, Mathf.CeilToInt(total * SampleRate));
            var data = new float[samples];

            foreach (var t in tones)
            {
                int s0 = Mathf.FloorToInt(t.start * SampleRate);
                int n = Mathf.CeilToInt(t.dur * SampleRate);
                const float attack = 0.015f;
                for (int i = 0; i < n; i++)
                {
                    int idx = s0 + i;
                    if (idx < 0 || idx >= samples) continue;
                    float time = i / (float)SampleRate;

                    float env = time < attack
                        ? time / attack
                        : Mathf.Pow(0.001f, (time - attack) / Mathf.Max(0.0001f, t.dur - attack));

                    float phase = 2f * Mathf.PI * t.freq * time;
                    float wave = t.wave == 0
                        ? Mathf.Sin(phase)
                        : Mathf.Asin(Mathf.Sin(phase)) * (2f / Mathf.PI);   // triangle

                    data[idx] += wave * env * t.vol;
                }
            }

            for (int i = 0; i < samples; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Play(string cue, int tier = 0)
        {
            if (Muted || source == null) return;
            string key = cue == "merge" ? "merge" + Mathf.Clamp(tier, 0, Core.TierTable.Max) : cue;
            if (clips.TryGetValue(key, out var clip) && clip != null) source.PlayOneShot(clip);
        }
    }
}
