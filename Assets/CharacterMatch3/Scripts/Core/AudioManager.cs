using UnityEngine;

namespace CharacterMatch3.Core
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int GeneratedSampleRate = 44100;

        public static AudioManager Instance { get; private set; }

        [Header("Placeholder clips")]
        public AudioClip swap;
        public AudioClip invalidSwap;
        public AudioClip normalMatch;
        public AudioClip cascade;
        public AudioClip lineClear;
        public AudioClip burst;
        public AudioClip rainbowActivation;
        public AudioClip blockerBreak;
        public AudioClip crateBreak;
        public AudioClip tokenDelivered;
        public AudioClip win;
        public AudioClip lose;
        public AudioClip buttonClick;

        [Header("Playback feel")]
        [SerializeField, Range(0f, 1f)] private float feedbackVolume = 0.86f;
        [SerializeField, Range(0f, 0.18f)] private float pitchJitter = 0.045f;

        private AudioSource source;

        private delegate float GeneratedSample(float time, float normalizedTime, int sampleIndex);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            source = GetComponent<AudioSource>();
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            EnsureGeneratedFallbackClips();
        }

        public void Play(AudioClip clip)
        {
            Play(clip, feedbackVolume, 1f - pitchJitter, 1f + pitchJitter);
        }

        public void PlayMatch(int cascadeIndex)
        {
            cascadeIndex = Mathf.Max(0, cascadeIndex);
            var clip = cascadeIndex == 0 ? normalMatch : cascade;
            var pitchCenter = 1f + Mathf.Clamp(cascadeIndex, 0, 4) * 0.035f;
            var volume = Mathf.Clamp01(feedbackVolume + cascadeIndex * 0.035f);
            Play(clip, volume, pitchCenter - pitchJitter, pitchCenter + pitchJitter);
        }

        public void PlaySpecial(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Line:
                    Play(lineClear, Mathf.Clamp01(feedbackVolume * 1.02f), 0.98f - pitchJitter, 1.08f + pitchJitter);
                    break;
                case PieceKind.Burst:
                    Play(burst, Mathf.Clamp01(feedbackVolume * 1.08f), 0.94f - pitchJitter, 1.02f + pitchJitter);
                    break;
                case PieceKind.Rainbow:
                    Play(rainbowActivation, Mathf.Clamp01(feedbackVolume * 1.04f), 1f - pitchJitter, 1.12f + pitchJitter);
                    break;
            }
        }

        public void PlayCrateBreak()
        {
            Play(crateBreak != null ? crateBreak : blockerBreak, Mathf.Clamp01(feedbackVolume * 1.14f), 0.88f - pitchJitter, 1.02f + pitchJitter);
        }

        public void Play(AudioClip clip, float volume, float minPitch, float maxPitch)
        {
            if (clip == null || source == null || !CharacterMatch3.Save.SaveManager.Data.soundEnabled)
            {
                return;
            }

            var lowPitch = Mathf.Min(minPitch, maxPitch);
            var highPitch = Mathf.Max(minPitch, maxPitch);
            source.pitch = Random.Range(lowPitch, highPitch);
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void PlayButton()
        {
            Play(buttonClick);
        }

        private void EnsureGeneratedFallbackClips()
        {
            if (swap == null)
            {
                swap = CreateSnapClip("Generated Swap Snap", 0.09f, 620f, 950f, 0.18f);
            }

            if (invalidSwap == null)
            {
                invalidSwap = CreateThudClip("Generated Invalid Swap", 0.14f, 130f, 70f);
            }

            if (normalMatch == null)
            {
                normalMatch = CreatePopClip("Generated Match Pop", 0.16f, 1080f, 340f, 0.34f);
            }

            if (cascade == null)
            {
                cascade = CreateSparkleClip("Generated Cascade Sparkle", 0.2f, 760f, 1380f);
            }

            if (lineClear == null)
            {
                lineClear = CreateWhooshClip("Generated Line Whoosh", 0.22f, 360f, 1450f);
            }

            if (burst == null)
            {
                burst = CreateBurstClip("Generated Mini Burst", 0.26f);
            }

            if (rainbowActivation == null)
            {
                rainbowActivation = CreateRainbowClip("Generated Rainbow Chime", 0.44f);
            }

            if (blockerBreak == null)
            {
                blockerBreak = CreateCrackClip("Generated Blocker Crack", 0.15f);
            }

            if (crateBreak == null)
            {
                crateBreak = CreateWoodBreakClip("Generated Wood Break", 0.24f);
            }

            if (tokenDelivered == null)
            {
                tokenDelivered = CreateSparkleClip("Generated Token Chime", 0.28f, 820f, 1680f);
            }

            if (win == null)
            {
                win = CreateRainbowClip("Generated Win Chime", 0.52f);
            }

            if (lose == null)
            {
                lose = CreateThudClip("Generated Lose Downbeat", 0.32f, 180f, 92f);
            }

            if (buttonClick == null)
            {
                buttonClick = CreateSnapClip("Generated Button Click", 0.06f, 880f, 1180f, 0.08f);
            }
        }

        private static AudioClip CreateGeneratedClip(string clipName, float duration, GeneratedSample sampler)
        {
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * GeneratedSampleRate));
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)GeneratedSampleRate;
                var normalizedTime = sampleCount > 1 ? i / (float)(sampleCount - 1) : 1f;
                data[i] = Mathf.Clamp(sampler(time, normalizedTime, i), -1f, 1f);
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, GeneratedSampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateSnapClip(string clipName, float duration, float startFrequency, float endFrequency, float noiseAmount)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.03f, 0.18f);
                var pitch = Mathf.Lerp(startFrequency, endFrequency, EaseOutCubic(normalizedTime));
                var click = Noise(sampleIndex) * noiseAmount * (1f - normalizedTime);
                var tone = Sine(pitch, time) * 0.34f + Sine(pitch * 2.01f, time) * 0.08f;
                return (tone + click) * envelope;
            });
        }

        private static AudioClip CreatePopClip(string clipName, float duration, float startFrequency, float endFrequency, float noiseAmount)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.035f, 0.12f);
                var pitch = Mathf.Lerp(startFrequency, endFrequency, EaseOutCubic(normalizedTime));
                var sparkle = Sine(pitch * 1.48f, time) * 0.08f * Mathf.Sin(normalizedTime * Mathf.PI);
                var body = Sine(pitch, time) * 0.42f;
                var fizz = Noise(sampleIndex) * noiseAmount * Mathf.Pow(1f - normalizedTime, 2.4f);
                return (body + sparkle + fizz) * envelope;
            });
        }

        private static AudioClip CreateSparkleClip(string clipName, float duration, float startFrequency, float endFrequency)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.08f, 0.42f);
                var pitch = Mathf.Lerp(startFrequency, endFrequency, EaseInOut(normalizedTime));
                var shimmer = Sine(pitch, time) * 0.2f + Sine(pitch * 1.505f, time) * 0.14f;
                var air = Noise(sampleIndex) * 0.08f * Mathf.Sin(normalizedTime * Mathf.PI);
                return (shimmer + air) * envelope;
            });
        }

        private static AudioClip CreateWhooshClip(string clipName, float duration, float startFrequency, float endFrequency)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.18f, 0.58f);
                var pitch = Mathf.Lerp(startFrequency, endFrequency, EaseOutCubic(normalizedTime));
                var sweep = Sine(pitch, time) * 0.22f;
                var air = Noise(sampleIndex) * 0.28f * Mathf.Sin(normalizedTime * Mathf.PI);
                return (sweep + air) * envelope;
            });
        }

        private static AudioClip CreateBurstClip(string clipName, float duration)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.02f, 0.16f);
                var thump = Sine(Mathf.Lerp(150f, 52f, EaseOutCubic(normalizedTime)), time) * 0.42f * (1f - normalizedTime);
                var fizz = Noise(sampleIndex) * 0.34f * Mathf.Pow(1f - normalizedTime, 2.1f);
                var sparkle = Sine(Mathf.Lerp(620f, 980f, normalizedTime), time) * 0.09f * Mathf.Sin(normalizedTime * Mathf.PI);
                return (thump + fizz + sparkle) * envelope;
            });
        }

        private static AudioClip CreateRainbowClip(string clipName, float duration)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.08f, 0.58f);
                var sweep = Sine(Mathf.Lerp(520f, 1480f, EaseInOut(normalizedTime)), time) * 0.16f;
                var chimeA = Sine(880f, time) * 0.13f * Mathf.Sin(Mathf.Clamp01(normalizedTime * 1.25f) * Mathf.PI);
                var chimeB = Sine(1320f, time) * 0.1f * Mathf.Sin(Mathf.Clamp01((normalizedTime - 0.08f) * 1.35f) * Mathf.PI);
                var shimmer = Noise(sampleIndex) * 0.07f * Mathf.Sin(normalizedTime * Mathf.PI);
                return (sweep + chimeA + chimeB + shimmer) * envelope;
            });
        }

        private static AudioClip CreateThudClip(string clipName, float duration, float startFrequency, float endFrequency)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.025f, 0.18f);
                var body = Sine(Mathf.Lerp(startFrequency, endFrequency, EaseOutCubic(normalizedTime)), time) * 0.42f;
                var sand = Noise(sampleIndex) * 0.04f * (1f - normalizedTime);
                return (body + sand) * envelope;
            });
        }

        private static AudioClip CreateCrackClip(string clipName, float duration)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.015f, 0.1f);
                var crack = Noise(sampleIndex * 7 + 11) * 0.3f * Mathf.Pow(1f - normalizedTime, 2.6f);
                var body = Sine(Mathf.Lerp(240f, 120f, normalizedTime), time) * 0.22f;
                return (crack + body) * envelope;
            });
        }

        private static AudioClip CreateWoodBreakClip(string clipName, float duration)
        {
            return CreateGeneratedClip(clipName, duration, (time, normalizedTime, sampleIndex) =>
            {
                var envelope = Envelope(normalizedTime, 0.01f, 0.46f);
                var snap = Noise(sampleIndex * 17 + 3) * 0.58f * Mathf.Pow(1f - normalizedTime, 4.1f);
                var splinterWindow = Mathf.Clamp01(1f - Mathf.Abs(normalizedTime - 0.16f) * 5.5f);
                var splinters = Noise(sampleIndex * 31 + 19) * 0.24f * Mathf.Pow(splinterWindow, 1.4f);
                var body = Sine(Mathf.Lerp(210f, 78f, EaseOutCubic(normalizedTime)), time) * 0.3f * Mathf.Pow(1f - normalizedTime, 1.2f);
                var debris = Noise(sampleIndex * 5 + 101) * 0.13f * Mathf.Sin(Mathf.Clamp01((normalizedTime - 0.08f) / 0.92f) * Mathf.PI) * Mathf.Pow(1f - normalizedTime, 0.9f);
                return (snap + splinters + body + debris) * envelope;
            });
        }

        private static float Sine(float frequency, float time)
        {
            return Mathf.Sin(2f * Mathf.PI * frequency * time);
        }

        private static float Noise(int seed)
        {
            var value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2f - 1f;
        }

        private static float Envelope(float normalizedTime, float attack, float releaseStart)
        {
            var attackValue = attack <= 0f ? 1f : Mathf.Clamp01(normalizedTime / attack);
            var releaseValue = releaseStart >= 1f ? 1f : 1f - Mathf.Clamp01((normalizedTime - releaseStart) / (1f - releaseStart));
            return Mathf.Clamp01(Mathf.Min(attackValue, releaseValue));
        }

        private static float EaseInOut(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float EaseOutCubic(float t)
        {
            var inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }
}
