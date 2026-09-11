using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;

namespace UpboatMe.Imaging
{
    public class Renderer
    {
        private static readonly IReadOnlyDictionary<string, string[]> FontFallbacks =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Impact"] = new[]
                {
                    "Impact",
                    "Arial Black",
                    "Liberation Sans Narrow",
                    "DejaVu Sans Condensed",
                    "DejaVu Sans",
                    "sans-serif",
                },
                ["Arial"] = new[] { "Arial", "Liberation Sans", "DejaVu Sans", "sans-serif" },
                ["Comic Sans MS"] = new[]
                {
                    "Comic Sans MS",
                    "Comic Neue",
                    "Chilanka",
                    "DejaVu Sans",
                    "sans-serif",
                },
                ["Segoe UI"] = new[]
                {
                    "Segoe UI",
                    "Noto Sans",
                    "Liberation Sans",
                    "DejaVu Sans",
                    "sans-serif",
                },
            };

        private readonly ConcurrentDictionary<string, SKTypeface> _privateTypefaces = new(
            StringComparer.OrdinalIgnoreCase
        );

        public byte[] Render(RenderParameters parameters)
        {
            using var baseBitmap = SKBitmap.Decode(parameters.FullImagePath);
            if (baseBitmap == null)
            {
                throw new InvalidOperationException(
                    $"Unable to decode image at {parameters.FullImagePath}."
                );
            }

            using var image = new SKBitmap(
                baseBitmap.Width,
                baseBitmap.Height,
                baseBitmap.ColorType,
                baseBitmap.AlphaType
            );
            using var canvas = new SKCanvas(image);
            canvas.DrawBitmap(baseBitmap, 0, 0);

            DrawWatermark(parameters, canvas, image.Width, image.Height);

            foreach (var line in parameters.Lines)
            {
                DrawLine(parameters, canvas, image.Width, image.Height, line);
            }

            using var encoded = EncodeByFileExtension(image, parameters.FullImagePath);
            return encoded.ToArray();
        }

        private static SKData EncodeByFileExtension(SKBitmap image, string fullImagePath)
        {
            var extension = Path.GetExtension(fullImagePath) ?? string.Empty;
            var format = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? SKEncodedImageFormat.Png
                : SKEncodedImageFormat.Jpeg;

            using var skImage = SKImage.FromBitmap(image);
            return skImage.Encode(format, 90);
        }

        private void DrawWatermark(
            RenderParameters parameters,
            SKCanvas canvas,
            int imageWidth,
            int imageHeight
        )
        {
            var padding = 2;
            var width = parameters.WatermarkImageWidth;
            var height = parameters.WatermarkImageHeight;

            using (var watermark = SKBitmap.Decode(parameters.FullWatermarkImageFilePath))
            {
                if (watermark != null)
                {
                    var destination = new SKRect(
                        imageWidth - width - padding,
                        imageHeight - height - padding,
                        imageWidth - padding,
                        imageHeight - padding
                    );
                    canvas.DrawBitmap(watermark, destination);
                }
            }

            var typeface = ResolveTypeface(
                parameters,
                parameters.WatermarkFont,
                parameters.WatermarkFontStyle,
                parameters.WatermarkText
            );
            using var fillPaint = CreateTextPaint(
                typeface,
                parameters.WatermarkFontSize,
                parameters.WatermarkFill.WithAlpha(150).ToSKColor(),
                SKPaintStyle.Fill,
                0
            );
            var metrics = fillPaint.FontMetrics;
            var textWidth = fillPaint.MeasureText(parameters.WatermarkText);
            var textHeight = metrics.Descent - metrics.Ascent;
            var bounds = new MemeRectangle(
                imageWidth - width - (int)Math.Ceiling(textWidth),
                imageHeight - (int)Math.Ceiling(textHeight),
                imageWidth,
                (int)Math.Ceiling(textHeight)
            );

            DrawText(
                canvas,
                new[] { parameters.WatermarkText },
                typeface,
                parameters.WatermarkFontSize,
                parameters.WatermarkStroke.WithAlpha(150),
                parameters.WatermarkStrokeWidth,
                parameters.WatermarkFill.WithAlpha(150),
                parameters.WatermarkFontStyle,
                MemeTextAlignment.Near,
                bounds
            );
        }

