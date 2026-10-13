#if UNITY_EDITOR || DEVELOPMENT_BUILD || KNITTED_QA
#nullable enable

using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace KnittedPicture.Game.Qa
{
    /// <summary>
    /// PNG без модулей ImageConversion и ScreenCapture (их нет в проекте): пиксели экрана → RGB PNG.
    /// Крупный экран уменьшается в целое число раз, чтобы отчёт не весил сотни мегабайт.
    /// </summary>
    public static class QaPng
    {
        /// <summary>Пиксели снизу вверх (как отдаёт Texture2D) → PNG; maxHeight — уменьшить, если выше.</summary>
        public static byte[] Encode(Color32[] pixels, int width, int height, int maxHeight = 960)
        {
            var step = Mathf.Max(1, Mathf.CeilToInt(height / (float)maxHeight));
            var w = width / step;
            var h = height / step;
            using var raw = new MemoryStream();
            using (var deflate = new DeflateStream(raw, System.IO.Compression.CompressionLevel.Fastest, true))
            {
                var row = new byte[1 + w * 3];
                for (var y = 0; y < h; y++)
                {
                    // PNG — сверху вниз, а текстура — снизу вверх.
                    var source = (height - 1 - y * step) * width;
                    row[0] = 0;
                    for (var x = 0; x < w; x++)
                    {
                        var p = pixels[source + x * step];
                        row[1 + x * 3] = p.r;
                        row[2 + x * 3] = p.g;
                        row[3 + x * 3] = p.b;
                    }

                    deflate.Write(row, 0, row.Length);
                }
            }

            var compressed = raw.ToArray();
            var adler = Adler32(pixels, width, height, step, w, h);
            using var png = new MemoryStream();
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            var header = new byte[13];
            Big(header, 0, w);
            Big(header, 4, h);
            header[8] = 8;
            header[9] = 2;
            Chunk(png, "IHDR", header);

            // zlib: заголовок, сжатые данные, Adler-32 несжатых.
            var zlib = new byte[2 + compressed.Length + 4];
            zlib[0] = 0x78;
            zlib[1] = 0x01;
            System.Array.Copy(compressed, 0, zlib, 2, compressed.Length);
            Big(zlib, zlib.Length - 4, (int)adler);
            Chunk(png, "IDAT", zlib);
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        private static uint Adler32(Color32[] pixels, int width, int height, int step, int w, int h)
        {
            uint a = 1, b = 0;
            void Add(byte v)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }

            for (var y = 0; y < h; y++)
            {
                var source = (height - 1 - y * step) * width;
                Add(0);
                for (var x = 0; x < w; x++)
                {
                    var p = pixels[source + x * step];
                    Add(p.r);
                    Add(p.g);
                    Add(p.b);
                }
            }

            return (b << 16) | a;
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            Big(len, 0, data.Length);
            s.Write(len, 0, 4);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t, 0, 4);
            s.Write(data, 0, data.Length);
            var crc = 0xffffffffu;
            foreach (var arr in new[] { t, data })
            {
                foreach (var x in arr)
                {
                    crc ^= x;
                    for (var k = 0; k < 8; k++)
                    {
                        crc = (crc & 1) != 0 ? 0xedb88320u ^ (crc >> 1) : crc >> 1;
                    }
                }
            }

            var c = new byte[4];
            Big(c, 0, (int)(crc ^ 0xffffffffu));
            s.Write(c, 0, 4);
        }

        private static void Big(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }
    }
}
#endif
