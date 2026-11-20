using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using Frogs.EditorTools;
using Frogs.Unity.Views;

namespace Frogs.Unity.EditModeTests
{
    /// <summary>
    /// The approved lily pad model's albedo atlas, read from the committed PNG
    /// for tests that check what is painted on a pad (#465). The imported
    /// texture is not readable — a player has no use for its pixels — and the
    /// project has no image-conversion module to decode it with
    /// <c>Texture2D.LoadImage</c>, so the file's own bytes are decoded here:
    /// an 8-bit, non-interlaced PNG is a zlib stream of filtered rows, and
    /// that is all the script writes.
    ///
    /// The atlas layout is tools/art/lilypad_model.py's, copied by hand: four
    /// 1024 px quadrants in a 2048 px square, quadrant <c>q</c> holding the
    /// <c>q</c>th entry of <c>LilyPadNotchAngles</c> at column <c>q % 2</c>,
    /// row <c>q / 2</c> counted up from the bottom (UV v = 0), its pad's disc
    /// inset by <see cref="UvMargin"/> of the quadrant on every side, notch
    /// pointing along +u.
    /// </summary>
    internal static class LilyPadAlbedo
    {
        /// <summary>The script's UV_MARGIN: each pad's disc sits this far inside its quadrant.</summary>
        public const float UvMargin = 0.04f;

        static int s_width;
        static int s_height;
        static Color32[] s_pixels;

        public static int Size
        {
            get
            {
                Load();
                return s_width;
            }
        }

        /// <summary>How many atlas pixels one pad's radius spans.</summary>
        public static float RadiusInPixels
        {
            get { return Size / 2f * (1f - (2f * UvMargin)) / 2f; }
        }

        /// <summary>
        /// The atlas pixel for the pad with this notch width, at an angle in
        /// degrees from its notch and that fraction of its radius out. The
        /// pad is symmetric about its notch's axis, so which way the angle
        /// runs does not matter.
        /// </summary>
        public static Color PixelAt(float notchWidth, float degreesFromNotch, float radiusFraction)
        {
            Load();

            var quadrant = QuadrantOf(notchWidth);
            var radians = degreesFromNotch * Mathf.Deg2Rad;
            var x = radiusFraction * Mathf.Cos(radians);
            var y = radiusFraction * Mathf.Sin(radians);

            var u = ((quadrant % 2) + UvMargin + ((x + 1f) / 2f * (1f - (2f * UvMargin)))) / 2f;
            var v = ((quadrant / 2) + UvMargin + ((y + 1f) / 2f * (1f - (2f * UvMargin)))) / 2f;

            var column = Mathf.Clamp(Mathf.FloorToInt(u * s_width), 0, s_width - 1);
            var row = Mathf.Clamp(Mathf.FloorToInt(v * s_height), 0, s_height - 1);

            // A PNG's rows run top-down; UV v runs bottom-up.
            return s_pixels[((s_height - 1 - row) * s_width) + column];
        }

        static int QuadrantOf(float notchWidth)
        {
            for (var q = 0; q < GameBoardLaneView.LilyPadNotchAngles.Count; q++)
            {
                if (Mathf.Approximately(GameBoardLaneView.LilyPadNotchAngles[q], notchWidth))
                {
                    return q;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(notchWidth), notchWidth, "not one of `LilyPadNotchAngles`");
        }

        static void Load()
        {
            if (s_pixels != null)
            {
                return;
            }

            s_pixels = DecodePng(File.ReadAllBytes(LilyPadModelImport.AlbedoPath), out s_width, out s_height);
        }

        /// <summary>
        /// The pixels of an 8-bit RGB or RGBA, non-interlaced PNG, top row
        /// first. Anything else is refused rather than misread.
        /// </summary>
        internal static Color32[] DecodePng(byte[] file, out int width, out int height)
        {
            width = 0;
            height = 0;
            var channels = 0;
            var data = new MemoryStream();

            // After the 8-byte signature, chunks: length, type, data, CRC.
            for (var at = 8; at < file.Length;)
            {
                var length = ReadInt(file, at);
                var type = System.Text.Encoding.ASCII.GetString(file, at + 4, 4);

                if (type == "IHDR")
                {
                    width = ReadInt(file, at + 8);
                    height = ReadInt(file, at + 12);

                    var bitDepth = file[at + 16];
                    var colourType = file[at + 17];
                    var interlace = file[at + 20];

                    if (bitDepth != 8 || interlace != 0 || (colourType != 2 && colourType != 6))
                    {
                        throw new NotSupportedException("Only 8-bit, non-interlaced RGB or RGBA PNGs are decoded here.");
                    }

                    channels = colourType == 2 ? 3 : 4;
                }
                else if (type == "IDAT")
                {
                    data.Write(file, at + 8, length);
                }
                else if (type == "IEND")
                {
                    break;
                }

                at += 12 + length;
            }

            // IDAT is a zlib stream: a 2-byte header, then raw deflate.
            data.Position = 2;
            var stride = width * channels;
            var raw = new byte[height * (stride + 1)];

            using (var inflate = new DeflateStream(data, CompressionMode.Decompress))
            {
                var read = 0;

                while (read < raw.Length)
                {
                    var n = inflate.Read(raw, read, raw.Length - read);

                    if (n == 0)
                    {
                        throw new InvalidDataException("The PNG's image data ended early.");
                    }

                    read += n;
                }
            }

            var rows = new byte[height * stride];

            for (var y = 0; y < height; y++)
            {
                var filter = raw[y * (stride + 1)];

                for (var x = 0; x < stride; x++)
                {
                    var value = raw[(y * (stride + 1)) + 1 + x];
                    var left = x >= channels ? rows[(y * stride) + x - channels] : 0;
                    var up = y > 0 ? rows[((y - 1) * stride) + x] : 0;
                    var upLeft = x >= channels && y > 0 ? rows[((y - 1) * stride) + x - channels] : 0;

                    int predicted;

                    switch (filter)
                    {
                        case 0: predicted = 0; break;
                        case 1: predicted = left; break;
                        case 2: predicted = up; break;
                        case 3: predicted = (left + up) / 2; break;
                        case 4: predicted = Paeth(left, up, upLeft); break;
                        default: throw new InvalidDataException("Unknown PNG row filter " + filter + ".");
                    }

                    rows[(y * stride) + x] = (byte)(value + predicted);
                }
            }

            var pixels = new Color32[width * height];

            for (var i = 0; i < pixels.Length; i++)
            {
                var o = i * channels;
                pixels[i] = new Color32(rows[o], rows[o + 1], rows[o + 2], channels == 4 ? rows[o + 3] : (byte)255);
            }

            return pixels;
        }

        static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);

            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        static int ReadInt(byte[] bytes, int at)
        {
            return (bytes[at] << 24) | (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
        }
    }
}
