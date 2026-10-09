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

        public void Buy(string productId, Action<bool> done)
        {
            var parameters = new ProductPurchaseParams(new ProductId(productId));
            RuStorePayClient.Instance.Purchase(
                parameters: parameters,
                onFailure: error =>
                {
                    Debug.LogWarning("[Pay] purchase: " + error);
                    _app.RunOnMain(() => done(false));
                },
                onSuccess: _ => _app.RunOnMain(() => done(true)));
        }

        public void Restore(Action<IReadOnlyCollection<string>> owned)
        {
            RuStorePayClient.Instance.GetPurchases(
                onFailure: error =>
                {
                    Debug.LogWarning("[Pay] purchases: " + error);
                    _app.RunOnMain(() => owned(Array.Empty<string>()));
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

                    _app.RunOnMain(() => owned(ids));
                });
        }
    }
}
#endif
