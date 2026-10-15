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

            var result = _scissors ? _game.PullSpool(column, depth) : _game.TapColumn(column);
            if (!result.Accepted)
            {
                Deny(column, depth, result.Reason);
                return;
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
                Sfx.Play(Sound.Knot);
                Haptics.Play(Haptic.Tap);
                RefreshField();
                RefreshBoosters();
                var view = FindFieldView(result.Knotted!.Id);
                if (view != null)
                {
                    Tweens.Punch(view.Root, 0.2f, 0.25f);
                    Fx.Sparkle(_fx, Ui.WorldCenter(view.Root), Palette.Danger, 6, 26f);
                }

                return;
            }

            Sfx.Play(Sound.Pick);
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
            var flight = LoomGame.WarmupSteps * (_tempo.Rush ? StepInterval / RushSpeed : StepInterval);
            foreach (var (placed, from) in flights)
            {
                var spool = placed.Spool;
                var flyer = SpoolView.Create(_fx, _spoolSize);
                flyer.Bind(spool, _colors[spool.Color], true);
                var target = SlotTarget(placed.Slot);
                var scale = SpoolSizeAt(placed.Slot) / _spoolSize;

                // Клубок взлетает дугой: в начале вытягивается вдоль рывка (его «взяли»), в полёте чуть
                // поворачивается, у станка сжимается к размеру слота. Приземление — сплющивание с отскоком и пух.
                var lift = (40f + Mathf.Abs(target.x - from.x) / Mathf.Max(0.0001f, _fx.lossyScale.x) * 0.12f) * _fx.lossyScale.x;
                var control = (from + target) / 2f + new Vector3(0f, lift, 0f);
                var spin = (target.x > from.x ? -1f : 1f) * 14f;
                var rt = flyer.Root;
                rt.position = from;
                Tweens.Run(rt, flight, t =>
                {
                    var e = Ease.OutCubic(t);
                    var u = 1f - e;
                    rt.position = u * u * from + 2f * u * e * control + e * e * target;
                    var size = Mathf.Lerp(1f, scale, t) * (1f + 0.12f * Ease.Bump(t));
                    var stretch = t < 0.22f ? 0.18f * Ease.Bump(t / 0.22f) : 0f;
                    rt.localScale = new Vector3(size * (1f - stretch * 0.6f), size * (1f + stretch), 1f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, spin * Ease.Bump(t));
                }, null, () =>
                {
                    Object.Destroy(flyer.Root.gameObject);
                    _arriving.Remove(spool.Id);
                    SyncLoom();
                    var slot = _slotViews[placed.Slot];
                    if (slot != null && slot.SpoolId == spool.Id)
                    {
                        var landed = slot.View.Root;
                        Tweens.Run(landed, 0.34f, t =>
                        {
                            var j = Ease.Jolt(t);
                            landed.localScale = new Vector3(1f + 0.22f * j, 1f - 0.18f * j, 1f);
                        }, null, () => landed.localScale = Vector3.one);
                        Fx.Dust(_fx, target + new Vector3(0f, -SpoolSizeAt(placed.Slot) * 0.42f * _fx.lossyScale.x, 0f), _colors[spool.Color]);
                        Haptics.Play(Haptic.Tap);
                    }
                });
            }

            SyncLoom();
        }

        /// <summary>Тап по слоту станка: с ножницами — срезать клубок, без них — подсказка про чужую пряжу.</summary>
        private void OnSlotTap(int slot)
        {
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
                    ToastOnce("foreign-loom", L.T("Эта пряжа картинке не нужна: срежь её ножницами"));
                }
                else if (spool.Warmup == 0 && !_game.HasWork(spool) && !_game.IsSurplus(spool))
                {
                    // Бледный клубок на станке: почему он не вяжет.
                    var held = Enumerable.Range(0, _game.Width).Any(x => _game.IsHeld(x) && _game.ColorAt(x, _game.FrontY(x)) == spool.Color);
                    ToastOnce("waiting", held
                        ? L.T("Ждёт: сначала довяжи клетки ниже пунктира")
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
        /// Пояснение показываем один раз за уровень на каждый случай: дальше отказ — просто встряска
        /// клубка и звук. Иначе за уровень набегал десяток одинаковых подсказок внизу экрана.
        /// </summary>
        private void ToastOnce(string key, string text)
        {
            if (_told.Add(key))
            {
                App.Toast(text);
            }
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
                    ToastOnce("locked", L.T("Под замком: сначала поставь на станок клубок с ключом"));
                    break;
                case RejectReason.LinkNotReady:
                    ToastOnce("link", LinkPartnerText(column, depth));
                    PunchLinkPartner(column, depth);
                    break;
                case RejectReason.Frozen:
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
                    Tweens.Shake(_loomBg, 5f, 0.25f);
                    break;
            }
        }
    }
}
