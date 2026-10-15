#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Звёздный сундук ----------

        /// <summary>Сколько звёзд нужно на сундук.</summary>
        public const int ChestEvery = 12;

        /// <summary>Звёзды к следующему сундуку (могут быть больше ChestEvery, если сундук не открыт).</summary>
        public int ChestProgress => System.Math.Max(0, TotalStars - _data.chestsOpened * ChestEvery);

        public bool CanOpenChest => ChestProgress >= ChestEvery;

        /// <summary>Сколько сундуков уже открыто: номер следующего.</summary>
        public int ChestsOpened => _data.chestsOpened;

        /// <summary>Следующий сундук — большой (каждый Chest.Cycle-й).</summary>
        public bool NextChestBig => Chest.IsBig(_data.chestsOpened);

        /// <summary>Редкость сундука, который откроется следующим.</summary>
        public Rarity NextChestRarity => Chest.RarityOf(_data.chestsOpened);

        /// <summary>Что лежит в сундуке, который откроется следующим. Не меняет сохранение.</summary>
        public ChestContents PeekChest() => Chest.Roll(_data.chestsOpened, CurrentLevel, HasYarn);

        /// <summary>
        /// Открыть сундук: монеты, выбранный бустер и выбранный моток (повтор — монетами).
        /// null — сундук ещё не накоплен или выбор вне списка.
        /// </summary>
        public Granted? ClaimChest(int boosterChoice, int yarnChoice)
        {
            if (!CanOpenChest)
            {
                return null;
            }

            var contents = PeekChest();
            if (boosterChoice < 0 || boosterChoice >= contents.Boosters.Length || yarnChoice < 0 || yarnChoice >= contents.Yarns.Length)
            {
                return null;
            }

            _data.chestsOpened++;
            var reward = new Reward(contents.Coins, contents.Boosters[boosterChoice], contents.BoosterCount, contents.Yarns[yarnChoice]);
            return Grant(reward, new System.Random(0));
        }
    }
}
