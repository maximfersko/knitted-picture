#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Куда уходят события: AppMetrica в сборке для магазина, журнал Unity в редакторе.</summary>
    public interface IAnalyticsSink
    {
        void Send(string name, string json);
    }

    /// <summary>
    /// События игры для аналитики: где игроки застревают, сколько попыток уходит на уровень, что помогает
    /// (бустеры, спасения, реклама), куда уходят из игры. Одна точка входа: Analytics.Event("level_win", ("level", 12), …).
    /// Параметры — только числа, строки и флаги, никаких личных данных. Полный список событий — docs/analytics.md.
    /// </summary>
    public static class Analytics
    {
        private static IAnalyticsSink _sink = new LogSink();
        private static readonly List<string> Recent = new List<string>();

        /// <summary>Последние события (для отладки и будущего автоигрока).</summary>
        public static IReadOnlyList<string> Last => Recent;

        public static void Init()
        {
#if KNITTED_APPMETRICA && (STORE_RUSTORE || STORE_GOOGLEPLAY) && !UNITY_EDITOR
            _sink = new AppMetricaSink(StoreConfig.AppMetricaApiKey);
#else
            _sink = new LogSink();
#endif
        }

        public static void Event(string name, params (string Key, object Value)[] parameters)
        {
            var json = ToJson(parameters);
            Recent.Add(name + " " + json);
            if (Recent.Count > 200)
            {
                Recent.RemoveAt(0);
            }

            try
            {
                _sink.Send(name, json);
            }
            catch (Exception e)
            {
                // Аналитика никогда не должна ронять игру.
                Debug.LogWarning("[Analytics] " + e.Message);
            }
        }

        public static string ToJson((string Key, object Value)[] parameters)
        {
            var sb = new StringBuilder("{");
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append('"').Append(Escape(parameters[i].Key)).Append("\":");
                switch (parameters[i].Value)
                {
                    case bool b:
                        sb.Append(b ? "true" : "false");
                        break;
                    case int n:
                        sb.Append(n.ToString(CultureInfo.InvariantCulture));
                        break;
                    case float f:
                        sb.Append(f.ToString("0.##", CultureInfo.InvariantCulture));
                        break;
                    case double d:
                        sb.Append(d.ToString("0.##", CultureInfo.InvariantCulture));
                        break;
                    default:
                        sb.Append('"').Append(Escape(parameters[i].Value?.ToString() ?? "")).Append('"');
                        break;
                }
            }

            return sb.Append('}').ToString();
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private sealed class LogSink : IAnalyticsSink
        {
            public void Send(string name, string json)
            {
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    Debug.Log($"[Analytics] {name} {json}");
                }
            }
        }
    }
}
