#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Ходы игрока: тапы по клубкам поля и слотам станка, отказы с объяснением.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Ходы ----------

        private void OnSpoolTap(int column, int depth)
        {
            if (_ended || _failShown || _finishing || App.HasPopups)
            {
                return;
            }

            _idle = 0;
            // Подсказку о натяжении не гасим мгновенным тапом, но и не держим: через секунду любой ход её убирает.
            if (!_tipKeep || Time.unscaledTime - _tipShownAt > 1.2f)
            {
                HideTip();
            }

            HideHint();
            CancelResponsiveInput();
            // Без ножниц работает только передний клубок. Тап по клубку из глубины раньше отправлял
            // передний — неожиданный ход, лишний тап к цели и, бывает, затор. Теперь — только подсказка.
            if (!_scissors && depth > 0)
            {
                var view = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
                if (view != null)
                {
                    Tweens.Shake(view.View.Root, 5f, 0.25f);
                }

                Sfx.Play(Sound.Deny, 0.6f);
                Haptics.Play(Haptic.Soft);
                ToastOnce("depth", L.T("Сначала уходит передний клубок"));
                return;
            }

            var selectedColumn = _game.Column(column);
            if (!_scissors && depth < selectedColumn.Count && selectedColumn[depth].Link != 0)
            {
                var marked = 0;
                for (var i = 0; i < _game.Slots.Count && marked < 2; i++)
                {
                    if (_game.Slots[i] == null)
                    {
                        Highlight(_slotFrames[i], Palette.LinkPair(selectedColumn[depth].Link));
                        marked++;
                    }
                }
            }
            var result = _scissors ? _game.PullSpool(column, depth) : _game.TapColumn(column);
            if (!result.Accepted)
            {
                var interval = _tempo.Rush ? StepInterval / RushSpeed : StepInterval;
                if (!_scissors && _responsive.Buffer(_game, column, result.Reason, Time.unscaledTime,
                    Mathf.Max(0f, interval - _stepClock), interval))
                {
                    ShowBufferFeedback(column);
                    Haptics.Play(Haptic.Soft);
                    return;
                }

                Deny(column, depth, result.Reason);
                return;
            }

            PresentMove(result);
        }

        /// <summary>Одинаковый отклик прямого и отложенного хода, включая полёт, цели и комбо.</summary>
        private void PresentMove(MoveResult result)
        {
            if (!App.Progress.FunnelSent(FunnelStep.FirstMove))
            {
                App.Funnel(FunnelStep.FirstMove);
            }

            if (_scissors)
            {
                PayScissors();
                _scissors = false;
                HideScissorsBanner();
            }

            RefreshGoals();

            if (result.Kind == MoveKind.KnotUntied)
            {
                Sfx.Play(Sound.Knot, 0.8f, 1f + 0.08f * Mathf.Min(5, ++_knotTone));
                Haptics.Play(Haptic.Tap);
                RefreshField();
                RefreshBoosters();
                var view = FindFieldView(result.Knotted!.Id);
                if (view != null)
                {
                    Tweens.Punch(view.Root, 0.05f, 0.2f);
                    Fx.LooseThreads(_fx, Ui.WorldCenter(view.Root), Palette.Danger, view.Size);
                }

                return;
            }

            Sfx.Play(Sound.Pick);
            _knotTone = 0;
            Haptics.Play(Haptic.Tap);
            OnComboGrow(_tempo.OnPlaced());
            var flights = new List<(Placement placed, Vector3 from)>();
            foreach (var placed in result.Placed)
            {
                var source = _field.FirstOrDefault(f => f.Column == placed.Column && f.Depth == placed.Depth);
                flights.Add((placed, source != null ? Ui.WorldCenter(source.View.Root) : Ui.WorldCenter(_fieldContent)));
                _arriving.Add(placed.Spool.Id);
            }

            RefreshField();
            RefreshBoosters();

            // Клубок с галочкой: его цвет уже довязан. Он съездит на станок и уйдёт — это не ошибка,
            // а способ расчистить колонку. Без пояснения исчезновение клубка выглядело багом.
            if (result.Placed.Any(p => _game.IsSurplus(p.Spool)))
            {
                ToastOnce("surplus", L.T("Цвет довязан: клубок сразу уйдёт со станка"));
            }

            foreach (var id in result.Revealed)
            {
                PunchField(id);
            }

            if (result.Unlocked.Count > 0)
            {
                Sfx.Play(Sound.Unlock);
                foreach (var id in result.Unlocked)
                {
                    PunchField(id);
                }
            }

            // Полёт длится ровно столько, сколько станок «разогревает» клубок: в «Разгоне» шаги вдвое короче,
            // и раньше клубок начинал вязать из пустого слота, ещё не долетев.
            var interval = (_tempo.Rush ? StepInterval / RushSpeed : StepInterval) / _finishSpeed;
            var flight = Mathf.Max(0.01f, LoomGame.WarmupSteps * interval - _stepClock);
            RectTransform? firstPartner = null;
            foreach (var (placed, from) in flights)
            {
                var spool = placed.Spool;
                var flyer = SpoolView.Create(_fx, _spoolSize);
                flyer.Bind(spool, _colors[spool.Color], true);
                flyer.SetFabric(_game.IsForeign(spool));
                var target = SlotTarget(placed.Slot);
                var scale = SpoolSizeAt(placed.Slot) / _spoolSize;

                // Клубок взлетает дугой: в начале вытягивается вдоль рывка (его «взяли»), в полёте чуть
                // поворачивается, у станка сжимается к размеру слота. Приземление — сплющивание с отскоком и пух.
                var lift = (40f + Mathf.Abs(target.x - from.x) / Mathf.Max(0.0001f, _fx.lossyScale.x) * 0.12f) * _fx.lossyScale.x;
                var control = (from + target) / 2f + new Vector3(0f, lift, 0f);
                var spin = (target.x > from.x ? -1f : 1f) * 14f;
                var rt = flyer.Root;
                if (spool.Link != 0)
                {
                    if (firstPartner == null)
                    {
                        firstPartner = rt;
                    }
                    else
                    {
                        Fx.LinkedFlight(_fx, firstPartner, rt, Palette.LinkPair(spool.Link), flight);
                    }
                }
                rt.position = from;
                var epoch = _presentationEpoch;
                void Arrive()
                {
                    if (_presentationDisposed || epoch != _presentationEpoch || !_flightCompletions.Remove(spool.Id)) { return; }
                    Tweens.Kill(rt);
                    Object.Destroy(flyer.Root.gameObject);
                    _arriving.Remove(spool.Id);
                    SyncLoom();
                }
                _flightCompletions[spool.Id] = Arrive;
                Tweens.Run(rt, flight, t =>
                {
                    var e = Ease.OutCubic(t);
                    var u = 1f - e;
                    rt.position = Tweens.Calm ? Vector3.Lerp(from, target, e) : u * u * from + 2f * u * e * control + e * e * target;
                    var size = Mathf.Lerp(1f, scale, t) * (Tweens.Calm ? 1f : 1f + 0.12f * Ease.Bump(t));
                    var stretch = !Tweens.Calm && t < 0.22f ? 0.18f * Ease.Bump(t / 0.22f) : 0f;
                    rt.localScale = new Vector3(size * (1f - stretch * 0.6f), size * (1f + stretch), 1f);
                    rt.localRotation = Tweens.Calm ? Quaternion.identity : Quaternion.Euler(0f, 0f, spin * Ease.Bump(t));
                }, null, () =>
                {
                    if (_presentationDisposed || epoch != _presentationEpoch || !_flightCompletions.ContainsKey(spool.Id)) { return; }
                    Arrive();
                    var slot = _slotViews[placed.Slot];
                    if (slot != null && slot.SpoolId == spool.Id)
                    {
                        var landed = slot.View.Root;
                        // Посадка и вязка используют один канал: новый толчок продолжает текущую позу.
                        Tweens.Kill(landed, 31);
                        var landedScale = landed.localScale;
                        Tweens.Run(landed, 0.34f, t =>
                        {
                            var j = Tweens.Calm ? 0f : Ease.Jolt(t);
                            landed.localScale = Vector3.Lerp(landedScale, Vector3.one, t) + new Vector3(0.22f * j, -0.18f * j, 0f);
                        }, null, () => landed.localScale = Vector3.one, channel: 31);
                        Fx.Dust(_fx, target + new Vector3(0f, -SpoolSizeAt(placed.Slot) * 0.42f * _fx.lossyScale.x, 0f), _colors[spool.Color]);
                        Haptics.Play(Haptic.Tap);

                        // Тихий «пух» посадки: клубок мягко сел в лунку (высота чуть гуляет в самом Sfx).
                        Sfx.Play(Sound.Puff, 0.2f);
                    }
                });
            }

            SyncLoom();
        }

        private readonly Dictionary<int, System.Action> _flightCompletions = new Dictionary<int, System.Action>();

        private void CompleteReadyFlights()
        {
            foreach (var spool in _game.Slots)
            {
                if (spool != null && spool.Warmup == 0 && _flightCompletions.TryGetValue(spool.Id, out var arrive)) { arrive(); }
            }
        }

        /// <summary>Тап по слоту станка: с ножницами — срезать клубок, без них — подсказка про чужую пряжу.</summary>
        private void OnSlotTap(int slot)
        {
            CancelResponsiveInput();
            if (_ended || _failShown || _finishing || App.HasPopups || slot >= _game.Slots.Count)
            {
                return;
            }

            var spool = _game.Slots[slot];
            if (spool == null)
            {
                return;
            }

            if (!_scissors)
            {
                if (_game.IsForeign(spool))
                {
                    ToastOnce("foreign-loom", L.T("Эта пряжа распустится сама за 6 шагов. Ножницы освободят место сразу"));
                }
                else if (spool.Warmup == 0 && !_game.HasWork(spool) && !_game.IsSurplus(spool))
                {
                    // Бледный клубок на станке: почему он не вяжет.
                    var held = Enumerable.Range(0, _game.Width).Any(x => _game.IsHeld(x) && _game.ColorAt(x, _game.FrontY(x)) == spool.Color);
                    ToastOnce("waiting", held
                        ? L.T("Ждёт: сначала довяжи клетки ниже линии натяжения")
                        : L.T("Ждёт своей очереди: его цвета пока нет внизу"));
                }

                return;
            }

            var head = _game.Slots.ToList().IndexOf(spool);
            var view = _slotViews[head];
            var at = view != null ? Ui.WorldCenter(view.View.Root) : Ui.WorldCenter(_slotFrames[slot]);
            var back = _game.ReturnsToField(spool);
            if (!_game.CutFromLoom(slot))
            {
                return;
            }

            PayScissors();
            _scissors = false;
            HideScissorsBanner();
            Fx.Poof(_fx, at, Palette.Ink3, _slotSpool * 1.4f);
            if (back)
            {
                // Нужный рисунку клубок не пропадает: он вернулся вперёд своей колонки.
                App.Toast(L.T("Клубок вернулся на поле: его пряжа ещё нужна картинке"));
            }
            Sfx.Play(Sound.Knot);
            Haptics.Play(Haptic.Medium);
            SyncLoom();
            RefreshField();
            RefreshBoosters();
            if (back)
            {
                PunchField(spool.Id);
            }

            _coins.text = App.Progress.Coins.ToString();
            CheckStatus();
        }

        /// <summary>
        /// Пояснение показываем один раз за сессию на каждый случай: дальше отказ — просто встряска
        /// клубка и звук. Редкие сообщения идут по очереди, чтобы каждое успели прочитать.
        /// </summary>
        private void ToastOnce(string key, string text)
        {
            if (!_told.Contains(key) && _queuedGameplayKeys.Add(key))
            {
                _gameplayToasts.Enqueue((key, text));
                ShowNextGameplayToast();
            }
        }

        /// <summary>Редкие игровые советы идут по одному, чтобы новый не заменял ещё непрочитанный.</summary>
        private readonly Queue<(string Key, string Text)> _gameplayToasts = new Queue<(string Key, string Text)>();
        private readonly HashSet<string> _queuedGameplayKeys = new HashSet<string>();
        private float _nextGameplayToastAt;

        private void ShowNextGameplayToast()
        {
            if (_gameplayToasts.Count == 0 || Time.unscaledTime < _nextGameplayToastAt || App.HasPopups || _ended || _failShown)
            {
                return;
            }

            var tip = _gameplayToasts.Dequeue();
            _queuedGameplayKeys.Remove(tip.Key);
            _told.Add(tip.Key);
            App.Toast(tip.Text);
            _nextGameplayToastAt = Time.unscaledTime + Mathf.Clamp(1.2f + tip.Text.Length * 0.025f, 1.9f, 3.6f) + 0.15f;
        }

        private void Deny(int column, int depth, RejectReason reason)
        {
            Sfx.Play(Sound.Deny);
            Haptics.Play(Haptic.Soft);
            var view = _field.FirstOrDefault(f => f.Column == column && f.Depth == depth);
            if (view != null)
            {
                Tweens.Shake(view.View.Root, 6f, 0.3f);
            }

            switch (reason)
            {
                case RejectReason.Locked:
                    var deepKey = DeepKeyColumn();
                    if (deepKey >= 0)
                    {
                        ToastOnce("locked", L.F("Ключ в колонке {0}: освободи клубки перед ним", deepKey + 1));
                    }
                    else
                    {
                        ToastOnce("locked", L.T("Под замком: сначала поставь на станок клубок с ключом"));
                    }
                    HighlightKey(column, depth);
                    break;
                case RejectReason.LinkNotReady:
                    ToastOnce("link", LinkPartnerText(column, depth));
                    PunchLinkPartner(column, depth);
                    HighlightHiddenPartner(column, depth);
                    break;
                case RejectReason.Frozen:
                    HighlightIce(column);
                    var ice = _game.Column(column)[0].Ice;
                    ToastOnce("ice", L.F("Во льду: оттает через {0} {1} этого цвета", ice, L.Plural(ice, "петлю", "петли", "петель")));
                    break;
                case RejectReason.NoFreeSlot:
                    var spool = depth < _game.Column(column).Count ? _game.Column(column)[depth] : null;
                    ToastOnce("noslot", spool != null && spool.Link != 0 && !_scissors
                        ? L.T("Связанным клубкам нужно 2 свободных места")
                        : spool != null && spool.Thick
                            ? L.T("Толстому клубку нужно 2 свободных места")
                            : L.T("Нет места: подожди, пока клубок довяжет"));
                    if (spool != null && spool.Link != 0 && !_scissors)
                    {
                        PunchLinkPartner(column, depth);
                    }
                    Tweens.Shake(_loomBg, 5f, 0.25f);
                    break;
            }
        }
    }
}
