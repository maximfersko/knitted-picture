#nullable enable

using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;
using UnityEngine;
using UnityEngine.UI;

namespace KnittedPicture.Game
{
    /// <summary>Бустеры: место, ножницы, отмена, время; оплата и режим ножниц.</summary>
    public sealed partial class GameScreen
    {
        // ---------- Бустеры ----------

        private void OnBooster(BoosterType type)
        {
            CancelResponsiveInput();
            if (_ended || _failShown || _finishing)
            {
                return;
            }

            if (!App.Progress.IsBoosterOpen(type, BoosterLevel))
            {
                DenyBooster(type, L.F("Откроется на уровне {0}", Economy.UnlockLevel(type)));
                return;
            }

            switch (type)
            {
                case BoosterType.Scissors:
                    if (_scissors)
                    {
                        SetScissors(false);
                    }
                    else if (_run.FreeScissors > 0 || App.Progress.CanAfford(type))
                    {
                        SetScissors(true);
                    }
                    else
                    {
                        NotEnough(type);
                    }

                    break;

                case BoosterType.Undo:
                    if (!_game.CanUndo)
                    {
                        DenyBooster(BoosterType.Undo, L.T("Пока нечего отменять"));
                    }
                    else if (_run.FreeUndo == 0 && App.Progress.Pay(type) == Payment.NotEnough)
                    {
                        NotEnough(type);
                    }
                    else
                    {
                        // Подарок серии — сначала он, и звезда за него не снимается.
                        if (_run.FreeUndo > 0)
                        {
                            _run.FreeUndo--;
                        }
                        else
                        {
                            _boostersUsed++;
                        }

                        App.Save();
                        TrackBooster(BoosterType.Undo, free: false);
                        _game.Undo();
                        _tempo.Break();
                        _finishedColors.Clear();
                        RestoreUndoneGold();
                        Sfx.Play(Sound.Pick);
                        RefreshAll();
                        CheckStatus();
                    }

                    break;

                case BoosterType.Slot:
                    if (_game.Slots.Count >= LoomGame.MaxSlots)
                    {
                        DenyBooster(BoosterType.Slot, L.F("На станке уже {0} мест, больше нельзя", LoomGame.MaxSlots));
                    }
                    else if (App.Progress.Pay(type) == Payment.NotEnough)
                    {
                        NotEnough(type);
                    }
                    else
                    {
                        App.Save();
                        _boostersUsed++;
                        TrackBooster(BoosterType.Slot, free: false);
                        _game.AddSlot();
                        RebuildSlots();
                        RefreshAll();
                        Sfx.Play(Sound.Unlock);
                        Tweens.Scale(_slotFrames[_slotFrames.Length - 1], 0.4f, 1f, 0.3f);
                        CheckStatus();
                    }

                    break;
            }

            _coins.text = App.Progress.Coins.ToString();
        }

        /// <summary>Отмена развязала золотые петли: они снова в игре, а засчитанное за них снимается.</summary>
        private void RestoreUndoneGold()
        {
            foreach (var key in _goldDone.Keys.ToList())
            {
                int x = key % _game.Width, y = key / _game.Width;
                if (!_game.IsKnitted(x, y))
                {
                    _goldDone.Remove(key);
                    _picture.RestoreGold(x, y);
                }
            }

            _goldCaught = _goldDone.Values.Count(caught => caught);
            _goldCoins = _goldCaught * _goldPerLoop;
            RefreshGoals();
        }

        /// <summary>
        /// Отмена, ножницы или новый слот меняют состояние вне шага вязки: если после них станок
        /// встал (или картинка готова), показать это сразу, иначе экран «зависнет» без окна.
        /// </summary>
        private void CheckStatus()
        {
            if (_ended || _failShown)
            {
                return;
            }

            if (_game.Status == GameStatus.Won || _game.Status == GameStatus.Jammed)
            {
                AfterStatus(_game.Status);
            }
        }

        /// <summary>Ножницы сработали: сначала тратятся бесплатные от серии (без потери звезды), потом запас или монеты.</summary>
        private void PayScissors()
        {
            // Ножницы из окна затора — это спасение, как «+1 слот»: одна звезда, а не две, и цена спасения
            // растёт с каждым следующим в этой попытке. Подарочные ножницы (серия, «Не сдаёмся») не спасение и
            // не бустер: ни звезду, ни цену следующих спасений они не трогают, даже взятые в окне затора.
            var free = _run.FreeScissors > 0;
            var rescue = _scissorsRescue && !free;
            var rescuesBefore = _rescues;
            _scissorsRescue = false;
            if (rescue)
            {
                _rescues++;
            }

            TrackBooster(BoosterType.Scissors, free: _run.FreeScissors > 0);
            if (_run.FreeScissors > 0)
            {
                _run.FreeScissors--;
            }
            else
            {
                if (rescue)
                {
                    App.Progress.PayRescue(BoosterType.Scissors, rescuesBefore);
                }
                else
                {
                    App.Progress.Pay(BoosterType.Scissors);
                }

                _boostersUsed++;
            }

            App.Save();
        }

        private void NotEnough(BoosterType type)
        {
            App.ShowPopup(new NotEnoughPopup(App, type, RefreshBoosters));
        }

        /// <summary>Единый отказ на кнопке бустера: тряска, звук, толчок и причина тостом.</summary>
        private void DenyBooster(BoosterType type, string text)
        {
            Ui.Deny(_boosters.TryGetValue(type, out var button) ? button.Tap.transform as RectTransform : null, text);
        }

        private void SetScissors(bool on)
        {
            CancelResponsiveInput();
            if (_scissors == on)
            {
                return;
            }

            if (!on)
            {
                // Передумал резать — спасение не засчитываем.
                _scissorsRescue = false;
            }

            _scissors = on;
            HideHint();
            if (on)
            {
                HideTip();
                _scissorsBanner = Ui.Card(Root, "ScissorsBanner", Pad, _fieldY - 50f, W - 2 * Pad, 40, 20, Palette.Primary, Palette.Primary, 0f);
                Ui.IconImage(_scissorsBanner, Icon.Scissors, Color.white, 24, 20, 20);
                Ui.Label(_scissorsBanner, L.T("Выбери клубок на поле или на станке"), 15, Color.white, 40, 0, W - 2 * Pad - 52, 40, true, TextAnchor.MiddleLeft);
                var group = _scissorsBanner.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
            }
            else
            {
                HideScissorsBanner();
            }

            RefreshField();
            RefreshBoosters();

            // Передумал резать, а станок стоит в заторе: вернуть окно затора.
            if (!on && !_failShown && !_ended && _game.Status == GameStatus.Jammed)
            {
                _failShown = true;
                ShowFail(FailKind.Jam);
            }
        }

        private void HideScissorsBanner()
        {
            if (_scissorsBanner != null)
            {
                Object.Destroy(_scissorsBanner.gameObject);
                _scissorsBanner = null;
            }
        }
    }
}
