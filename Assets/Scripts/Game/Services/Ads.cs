#nullable enable

using System;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Реклама. Сейчас заглушка; при подключении SDK издателя меняется только реализация.</summary>
    public interface IAds
    {
        /// <summary>Видео за награду. done(true) — досмотрено, награду выдаём.</summary>
        void ShowRewarded(string placement, Action<bool> done);

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
            return new YandexAds(app);
#else
            return new StubAds(app);
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

        public void ShowRewarded(string placement, Action<bool> done)
        {
            Debug.Log($"[Ads] rewarded: {placement}");
            _app.ShowRewardedStub(() => done(true));
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
}
