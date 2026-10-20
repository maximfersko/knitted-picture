#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Game
{
    /// <summary>Чем кончилась покупка.</summary>
    public enum BuyResult
    {
        /// <summary>Оплачено, товар выдаём.</summary>
        Ok,

        /// <summary>Игрок сам отменил оплату: деньги не списаны, ругать нечего.</summary>
        Cancelled,

        /// <summary>Не вышло (нет сети, отказ магазина, ошибка SDK).</summary>
        Failed,
    }

    /// <summary>Чем кончилась проверка прежних покупок.</summary>
    public enum RestoreResult
    {
        /// <summary>Магазин ответил, покупки есть.</summary>
        Restored,

        /// <summary>Магазин ответил, покупок нет.</summary>
        NothingFound,

        /// <summary>Магазин не ответил (нет сети, ошибка SDK): неизвестно, есть ли покупки.</summary>
        Failed,
    }

    /// <summary>Покупки внутри игры. Реализация зависит от магазина; без SDK — «покупок нет».</summary>
    public interface IPurchases
    {
        /// <summary>Можно ли сейчас покупать (SDK подключён и магазин разрешает оплату).</summary>
        bool Available { get; }

        /// <summary>Купить товар. done вызывается один раз, в главном потоке.</summary>
        void Buy(string productId, Action<BuyResult> done);

        /// <summary>
        /// Какие разовые покупки у игрока уже есть (после переустановки, на новом телефоне). done вызывается один раз,
        /// в главном потоке; список пуст при NothingFound и Failed.
        /// </summary>
        void Restore(Action<RestoreResult, IReadOnlyCollection<string>> done);
    }

    /// <summary>Покупок нет: Google Play, редактор, сборка без SDK RuStore.</summary>
    public sealed class NoPurchases : IPurchases
    {
        public bool Available => false;

        public void Buy(string productId, Action<BuyResult> done) => done(BuyResult.Failed);

        public void Restore(Action<RestoreResult, IReadOnlyCollection<string>> done) => done(RestoreResult.NothingFound, Array.Empty<string>());
    }

    public static class PurchasesFactory
    {
        public static IPurchases Create(App app)
        {
#if KNITTED_RUSTORE_PAY && STORE_RUSTORE && !UNITY_EDITOR
            return new RuStorePurchases(app);
#else
            return new NoPurchases();
#endif
        }
    }
}
