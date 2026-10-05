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
    }
}
