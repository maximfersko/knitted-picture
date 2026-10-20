using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using KnittedPicture;
using KnittedPicture.Core;
using Xunit;

namespace KnittedPicture.Core.Tests
{
    /// <summary>
    /// Английская таблица: у каждой строки из кода и у каждого уровня есть перевод, подстановки те же.
    /// Язык всей игры здесь не переключается (тесты идут параллельно): проверяем таблицу напрямую.
    /// </summary>
    public class LocalizationTests
    {
        private const string Literal = "\"((?:[^\"\\\\]|\\\\.)*)\"";

        [Fact]
        public void Every_string_in_code_has_an_english_translation_with_the_same_placeholders()
        {
            var missing = new List<string>();
            var broken = new List<string>();
            foreach (var key in CodeKeys())
            {
                if (!En.Table.TryGetValue(key, out var text))
                {
                    missing.Add(key);
                    continue;
                }

                if (!SamePlaceholders(key, text))
                {
                    broken.Add(key + " => " + text);
                }
            }

            Assert.True(missing.Count == 0, "Нет перевода:\n" + string.Join("\n", missing));
            Assert.True(broken.Count == 0, "Подстановки не совпадают:\n" + string.Join("\n", broken));
        }

        [Fact]
        public void Every_table_entry_keeps_placeholders_and_has_no_russian_left()
        {
            // Вся таблица, а не только строки из кода: ключи без русских букв («+{0} {1}») тоже переводятся.
            var broken = En.Table.Where(p => !SamePlaceholders(p.Key, p.Value)).Select(p => p.Key + " => " + p.Value).ToList();
            Assert.True(broken.Count == 0, "Подстановки не совпадают:\n" + string.Join("\n", broken));
            var russian = En.Table.Where(p => Regex.IsMatch(p.Value, "[А-Яа-яЁё]")).Select(p => p.Value).ToList();
            Assert.True(russian.Count == 0, "Русский в переводе:\n" + string.Join("\n", russian));
        }

        [Fact]
        public void Every_level_name_and_tip_is_translated()
        {
            var missing = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(Root(), "Assets", "Resources", "Levels"), "level_*.json"))
            {
                var level = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(file), new JsonSerializerOptions { IncludeFields = true })!;
                foreach (var text in new[] { level.name, level.tip }.Where(t => !string.IsNullOrEmpty(t)))
                {
                    if (!En.Table.ContainsKey(text))
                    {
                        missing.Add(Path.GetFileName(file) + ": " + text);
                    }
                }
            }

            Assert.True(missing.Count == 0, "Нет перевода:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void Data_names_are_translated()
        {
            var names = Collections.All.Select(c => c.Name)
                .Concat(YarnCatalog.All.SelectMany(y => new[] { y.Name, y.Description }))
                .Concat(YarnCatalog.Sets.Select(s => s.Name))
                .Concat(Achievements.All.SelectMany(a => new[] { a.Title, a.Description }))
                .Concat(AlbumFrames.All.Select(f => f.Name));
            var untranslated = names.Where(n => L.Translate(En.Table, n) == n).ToList();
            Assert.True(untranslated.Count == 0, "Нет перевода:\n" + string.Join("\n", untranslated));
        }

        [Fact]
        public void Coins_genitive_for_shortage_follows_russian_forms_and_english_has_two()
        {
            Assert.Equal("монеты", YarnCatalog.CoinsGenitive(1));
            Assert.Equal("монет", YarnCatalog.CoinsGenitive(2));
            Assert.Equal("монет", YarnCatalog.CoinsGenitive(5));
            Assert.Equal("монеты", YarnCatalog.CoinsGenitive(21));
            Assert.Equal("coin", L.PluralIn(En.Table, 1, "монеты", "монет", "монет"));
            Assert.Equal("coins", L.PluralIn(En.Table, 2, "монеты", "монет", "монет"));
        }

        [Fact]
        public void Numbers_fall_back_to_a_template_and_plurals_use_two_english_forms()
        {
            Assert.Equal("Finish all 65 pictures", L.Translate(En.Table, "Довяжи все 65 картинок"));
            Assert.Equal("Level 12", L.Translate(En.Table, "Уровень 12"));
            Assert.Equal("stitch", L.PluralIn(En.Table, 1, "петлю", "петли", "петель"));
            Assert.Equal("stitches", L.PluralIn(En.Table, 3, "петлю", "петли", "петель"));
            Assert.Equal("stitches", L.PluralIn(En.Table, 21, "петлю", "петли", "петель"));
            Assert.Equal("петли", L.RussianPlural(22, "петлю", "петли", "петель"));
            Assert.Equal("Нет такого", L.Translate(En.Table, "Нет такого"));
        }

        [Fact]
        public void English_text_keeps_the_house_style()
        {
            // Без длинных тире в текстах для игрока (как и в русском).
            var dashed = En.Table.Where(p => p.Value.Contains('—')).Select(p => p.Value).ToList();
            Assert.Empty(dashed);
        }

        private static IEnumerable<string> CodeKeys()
        {
            var scripts = Path.Combine(Root(), "Assets", "Scripts");
            var keys = new HashSet<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "Qa" + Path.DirectorySeparatorChar) || file.EndsWith("En.cs") || file.EndsWith("L.cs"))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                foreach (Match m in Regex.Matches(source, @"L\.[TF]\(" + Literal))
                {
                    keys.Add(Unescape(m.Groups[1].Value));
                }

                foreach (Match m in Regex.Matches(source, @"L\.Plural\([^,]+,\s*" + Literal + @",\s*" + Literal + @",\s*" + Literal))
                {
                    keys.Add(Unescape(m.Groups[1].Value) + "|" + Unescape(m.Groups[3].Value));
                }
            }

            return keys.Where(k => Regex.IsMatch(k, "[А-Яа-яЁё]"));
        }

        private static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        private static bool SamePlaceholders(string a, string b)
        {
            static string Holes(string s) => string.Join(",", Regex.Matches(s, @"\{\{?\d+(:[^}]*)?\}\}?").Select(m => m.Value).OrderBy(v => v));
            return Holes(a) == Holes(b) && a.Contains('|') == b.Contains('|');
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
            {
                dir = dir.Parent;
            }

            return dir!.FullName;
        }
    }
}
