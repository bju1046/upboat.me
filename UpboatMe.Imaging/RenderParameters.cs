using System;
using System.Collections.Generic;

namespace UpboatMe.Imaging
{
    public enum MemeTextAlignment
    {
        Near,
        Center,
        Far,
    }

    public enum MemeFontStyle
    {
        Regular,
        Bold,
        Italic,
        BoldItalic,
    }

    public struct MemeRectangle
    {
        public MemeRectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int Right => X + Width;
    }

    public readonly struct MemeColor
    {
        public MemeColor(byte red, byte green, byte blue, byte alpha = 255)
        {
            A = alpha;
            R = red;
            G = green;
            B = blue;
        }

        public byte A { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public MemeColor WithAlpha(byte alpha)
        {
            return new MemeColor(R, G, B, alpha);
        }

        public static MemeColor FromArgb(byte alpha, byte red, byte green, byte blue)
        {
            return new MemeColor(red, green, blue, alpha);
        }
    }

    public static class MemeColors
    {
        private static readonly Dictionary<string, MemeColor> NamedColors = new Dictionary<
            string,
            MemeColor
        >(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = new MemeColor(0, 0, 0),
            ["white"] = new MemeColor(255, 255, 255),
            ["whitesmoke"] = new MemeColor(245, 245, 245),
            ["hotpink"] = new MemeColor(255, 105, 180),
            ["forestgreen"] = new MemeColor(34, 139, 34),
            ["yellow"] = new MemeColor(255, 255, 0),
            ["blue"] = new MemeColor(0, 0, 255),
            ["orange"] = new MemeColor(255, 165, 0),
            ["red"] = new MemeColor(255, 0, 0),
        };

        public static MemeColor FromName(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
            {
                return NamedColors["black"];
            }

            if (NamedColors.TryGetValue(colorName.Trim(), out var color))
            {
                return color;
            }

            throw new ArgumentException(
                $"Color '{colorName}' is not registered for meme rendering."
            );
        }
    }

    public class RenderParameters
    {
        public RenderParameters()
        {
            FullImagePath = string.Empty;
            FullWatermarkImageFilePath = string.Empty;
            WatermarkText = string.Empty;
            WatermarkFont = "Arial";
            PrivateFontFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Lines = new List<LineParameters>();
        }

        public string FullImagePath { get; set; }
        public bool DebugMode { get; set; }
        public string FullWatermarkImageFilePath { get; set; }
        public int WatermarkImageWidth { get; set; }
        public int WatermarkImageHeight { get; set; }
        public string WatermarkText { get; set; }
        public MemeColor WatermarkStroke { get; set; }
        public MemeColor WatermarkFill { get; set; }
        public int WatermarkStrokeWidth { get; set; }
        public string WatermarkFont { get; set; }
        public MemeFontStyle WatermarkFontStyle { get; set; }
        public int WatermarkFontSize { get; set; }
        public IDictionary<string, string> PrivateFontFiles { get; set; }
        public List<LineParameters> Lines { get; set; }
    }

    public class LineParameters
    {
        public LineParameters()
        {
            Text = string.Empty;
        }

        public string Text { get; set; }
        public int FontSize { get; set; }
        public float HeightPercent { get; set; }
        public MemeRectangle? Bounds { get; set; }
        public string Font { get; set; }
        public MemeColor Fill { get; set; }
        public MemeColor Stroke { get; set; }
        public int StrokeWidth { get; set; }
        public MemeFontStyle FontStyle { get; set; }
        public MemeTextAlignment TextAlignment { get; set; }
        public bool DoForceTextToAllCaps { get; set; }
        public bool HugBottom { get; set; }
    }
}
