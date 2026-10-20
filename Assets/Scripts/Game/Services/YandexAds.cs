#nullable enable

#if KNITTED_YANDEX_ADS && (STORE_RUSTORE || STORE_GOOGLEPLAY) && !UNITY_EDITOR
using System;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace KnittedPicture.Game
{
    /// <summary>
    /// Реклама Яндекса (Yandex Mobile Ads, плагин для Unity, API версий 6–7: загрузчики с событиями).
    /// Видео и межуровневая реклама загружаются заранее и после показа — сразу следующие; при ошибке
    /// загрузки — повтор через 15, 30, 60… секунд. Ответы SDK возвращаются в главный поток (App.RunOnMain).
    /// ВАЖНО: написано по документации и примерам плагина без доступа к самому пакету. Если в установленной
    /// версии имена другие (например, MobileAds переименован в YandexAds), правки нужны только здесь.
    /// </summary>
    public sealed class YandexAds : IAds
    {
        private readonly App _app;
        private readonly RewardedAdLoader _rewardedLoader = new RewardedAdLoader();
        private readonly InterstitialAdLoader _interstitialLoader = new InterstitialAdLoader();
        private RewardedAd? _rewarded;
        private Interstitial? _interstitial;
        private float _rewardedRetry = 15f;
        private float _interstitialRetry = 15f;

        public YandexAds(App app)
        {
            _app = app;
            _rewardedLoader.OnAdLoaded += (_, args) => _app.RunOnMain(() =>
            {
                _rewarded = args.RewardedAd;
                _rewardedRetry = 15f;
            });
            _rewardedLoader.OnAdFailedToLoad += (_, args) => _app.RunOnMain(() =>
            {
                Debug.LogWarning("[Ads] rewarded load failed: " + args.Message);
                _app.After(_rewardedRetry, LoadRewarded);
                _rewardedRetry = Mathf.Min(_rewardedRetry * 2f, 300f);
            });
            _interstitialLoader.OnAdLoaded += (_, args) => _app.RunOnMain(() =>
            {
                _interstitial = args.Interstitial;
                _interstitialRetry = 15f;
            });
            _interstitialLoader.OnAdFailedToLoad += (_, args) => _app.RunOnMain(() =>
            {
                Debug.LogWarning("[Ads] interstitial load failed: " + args.Message);
                _app.After(_interstitialRetry, LoadInterstitial);
                _interstitialRetry = Mathf.Min(_interstitialRetry * 2f, 300f);
            });
        }

        public bool NeedsConsent => true;

        public void SetPersonalized(bool allowed)
        {
            // Согласие передаётся при каждом запуске до загрузки рекламы — так требует Яндекс.
            MobileAds.SetUserConsent(allowed);
            if (_rewarded == null)
            {
                LoadRewarded();
            }

            if (_interstitial == null)
            {
                LoadInterstitial();
            }
        }

        public void ShowRewarded(string placement, Action<RewardedResult> done)
        {
            var ad = _rewarded;
            if (ad == null)
            {
                LoadRewarded();
                done(RewardedResult.Unavailable);
                return;
            }

            _rewarded = null;
            var earned = false;
            var failed = false;
            var finished = false;
            void Finish()
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                ad.Destroy();
                LoadRewarded();
                done(earned ? RewardedResult.Watched : failed ? RewardedResult.Unavailable : RewardedResult.Skipped);
            }

            ad.OnRewarded += (_, _) => _app.RunOnMain(() => earned = true);
            ad.OnAdDismissed += (_, _) => _app.RunOnMain(Finish);
            ad.OnAdFailedToShow += (_, _) => _app.RunOnMain(() =>
            {
                failed = true;
                Finish();
            });
            ad.Show();
        }

        public void ShowInterstitial(string placement, Action done)
        {
            var ad = _interstitial;
            if (ad == null)
            {
                LoadInterstitial();
                done();
                return;
            }

            _interstitial = null;
            var finished = false;
            void Finish()
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                ad.Destroy();
                LoadInterstitial();
                done();
            }

            ad.OnAdDismissed += (_, _) => _app.RunOnMain(Finish);
            ad.OnAdFailedToShow += (_, _) => _app.RunOnMain(Finish);
            ad.Show();
        }

        private void LoadRewarded() =>
            _rewardedLoader.LoadAd(new AdRequestConfiguration.Builder(StoreConfig.RewardedAdUnit).Build());

        private void LoadInterstitial() =>
            _interstitialLoader.LoadAd(new AdRequestConfiguration.Builder(StoreConfig.InterstitialAdUnit).Build());
    }
}
#endif
