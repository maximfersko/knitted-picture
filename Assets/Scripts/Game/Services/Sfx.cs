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
            _clips[Sound.Click] = Tone("click", new[] { 900f }, 0.035f, 0.25f);
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
