#nullable enable

using System;

namespace KnittedPicture.Core
{
    /// <summary>
    /// Комбо за темп. Каждый клубок, поставленный на станок, растит серию — если станок
    /// не простаивал дольше <see cref="Grace"/> секунд. Простой дольше — серия сгорает.
    /// С <see cref="RushFrom"/> включается «Разгон»: станок вяжет вдвое быстрее,
    /// а каждые <see cref="RushStitchesPerCoin"/> петель дают монету.
    /// </summary>
    public sealed class TempoCombo
    {
        /// <summary>Сколько секунд станок может простоять, прежде чем серия сгорит.</summary>
        public const float Grace = 1.0f;

        /// <summary>Серия, с которой начинается «Разгон».</summary>
        public const int RushFrom = 5;

        /// <summary>Серия, с которой комбо видно игроку и считается для золотых петель.</summary>
        public const int ActiveFrom = 2;

        public const int RushStitchesPerCoin = 6;

        private int _rushStitches;

        /// <summary>Комбо со стандартными правилами или с бонусами пряжи «в работе».</summary>
        public TempoCombo(float grace = Grace, int rushStitchesPerCoin = RushStitchesPerCoin)
        {
            GraceTime = grace;
            StitchesPerCoin = rushStitchesPerCoin;
        }

        /// <summary>Сколько секунд простоя переживает серия на этом уровне.</summary>
        public float GraceTime { get; }

        /// <summary>Сколько петель «Разгона» дают монету на этом уровне.</summary>
        public int StitchesPerCoin { get; }

        public int Combo { get; private set; }

        /// <summary>Лучшая серия за уровень.</summary>
        public int Best { get; private set; }

        /// <summary>Секунды простоя станка с последней работы.</summary>
        public float Idle { get; private set; }

        public bool Active => Combo >= ActiveFrom;

        public bool Rush => Combo >= RushFrom;

        /// <summary>Монеты, заработанные в «Разгоне».</summary>
        public int RushCoins { get; private set; }

        /// <summary>Сколько осталось до сгорания серии: 1 — станок работает, 0 — серия сгорела.</summary>
        public float Heat => Combo > 0 ? Math.Max(0f, 1f - Idle / GraceTime) : 0f;

        /// <summary>Сколько раз за уровень включался «Разгон».</summary>
        public int RushCount { get; private set; }

        /// <summary>Клубок встал на станок. Возвращает новую длину серии.</summary>
        public int OnPlaced()
        {
            Combo++;
            if (Combo == RushFrom)
            {
                RushCount++;
            }

            Idle = 0;
            Best = Math.Max(Best, Combo);
            return Combo;
        }

        /// <summary>
        /// Время идёт. working — станку есть чем заняться (клубок вяжет или долетает).
        /// Возвращает true, если серия сгорела именно сейчас.
        /// </summary>
        public bool Update(float dt, bool working)
        {
            if (Combo == 0)
            {
                Idle = 0;
                return false;
            }

            if (working)
            {
                Idle = 0;
                return false;
            }

            Idle += dt;
            if (Idle < GraceTime)
            {
                return false;
            }

            Break();
            return true;
        }

        /// <summary>Связана петля. Возвращает true, если в «Разгоне» набралась монета.</summary>
        public bool OnStitch()
        {
            if (!Rush)
            {
                return false;
            }

            _rushStitches++;
            if (_rushStitches < StitchesPerCoin)
            {
                return false;
            }

            _rushStitches = 0;
            RushCoins++;
            return true;
        }

        /// <summary>Серия обрывается: затор, моль или долгий простой.</summary>
        public void Break()
        {
            Combo = 0;
            Idle = 0;
            _rushStitches = 0;
        }
    }
}
