using System;
using System.IO;
using System.IO.Compression;
using KnittedPicture.Game;

namespace KnittedPicture.ArtPreview
{
    /// <summary>Минимальный PNG-писатель (RGBA, без фильтров) и сборка макетов из растров.</summary>
    public static class Png
    {
        public static void Save(Raster raster, string path)
        {
            using var file = File.Create(path);
            file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            BigEndian(header, 0, raster.Width);
            BigEndian(header, 4, raster.Height);
            header[8] = 8;
            header[9] = 6;
            Chunk(file, "IHDR", header);
            var bytes = raster.ToBytes();
            using var raw = new MemoryStream();
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, true))
            {
                for (var y = raster.Height - 1; y >= 0; y--)
                {
                    z.WriteByte(0);
                    z.Write(bytes, y * raster.Width * 4, raster.Width * 4);
                }
            }

            Chunk(file, "IDAT", raw.ToArray());
            Chunk(file, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BigEndian(len, 0, data.Length);
            s.Write(len);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crc = Crc(typeBytes, data);
            var c = new byte[4];
            BigEndian(c, 0, (int)crc);
            s.Write(c);
        }

        private static uint Crc(byte[] a, byte[] b)
        {
            var c = 0xffffffffu;
            foreach (var arr in new[] { a, b })
            {
                foreach (var x in arr)
                {
                    c ^= x;
                    for (var k = 0; k < 8; k++)
                    {
                        c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
                    }
                }
            }

            return c ^ 0xffffffffu;
        }

        private static void BigEndian(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }

        /// <summary>
        /// Положить растр src в прямоугольник (x, y, w, h) холста dst (y — низ прямоугольника, вверх),
        /// с усреднением при уменьшении, цветом-множителем tint и поворотом не занимаемся.
        /// </summary>
        public static void Put(Raster dst, Raster src, float x, float y, float w, float h, Rgba? tint = null, float alpha = 1f)
        {
            var t = tint ?? Rgba.White;
            var x0 = (int)MathF.Floor(x);
            var y0 = (int)MathF.Floor(y);
            var x1 = (int)MathF.Ceiling(x + w);
            var y1 = (int)MathF.Ceiling(y + h);
            var sx = src.Width / w;
            var sy = src.Height / h;
            var n = Math.Max(1, (int)MathF.Ceiling(MathF.Max(sx, sy)));
            for (var py = y0; py < y1; py++)
            {
                for (var px = x0; px < x1; px++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (var j = 0; j < n; j++)
                    {
                        for (var i = 0; i < n; i++)
                        {
                            var u = (px + (i + 0.5f) / n - x) * sx;
                            var v = (py + (j + 0.5f) / n - y) * sy;
                            if (u < 0 || v < 0 || u >= src.Width || v >= src.Height)
                            {
                                continue;
                            }

                            var p = src[(int)u, (int)v];
                            r += p.R * p.A;
                            g += p.G * p.A;
                            b += p.B * p.A;
                            a += p.A;
                        }
                    }

                    if (a <= 0)
                    {
                        continue;
                    }

                    var cnt = n * n;
                    dst.Blend(px, py, new Rgba(r / a * t.R, g / a * t.G, b / a * t.B, a / cnt * t.A * alpha));
                }
            }
        }

        public static void Fill(Raster dst, Rgba color)
        {
            for (var i = 0; i < dst.Pixels.Length; i++)
            {
                dst.Pixels[i] = color;
            }
        }
    }
}
