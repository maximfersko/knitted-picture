#nullable enable

using System;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Чем кончился показ видео за награду.</summary>
    public enum RewardedResult
    {
        /// <summary>Досмотрено до конца: награду выдаём.</summary>
        Watched,

        /// <summary>Игрок закрыл видео раньше времени: награды нет, но и ошибки нет.</summary>
        Skipped,

        /// <summary>Видео нет (не загрузилось, нет сети) или оно не показалось: награды нет.</summary>
        Unavailable,
    }

    /// <summary>Реклама. Сейчас заглушка; при подключении SDK издателя меняется только реализация.</summary>
    public interface IAds
    {
        /// <summary>Видео за награду. done вызывается один раз, в главном потоке; награду выдаём только при Watched.</summary>
        void ShowRewarded(string placement, Action<RewardedResult> done);

        /// <summary>Межуровневая реклама. done вызывается всегда, даже если показать нечего.</summary>
        void ShowInterstitial(string placement, Action done);

        /// <summary>Настоящая реклама, которой нужно согласие на персональные данные (152-ФЗ, правила Яндекса).</summary>
        bool NeedsConsent { get; }

        /// <summary>Персональная реклама разрешена или нет. Вызывается при запуске и при смене в настройках.</summary>
        void SetPersonalized(bool allowed);
    }

    public static class AdsFactory
    {
        /// <summary>
        /// Настоящая реклама — только в сборке для магазина с установленным плагином Яндекса
        /// (символ KNITTED_YANDEX_ADS ставит меню сборки). В редакторе реклама Яндекса не работает — там заглушка.
        /// </summary>
        public static IAds Create(App app)
        {
#if KNITTED_YANDEX_ADS && (STORE_RUSTORE || STORE_GOOGLEPLAY) && !UNITY_EDITOR
            return new TrackedAds(new YandexAds(app));
#else
            return new TrackedAds(new StubAds(app));
#endif
        }
    }

    /// <summary>Заглушка: «видео» — плашка на полторы секунды, межуровневая — только запись в лог.</summary>
    public sealed class StubAds : IAds
    {
        private readonly App _app;

        public StubAds(App app)
        {
            _app = app;
        }

        public void ShowRewarded(string placement, Action<RewardedResult> done)
        {
            Debug.Log($"[Ads] rewarded: {placement}");
            _app.ShowRewardedStub(() => done(RewardedResult.Watched));
        }

        public void ShowInterstitial(string placement, Action done)
        {
            Debug.Log($"[Ads] interstitial: {placement}");
            done();
        }

        public bool NeedsConsent => StoreConfig.Flavor == StoreFlavor.Dev && UnityEngine.Application.isEditor;

        public void SetPersonalized(bool allowed)
        {
            Debug.Log($"[Ads] personalized: {allowed}");
        }
    }

    /// <summary>Обёртка, которая пишет в аналитику каждый показ: где предложили видео и чем кончилось (watched, skipped, unavailable).</summary>
    public sealed class TrackedAds : IAds
    {
        private readonly IAds _inner;

        public TrackedAds(IAds inner)
        {
            _inner = inner;
        }

        public bool NeedsConsent => _inner.NeedsConsent;

        public void SetPersonalized(bool allowed) => _inner.SetPersonalized(allowed);

        public void ShowRewarded(string placement, Action<RewardedResult> done) =>
            _inner.ShowRewarded(placement, result =>
            {
                Analytics.Event("ad_rewarded", ("placement", placement), ("watched", result == RewardedResult.Watched),
                    ("result", result.ToString().ToLowerInvariant()));
                done(result);
            });

        public void ShowInterstitial(string placement, Action done) =>
            _inner.ShowInterstitial(placement, () =>
            {
                Analytics.Event("ad_interstitial", ("placement", placement));
                done();
            });
    }
}
