#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Награды ----------

        /// <summary>Выдать награду. Случайный моток разыгрывается здесь; повтор превращается в монеты.</summary>
        public Granted Grant(Reward reward, System.Random rng)
        {
            _data.coins += reward.Coins;
            if (reward.Booster != null)
            {
                AddStock(reward.Booster.Value, reward.BoosterCount);
            }

            var yarn = reward.Yarn == DailyGift.RandomYarn ? RollYarn(rng) : reward.Yarn;
            var granted = new Reward(reward.Coins, reward.Booster, reward.BoosterCount, yarn);
            if (yarn < 0)
            {
                return new Granted(granted, false, 0);
            }

            // Повтор сначала поднимает уровень мотка, на максимальном уровне копит к золотой версии,
            // и только у золотого мотка становится монетами.
            var maxedBefore = YarnLevel(yarn) >= YarnCatalog.MaxLevel;
            var goldenBefore = IsGolden(yarn);
            var isNew = AddYarn(yarn);
            var goldStep = !isNew && maxedBefore && !goldenBefore ? GoldProgress(yarn) : 0;
            var duplicate = isNew || !maxedBefore || !goldenBefore ? 0 : YarnCatalog.DuplicateCoins(YarnCatalog.All[yarn].Rarity);
            _data.coins += duplicate;
            return new Granted(granted, isNew, duplicate, goldStep);
        }
    }
}
