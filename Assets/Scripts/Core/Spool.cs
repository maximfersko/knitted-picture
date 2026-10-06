#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Клубок: цвет, сколько петель осталось и особые свойства.</summary>
    public sealed class Spool
    {
        public Spool(
            int id,
            int color,
            int loops,
            bool hidden = false,
            int link = 0,
            int knots = 0,
            bool locked = false,
            bool isKey = false,
            bool rainbow = false,
            bool thick = false,
            int ice = 0)
        {
            Id = id;
            Color = color;
            Loops = loops;
            Hidden = hidden;
            Link = link;
            Knots = knots;
            Locked = locked;
            IsKey = isKey;
            Rainbow = rainbow;
            Thick = thick;
            Ice = ice;
        }

        /// <summary>Постоянный номер клубка: по нему вид находит свой объект, в том числе после отмены хода.</summary>
        public int Id { get; }

        public int Color { get; }

        public int Loops { get; internal set; }

        /// <summary>Клубок был задуман скрытым.</summary>
        public bool Hidden { get; }

        /// <summary>Скрытый клубок уже побывал впереди и показал цвет.</summary>
        public bool Revealed { get; internal set; }

        /// <summary>Видно ли игроку цвет клубка.</summary>
        public bool IsColorVisible => !Hidden || Revealed;

        /// <summary>Номер связки, 0 — без связки. Ножницы перерезают связку.</summary>
        public int Link { get; internal set; }

        public int Knots { get; internal set; }

        public bool Locked { get; internal set; }

        public bool IsKey { get; }

        /// <summary>Радужный: вяжет любой цвет на фронте.</summary>
        public bool Rainbow { get; }

        /// <summary>Толстый: занимает 2 слота и вяжет по 2 петли за шаг.</summary>
        public bool Thick { get; }

        /// <summary>Сколько петель его цвета осталось связать, чтобы лёд растаял. 0 — не во льду.</summary>
        public int Ice { get; internal set; }

        public bool Frozen => Ice > 0;

        /// <summary>Сколько слотов занимает на станке.</summary>
        public int Size => Thick ? 2 : 1;

        /// <summary>Шаги до начала вязки после того, как клубок встал на станок (пока он «летит»).</summary>
        public int Warmup { get; internal set; }

        /// <summary>Клубок хоть раз простаивал на станке без работы — значит, он не «идеальный».</summary>
        public bool Stalled { get; internal set; }

        internal Spool Clone() => new Spool(Id, Color, Loops, Hidden, Link, Knots, Locked, IsKey, Rainbow, Thick, Ice)
        {
            Revealed = Revealed,
            Warmup = Warmup,
            Stalled = Stalled,
        };
    }
}