        private void DrawLine(
            RenderParameters parameters,
            SKCanvas canvas,
            int imageWidth,
            int imageHeight,
            LineParameters line
        )
        {
            var maxHeight = (int)Math.Ceiling(imageHeight * (line.HeightPercent / 100));
            var bounds = line.Bounds ?? new MemeRectangle(0, 0, imageWidth, maxHeight);

            if (line.HugBottom)
            {
                bounds.Y = imageHeight - bounds.Height - 1;
            }

            var fontSize = line.FontSize;
            var typeface = ResolveTypeface(parameters, line.Font, line.FontStyle, line.Text);

            while (true)
            {
                using var measurePaint = CreateTextPaint(
                    typeface,
                    fontSize,
                    line.Fill.ToSKColor(),
                    SKPaintStyle.Fill,
                    0
                );
                var metrics = measurePaint.FontMetrics;
                var lines = WrapText(line.Text, measurePaint, bounds.Width);
                var lineHeight = metrics.Descent - metrics.Ascent;
                var textHeight = lineHeight * lines.Count;

                if (textHeight > bounds.Height && fontSize > 10)
                {
                    fontSize -= 2;
                    continue;
                }

                DrawText(
                    canvas,
                    lines,
                    typeface,
                    fontSize,
                    line.Stroke,
                    line.StrokeWidth,
                    line.Fill,
                    line.FontStyle,
                    line.TextAlignment,
                    bounds
                );

                if (parameters.DebugMode)
                {
                    DrawBoxes(canvas, imageWidth, imageHeight, bounds);
                }

                break;
            }
        }

        private SKTypeface ResolveTypeface(
            RenderParameters parameters,
            string fontName,
            MemeFontStyle style,
            string text
        )
        {
            if (
                parameters.PrivateFontFiles.TryGetValue(fontName, out var fontFilePath)
                && File.Exists(fontFilePath)
            )
            {
                var privateTypeface = _privateTypefaces.GetOrAdd(
                    fontName,
                    _ => SKTypeface.FromFile(fontFilePath)
                );
                if (privateTypeface != null && privateTypeface.ContainsGlyphs(text))
                {
                    return privateTypeface;
                }
            }

            var skStyle = style switch
            {
                MemeFontStyle.Bold => SKFontStyle.Bold,
                MemeFontStyle.Italic => SKFontStyle.Italic,
                MemeFontStyle.BoldItalic => SKFontStyle.BoldItalic,
                _ => SKFontStyle.Normal,
            };

            var candidates = FontFallbacks.TryGetValue(fontName, out var fallbackCandidates)
                ? fallbackCandidates
                : new[] { fontName, "sans-serif" };

            foreach (var candidate in candidates)
            {
                var resolved = SKTypeface.FromFamilyName(candidate, skStyle);
                if (resolved != null && resolved.ContainsGlyphs(text))
                {
                    return resolved;
                }
            }

            return SKTypeface.Default;
        }

        private static SKPaint CreateTextPaint(
            SKTypeface typeface,
            float fontSize,
            SKColor color,
            SKPaintStyle style,
            float strokeWidth
        )
        {
            return new SKPaint
            {
                IsAntialias = true,
                Typeface = typeface,
                TextSize = fontSize,
                Color = color,
                Style = style,
                StrokeWidth = strokeWidth,
                StrokeJoin = SKStrokeJoin.Round,
            };
        }

