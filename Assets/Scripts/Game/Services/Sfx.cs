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
    /// Короткие мягкие звуки. Интерфейсные — настоящие записи из Resources/Sounds (Kenney Interface Sounds, CC0),
    /// музыкальные (победа, звезда, комбо, тупик) — синтезированы в тон фоновой мелодии. Если файла нет,
    /// звучит синтезированный вариант: игра никогда не остаётся без звука.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int Rate = 44100;
        private static Sfx? _instance;
        private readonly Dictionary<Sound, AudioClip> _clips = new Dictionary<Sound, AudioClip>();
        private readonly Dictionary<Sound, float> _gain = new Dictionary<Sound, float>();

        /// <summary>
        /// Записи вместо синтеза: файл в Resources/Sounds и громкость относительно синтеза
        /// (записи нормированы до −6 дБ, синтез тише, поэтому множитель меньше единицы).
        /// </summary>
        private static readonly (Sound Sound, string File, float Gain)[] Recorded =
        {
            (Sound.Click, "click", 0.55f),
            (Sound.Needles, "needles", 0.5f),
            (Sound.Puff, "puff", 0.45f),
            (Sound.Pick, "pick", 0.55f),
            (Sound.Stitch, "stitch", 0.35f),
            (Sound.SpoolDone, "spool_done", 0.6f),
            (Sound.Deny, "deny", 0.5f),
            (Sound.Knot, "knot", 0.5f),
            (Sound.Unlock, "unlock", 0.55f),
            (Sound.Coin, "coin", 0.45f),
        };

        /// <summary>Сколько голосов в пуле: звуки идут параллельно, каждый со своей высотой и громкостью.</summary>
        private const int Voices = 6;

        /// <summary>Разброс высоты у коротких «бытовых» звуков (плюс-минус 3.5%): повтор не звучит как копия.</summary>
        private const float PitchSpread = 0.035f;

        /// <summary>Сколько петель могут звучать одновременно: лишние пропускаются, иначе очередь превращается в кашу.</summary>
        private const int MaxStitchVoices = 2;

        private readonly AudioSource[] _voices = new AudioSource[Voices];

        /// <summary>Что играет в каждом голосе (для лимита петель).</summary>
        private readonly Sound[] _voiceSound = new Sound[Voices];
        private int _nextVoice;
        private float _lastStitch;
        private int _stitchCombo;

        /// <summary>Сдвиг высоты текущей «лесенки» петель: выбирается в её начале и держится до конца серии.</summary>
        private float _stitchBase = 1f;

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

        /// <summary>Звук с высотой тона: звёзды и монеты подряд звучат всё выше.</summary>
        public static void Play(Sound sound, float volume, float pitch)
        {
            if (_instance == null || !Enabled || !_instance._clips.TryGetValue(sound, out var clip))
            {
                return;
            }

            // Высоту задал вызывающий (музыкальная «лесенка» звёзд и монет): разброс не добавляем.
            _instance.Voice(sound, clip, volume, pitch);
        }

        private void Awake()
        {
            for (var i = 0; i < Voices; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                _voices[i] = voice;
            }

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

            foreach (var (sound, file, gain) in Recorded)
            {
                var clip = Resources.Load<AudioClip>("Sounds/" + file);
                if (clip != null)
                {
                    _clips[sound] = clip;
                    _gain[sound] = gain;
                }
            }
        }

        private float Gain(Sound sound) => _gain.TryGetValue(sound, out var gain) ? gain : 1f;

        private void PlayInternal(Sound sound, float volume)
        {
            if (!_clips.TryGetValue(sound, out var clip))
            {
                return;
            }

            var pitch = 1f;
            if (sound == Sound.Stitch)
            {
                // Петли подряд звучат всё выше — приятная «очередь». Высота ступеньки принадлежит только этому голосу.
                // Серия целиком чуть сдвинута случайно, а шаги внутри неё ровные: разброс не ломает лесенку.
                var inRun = Time.unscaledTime - _lastStitch < 0.15f;
                _stitchCombo = inRun ? Mathf.Min(_stitchCombo + 1, 24) : 0;
                _lastStitch = Time.unscaledTime;
                if (!inRun)
                {
                    _stitchBase = Spread();
                }

                pitch = _stitchBase * (1f + _stitchCombo * 0.025f);
                if (PlayingCount(Sound.Stitch) >= MaxStitchVoices)
                {
                    // Лесенка уже сдвинулась, а сам звук пропускаем: частая очередь не копит голоса.
                    return;
                }
            }
            else if (sound == Sound.Click || sound == Sound.Pick || sound == Sound.Coin || sound == Sound.Puff)
            {
                pitch = Spread();
            }

            Voice(sound, clip, volume, pitch);
        }

        /// <summary>Случайный множитель высоты в пределах разброса.</summary>
        private static float Spread() => 1f + Random.Range(-PitchSpread, PitchSpread);

        /// <summary>Сколько голосов сейчас играют этот звук.</summary>
        private int PlayingCount(Sound sound)
        {
            var count = 0;
            for (var i = 0; i < Voices; i++)
            {
                if (_voices[i].isPlaying && _voiceSound[i] == sound)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Сыграть клип отдельным голосом пула: у каждого свои клип, высота и громкость. Голоса идут по кругу,
        /// свободный берётся первым; если заняты все, прерывается самый давний.
        /// </summary>
        private void Voice(Sound sound, AudioClip clip, float volume, float pitch)
        {
            var index = _nextVoice;
            for (var i = 0; i < Voices; i++)
            {
                var candidate = (_nextVoice + i) % Voices;
                if (!_voices[candidate].isPlaying)
                {
                    index = candidate;
                    break;
                }
            }

            _nextVoice = (index + 1) % Voices;
            var voice = _voices[index];
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume * Gain(sound));
            _voiceSound[index] = sound;
            voice.Play();
        }

        // Все звуки — чистые тоны с плавной атакой и затуханием до нуля. Шум (случайные отсчёты) не используем:
        // на телефонных динамиках он слышен как шипение.

        /// <summary>Мягкий тап: низкий тон, за 50 мс сползающий с 240 до 170 Гц, без щелчка в начале.</summary>
        private static AudioClip WoolTap()
        {
            var length = Mathf.CeilToInt(0.05f * Rate);
            var data = new float[length];
            var phase = 0.0;
            for (var i = 0; i < length; i++)
            {
                var k = i / (float)length;
                var freq = Mathf.Lerp(240f, 170f, k);
                phase += 2.0 * Mathf.PI * freq / Rate;
                var envelope = Mathf.Min(1f, k * 25f) * Mathf.Pow(1f - k, 2.5f);
                data[i] = (float)(Mathf.Sin((float)phase) * 0.85 + Mathf.Sin((float)(phase * 2.0)) * 0.15) * envelope * 0.3f;
            }

            return Clip("woolTap", data);
        }

        /// <summary>Два коротких деревянных «тука», как стукнули спицы: тоны 1500 и 1050 Гц с очень быстрым затуханием.</summary>
        private static AudioClip Needles()
        {
            var length = Mathf.CeilToInt(0.11f * Rate);
            var data = new float[length];
            foreach (var (start, freq, gain) in new[] { (0f, 1500f, 1f), (0.05f, 1050f, 0.75f) })
            {
                var from = Mathf.RoundToInt(start * Rate);
                var count = Mathf.RoundToInt(0.04f * Rate);
                for (var i = 0; i < count && from + i < length; i++)
                {
                    var t = (float)i / Rate;
                    var k = i / (float)count;
                    var envelope = Mathf.Min(1f, k * 60f) * Mathf.Exp(-k * 7f) * (1f - k);
                    var body = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * freq * 1.5f * t) * 0.3f;
                    data[from + i] += body * envelope * gain * 0.22f;
                }
            }

            return Clip("needles", data);
        }

        /// <summary>Тихий мягкий «пуф»: низкий тон 330 → 260 Гц с плавным нарастанием и затуханием.</summary>
        private static AudioClip Puff()
        {
            var length = Mathf.CeilToInt(0.12f * Rate);
            var data = new float[length];
            var phase = 0.0;
            for (var i = 0; i < length; i++)
            {
                var k = i / (float)length;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(330f, 260f, k) / Rate;
                var envelope = Mathf.Sin(Mathf.PI * k);
                data[i] = Mathf.Sin((float)phase) * envelope * envelope * 0.18f;
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
