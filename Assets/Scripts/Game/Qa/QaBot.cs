#if UNITY_EDITOR || DEVELOPMENT_BUILD || KNITTED_QA
#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnittedPicture.Game.Qa
{
    /// <summary>Что нашёл бот в живой игре.</summary>
    public sealed class QaIssue
    {
        public string Kind = string.Empty;
        public string Severity = string.Empty;
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public string Screen = string.Empty;
        public float Time;
        public int Count = 1;
        public string Shot = string.Empty;
        public List<string> Before = new List<string>();
    }

    /// <summary>
    /// QA-бот внутри игры: нажимает настоящие кнопки интерфейса (через EventSystem, как палец), проходит экраны
    /// и уровни, и всё время следит за игрой. Скриншот и последние действия сохраняются, когда:
    /// - в лог упало исключение или ошибка;
    /// - кадр дольше 100 мс (рывок), память растёт от уровня к уровню;
    /// - текст обрезан, кнопка за краем экрана, перекрыта другой или слишком мала, координаты NaN;
    /// - нажать нечего (игра застряла).
    /// Кроме обычных нажатий — двойные, серии, нажатия в пустоту, «назад», сворачивание и возврат,
    /// перестройка экрана (поворот), свайпы каруселей, долгое бездействие.
    /// Запуск: меню Knitted → QA в редакторе, ключ -knittedQa [минут] у сборки, в Development-сборке —
    /// четыре пальца на экран на секунду. Отчёт: persistentDataPath/qa/дата/report.html.
    /// В релизную сборку не попадает.
    /// </summary>
    public sealed class QaBot : MonoBehaviour
    {
        public const string PrefsKey = "knitted.qa.minutes";

        private static QaBot? _instance;

        private readonly ConcurrentQueue<(LogType Type, string Message, string Stack)> _logs = new ConcurrentQueue<(LogType, string, string)>();
        private readonly List<QaIssue> _issues = new List<QaIssue>();
        private readonly List<string> _journal = new List<string>();
        private readonly List<string> _journey = new List<string>();
        private readonly Dictionary<string, int> _unreachable = new Dictionary<string, int>();
        private readonly Dictionary<string, (int Frames, float Sum, float Max)> _frames = new Dictionary<string, (int, float, float)>();
        private readonly List<(float Time, long Bytes, int Textures)> _memory = new List<(float, long, int)>();
        private readonly Dictionary<string, int> _counters = new Dictionary<string, int>();
        private readonly Queue<(string File, Action<string>? Done)> _shots = new Queue<(string, Action<string>?)>();

        private System.Random _rng = new System.Random(1);
        private string _dir = string.Empty;
        private float _start;
        private float _end;
        private float _nextAction;
        private float _nextUiCheck;
        private float _nextMemory;
        private float _lastTapAvailable;
        private float _quietUntil;
        private object? _screen;
        private string _screenName = "-";
        private int _shotNumber;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var minutes = PlayerPrefs.GetFloat(PrefsKey, 0f);
            if (minutes > 0f)
            {
                PlayerPrefs.DeleteKey(PrefsKey);
                PlayerPrefs.Save();
                Launch(minutes, 1);
                return;
            }

            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-knittedQa");
            if (at >= 0)
            {
                var value = at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 10f;
                Launch(value, 1);
                return;
            }

            if (Debug.isDebugBuild && !Application.isEditor)
            {
                var gesture = new GameObject("KnittedQAGesture");
                DontDestroyOnLoad(gesture);
                gesture.AddComponent<QaGesture>();
            }
        }

        public static bool Running => _instance != null;

        /// <summary>Запустить бота на minutes минут.</summary>
        public static void Launch(float minutes, int seed)
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("KnittedQA");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<QaBot>();
            _instance.Begin(minutes, seed);
        }

        public static void StopNow() => _instance?.Finish();

        private void Begin(float minutes, int seed)
        {
            _rng = new System.Random(seed);
            _start = UnityEngine.Time.realtimeSinceStartup;
            _end = _start + minutes * 60f;
            _nextAction = _start + 2f;
            _lastTapAvailable = _start;
            _dir = System.IO.Path.Combine(Application.persistentDataPath, "qa", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(_dir);
            Application.logMessageReceivedThreaded += OnLog;
            Log($"бот запущен на {minutes:0.#} мин, зерно {seed}, экран {Screen.width}×{Screen.height}, {SystemInfo.deviceModel}");
            StartCoroutine(Shooter());
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit() => Finish();

        /// <summary>Лог приходит из любого потока: только складываем, разбираем в Update.</summary>
        private void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Exception or LogType.Error or LogType.Assert)
            {
                _logs.Enqueue((type, message, stack));
            }
        }

        private float Now => UnityEngine.Time.realtimeSinceStartup - _start;

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            while (_logs.TryDequeue(out var log))
            {
                var first = log.Message.Split('\n')[0];
                Issue("log:" + Hash(first), log.Type == LogType.Exception ? "ПАДЕНИЕ" : "ОШИБКА",
                    log.Type == LogType.Exception ? "исключение в игре" : "ошибка в логе", log.Message + "\n" + log.Stack);
            }

            TrackScreen();
            TrackFrame();
            var t = UnityEngine.Time.realtimeSinceStartup;
            if (t >= _nextMemory)
            {
                _nextMemory = t + 20f;
                SampleMemory();
            }

            if (t >= _nextUiCheck)
            {
                _nextUiCheck = t + 3f;
                CheckUi();
            }

            if (t >= _end)
            {
                Finish();
                return;
            }

            if (t >= _nextAction)
            {
                _nextAction = t + Mathf.Lerp(0.12f, 0.45f, (float)_rng.NextDouble());
                try
                {
                    Act();
                }
                catch (Exception e)
                {
                    // Ошибка самого бота — тоже в отчёт, но игру не роняем.
                    Issue("bot:" + e.GetType().Name, "ВНИМАНИЕ", "сам бот споткнулся", e.ToString());
                }
            }
        }

        // ---------- Экран и кадр ----------

        private void TrackScreen()
        {
            var app = App.Instance;
            var screen = app?.CurrentScreen;
            if (!ReferenceEquals(screen, _screen))
            {
                _screen = screen;
                _screenName = screen?.GetType().Name ?? "-";
                _quietUntil = UnityEngine.Time.realtimeSinceStartup + 0.6f;
                _nextUiCheck = UnityEngine.Time.realtimeSinceStartup + 0.7f;
                Log("экран " + _screenName);
                if (_journey.Count < 60)
                {
                    var name = $"journey-{_journey.Count:00}-{_screenName}.png";
                    _journey.Add(name);
                    Shoot(name, null, 0.5f);
                }
            }
        }

        private void TrackFrame()
        {
            var dt = UnityEngine.Time.unscaledDeltaTime;
            var key = _screenName;
            var (frames, sum, max) = _frames.TryGetValue(key, out var f) ? f : (0, 0f, 0f);
            _frames[key] = (frames + 1, sum + dt, Mathf.Max(max, dt));
            if (dt > 0.1f && UnityEngine.Time.realtimeSinceStartup > _quietUntil)
            {
                Issue("hitch:" + _screenName, "ВНИМАНИЕ", $"рывок: кадр {dt * 1000f:0} мс на экране {_screenName}",
                    $"кадр {dt * 1000f:0} мс (норма для 60 к/с — 17 мс)");
            }
        }

        private void SampleMemory()
        {
            var bytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            _memory.Add((Now, bytes, textures));
        }

        // ---------- Действия ----------

        private void Act()
        {
            var roll = _rng.NextDouble();
            if (roll < 0.035)
            {
                CallApp("OnBack");
                Log("назад");
            }
            else if (roll < 0.05)
            {
                StartCoroutine(PauseResume());
            }
            else if (roll < 0.06)
            {
                CallApp("Rebuild");
                _quietUntil = UnityEngine.Time.realtimeSinceStartup + 1f;
                Log("перестройка экрана (поворот, вырез)");
            }
            else if (roll < 0.09)
            {
                StartCoroutine(Swipe());
            }
            else if (roll < 0.11)
            {
                var wait = Mathf.Lerp(3f, 8f, (float)_rng.NextDouble());
                _nextAction = UnityEngine.Time.realtimeSinceStartup + wait;
                Log($"бездействие {wait:0.0} с");
            }
            else if (roll < 0.15)
            {
                var point = new Vector2((float)_rng.NextDouble() * Screen.width, (float)_rng.NextDouble() * Screen.height);
                Log($"тап в точку {point.x:0},{point.y:0}");
                Press(point);
            }
            else
            {
                var target = PickTap();
                if (target == null)
                {
                    if (Now > 5f && UnityEngine.Time.realtimeSinceStartup - _lastTapAvailable > 20f)
                    {
                        Issue("stuck:" + _screenName, "ОШИБКА", $"нечего нажать 20 с на экране {_screenName}: игра застряла?", "ни одной доступной кнопки");
                        _lastTapAvailable = UnityEngine.Time.realtimeSinceStartup;
                        CallApp("OnBack");
                    }

                    return;
                }

                _lastTapAvailable = UnityEngine.Time.realtimeSinceStartup;
                var center = Center((RectTransform)target.transform);
                Log($"тап «{Path(target.transform)}»");
                Press(center);
                var extra = _rng.NextDouble();
                if (extra < 0.06)
                {
                    Log("двойной тап в том же кадре");
                    Press(center);
                }
                else if (extra < 0.1)
                {
                    Log("серия быстрых тапов");
                    _nextAction = UnityEngine.Time.realtimeSinceStartup + 0.02f;
                }
            }
        }

        /// <summary>Нажать в точке экрана, как палец: луч по интерфейсу, нажатие, отпускание, клик — по иерархии.</summary>
        private static void Press(Vector2 point)
        {
            var system = EventSystem.current;
            if (system == null)
            {
                return;
            }

            var data = new PointerEventData(system) { position = point, pressPosition = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>();
            system.RaycastAll(data, hits);
            if (hits.Count == 0)
            {
                return;
            }

            var hit = hits[0];
            data.pointerCurrentRaycast = hit;
            data.pointerPressRaycast = hit;
            var pressed = ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = pressed;
            data.rawPointerPress = hit.gameObject;
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerUpHandler);
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (click != null)
            {
                ExecuteEvents.Execute(click, data, ExecuteEvents.pointerClickHandler);
            }
        }

        /// <summary>Доступная кнопка: включена, видна, на экране и именно её достаёт луч в её центре. Клубки поля — чаще.</summary>
        private Tap? PickTap()
        {
            var candidates = new List<Tap>();
            var weights = new List<float>();
            var popups = App.Instance != null && App.Instance.HasPopups;
            foreach (var tap in UnityEngine.Object.FindObjectsByType<Tap>(FindObjectsSortMode.None))
            {
                if (!tap.isActiveAndEnabled || !tap.Interactable || !Visible(tap.transform))
                {
                    continue;
                }

                var rt = (RectTransform)tap.transform;
                if (!OnScreen(rt, 0f))
                {
                    continue;
                }

                var path = Path(tap.transform);
                if (!Reachable(tap, Center(rt)))
                {
                    var n = _unreachable.TryGetValue(path, out var c) ? c + 1 : 1;
                    _unreachable[path] = n;
                    if (n == 40 && !popups)
                    {
                        Issue("blocked:" + path, "ВНИМАНИЕ", "кнопку не нажать: её центр перекрыт другим элементом",
                            $"«{path}» на экране {_screenName}: луч в её центр попадает в другое");
                    }

                    continue;
                }

                candidates.Add(tap);
                weights.Add(path.Contains("Spool") ? 3f : path.Contains("Booster") ? 0.6f : 1f);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var total = weights.Sum();
            var roll = (float)_rng.NextDouble() * total;
            for (var i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0f)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static bool Reachable(Tap tap, Vector2 point)
        {
            var system = EventSystem.current;
            if (system == null)
            {
                return false;
            }

            var hits = new List<RaycastResult>();
            system.RaycastAll(new PointerEventData(system) { position = point }, hits);
            if (hits.Count == 0)
            {
                return false;
            }

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            return handler == tap.gameObject;
        }

        /// <summary>Свёрнуть и вернуть игру: пауза, моль, автосохранение.</summary>
        private IEnumerator PauseResume()
        {
            Log("свернули игру");
            var app = App.Instance;
            if (app == null)
            {
                yield break;
            }

            app.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            app.SendMessage("OnApplicationPause", true, SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSecondsRealtime(Mathf.Lerp(0.2f, 2f, (float)_rng.NextDouble()));
            app.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);
            app.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
            _quietUntil = UnityEngine.Time.realtimeSinceStartup + 1f;
            Log("вернулись в игру");
        }

        /// <summary>Свайп по тому, что умеет тянуться (карусели, вкладки).</summary>
        private IEnumerator Swipe()
        {
            var draggable = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m is IDragHandler && m.isActiveAndEnabled).ToList();
            var system = EventSystem.current;
            if (draggable.Count == 0 || system == null)
            {
                yield break;
            }

            var target = draggable[_rng.Next(draggable.Count)];
            var start = Center((RectTransform)target.transform);
            var dx = (float)(_rng.NextDouble() - 0.5) * Screen.width * 0.9f;
            Log($"свайп «{Path(target.transform)}» на {dx:0} px");
            var data = new PointerEventData(system) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.beginDragHandler);
            for (var i = 1; i <= 6; i++)
            {
                yield return null;
                if (target == null)
                {
                    yield break;
                }

                data.delta = new Vector2(dx / 6f, 0f);
                data.position = start + new Vector2(dx * i / 6f, 0f);
                ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.dragHandler);
            }

            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.endDragHandler);
        }

        private static void CallApp(string method)
        {
            var app = App.Instance;
            app?.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(app, null);
        }

        // ---------- Проверки интерфейса ----------

        private void CheckUi()
        {
            foreach (var text in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                if (!text.isActiveAndEnabled || string.IsNullOrWhiteSpace(text.text) || !Visible(text.transform) || text.color.a < 0.05f)
                {
                    continue;
                }

                var generator = text.cachedTextGenerator;
                var shown = generator?.characterCountVisible ?? int.MaxValue;
                var length = text.text.Replace("\n", string.Empty).Length;
                if (shown + 1 < length)
                {
                    Issue("text-cut:" + Path(text.transform), "ВНИМАНИЕ", "текст обрезан",
                        $"«{text.text}»: видно {shown} символов из {length} на экране {_screenName}");
                }
            }

            foreach (var tap in UnityEngine.Object.FindObjectsByType<Tap>(FindObjectsSortMode.None))
            {
                if (!tap.isActiveAndEnabled || !tap.Interactable || !Visible(tap.transform))
                {
                    continue;
                }

                var rt = (RectTransform)tap.transform;
                var p = rt.position;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(rt.lossyScale.x))
                {
                    Issue("nan:" + Path(rt), "ОШИБКА", "координаты кнопки — NaN", Path(rt));
                    continue;
                }

                if (!OnScreen(rt, 6f) && OnScreen(rt, -1e6f))
                {
                    Issue("offscreen:" + Path(rt), "ВНИМАНИЕ", "кнопка выходит за край экрана", $"«{Path(rt)}» на экране {_screenName}");
                }

                var size = Size(rt);
                if (size.x > 0 && size.y > 0 && Mathf.Min(size.x, size.y) < Screen.width * 0.045f && OnScreen(rt, 0f))
                {
                    Issue("tiny:" + Path(rt), "ВНИМАНИЕ", "кнопка меньше пальца", $"«{Path(rt)}»: {size.x:0}×{size.y:0} px при ширине экрана {Screen.width}");
                }
            }

            foreach (var image in UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                if (image.isActiveAndEnabled && image.sprite == null && image.type == Image.Type.Sliced && image.color.a > 0.05f && Visible(image.transform))
                {
                    Issue("no-sprite:" + Path(image.transform), "ВНИМАНИЕ", "плашка без картинки (белый прямоугольник)", Path(image.transform));
                }
            }
        }

        // ---------- Геометрия ----------

        private static Vector2 Center(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return (c[0] + c[2]) / 2f;
        }

        private static Vector2 Size(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return new Vector2(Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y));
        }

        /// <summary>Прямоугольник целиком на экране (с запасом margin; отрицательный — хоть частично).</summary>
        private static bool OnScreen(RectTransform rt, float margin)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            if (margin < -1000f)
            {
                return c[2].x > 0 && c[0].x < Screen.width && c[2].y > 0 && c[0].y < Screen.height;
            }

            return c[0].x >= -margin && c[0].y >= -margin && c[2].x <= Screen.width + margin && c[2].y <= Screen.height + margin;
        }

        /// <summary>Видно ли: все CanvasGroup сверху не прозрачны.</summary>
        private static bool Visible(Transform t)
        {
            var alpha = 1f;
            foreach (var group in t.GetComponentsInParent<CanvasGroup>())
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            return alpha > 0.05f;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var p = t; p != null && parts.Count < 5; p = p.parent)
            {
                parts.Add(p.name);
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Hash(string s)
        {
            unchecked
            {
                var h = 17;
                foreach (var ch in s)
                {
                    h = h * 31 + ch;
                }

                return (h & 0x7fffffff).ToString("x");
            }
        }

        // ---------- Находки и скриншоты ----------

        private void Log(string what)
        {
            _journal.Add($"{Now,7:0.0} с  [{_screenName}] {what}");
        }

        /// <summary>Записать находку. Повтор той же — только счёт; скриншот — у первых трёх видов каждой находки.</summary>
        private void Issue(string kind, string severity, string title, string detail)
        {
            var existing = _issues.FirstOrDefault(i => i.Kind == kind);
            if (existing != null)
            {
                existing.Count++;
                return;
            }

            var issue = new QaIssue
            {
                Kind = kind,
                Severity = severity,
                Title = title,
                Detail = detail,
                Screen = _screenName,
                Time = Now,
                Before = _journal.Skip(Math.Max(0, _journal.Count - 25)).ToList(),
            };
            _issues.Add(issue);
            Log("✗ " + title);
            var family = kind.Split(':')[0];
            var shots = _counters.TryGetValue(family, out var n) ? n : 0;
            if (shots < 12)
            {
                _counters[family] = shots + 1;
                Shoot($"issue-{++_shotNumber:000}-{family}.png", file => issue.Shot = file, 0f);
            }
        }

        private void Shoot(string file, Action<string>? done, float delay)
        {
            if (delay <= 0f)
            {
                _shots.Enqueue((file, done));
            }
            else
            {
                StartCoroutine(Later(delay, () => _shots.Enqueue((file, done))));
            }
        }

        private static IEnumerator Later(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            action();
        }

        /// <summary>Снимок в конце кадра: интерфейс уже нарисован. Один снимок за кадр.</summary>
        private IEnumerator Shooter()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                if (_shots.Count == 0)
                {
                    continue;
                }

                var (file, done) = _shots.Dequeue();
                try
                {
                    var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false);
                    var bytes = QaPng.Encode(tex.GetPixels32(), tex.width, tex.height);
                    Destroy(tex);
                    File.WriteAllBytes(System.IO.Path.Combine(_dir, file), bytes);
                    done?.Invoke(file);
                }
                catch (Exception e)
                {
                    Log("скриншот не удался: " + e.Message);
                }
            }
        }

        // ---------- Итог ----------

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            SampleMemory();
            if (_memory.Count >= 4)
            {
                var first = _memory[1];
                var last = _memory[_memory.Count - 1];
                var growing = _memory.Skip(1).Zip(_memory.Skip(2), (a, b) => b.Bytes >= a.Bytes).Count(x => x) >= (_memory.Count - 2) * 0.8;
                if (growing && last.Bytes - first.Bytes > 150L * 1024 * 1024)
                {
                    Issue("memory-growth", "ВНИМАНИЕ", "память растёт всё время игры",
                        $"{first.Bytes / 1048576} МБ → {last.Bytes / 1048576} МБ, текстур {first.Textures} → {last.Textures}");
                }
            }

            File.WriteAllText(System.IO.Path.Combine(_dir, "report.html"), QaReport.Html(this), System.Text.Encoding.UTF8);
            File.WriteAllText(System.IO.Path.Combine(_dir, "journal.txt"), string.Join("\n", _journal), System.Text.Encoding.UTF8);
            File.WriteAllText(System.IO.Path.Combine(_dir, "report.json"), QaReport.Json(this), System.Text.Encoding.UTF8);
            Debug.Log($"[QA] бот закончил: находок {_issues.Count}, отчёт {System.IO.Path.Combine(_dir, "report.html")}");
            StartCoroutine(Later(1f, () => Destroy(gameObject)));
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-knittedQaQuit") >= 0)
            {
                Application.Quit(_issues.Any(i => i.Severity is "ПАДЕНИЕ" or "ОШИБКА") ? 1 : 0);
            }
        }

        // Доступ для отчёта.
        internal IReadOnlyList<QaIssue> Issues => _issues;

        internal IReadOnlyList<string> Journal => _journal;

        internal IReadOnlyList<string> Journey => _journey;

        internal IReadOnlyDictionary<string, (int Frames, float Sum, float Max)> Frames => _frames;

        internal IReadOnlyList<(float Time, long Bytes, int Textures)> Memory => _memory;

        internal float Duration => Now;

        internal string Folder => _dir;
    }

    /// <summary>Development-сборка: четыре пальца на экран на секунду — запустить бота на 10 минут.</summary>
    public sealed class QaGesture : MonoBehaviour
    {
        private float _held;

        private void Update()
        {
            var touches = 0;
#if ENABLE_INPUT_SYSTEM
            var screen = UnityEngine.InputSystem.Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (touch.isInProgress)
                    {
                        touches++;
                    }
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            touches = Input.touchCount;
#endif
            _held = touches >= 4 ? _held + Time.unscaledDeltaTime : 0f;
            if (_held > 1f && !QaBot.Running)
            {
                _held = 0f;
                QaBot.Launch(10f, Environment.TickCount);
                App.Instance?.Toast("QA-бот запущен на 10 минут");
            }
        }
    }
}
#endif
