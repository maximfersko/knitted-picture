#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Клубок: цвет, сколько петель осталось и особые свойства.</summary>
    public sealed class Spool
    {
        public Spool(int id, int color, int loops, bool hidden = false, int link = 0, int knots = 0, bool locked = false, bool isKey = false)
        {
            Id = id;
            Color = color;
            Loops = loops;
            Hidden = hidden;
            Link = link;
            Knots = knots;
            Locked = locked;
            IsKey = isKey;
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

        internal Spool Clone() => new Spool(Id, Color, Loops, Hidden, Link, Knots, Locked, IsKey) { Revealed = Revealed };
    }
}
