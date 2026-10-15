#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Посылка после перерыва ----------

        /// <summary>Сколько дней без игры нужно для посылки от бабушки.</summary>
        public const int ComebackDays = 3;

        /// <summary>Посылка не чаще, чем раз в столько дней: редко играющим она не становится вторым подарком дня.</summary>
        public const int ComebackCooldown = 14;

        /// <summary>
        /// Игрок зашёл в игру: если его не было ComebackDays дней и больше, ждёт посылка. Запоминает день.
        /// Часы назад посылку не дают и последний день не сдвигают назад.
        /// </summary>
        public bool Visit(int today)
        {
            if (_data.lastDay >= 0 && today - _data.lastDay >= ComebackDays && _data.completed.Count > 0 && !_data.comebackPending &&
                today - _data.comebackLastDay >= ComebackCooldown)
            {
                _data.comebackPending = true;
                _data.comebackLastDay = today;
                _data.comebackDays = today - _data.lastDay;
            }

            _data.lastDay = System.Math.Max(_data.lastDay, today);
            return _data.comebackPending;
        }

        public bool ComebackPending => _data.comebackPending;

        /// <summary>Редкость посылки: на ступень выше следующего сундука (не ниже необычной).</summary>
        public Rarity ComebackRarity => (Rarity)System.Math.Max((int)Rarity.Uncommon, System.Math.Min((int)Rarity.Legendary, (int)NextChestRarity + 1));

        /// <summary>
        /// Открыть посылку: монеты как в сундуке этой редкости (середина), бустер из открытых и моток не ниже
        /// её редкости (легендарная — не ниже эпической). Следующая новая картинка — с полным тёплым стартом.
        /// </summary>
        public Granted? ClaimComeback(System.Random rng)
        {
            if (!_data.comebackPending)
            {
                return null;
            }

            var rarity = ComebackRarity;
            var range = Chest.CoinRange(rarity);
            var open = new List<BoosterType>();
            foreach (BoosterType type in System.Enum.GetValues(typeof(BoosterType)))
            {
                if (IsBoosterOpen(type, CurrentLevel))
                {
                    open.Add(type);
                }
            }

            BoosterType? booster = open.Count > 0 ? open[rng.Next(open.Count)] : (BoosterType?)null;
            var floor = rarity > Rarity.Epic ? Rarity.Epic : rarity;
            var yarn = RollYarn(rng, floor);
            _data.comebackPending = false;
            _data.comebackWarm = true;
            return Grant(new Reward((range.Min + range.Max) / 2, booster, Chest.BoosterCountOf(rarity), yarn), rng);
        }
    }
}
