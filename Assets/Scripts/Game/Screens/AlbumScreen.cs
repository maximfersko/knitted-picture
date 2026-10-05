#nullable enable

using KnittedPicture.Core;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Альбом: все коллекции, готовые картинки можно перевязать.</summary>
    public sealed class AlbumScreen : ScreenView
    {
        public AlbumScreen(App app)
            : base(app, "Album")
        {
            Ui.RoundButton(Root, Icon.Back, 16, 8).OnClick(app.ShowHome);
            Ui.Label(Root, "Альбом", 24, Palette.Ink, 70, 8, W - 140, 44);
            Ui.CoinPill(Root, app.Progress.Coins, W - 16, 8);

            var y = 72f;
            var gap = 10f;
            foreach (var collection in Collections.All)
            {
                var tile = Mathf.Floor((W - 32f - gap * (collection.Count - 1)) / collection.Count);
                var done = 0;
                for (var id = collection.FirstLevel; id <= collection.LastLevel; id++)
                {
                    if (app.Progress.IsCompleted(id))
                    {
                        done++;
                    }
                }

                Ui.Label(Root, collection.Name, 18, Palette.Ink, 18, y, W - 140, 26, true, TextAnchor.MiddleLeft);
                Ui.Label(Root, $"{done} из {collection.Count}", 15, Palette.Ink2, W - 118, y, 100, 26, true, TextAnchor.MiddleRight);
                y += 34f;

                for (var i = 0; i < collection.Count; i++)
                {
                    var id = collection.FirstLevel + i;
                    if (!app.Levels.Has(id))
                    {
                        continue;
                    }

                    var level = app.Levels.Get(id);
                    var state = app.Progress.IsCompleted(id) ? TileState.Done : app.Progress.IsAvailable(id) ? TileState.Current : TileState.Locked;
                    var x = 16 + i * (tile + gap);
                    MiniPicture.Tile(Root, level, state, x, y, tile).OnClick(() => app.StartLevel(id));
                    var name = Ui.Label(Root, state == TileState.Locked ? "???" : level.name, 11, Palette.Ink2, x - 4, y + tile + 4, tile + 8, 28, false, TextAnchor.UpperCenter);
                    name.lineSpacing = 0.85f;
                }

                y += tile + 44f;
            }
        }
    }
}
