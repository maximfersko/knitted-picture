#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Живая моль на уровне с таймером. Маленькая (около клетки картинки), вид сверху, собрана из частей:
    /// две пары крыльев, тельце, два усика и мягкая тень. Всё движение процедурное и непрерывное:
    /// <list type="bullet">
    /// <item>поворот — пружина без перелёта, моль не «щёлкает» направлением;</item>
    /// <item>взмах — сжатие крыла по ширине, заднее крыло отстаёт от переднего на долю взмаха;</item>
    /// <item>на виражах крен: внутреннее крыло складывается сильнее;</item>
    /// <item>усики — пружины с инерцией: отстают на поворотах и разгоне, вздрагивают от укуса;</item>
    /// <item>в полёте моль поднимается: тень отходит, бледнеет и сжимается, сама моль чуть крупнее;</item>
    /// <item>по краю моль ходит семенящей походкой с покачиванием, на месте дышит и изредка обмахивается крыльями.</item>
    /// </list>
    /// Чем ближе конец, тем выше Agitation: чаще взмахи, живее усики. Время анимации не зависит от паузы игры.
    /// Координаты — локальные в слое, где моль создана (y вверх).
    /// </summary>
    public sealed class MothView : MonoBehaviour
    {
        private const float ChewTime = 0.22f;

        private RectTransform _root = null!;
        private RectTransform _shadow = null!;
        private Image _shadowImage = null!;
        private CanvasGroup _group = null!;
        private RectTransform _body = null!;
        private readonly RectTransform[] _fore = new RectTransform[2];
        private readonly RectTransform[] _hind = new RectTransform[2];
        private readonly RectTransform[] _antennae = new RectTransform[2];
        private float _span;

        private enum Mode
        {
            Rest,
            Crawl,
            Fly,
        }

        private Mode _mode;
        private Vector2 _pos;
        private Vector2 _velocity;
        private float _angle;
        private float _angleVelocity;
        private float _targetAngle;
        private float _endAngle;
        private float _height;
        private Vector2 _from;
        private Vector2 _control;
        private Vector2 _to;
        private float _t;
        private float _duration;
        private bool _leaving;
        private Action? _arrived;

        private float _phase;
        private float _amp;
        private float _hz = 4f;
        private float _burst;
        private float _nextFlutter = 2f;

        private readonly float[] _antenna = new float[2];
        private readonly float[] _antennaVelocity = new float[2];

        private float _chew;
        private int _chews;
        private int _bite;
        private Action<int>? _onBite;
        private Action? _chewed;

        private float _pop;
        private float _clock;

        /// <summary>0 — спокойна, 1 — вот-вот съест: чаще машет крыльями, живее усики.</summary>
        public float Agitation { get; set; }

        public bool Flying => _mode == Mode.Fly;

        /// <summary>Где моль сейчас (в слое).</summary>
        public Vector2 Position => _pos;

        public bool Shown => _root.gameObject.activeSelf;

        public float Span => _span;

        /// <summary>Моль с размахом крыльев span точек в слое layer.</summary>
        public static MothView Create(RectTransform layer, float span)
        {
            var shadow = Ui.Image(layer, "MothShadow", Art.Glow, Color.clear);
            var root = Ui.Node(layer, "Moth");
            var view = root.gameObject.AddComponent<MothView>();
            view.Build(root, shadow, span);
            return view;
        }

        private void Build(RectTransform root, Image shadow, float span)
        {
            _root = root;
            _span = span;
            _shadowImage = shadow;
            _shadow = shadow.rectTransform;
            _shadow.sizeDelta = new Vector2(span * 0.85f, span * 0.6f);
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Размах прототипа — около 92 точек; всё остальное масштабируется от него.
            var k = span / 92f;
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var at = new Vector2(side * MothArt.WingBase.x, MothArt.WingBase.y) * k;
                _hind[i] = Part("Hind", MothArt.HindWing, MothArt.WingSize * k, MothArt.WingPivot, at, side);
            }

            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var at = new Vector2(side * MothArt.WingBase.x, MothArt.WingBase.y) * k;
                _fore[i] = Part("Fore", MothArt.ForeWing, MothArt.WingSize * k, MothArt.WingPivot, at, side);
            }

            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var at = new Vector2(side * MothArt.AntennaBase.x, MothArt.AntennaBase.y) * k;
                _antennae[i] = Part("Antenna", MothArt.Antenna, MothArt.AntennaSize * k, MothArt.AntennaPivot, at, side);
            }

            _body = Part("Body", MothArt.Body, MothArt.BodySize * k, new Vector2(0.5f, 0.5f), Vector2.zero, 1f);
            _nextFlutter = Random.Range(1.5f, 3f);
            Apply();
        }

        private RectTransform Part(string name, Sprite sprite, Vector2 size, Vector2 pivot, Vector2 at, float flip)
        {
            var image = Ui.Image(_root, name, sprite, Color.white);
            var rt = image.rectTransform;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = at;
            rt.localScale = new Vector3(flip, 1f, 1f);
            return rt;
        }

        /// <summary>Направление «головой туда» в градусах поворота (голова вверх — 0, против часовой — плюс).</summary>
        public static float Facing(Vector2 direction) => Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;

        /// <summary>Сразу поставить моль на место (перестройка экрана).</summary>
        public void Place(Vector2 at, float facing)
        {
            Show();
            _mode = Mode.Rest;
            _pos = at;
            _angle = _targetAngle = _endAngle = facing;
            _angleVelocity = 0f;
            _height = 0f;
            _leaving = false;
            _group.alpha = 1f;
            Apply();
        }

        /// <summary>Перебраться на новое место: близко — пешком по краю, далеко — перелётом.</summary>
        public void MoveTo(Vector2 at, float facing, Action? arrived = null)
        {
            var distance = Vector2.Distance(_pos, at);
            if (_mode == Mode.Fly)
            {
                FlyTo(at, facing, arrived);
            }
            else if (distance < 0.5f)
            {
                _endAngle = _targetAngle = facing;
                arrived?.Invoke();
            }
            else if (distance < _span * 2.2f)
            {
                Crawl(at, facing, arrived);
            }
            else
            {
                FlyTo(at, facing, arrived);
            }
        }

        public void Crawl(Vector2 at, float facing, Action? arrived = null)
        {
            Show();
            _mode = Mode.Crawl;
            _from = _pos;
            _to = at;
            _t = 0f;
            _endAngle = facing;
            _duration = Mathf.Clamp(Vector2.Distance(_from, _to) / (_span * 0.9f), 0.35f, 2.2f);
            _arrived = arrived;
        }

        /// <summary>Перелёт по дуге: взлёт, вираж, посадка с парой медленных взмахов.</summary>
        public void FlyTo(Vector2 at, float facing, Action? arrived = null)
        {
            Show();
            var start = _pos;
            var delta = at - start;
            var distance = delta.magnitude;

            // Дуга чуть вбок и вверх: полёт не по линейке, а «по-мотыльковому».
            var side = new Vector2(-delta.y, delta.x).normalized * (distance * 0.2f * (Random.value < 0.5f ? -1f : 1f));
            _control = (start + at) / 2f + side + Vector2.up * Mathf.Max(_span * 0.7f, distance * 0.25f);
            _from = start;
            _to = at;
            _t = 0f;
            _duration = Mathf.Clamp(0.3f + distance / (_span * 9f), 0.45f, 1.2f);
            _endAngle = facing;
            _arrived = arrived;
            _mode = Mode.Fly;
            _leaving = false;
        }

        /// <summary>Прилететь из-за края экрана.</summary>
        public void FlyIn(Vector2 from, Vector2 at, float facing, Action? arrived = null)
        {
            Place(from, Facing(at - from));
            _group.alpha = 0f;
            FlyTo(at, facing, arrived);
        }

        /// <summary>Улететь за край и спрятаться: победа или «финиш».</summary>
        public void FlyAway(Vector2 to, Action? done = null)
        {
            if (!Shown)
            {
                done?.Invoke();
                return;
            }

            Startle();
            FlyTo(to, Facing(to - _pos), () =>
            {
                Hide();
                done?.Invoke();
            });
            _leaving = true;
        }

        /// <summary>Несколько укусов подряд: голова ныряет вперёд, усики вздрагивают; onBite — на каждом.</summary>
        public void Chew(int count, Action<int>? onBite, Action? done)
        {
            _chews = count;
            _bite = 0;
            _chew = ChewTime;
            _onBite = onBite;
            _chewed = done;
        }

        /// <summary>Вздрогнуть: короткий хлопок крыльями и «прыжок» размера.</summary>
        public void Startle()
        {
            _pop = 1f;
            _burst = 0.35f;
            _antennaVelocity[0] += 220f;
            _antennaVelocity[1] += 220f;
        }

        public void Show()
        {
            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
                _shadow.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            _mode = Mode.Rest;
            _root.gameObject.SetActive(false);
            _shadow.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_shadow != null)
            {
                Destroy(_shadow.gameObject);
            }
        }

        private void Update()
        {
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (dt <= 0f)
            {
                return;
            }

            _clock += dt;
            var previous = _pos;
            var previousVelocity = _velocity;
            var gait = 0f;
            switch (_mode)
            {
                case Mode.Fly:
                    StepFlight(dt);
                    break;
                case Mode.Crawl:
                    gait = StepCrawl(dt);
                    break;
            }

            _velocity = (_pos - previous) / dt;
            var acceleration = (_velocity - previousVelocity) / dt;

            // Поворот — критически затухающая пружина: быстро, плавно и без перелёта.
            var stiffness = _mode == Mode.Fly ? 15f : 10f;
            var delta = Mathf.DeltaAngle(_angle, _targetAngle);
            _angleVelocity += (stiffness * stiffness * delta - 2f * stiffness * _angleVelocity) * dt;
            _angle += _angleVelocity * dt;

            StepWings(dt);
            StepAntennae(dt, acceleration);
            StepChew(dt);
            _pop = Mathf.Max(0f, _pop - dt * 4f);
            Apply(gait);
        }

        private void StepFlight(float dt)
        {
            _t = Mathf.Min(1f, _t + dt / _duration);
            var e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * _t);
            _pos = Bezier(_from, _control, _to, e);
            _height = Mathf.Sin(Mathf.PI * _t);
            var tangent = 2f * (1f - e) * (_control - _from) + 2f * e * (_to - _control);
            if (_t < 0.85f && tangent.sqrMagnitude > 0.01f)
            {
                _targetAngle = Facing(tangent);
            }
            else
            {
                _targetAngle = _endAngle;
            }

            if (_leaving)
            {
                _group.alpha = 1f - SmoothStep(0.55f, 1f, _t);
            }
            else
            {
                _group.alpha = Mathf.Max(_group.alpha, SmoothStep(0f, 0.25f, _t));
            }

            if (_t >= 1f)
            {
                _mode = Mode.Rest;
                _height = 0f;
                _burst = 0.55f;
                Finish();
            }
        }

        private float StepCrawl(float dt)
        {
            _t = Mathf.Min(1f, _t + dt / _duration);
            var e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * _t);
            var path = _to - _from;
            _pos = _from + path * e;
            _targetAngle = _t < 0.8f && path.sqrMagnitude > 0.01f ? Facing(path) : _endAngle;
            if (_t >= 1f)
            {
                _mode = Mode.Rest;
                Finish();
            }

            return Mathf.Sin(Mathf.PI * _t);
        }

        private void Finish()
        {
            var arrived = _arrived;
            _arrived = null;
            arrived?.Invoke();
        }

        private void StepWings(float dt)
        {
            float targetAmp;
            float hz;
            if (_mode == Mode.Fly)
            {
                targetAmp = 0.82f;
                hz = 12f + 2f * Agitation;
            }
            else if (_mode == Mode.Crawl)
            {
                targetAmp = 0.18f;
                hz = 7f;
            }
            else if (_burst > 0f)
            {
                _burst -= dt;
                targetAmp = 0.5f + 0.25f * Agitation;
                hz = 4.5f + 3.5f * Agitation;
            }
            else
            {
                targetAmp = 0f;
                hz = _hz;
                _nextFlutter -= dt;
                if (_nextFlutter <= 0f)
                {
                    // Обмахнуться: пара медленных взмахов. Спокойная — раз в несколько секунд, взволнованная — часто.
                    var rate = 4.5f + 3.5f * Agitation;
                    _burst = 2f / rate;
                    _nextFlutter = Random.Range(Mathf.Lerp(3.5f, 0.8f, Agitation), Mathf.Lerp(6f, 1.6f, Agitation));
                }
            }

            _amp = Mathf.MoveTowards(_amp, targetAmp, dt * (targetAmp > _amp ? 6f : 2.2f));
            _hz = Mathf.Lerp(_hz, hz, 1f - Mathf.Exp(-dt * 8f));
            _phase += dt * _hz * Mathf.PI * 2f;
        }

        private void StepAntennae(float dt, Vector2 acceleration)
        {
            var forward = new Vector2(-Mathf.Sin(_angle * Mathf.Deg2Rad), Mathf.Cos(_angle * Mathf.Deg2Rad));
            var push = Mathf.Clamp(Vector2.Dot(acceleration, forward) / Mathf.Max(1f, _span), -400f, 400f);
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;

                // Покой: чуть врозь и медленно покачиваются (у взволнованной — живее и шире).
                var sway = Mathf.Sin(_clock * (1.6f + 2.4f * Agitation) + i * 1.3f) * (3f + 5f * Agitation);
                var target = 3f + sway;

                // Инерция: на развороте оба усика отстают, на разгоне отгибаются назад.
                var force = 130f * (target - _antenna[i]) - 10f * _antennaVelocity[i] + side * _angleVelocity * 0.9f + push * 0.25f;
                _antennaVelocity[i] += force * dt;
                _antenna[i] = Mathf.Clamp(_antenna[i] + _antennaVelocity[i] * dt, -22f, 32f);
            }
        }

        private void StepChew(float dt)
        {
            if (_chews <= 0)
            {
                return;
            }

            _chew -= dt;
            if (_chew > 0f)
            {
                return;
            }

            _bite++;
            _chews--;
            _antennaVelocity[0] += 160f;
            _antennaVelocity[1] += 160f;
            _onBite?.Invoke(_bite);
            if (_chews > 0)
            {
                _chew = ChewTime;
                return;
            }

            var done = _chewed;
            _chewed = null;
            _onBite = null;
            done?.Invoke();
        }

        /// <summary>Перенести состояние на части: позиция, поворот, крылья, усики, тень.</summary>
        private void Apply(float gait = 0f)
        {
            var forward = new Vector2(-Mathf.Sin(_angle * Mathf.Deg2Rad), Mathf.Cos(_angle * Mathf.Deg2Rad));

            // Укус: голова ныряет вперёд и обратно, тельце чуть сплющивается.
            var bite = _chews > 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - _chew / ChewTime)) : 0f;
            var lift = new Vector2(0f, _height * _span * 0.3f);
            var at = _pos + lift + forward * (bite * _span * 0.07f);

            // Походка: мелкое покачивание и шаг вбок-вбок.
            var wiggle = Mathf.Sin(_clock * Mathf.PI * 2f * 7f) * 6f * gait;
            var sideStep = new Vector2(forward.y, -forward.x) * (Mathf.Sin(_clock * Mathf.PI * 2f * 7f) * _span * 0.015f * gait);
            _root.localPosition = new Vector3(at.x + sideStep.x, at.y + sideStep.y, 0f);
            _root.localRotation = Quaternion.Euler(0f, 0f, _angle + wiggle);
            var scale = (1f + 0.18f * _height) * (1f + 0.14f * Mathf.Sin(Mathf.PI * _pop) * _pop);
            _root.localScale = Vector3.one * scale;

            var breath = Mathf.Sin(_clock * Mathf.PI * 2f * 0.6f);
            _body.localScale = new Vector3(1f + 0.04f * bite, 1f + 0.02f * breath - 0.07f * bite, 1f);

            // Крен на вираже: крыло с внутренней стороны поворота складывается сильнее.
            var bank = Mathf.Clamp(_angleVelocity / 360f, -1f, 1f);
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                var inner = 1f - 0.3f * Mathf.Max(0f, i == 0 ? bank : -bank);
                var rest = 1f - 0.03f * (0.5f + 0.5f * Mathf.Sin(_clock * 2.1f));
                var fore = (1f - _amp * (0.5f - 0.5f * Mathf.Cos(_phase))) * inner * rest;
                var hind = (1f - _amp * (0.5f - 0.5f * Mathf.Cos(_phase - 0.7f))) * inner * rest;
                _fore[i].localScale = new Vector3(side * Mathf.Lerp(0.14f, 1f, fore), 1f, 1f);
                _hind[i].localScale = new Vector3(side * Mathf.Lerp(0.18f, 1f, hind), 1f, 1f);
                _fore[i].localRotation = Quaternion.Euler(0f, 0f, side * -7f * (1f - fore));
                _hind[i].localRotation = Quaternion.Euler(0f, 0f, side * 5f * (1f - hind));
                _antennae[i].localRotation = Quaternion.Euler(0f, 0f, -side * _antenna[i]);
            }

            // Тень на полотне: в полёте отходит, сжимается и бледнеет.
            var offset = new Vector2(0.07f, -0.11f) * _span * (1f + 3.5f * _height);
            _shadow.localPosition = new Vector3(_pos.x + offset.x, _pos.y + offset.y, 0f);
            _shadow.localRotation = Quaternion.Euler(0f, 0f, _angle);
            _shadow.localScale = Vector3.one * (1f - 0.35f * _height);
            _shadowImage.color = Palette.WithAlpha(Palette.Ink, 0.2f * (1f - 0.55f * _height) * _group.alpha);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            var u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static float SmoothStep(float from, float to, float t)
        {
            t = Mathf.Clamp01((t - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
