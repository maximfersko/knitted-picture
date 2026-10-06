#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Подарок: монеты, бустеры и, может быть, моток пряжи в коллекцию.</summary>
    public readonly struct Reward
    {
        public Reward(int coins, BoosterType? booster = null, int boosterCount = 0, int yarn = -1)
        {
            Coins = coins;
            Booster = booster;
            BoosterCount = booster == null ? 0 : System.Math.Max(1, boosterCount);
            Yarn = yarn;
        }

        public int Coins { get; }

        public BoosterType? Booster { get; }

        public int BoosterCount { get; }

        /// <summary>Моток пряжи из каталога, -1 — без пряжи.</summary>
        public int Yarn { get; }

        public static Reward CoinsOnly(int coins) => new Reward(coins);
    }

    /// <summary>Что игрок получил, когда забрал подарок: моток мог оказаться повтором — тогда монеты.</summary>
    public readonly struct Granted
    {
        public Granted(Reward reward, bool newYarn, int duplicateCoins)
        {
            Reward = reward;
            NewYarn = newYarn;
            DuplicateCoins = duplicateCoins;
        }

        public Reward Reward { get; }

        public bool NewYarn { get; }

        public int DuplicateCoins { get; }

        public int TotalCoins => Reward.Coins + DuplicateCoins;
    }
}
