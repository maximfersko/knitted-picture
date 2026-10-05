#nullable enable

namespace KnittedPicture.Core
{
    /// <summary>Клубок: цвет и сколько петель в нём осталось.</summary>
    public sealed class Spool
    {
        public Spool(int id, int color, int loops)
        {
            Id = id;
            Color = color;
            Loops = loops;
        }

        /// <summary>Постоянный номер клубка: по нему вид находит свой объект, в том числе после отмены хода.</summary>
        public int Id { get; }

        public int Color { get; }

        public int Loops { get; internal set; }

        internal Spool Clone() => new Spool(Id, Color, Loops);
    }
}
