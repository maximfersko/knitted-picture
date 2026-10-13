using System.Collections.Generic;
using System.Linq;
using KnittedPicture.Core;

namespace KnittedPicture.QA
{
    /// <summary>
    /// Покрытие: какие ситуации бот реально видел. Без этого «ошибок не найдено» ничего не значит —
    /// может, бот просто туда не доходил. Список Expected — то, что обязано встретиться за полный прогон;
    /// чего не было, отчёт показывает красным как слепое пятно.
    /// </summary>
    public sealed class Coverage
    {
        public static readonly string[] Expected =
        {
            "статус: победа", "статус: затор", "статус: моль съела",
            "отказ: колонка пуста", "отказ: нет слота", "отказ: замок", "отказ: лёд", "отказ: пара не готова", "отказ: игра окончена",
            "узелок развязан", "замок снят ключом", "скрытый клубок открылся", "пара поставлена", "толстый поставлен",
            "радуга связала петлю", "лишняя пряжа ушла со станка", "лёд растаял", "идеальный клубок",
            "ножницы с поля", "ножницы с поля при заторе", "срез со станка", "срез вернул клубок на поле", "срез во время полёта",
            "+1 слот", "слоты до максимума", "+1 слот при заторе", "отмена", "отмена после среза", "отмена при заторе",
            "отмена до самого начала", "спасение после моли", "ход при съеденной картинке", "подсказка привела к ходу",
            "натяжение держит столбец", "победа решена заранее", "совет: заблокировано", "совет: чужая пряжа", "совет: натяжение",
            "совет: узелки", "совет: короткие первыми", "слот поможет при заторе", "ножницы помогут при заторе", "чужая пряжа на станке",
        };

        public readonly Dictionary<string, long> Hits = new Dictionary<string, long>();

        public void Hit(string feature, long n = 1) => Hits[feature] = (Hits.TryGetValue(feature, out var v) ? v : 0) + n;

        public IEnumerable<string> Missing => Expected.Where(e => !Hits.ContainsKey(e));

        /// <summary>Записать всё, что случилось на действии. Возвращает «подпись» новизны для исследователя.</summary>
        public void Observe(LoomGame g, Before b, QaAction a, Outcome o)
        {
            if (g.Status != b.Status)
            {
                Hit(g.Status switch
                {
                    GameStatus.Won => "статус: победа",
                    GameStatus.Jammed => "статус: затор",
                    GameStatus.TimeUp => "статус: моль съела",
                    _ => "статус: снова игра",
                });
            }

            if (o.Move != null)
            {
                var m = o.Move;
                if (!m.Accepted)
                {
                    Hit(m.Reason switch
                    {
                        RejectReason.EmptyColumn => "отказ: колонка пуста",
                        RejectReason.NoFreeSlot => "отказ: нет слота",
                        RejectReason.Locked => "отказ: замок",
                        RejectReason.Frozen => "отказ: лёд",
                        RejectReason.LinkNotReady => "отказ: пара не готова",
                        RejectReason.GameOver => "отказ: игра окончена",
                        _ => "отказ: " + m.Reason,
                    });
                    if (m.Reason == RejectReason.GameOver && b.Status == GameStatus.TimeUp)
                    {
                        Hit("ход при съеденной картинке");
                    }
                }
                else
                {
                    if (m.Kind == MoveKind.KnotUntied)
                    {
                        Hit("узелок развязан");
                    }

                    if (m.Unlocked.Count > 0)
                    {
                        Hit("замок снят ключом");
                    }

                    if (m.Revealed.Count > 0)
                    {
                        Hit("скрытый клубок открылся");
                    }

                    if (m.Placed.Count == 2)
                    {
                        Hit("пара поставлена");
                    }

                    if (m.Placed.Any(p => p.Spool.Thick))
                    {
                        Hit("толстый поставлен");
                    }

                    if (m.Placed.Any(p => !p.Spool.Rainbow && g.IsForeign(p.Spool)))
                    {
                        Hit("чужая пряжа на станке");
                    }

                    if (a.Kind == ActionKind.Pull)
                    {
                        Hit("ножницы с поля");
                        if (b.Status == GameStatus.Jammed)
                        {
                            Hit("ножницы с поля при заторе");
                        }
                    }

                    if (a.Kind == ActionKind.Hint)
                    {
                        Hit("подсказка привела к ходу");
                    }
                }
            }

            foreach (var r in o.Steps)
            {
                foreach (var st in r.Stitches)
                {
                    if (b.Colors.TryGetValue(st.SpoolId, out var sp) && sp.Rainbow)
                    {
                        Hit("радуга связала петлю");
                    }
                }

                foreach (var c in r.Completed)
                {
                    if (c.Leftover > 0)
                    {
                        Hit("лишняя пряжа ушла со станка");
                    }

                    if (c.Perfect)
                    {
                        Hit("идеальный клубок");
                    }
                }

                if (r.Thawed.Count > 0)
                {
                    Hit("лёд растаял");
                }
            }

            switch (a.Kind)
            {
                case ActionKind.Cut when o.Flag == true:
                    Hit("срез со станка");
                    if (State.Spools(g).Count() == b.Loops.Count)
                    {
                        Hit("срез вернул клубок на поле");
                    }

                    if (a.A >= 0 && a.A < b.Warmups.Length && b.Warmups[a.A] > 0)
                    {
                        Hit("срез во время полёта");
                    }

                    break;
                case ActionKind.AddSlot when o.Flag == true:
                    Hit("+1 слот");
                    if (g.Slots.Count == LoomGame.MaxSlots)
                    {
                        Hit("слоты до максимума");
                    }

                    if (b.Status == GameStatus.Jammed)
                    {
                        Hit("+1 слот при заторе");
                    }

                    break;
                case ActionKind.Undo when o.Flag == true:
                    Hit("отмена");
                    if (b.Status == GameStatus.Jammed)
                    {
                        Hit("отмена при заторе");
                    }

                    if (!g.CanUndo)
                    {
                        Hit("отмена до самого начала");
                    }

                    break;
                case ActionKind.AddTime when b.Status == GameStatus.TimeUp && g.Status == GameStatus.Playing:
                    Hit("спасение после моли");
                    break;
            }

            if (g.Status == GameStatus.Playing && Enumerable.Range(0, g.Width).Any(g.IsHeld))
            {
                Hit("натяжение держит столбец");
            }

            if (g.Status == GameStatus.Jammed && b.Status != GameStatus.Jammed)
            {
                Hit(g.Advice() switch
                {
                    JamAdvice.Blocked => "совет: заблокировано",
                    JamAdvice.ForeignYarn => "совет: чужая пряжа",
                    JamAdvice.Tension => "совет: натяжение",
                    JamAdvice.Knots => "совет: узелки",
                    _ => "совет: короткие первыми",
                });
                if (g.SlotWouldHelp)
                {
                    Hit("слот поможет при заторе");
                }

                if (g.ScissorsWouldHelp)
                {
                    Hit("ножницы помогут при заторе");
                }
            }
        }

        /// <summary>Отмену после среза засчитываем отдельно: эпизод знает, что было перед отменой.</summary>
        public void UndoAfterCut() => Hit("отмена после среза");

        public void WinCertain() => Hit("победа решена заранее");

        public void Merge(Coverage other)
        {
            foreach (var kv in other.Hits)
            {
                Hit(kv.Key, kv.Value);
            }
        }
    }
}
