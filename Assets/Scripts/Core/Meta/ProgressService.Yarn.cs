#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace KnittedPicture.Core
{
    public sealed partial class ProgressService
    {
        // ---------- Корзинка пряжи ----------

        public int YarnCount(int yarn) => yarn >= 0 && yarn < _data.yarns.Count ? _data.yarns[yarn] : 0;

        public bool HasYarn(int yarn) => YarnCount(yarn) > 0;

        /// <summary>Уровень мотка 0..3: сколько штук собрано, но не больше максимума.</summary>
        public int YarnLevel(int yarn) => System.Math.Min(YarnCount(yarn), YarnCatalog.MaxLevel);

        /// <summary>Шаги к золотой версии: повторы сверх максимального уровня, 0..YarnCatalog.GoldenAfter.</summary>
        public int GoldProgress(int yarn) => System.Math.Max(0, System.Math.Min(YarnCount(yarn) - YarnCatalog.MaxLevel, YarnCatalog.GoldenAfter));

        /// <summary>Золотая версия мотка: особый вид и бонус сильнее на 1.</summary>
        public bool IsGolden(int yarn) => YarnCount(yarn) >= YarnCatalog.MaxLevel + YarnCatalog.GoldenAfter;

        /// <summary>Моток «в работе», -1 — нет (или его нет в каталоге этой версии игры).</summary>
        public int ActiveYarn => _data.activeYarn < YarnCatalog.Count && HasYarn(_data.activeYarn) ? _data.activeYarn : -1;

        /// <summary>Взять моток в работу: его бонус будет действовать в уровнях.</summary>
        public bool SetActiveYarn(int yarn)
        {
            if (yarn >= YarnCatalog.Count || !HasYarn(yarn))
            {
                return false;
            }

            _data.activeYarn = yarn;
            return true;
        }

        /// <summary>Бонусы пряжи уже открыты: игрок дошёл до <see cref="YarnCatalog.PerksFromLevel"/>.</summary>
        public bool PerksOpen => _data.nextLevel >= YarnCatalog.PerksFromLevel;

        /// <summary>Сила бонуса perk от мотка в работе; 0 — бонусы ещё закрыты, у мотка другой бонус или мотка нет.</summary>
        public int PerkPower(YarnPerk perk)
        {
            var yarn = ActiveYarn;
            return PerksOpen && yarn >= 0 && YarnCatalog.PerkOf(yarn) == perk ? YarnCatalog.Power(yarn, YarnLevel(yarn), IsGolden(yarn)) : 0;
        }

        /// <summary>Сила мотка, если получить ещё один экземпляр: уровень растёт до максимума, затем копится золото.</summary>
        public int PowerAfterTake(int yarn)
        {
            if (yarn < 0 || yarn >= YarnCatalog.Count)
            {
                return 0;
            }

            var level = System.Math.Min(YarnLevel(yarn) + 1, YarnCatalog.MaxLevel);
            var golden = YarnCount(yarn) + 1 >= YarnCatalog.MaxLevel + YarnCatalog.GoldenAfter;
            return YarnCatalog.Power(yarn, level, golden);
        }

        /// <summary>Сила мотка на спицах; 0 — мотка в работе нет.</summary>
        public int ActivePower
        {
            get
            {
                var yarn = ActiveYarn;
                return yarn < 0 ? 0 : PowerOf(yarn);
            }
        }

        /// <summary>Текущая сила имеющегося мотка; 0 — такого мотка нет.</summary>
        public int PowerOf(int yarn)
        {
            return yarn >= 0 && yarn < YarnCatalog.Count && HasYarn(yarn) ? YarnCatalog.Power(yarn, YarnLevel(yarn), IsGolden(yarn)) : 0;
        }

        /// <summary>
        /// Моток yarn при силе power сильнее мотка на спицах: тот же бонус и сила больше (разные бонусы не сравниваются,
        /// монеты за картинку и монеты за петлю не «лучше» друг друга). Единственное место, где живёт это правило.
        /// </summary>
        public bool StrongerThanActive(int yarn, int power)
        {
            var active = ActiveYarn;
            return PerksOpen && yarn != active && (active < 0 || (YarnCatalog.PerkOf(yarn) == YarnCatalog.PerkOf(active) && power > ActivePower));
        }

        /// <summary>
        /// Знакомство с натяжением полотна — один раз, в тот момент, когда оно впервые остановило столбик.
        /// </summary>
        public bool TakeTensionIntro()
        {
            const int introId = 101;
            if (_data.introsShown.Contains(introId))
            {
                return false;
            }

            _data.introsShown.Add(introId);
            return true;
        }

        /// <summary>
        /// Знакомство с бонусами пряжи — один раз, на первом уровне не ниже <see cref="YarnCatalog.PerksFromLevel"/>:
        /// если на самом уровне открытия оно не показалось (там мог быть новый бустер), покажется на следующем.
        /// </summary>
        public bool TakePerksIntro(int levelId)
        {
            const int introId = 100;
            if (levelId < YarnCatalog.PerksFromLevel || _data.introsShown.Contains(introId))
            {
                return false;
            }

            _data.introsShown.Add(introId);
            return true;
        }

        public int YarnKinds
        {
            get
            {
                var kinds = 0;
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    if (HasYarn(i))
                    {
                        kinds++;
                    }
                }

                return kinds;
            }
        }

        /// <summary>Сколько разных мотков без сезонных: для «Корзинка полна».</summary>
        public int BaseYarnKinds
        {
            get
            {
                var kinds = 0;
                for (var i = 0; i < YarnCatalog.Count; i++)
                {
                    kinds += HasYarn(i) && !YarnCatalog.All[i].Seasonal ? 1 : 0;
                }

                return kinds;
            }
        }

        /// <summary>Положить моток в корзинку. true — такого ещё не было.</summary>
        public bool AddYarn(int yarn)
        {
            if (yarn < 0 || yarn >= YarnCatalog.Count)
            {
                return false;
            }

            while (_data.yarns.Count <= yarn)
            {
                _data.yarns.Add(0);
            }

            _data.yarns[yarn]++;

            // Первый моток сразу берётся в работу: бонус начинает действовать без лишних шагов.
            if (!HasYarn(_data.activeYarn))
            {
                _data.activeYarn = yarn;
            }
            return _data.yarns[yarn] == 1;
        }

        /// <summary>
        /// Случайный моток: редкие выпадают реже, ненайденные в полтора раза чаще. Если двенадцать мотков
        /// подряд не было эпического, следующий — эпический (защита от невезения).
        /// </summary>
        public int RollYarn(System.Random rng) => RollYarn(rng, Rarity.Common);

        /// <summary>Случайный моток не ниже редкости floor (посылка после перерыва, награды сезона).</summary>
        public int RollYarn(System.Random rng, Rarity floor)
        {
            var epicOnly = _data.yarnsSinceEpic >= YarnCatalog.EpicPity;
            if (epicOnly && floor < Rarity.Epic)
            {
                floor = Rarity.Epic;
            }

            var total = 0;
            var weights = new int[YarnCatalog.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                var rarity = YarnCatalog.All[i].Rarity;
                weights[i] = rarity < floor ? 0 : YarnCatalog.RollWeight(i, HasYarn(i));
                total += weights[i];
            }

            var pick = PickWeighted(weights, total, rng);
            _data.yarnsSinceEpic = YarnCatalog.All[pick].Rarity >= Rarity.Epic ? 0 : _data.yarnsSinceEpic + 1;
            return pick;
        }

        private static int PickWeighted(int[] weights, int total, System.Random rng)
        {

            var roll = rng.Next(total);
            for (var i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        public bool IsSetDone(YarnSet set)
        {
            for (var i = set.First; i < set.First + set.Count; i++)
            {
                if (!HasYarn(i))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanClaimSet(YarnSet set) => IsSetDone(set) && !_data.yarnSetsClaimed.Contains(set.Id);

        public bool IsSetClaimed(YarnSet set) => _data.yarnSetsClaimed.Contains(set.Id);

        public Granted? ClaimSet(YarnSet set, System.Random rng)
        {
            if (!CanClaimSet(set))
            {
                return null;
            }

            _data.yarnSetsClaimed.Add(set.Id);
            return Grant(set.Reward, rng);
        }
    }
}
