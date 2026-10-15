#nullable enable

using System.Threading.Tasks;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Тихая фоновая мелодия, синтезированная кодом, как и звуки: без аудиофайлов. Спокойный круг аккордов
    /// C – Am – F – G (76 ударов в минуту): мягкая подложка, перебор «щипком» и низкий бас на сильные доли.
    /// Петля бесшовная — хвосты нот в конце переносятся в начало. Считается в фоновом потоке при запуске
    /// и начинает звучать плавным нарастанием, когда готова. Отдельный переключатель «Музыка» в настройках.
    /// </summary>
    public sealed class Music : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float Bpm = 76f;
        private const float Volume = 0.16f;

        private static Music? _instance;
        private AudioSource? _source;
        private Task<float[]>? _render;
        private bool _enabled = true;
        private float _level;

        public static void Install(GameObject host, bool enabled)
        {
            _instance = host.AddComponent<Music>();
            _instance._enabled = enabled;
        }

        public static bool Enabled
        {
            get => _instance != null && _instance._enabled;
            set
            {
                if (_instance != null)
                {
                    _instance._enabled = value;
                }
            }
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;
            _render = Task.Run(Render);
        }

        private void Update()
        {
            if (_source == null)
            {
                return;
            }

            if (_render != null && _render.IsCompleted)
            {
                if (_render.Status == TaskStatus.RanToCompletion)
                {
                    var data = _render.Result;
                    var clip = AudioClip.Create("music", data.Length, 1, Rate, false);
                    clip.SetData(data, 0);
                    _source.clip = clip;
                    _source.Play();
                }

                _render = null;
            }

            // Плавно к нужной громкости: включили — нарастает за 2 с, выключили — затихает за полсекунды.
            var target = _enabled ? Volume : 0f;
            var speed = _enabled ? Volume / 2f : Volume / 0.5f;
            _level = Mathf.MoveTowards(_level, target, speed * Time.unscaledDeltaTime);
            _source.volume = _level;
        }

        /// <summary>Вся петля: 2 круга по 4 аккорда, в каждом аккорде 4 доли.</summary>
        private static float[] Render()
        {
            var beat = 60f / Bpm;
            var chordLength = beat * 4f;
            var loops = 2;
            var length = Mathf.RoundToInt(chordLength * 4 * loops * Rate);
            var data = new float[length];

            // Аккорды (частоты): C, Am, F, G — в средней октаве; бас — на октаву ниже основного тона.
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f },
                new[] { 220.00f, 261.63f, 329.63f },
                new[] { 174.61f, 220.00f, 261.63f },
                new[] { 196.00f, 246.94f, 293.66f },
            };

            // Перебор на восьмые: номер ноты аккорда (верхняя октава для 3) или пауза (-1). Второй круг чуть другой.
            int[][] patterns =
            {
                new[] { 0, 1, 2, 3, 2, 1, -1, 1 },
                new[] { 0, 2, 1, 3, -1, 2, 1, 2 },
            };

            for (var loop = 0; loop < loops; loop++)
            {
                for (var c = 0; c < chords.Length; c++)
                {
                    var start = (loop * chords.Length + c) * chordLength;
                    var chord = chords[c];

                    // Подложка: три тихих тона с медленной атакой, звучат весь аккорд и чуть заходят на следующий.
                    foreach (var f in chord)
                    {
                        Note(data, start, chordLength * 1.15f, f, 0.05f, 0.9f, 1.2f, 0f);
                    }

                    // Бас на первую и третью долю.
                    Note(data, start, beat * 1.8f, chord[0] / 2f, 0.16f, 0.02f, 0.9f, 2.5f);
                    Note(data, start + beat * 2f, beat * 1.8f, chord[0] / 2f, 0.12f, 0.02f, 0.9f, 2.5f);

                    // Перебор «щипком»: мягкая атака и быстрое затухание, чуть звонче за счёт второй гармоники.
                    var pattern = patterns[loop];
                    for (var i = 0; i < pattern.Length; i++)
                    {
                        var n = pattern[i];
                        if (n < 0)
                        {
                            continue;
                        }

                        var f = n == 3 ? chord[0] * 2f : chord[n] * 2f;
                        Note(data, start + i * beat / 2f, beat * 1.6f, f, 0.1f, 0.006f, 1.4f, 4f, 0.25f);
                    }
                }
            }

            // Мягкий предел: громкие места не хрипят.
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (float)System.Math.Tanh(data[i] * 1.4f) * 0.8f;
            }

            return data;
        }

        /// <summary>
        /// Нота: синус (и немного второй гармоники) с огибающей — атака attack секунд, затем экспоненциальное
        /// затухание decay (0 — ровно до конца с плавным спадом). Отсчёты за концом петли переносятся в начало.
        /// </summary>
        private static void Note(float[] data, float start, float duration, float freq, float gain, float attack, float release, float decay,
            float harmonic = 0.1f)
        {
            var from = Mathf.RoundToInt(start * Rate);
            var count = Mathf.RoundToInt(duration * Rate);
            var w = 2.0 * System.Math.PI * freq / Rate;
            for (var i = 0; i < count; i++)
            {
                var t = (float)i / Rate;
                var env = Mathf.Min(1f, t / Mathf.Max(0.001f, attack));
                env *= decay > 0f ? Mathf.Exp(-t * decay) : 1f;
                var tail = duration - t;
                env *= Mathf.Clamp01(tail / Mathf.Max(0.001f, release));
                var phase = w * i;
                var s = (float)(System.Math.Sin(phase) + harmonic * System.Math.Sin(phase * 2.0));
                var index = (from + i) % data.Length;
                data[index] += s * env * gain;
            }
        }
    }
}
