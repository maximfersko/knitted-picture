#if SDK_STUBS
// Заглушки API Yandex Mobile Ads (плагин Unity 6–7) и RuStore Pay SDK — только чтобы проверить, что
// YandexAds.cs и RuStorePurchases.cs компилируются против ожидаемых имён. В Unity используются настоящие SDK.
#nullable enable
using System;
using System.Collections.Generic;

namespace YandexMobileAds.Base
{
    public sealed class AdRequestConfiguration
    {
        public sealed class Builder
        {
            public Builder(string adUnitId) { }
            public AdRequestConfiguration Build() => new AdRequestConfiguration();
        }
    }

    public class AdFailedToLoadEventArgs : EventArgs { public string Message = ""; }
    public class AdFailureEventArgs : EventArgs { public string Message = ""; }
    public class Reward { public int amount; public string type = ""; }
}

namespace YandexMobileAds
{
    using YandexMobileAds.Base;

    public static class MobileAds { public static void SetUserConsent(bool consent) { } }

    public class RewardedAd
    {
        public event EventHandler<Reward>? OnRewarded;
        public event EventHandler<EventArgs>? OnAdDismissed;
        public event EventHandler<AdFailureEventArgs>? OnAdFailedToShow;
        public void Show() { }
        public void Destroy() { }
    }

    public class Interstitial
    {
        public event EventHandler<EventArgs>? OnAdDismissed;
        public event EventHandler<AdFailureEventArgs>? OnAdFailedToShow;
        public void Show() { }
        public void Destroy() { }
    }

    public class RewardedAdLoadedEventArgs : EventArgs { public RewardedAd RewardedAd = new RewardedAd(); }
    public class InterstitialAdLoadedEventArgs : EventArgs { public Interstitial Interstitial = new Interstitial(); }

    public class RewardedAdLoader
    {
        public event EventHandler<RewardedAdLoadedEventArgs>? OnAdLoaded;
        public event EventHandler<AdFailedToLoadEventArgs>? OnAdFailedToLoad;
        public void LoadAd(AdRequestConfiguration configuration) { }
    }

    public class InterstitialAdLoader
    {
        public event EventHandler<InterstitialAdLoadedEventArgs>? OnAdLoaded;
        public event EventHandler<AdFailedToLoadEventArgs>? OnAdFailedToLoad;
        public void LoadAd(AdRequestConfiguration configuration) { }
    }
}

namespace RuStore.PayClient
{
    public class RuStoreError { }
    public class PurchaseAvailabilityResult { public bool isAvailable; }
    public sealed class ProductId { public ProductId(string value) { this.value = value; } public string value; }
    public sealed class ProductPurchaseParams { public ProductPurchaseParams(ProductId productId) { } }
    public class ProductPurchaseResult { }
    public interface IPurchase { }
    public enum ProductPurchaseStatus { PAID, CONFIRMED, CANCELLED }
    public class ProductPurchase : IPurchase { public ProductId productId = new ProductId(""); public ProductPurchaseStatus status; }

    public sealed class RuStorePayClient
    {
        public static RuStorePayClient Instance { get; } = new RuStorePayClient();
        public void GetPurchaseAvailability(Action<RuStoreError> onFailure, Action<PurchaseAvailabilityResult> onSuccess) { }
        public void Purchase(ProductPurchaseParams parameters, Action<RuStoreError> onFailure, Action<ProductPurchaseResult> onSuccess) { }
        public void GetPurchases(Action<RuStoreError> onFailure, Action<List<IPurchase>> onSuccess) { }
    }
}
#endif
