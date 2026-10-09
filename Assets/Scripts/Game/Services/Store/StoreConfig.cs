#nullable enable

namespace KnittedPicture.Game
{
    /// <summary>Под какой магазин собрана игра. Задаётся символом компиляции из меню сборки (Knitted → Сборка).</summary>
    public enum StoreFlavor
    {
        /// <summary>Сборка из редактора или без символа магазина: реклама и покупки — заглушки.</summary>
        Dev,
        RuStore,
        GooglePlay,
    }

    /// <summary>
    /// Всё, что зависит от магазина, в одном месте: идентификаторы рекламных блоков Яндекса,
    /// товары, ссылка на политику конфиденциальности. Перед выпуском замени «demo-…» на свои
    /// идентификаторы из кабинета Рекламной сети Яндекса (формат R-M-XXXXXXX-Y).
    /// </summary>
    public static class StoreConfig
    {
#if STORE_RUSTORE
        public const StoreFlavor Flavor = StoreFlavor.RuStore;
#elif STORE_GOOGLEPLAY
        public const StoreFlavor Flavor = StoreFlavor.GooglePlay;
#else
        public const StoreFlavor Flavor = StoreFlavor.Dev;
#endif

        /// <summary>Политика конфиденциальности (docs/privacy.html, опубликованная через GitHub Pages).</summary>
        public const string PrivacyPolicyUrl = "https://maximfersko.github.io/knitted-picture/privacy.html";

        /// <summary>Видео за награду. «demo-…» — тестовые блоки Яндекса: показывают пробную рекламу, денег не приносят.</summary>
        public const string RewardedAdUnit = "demo-rewarded-yandex";

        /// <summary>Межуровневая реклама.</summary>
        public const string InterstitialAdUnit = "demo-interstitial-yandex";

        /// <summary>Разовая покупка «Без рекламы» (в RuStore — товар типа «непотребляемый» с этим кодом).</summary>
        public const string NoAdsProduct = "no_ads";

        /// <summary>Покупки есть только в RuStore: Google Play не выплачивает деньги разработчикам из России.</summary>
        public static bool PurchasesEnabled => Flavor == StoreFlavor.RuStore;
    }
}
