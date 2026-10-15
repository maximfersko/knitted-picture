#nullable enable

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KnittedPicture
{
    /// <summary>
    /// Перевод интерфейса. Ключ — русская строка из кода: русский остаётся исходным языком, а таблица
    /// другого языка сопоставляет ему перевод. Нет перевода — показывается русский текст, а не пустота.
    /// Строки с числами пишутся шаблоном: <c>L.F("Уровень {0}", n)</c>; ключ — сам шаблон.
    /// </summary>
    public static class L
    {
        public const string Russian = "ru";
        public const string English = "en";

        private static Dictionary<string, string>? _table;

        /// <summary>Текущий язык: "ru" или "en".</summary>
        public static string Lang { get; private set; } = Russian;

        public static bool IsRussian => Lang == Russian;

        public static void Set(string lang)
        {
            Lang = lang == English ? English : Russian;
            _table = Lang == English ? En.Table : null;
        }

        /// <summary>Перевод строки. Если точного ключа нет, числа в строке пробуем как шаблон {0}, {1}…</summary>
        public static string T(string ru) => _table == null ? ru : Translate(_table, ru);

        /// <summary>Перевод по заданной таблице: для T и для тестов, без переключения языка всей игры.</summary>
        public static string Translate(IReadOnlyDictionary<string, string> table, string ru)
        {
            if (string.IsNullOrEmpty(ru))
            {
                return ru;
            }

            if (table.TryGetValue(ru, out var text))
            {
                return text;
            }

            var (template, numbers) = Template(ru);
            if (numbers.Count > 0 && table.TryGetValue(template, out var pattern))
            {
                return string.Format(CultureInfo.InvariantCulture, pattern, numbers.ToArray());
            }

            return ru;
        }

        /// <summary>Шаблон с подстановками: ключ — русский шаблон, подстановки те же для всех языков.</summary>
        public static string F(string ru, params object?[] args)
        {
            var pattern = _table != null && _table.TryGetValue(ru, out var text) ? text : ru;
            return string.Format(CultureInfo.InvariantCulture, pattern, args);
        }

        /// <summary>
        /// Слово при числе. По-русски три формы (1 петля, 2 петли, 5 петель); в английском две —
        /// ключ перевода «одна|много» по русским формам, перевод тоже «одна|много» (stitch|stitches).
        /// </summary>
        public static string Plural(int n, string one, string few, string many) =>
            _table == null ? RussianPlural(n, one, few, many) : PluralIn(_table, n, one, few, many);

        public static string PluralIn(IReadOnlyDictionary<string, string> table, int n, string one, string few, string many)
        {
            if (table.TryGetValue(one + "|" + many, out var pair))
            {
                var bar = pair.IndexOf('|');
                return bar < 0 ? pair : n == 1 ? pair.Substring(0, bar) : pair.Substring(bar + 1);
            }

            return RussianPlural(n, one, few, many);
        }

        public static string RussianPlural(int n, string one, string few, string many)
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11)
            {
                return one;
            }

            return mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14) ? few : many;
        }

        /// <summary>«Ур. 12 · 3 звезды» → («Ур. {0} · {1} звезды», [12, 3]).</summary>
        private static (string template, List<object> numbers) Template(string text)
        {
            var numbers = new List<object>();
            var builder = new StringBuilder(text.Length);
            var i = 0;
            while (i < text.Length)
            {
                if (char.IsDigit(text[i]))
                {
                    var start = i;
                    while (i < text.Length && char.IsDigit(text[i]))
                    {
                        i++;
                    }

                    builder.Append('{').Append(numbers.Count).Append('}');
                    numbers.Add(text.Substring(start, i - start));
                    continue;
                }

                builder.Append(text[i]);
                i++;
            }

            return (builder.ToString(), numbers);
        }
    }
}
