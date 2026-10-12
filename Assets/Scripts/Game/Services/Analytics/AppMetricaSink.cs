#nullable enable

#if KNITTED_APPMETRICA && (STORE_RUSTORE || STORE_GOOGLEPLAY) && !UNITY_EDITOR
using Io.AppMetrica;

namespace KnittedPicture.Game
{
    /// <summary>
    /// События в AppMetrica (Яндекс): бесплатно, работает в России, отчёты и воронки в веб-кабинете.
    /// Компилируется, только когда плагин AppMetrica установлен (символ KNITTED_APPMETRICA ставит меню сборки).
    /// Геолокацию не собираем. ВАЖНО: написано по документации плагина AppMetrica для Unity (версии 6+,
    /// пространство имён Io.AppMetrica) без доступа к самому пакету; если имена другие — правки только здесь.
    /// </summary>
    public sealed class AppMetricaSink : IAnalyticsSink
    {
        public AppMetricaSink(string apiKey)
        {
            AppMetrica.Activate(new AppMetricaConfig(apiKey)
            {
                LocationTracking = false,
                SessionsAutoTrackingEnabled = true,
            });
        }

        public void Send(string name, string json) => AppMetrica.ReportEvent(name, json);
    }
}
#endif
