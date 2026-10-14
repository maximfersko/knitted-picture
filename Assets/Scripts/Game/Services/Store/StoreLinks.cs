#nullable enable

using System;
using UnityEngine;

namespace KnittedPicture.Game
{
    /// <summary>Ссылки «Оценить игру» и «Написать нам». null — в этой сборке строки нет.</summary>
    public static class StoreLinks
    {
        /// <summary>Страница игры в своём магазине: оценка там, откуда игру скачали. Сборка из редактора — нет.</summary>
        public static string? Rate => StoreConfig.Flavor switch
        {
            StoreFlavor.RuStore => "https://www.rustore.ru/catalog/app/" + Application.identifier,
            StoreFlavor.GooglePlay => "https://play.google.com/store/apps/details?id=" + Application.identifier,
            _ => null,
        };

        /// <summary>
        /// Письмо разработчику: тема с названием и версией, внизу — телефон, система и уровень, чтобы на вопрос
        /// «а у меня не работает» не пришлось переспрашивать. Пустая почта в StoreConfig — строки нет.
        /// </summary>
        public static string? Support(App app)
        {
            if (string.IsNullOrWhiteSpace(StoreConfig.SupportEmail))
            {
                return null;
            }

            var subject = $"{Application.productName} {Application.version}";
            var body = "\n\n\n---\n"
                + $"Версия: {Application.version} ({StoreConfig.Flavor})\n"
                + $"Телефон: {SystemInfo.deviceModel}\n"
                + $"Система: {SystemInfo.operatingSystem}\n"
                + $"Уровень: {app.Progress.CurrentLevel}";
            return $"mailto:{StoreConfig.SupportEmail}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        }
    }
}
