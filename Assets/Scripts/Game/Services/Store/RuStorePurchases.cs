#nullable enable

#if KNITTED_RUSTORE_PAY && STORE_RUSTORE && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using RuStore.PayClient;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Покупки через RuStore Pay SDK. Компилируется, только когда SDK установлен в проект (символ
    /// KNITTED_RUSTORE_PAY ставит меню сборки) и сборка — для RuStore.
    /// ВАЖНО: написано по документации RuStore Pay SDK для Unity, но без доступа к самому пакету.
    /// Если после установки SDK компилятор ругается на имена — правки нужны только в этом файле,
    /// остальная игра работает через IPurchases.
    /// </summary>
    public sealed class RuStorePurchases : IPurchases
    {
        private readonly App _app;
        private bool _available;

        public RuStorePurchases(App app)
        {
            _app = app;
            RuStorePayClient.Instance.GetPurchaseAvailability(
                onFailure: error => Debug.LogWarning("[Pay] availability: " + error),
                onSuccess: result => _app.RunOnMain(() => _available = result.isAvailable));
        }

        public bool Available => _available;

        public void Buy(string productId, Action<BuyResult> done)
        {
            var parameters = new ProductPurchaseParams(new ProductId(productId));
            RuStorePayClient.Instance.Purchase(
                parameters: parameters,
                onFailure: error =>
                {
                    Debug.LogWarning("[Pay] purchase: " + error);
                    var result = IsCancel(error) ? BuyResult.Cancelled : BuyResult.Failed;
                    _app.RunOnMain(() => done(result));
                },
                onSuccess: _ => _app.RunOnMain(() => done(BuyResult.Ok)));
        }

        public void Restore(Action<RestoreResult, IReadOnlyCollection<string>> done)
        {
            RuStorePayClient.Instance.GetPurchases(
                onFailure: error =>
                {
                    Debug.LogWarning("[Pay] purchases: " + error);
                    _app.RunOnMain(() => done(RestoreResult.Failed, Array.Empty<string>()));
                },
                onSuccess: purchases =>
                {
                    var ids = new List<string>();
                    foreach (var purchase in purchases)
                    {
                        if (purchase is ProductPurchase product && product.status == ProductPurchaseStatus.CONFIRMED)
                        {
                            ids.Add(product.productId.value);
                        }
                    }

                    _app.RunOnMain(() => done(ids.Count > 0 ? RestoreResult.Restored : RestoreResult.NothingFound, ids));
                });
        }

        /// <summary>
        /// Отмена оплаты самим игроком. В коде нет документированного признака отмены, поэтому смотрим на имя типа
        /// и текст ошибки («...Cancelled...»): так называется ошибка отмены в RuStore Pay SDK. Не узнали — это обычный сбой.
        /// ПРОВЕРИТЬ: признак отмены нужно сверить с версией RuStore Pay SDK на устройстве. Если отмена не опознана,
        /// игрок увидит окно сбоя (без выдачи товара), а не тост об отмене.
        /// </summary>
        private static bool IsCancel(object? error)
        {
            var text = (error?.GetType().Name ?? string.Empty) + " " + error;
            return text.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
