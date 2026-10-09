#nullable enable

using System;
using System.Collections.Generic;

namespace KnittedPicture.Game
{
    /// <summary>Покупки внутри игры. Реализация зависит от магазина; без SDK — «покупок нет».</summary>
    public interface IPurchases
    {
        /// <summary>Можно ли сейчас покупать (SDK подключён и магазин разрешает оплату).</summary>
        bool Available { get; }

        /// <summary>Купить товар. done(true) — оплачено, выдаём.</summary>
        void Buy(string productId, Action<bool> done);

        /// <summary>Какие разовые покупки у игрока уже есть (после переустановки, на новом телефоне).</summary>
        void Restore(Action<IReadOnlyCollection<string>> owned);
    }

    /// <summary>Покупок нет: Google Play, редактор, сборка без SDK RuStore.</summary>
    public sealed class NoPurchases : IPurchases
    {
        public bool Available => false;

        public void Buy(string productId, Action<bool> done) => done(false);

        public void Restore(Action<IReadOnlyCollection<string>> owned) => owned(Array.Empty<string>());
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