        private static void DrawText(
            SKCanvas canvas,
            IReadOnlyList<string> lines,
            SKTypeface typeface,
            int fontSize,
            MemeColor stroke,
            int strokeWidth,
            MemeColor fill,
            MemeFontStyle fontStyle,
            MemeTextAlignment textAlignment,
            MemeRectangle bounds
        )
        {
            using var fillPaint = CreateTextPaint(
                typeface,
                fontSize,
                fill.ToSKColor(),
                SKPaintStyle.Fill,
                0
            );
            var metrics = fillPaint.FontMetrics;
            var lineHeight = metrics.Descent - metrics.Ascent;
            for (var index = 0; index < lines.Count; index++)
            {
                var textWidth = fillPaint.MeasureText(lines[index]);
                var x = (float)bounds.X;
                if (textAlignment == MemeTextAlignment.Center)
                {
                    x = bounds.X + (bounds.Width - textWidth) / 2f;
                }
                else if (textAlignment == MemeTextAlignment.Far)
                {
                    x = bounds.Right - textWidth;
                }

                var baseline = bounds.Y - metrics.Ascent + index * lineHeight;

                if (strokeWidth >= 0)
                {
                    using var path = fillPaint.GetTextPath(lines[index], x, baseline);
                    using var strokePaint = CreateTextPaint(
                        typeface,
                        fontSize,
                        stroke.ToSKColor(),
                        SKPaintStyle.Stroke,
                        strokeWidth
                    );
                    canvas.DrawPath(path, strokePaint);
                    canvas.DrawPath(path, fillPaint);
                }
                else
                {
                    canvas.DrawText(lines[index], x, baseline, fillPaint);
                }
            }
        }

        internal static IReadOnlyList<string> WrapText(
            string text,
            SKPaint paint,
            float maxWidth
        )
        {
            var result = new List<string>();
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var current = string.Empty;

                foreach (var word in words)
                {
                    var candidate = string.IsNullOrEmpty(current)
                        ? word
                        : current + " " + word;
                    if (paint.MeasureText(candidate) <= maxWidth)
                    {
                        current = candidate;
                        continue;
                    }

                    if (!string.IsNullOrEmpty(current))
                    {
                        result.Add(current);
                        current = string.Empty;
                    }

                    foreach (var character in SplitWordToFit(word, paint, maxWidth))
                    {
                        if (string.IsNullOrEmpty(current))
                        {
                            current = character;
                        }
                        else if (paint.MeasureText(current + character) <= maxWidth)
                        {
                            current += character;
                        }
                        else
                        {
                            result.Add(current);
                            current = character;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(current) || words.Length == 0)
                {
                    result.Add(current);
                }
            }

            return result.Count == 0 ? new[] { string.Empty } : result;
        }

        private static IEnumerable<string> SplitWordToFit(
            string word,
            SKPaint paint,
            float maxWidth
        )
        {
            foreach (var character in word.Select(character => character.ToString()))
            {
                yield return character;
            }
        }

        private static void DrawBoxes(
            SKCanvas canvas,
            int imageWidth,
            int imageHeight,
            MemeRectangle bounds
        )
        {
            using (
                var brush = new SKPaint
                {
                    Color = new SKColor(255, 0, 0, 150),
                    Style = SKPaintStyle.Fill,
                }
            )
            {
                canvas.DrawRect(
                    new SKRect(bounds.X, bounds.Y, bounds.Right, bounds.Y + bounds.Height),
                    brush
                );
            }

            using var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 12,
                IsAntialias = true,
                Typeface = SKTypeface.Default,
            };

            for (var y = 0; y < imageHeight; y += 20)
            {
                canvas.DrawText(y.ToString(CultureInfo.InvariantCulture), 0, y, textPaint);
            }

            canvas.DrawText($"H: {imageHeight}, W: {imageWidth}", imageWidth / 2f, 20, textPaint);
        }
    }

    internal static class MemeColorExtensions
    {
        public static SKColor ToSKColor(this MemeColor color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
    }
}
