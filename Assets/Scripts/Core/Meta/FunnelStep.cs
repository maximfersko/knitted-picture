#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>
    /// Шаги воронки первых минут: каждый уходит в аналитику один раз за всю жизнь установки.
    /// Не переименовывать и не перенумеровывать: это имена событий в AppMetrica. Новые шаги только в конец
    /// (значение 0..30, это номер бита в <see cref="PlayerProgress.funnel"/>).
    /// </summary>
    public enum FunnelStep
    {
        /// <summary>Первый запуск игры.</summary>
        FirstOpen = 0,

        /// <summary>Показан первый уровень.</summary>
        FirstLevelShown = 1,

        /// <summary>Первый ход (тап по клубку).</summary>
        FirstMove = 2,

        /// <summary>Победа на уровне 1.</summary>
        Level1Win = 3,

        /// <summary>Начат уровень 2.</summary>
        Level2Start = 4,

        /// <summary>Победа на уровне 2.</summary>
        Level2Win = 5,

        /// <summary>Победа на уровне 3.</summary>
        Level3Win = 6,

        /// <summary>Первый проигрыш.</summary>
        FirstFail = 7,

        /// <summary>Первый показ главной.</summary>
        FirstHome = 8,

        /// <summary>Победа на уровне 5.</summary>
        Level5Win = 9,

        /// <summary>Победа на уровне 10.</summary>
        Level10Win = 10,
    }

    /// <summary>Имя события шага воронки.</summary>
    public static class FunnelStepNames
    {
        private static readonly string[] Names = Build();

        private static string[] Build()
        {
            var values = (FunnelStep[])System.Enum.GetValues(typeof(FunnelStep));
            var max = 0;
            foreach (var v in values)
            {
                max = System.Math.Max(max, (int)v);
            }

            var names = new string[max + 1];
            foreach (var v in values)
            {
                names[(int)v] = "ftue_" + ((int)v + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + "_" + Snake(v.ToString());
            }

            return names;
        }

        private static string Snake(string pascal)
        {
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < pascal.Length; i++)
            {
                var c = pascal[i];
                var prevLetter = i > 0 && char.IsLetter(pascal[i - 1]);
                var nextLower = i + 1 < pascal.Length && char.IsLower(pascal[i + 1]);
                var prevLower = i > 0 && (char.IsLower(pascal[i - 1]) || char.IsDigit(pascal[i - 1]));
                if (i > 0 && (char.IsUpper(c) && (prevLower || (prevLetter && nextLower)) ||
                              char.IsDigit(c) && !char.IsDigit(pascal[i - 1])))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        /// <summary>Имя события: ftue_NN_имя_шага. Строки считаются один раз, повторные вызовы без выделений памяти.</summary>
        public static string EventName(this FunnelStep s)
        {
            var i = (int)s;
            return i >= 0 && i < Names.Length && Names[i] != null ? Names[i] : "ftue_" + (i + 1).ToString("00") + "_step";
        }
    }
}
