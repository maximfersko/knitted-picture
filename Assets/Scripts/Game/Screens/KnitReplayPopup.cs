#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Крупный просмотр «как вязалось» из карточки уровня в альбоме: картинка в выбранной рамке альбома вяжется
    /// заново «змейкой» по рядам. «Ещё раз» запускает прогон снова; тап по картинке во время прогона показывает готовое.
    /// Закрывается крестиком, тапом мимо и системной «Назад».
    /// </summary>
    public sealed class KnitReplayPopup : Popup
    {
        private const float PlaySeconds = 3.5f;

        public KnitReplayPopup(App app, LevelData level)
            : base(app)
        {
            AddTitle(level.Title, 24);

            // Рамка и картинка в проёме, как у плитки альбома (MiniPicture.Framed), но таймлапс вместо готовой картинки.
            var s = Mathf.Min(CardWidth - 48f, 300f);
            var frameId = app.Progress.SelectedFrame.Id;
            var holder = Ui.Place(Ui.Node(Card, "Replay"), (CardWidth - s) / 2f, Cursor, s, s);
            var frame = Ui.Image(holder, "Frame", Art2.Frame(frameId), Color.white);
            Ui.Place(frame.rectTransform, 0, 0, s, s);
            var replay = KnitReplay.Create(holder, level, s / 2f, s / 2f, s * FrameRaster.Opening);
            FrameFx.Attach(holder, frameId, s, lite: false);
            Space(s + 16f);

            void Again()
            {
                replay.Play(PlaySeconds);
                Analytics.Event("replay_view", ("from", "album"), ("level", level.id));
            }

            replay.IdleTap = Again;
            AddPrimary(L.T("Ещё раз")).OnClick(Again);
            Finish(8);
            Again();
        }
    }
}
