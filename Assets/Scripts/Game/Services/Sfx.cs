#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace KnittedPicture.Game
{
    public enum Sound
    {
        Click,
        Pick,
        Stitch,
        SpoolDone,
        Deny,
        Knot,
        Unlock,
        Win,
        Jam,
        Coin,
        Combo,
        Star,

        /// <summary>Главная кнопка: два коротких деревянных щелчка, как стукнули спицы.</summary>
        Needles,

        /// <summary>Окно открылось: тихий мягкий «пух», как шерсть.</summary>
        Puff,
    }

    /// <summary>
    /// Короткие мягкие звуки, синтезированные при запуске: без аудиофайлов.
    /// Позже их легко заменить на настоящие клипы с теми же именами.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int Rate = 44100;
        private static Sfx? _instance;
        private readonly Dictionary<Sound, AudioClip> _clips = new Dictionary<Sound, AudioClip>();
        private AudioSource? _source;
        private float _lastStitch;
        private int _stitchCombo;

        public static bool Enabled { get; set; } = true;

        public static void Install(GameObject host)
        {
            _instance = host.AddComponent<Sfx>();
        }

        public static void Play(Sound sound, float volume = 1f)
        {
            if (_instance == null || !Enabled)
            {
                return;
            }

            _instance.PlayInternal(sound, volume);
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            // Щелчок — мягкий, «шерстяной»: глухой низкий тук и чуть шороха вместо звонкого писка.
            _clips[Sound.Click] = WoolTap();
            _clips[Sound.Needles] = Needles();
            _clips[Sound.Puff] = Puff();
            _clips[Sound.Pick] = Tone("pick", new[] { 520f, 780f }, 0.09f, 0.35f);
            _clips[Sound.Stitch] = Tone("stitch", new[] { 1250f }, 0.03f, 0.12f);
            _clips[Sound.SpoolDone] = Tone("done", new[] { 660f, 990f }, 0.12f, 0.3f);
            _clips[Sound.Deny] = Tone("deny", new[] { 220f, 180f }, 0.14f, 0.3f);
            _clips[Sound.Knot] = Tone("knot", new[] { 400f, 600f }, 0.08f, 0.3f);
            _clips[Sound.Unlock] = Tone("unlock", new[] { 700f, 1050f, 1400f }, 0.18f, 0.3f);
            _clips[Sound.Win] = Tone("win", new[] { 523f, 659f, 784f, 1046f }, 0.6f, 0.35f);
            _clips[Sound.Jam] = Tone("jam", new[] { 392f, 330f, 262f }, 0.45f, 0.3f);
            _clips[Sound.Coin] = Tone("coin", new[] { 988f, 1319f }, 0.16f, 0.28f);
            _clips[Sound.Combo] = Tone("combo", new[] { 784f, 988f, 1175f, 1568f }, 0.28f, 0.3f);
            _clips[Sound.Star] = Tone("star", new[] { 1319f, 1760f }, 0.22f, 0.32f);
        }

        private void PlayInternal(Sound sound, float volume)
        {
            if (_source == null || !_clips.TryGetValue(sound, out var clip))
            {
                return;
            }

            if (sound == Sound.Stitch)
            {
                // Петли подряд звучат всё выше — приятная «очередь».
                _stitchCombo = Time.unscaledTime - _lastStitch < 0.15f ? Mathf.Min(_stitchCombo + 1, 24) : 0;
                _lastStitch = Time.unscaledTime;
                _source.pitch = 1f + _stitchCombo * 0.025f;
            }
            else
            {
                _source.pitch = 1f;
            }

            _source.PlayOneShot(clip, volume);
        }

        /// <summary>Глухой мягкий тап: низкий тон 190 Гц с быстрым затуханием и тёплый шорох.</summary>
        private static AudioClip WoolTap()
        {
            var length = Mathf.CeilToInt(0.06f * Rate);
            var data = new float[length];
            var rng = new System.Random(3);
            var low = 0f;
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / Rate;
                var k = i / (float)length;
                var envelope = Mathf.Min(1f, k * 40f) * Mathf.Pow(1f - k, 3f);
                low += (((float)rng.NextDouble() * 2f - 1f) - low) * 0.08f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.7f + low * 1.6f) * envelope * 0.32f;
            }

            return Clip("woolTap", data);
        }

        /// <summary>Два сухих деревянных щелчка подряд: короткий шум, «окрашенный» резонансом около 2,4 кГц.</summary>
        private static AudioClip Needles()
        {
            var length = Mathf.CeilToInt(0.1f * Rate);
            var data = new float[length];
            var rng = new System.Random(11);
            foreach (var (start, gain) in new[] { (0f, 1f), (0.045f, 0.7f) })
            {
                var from = Mathf.RoundToInt(start * Rate);
                var count = Mathf.RoundToInt(0.018f * Rate);
                for (var i = 0; i < count && from + i < length; i++)
                {
                    var t = (float)i / Rate;
                    var k = i / (float)count;
                    var envelope = Mathf.Pow(1f - k, 4f);
                    var noise = (float)rng.NextDouble() * 2f - 1f;
                    var body = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 1150f * t) * 0.4f;
                    data[from + i] += (body * 0.7f + noise * 0.3f) * envelope * gain * 0.3f;
                }
            }

            return Clip("needles", data);
        }

        /// <summary>Мягкий «пух»: сглаженный шум, который плавно нарастает и тает, — без тона и без щелчка.</summary>
        private static AudioClip Puff()
        {
            var length = Mathf.CeilToInt(0.16f * Rate);
            var data = new float[length];
            var rng = new System.Random(5);
            var low = 0f;
            var lower = 0f;
            for (var i = 0; i < length; i++)
            {
                var k = i / (float)length;
                var envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.6f));
                low += (((float)rng.NextDouble() * 2f - 1f) - low) * 0.12f;
                lower += (low - lower) * 0.2f;
                data[i] = lower * envelope * 2.6f;
            }

            return Clip("puff", data);
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Последовательность нот с мягкой атакой и затуханием.</summary>
        private static AudioClip Tone(string name, float[] notes, float duration, float volume)
        {
            var length = Mathf.CeilToInt(duration * Rate);
            var data = new float[length];
            var perNote = length / notes.Length;
            for (var i = 0; i < length; i++)
            {
                var note = Mathf.Min(i / Mathf.Max(1, perNote), notes.Length - 1);
                var local = (i - note * perNote) / (float)Mathf.Max(1, perNote);
                var envelope = Mathf.Min(1f, local * 12f) * Mathf.Pow(1f - local, 2f);
                var t = (float)i / Rate;
                var wave = Mathf.Sin(2f * Mathf.PI * notes[note] * t) * 0.8f + Mathf.Sin(4f * Mathf.PI * notes[note] * t) * 0.2f;
                data[i] = wave * envelope * volume;
            }

            var clip = AudioClip.Create(name, length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
