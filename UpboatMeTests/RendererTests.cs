using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UpboatMe.Imaging;

namespace UpboatMeTests
{
    [TestClass]
    public class RendererTests
    {
        [TestMethod]
        public void RenderDefaultFontMemeReturnsJpegBytes()
        {
            var contentRoot = GetContentRoot();
            var renderer = new Renderer();

            var bytes = renderer.Render(
                new RenderParameters
                {
                    FullImagePath = Path.Combine(contentRoot, "Images", "0152-success-kid.jpg"),
                    FullWatermarkImageFilePath = Path.Combine(
                        contentRoot,
                        "Content",
                        "UpBoatWatermark.png"
                    ),
                    WatermarkImageWidth = 25,
                    WatermarkImageHeight = 25,
                    WatermarkText = "upboat.me",
                    WatermarkFont = "Arial",
                    WatermarkFontStyle = MemeFontStyle.Regular,
                    WatermarkFontSize = 9,
                    WatermarkStroke = MemeColors.FromName("Black"),
                    WatermarkFill = MemeColors.FromName("White"),
                    WatermarkStrokeWidth = 1,
                    Lines = new List<LineParameters>
                    {
                        new LineParameters
                        {
                            Text = "top text",
                            Font = "Impact",
                            FontSize = 40,
                            HeightPercent = 25,
                            Fill = MemeColors.FromName("White"),
                            Stroke = MemeColors.FromName("Black"),
                            StrokeWidth = 5,
                            TextAlignment = MemeTextAlignment.Center,
                        },
                        new LineParameters
                        {
                            Text = "bottom text",
                            Font = "Impact",
                            FontSize = 40,
                            HeightPercent = 25,
                            Fill = MemeColors.FromName("White"),
                            Stroke = MemeColors.FromName("Black"),
                            StrokeWidth = 5,
                            TextAlignment = MemeTextAlignment.Center,
                            HugBottom = true,
                        },
                    },
                }
            );

            AssertJpeg(bytes);
        }

        [TestMethod]
        public void RenderPrivateFontMemeReturnsJpegBytes()
        {
            var contentRoot = GetContentRoot();
            var renderer = new Renderer();

            var bytes = renderer.Render(
                new RenderParameters
                {
                    FullImagePath = Path.Combine(
                        contentRoot,
                        "Images",
                        "0020-batman-slapping-robin.jpg"
                    ),
                    FullWatermarkImageFilePath = Path.Combine(
                        contentRoot,
                        "Content",
                        "UpBoatWatermark.png"
                    ),
                    WatermarkImageWidth = 25,
                    WatermarkImageHeight = 25,
                    WatermarkText = "upboat.me",
                    WatermarkFont = "Arial",
                    WatermarkFontStyle = MemeFontStyle.Regular,
                    WatermarkFontSize = 9,
                    WatermarkStroke = MemeColors.FromName("Black"),
                    WatermarkFill = MemeColors.FromName("White"),
                    WatermarkStrokeWidth = 1,
                    PrivateFontFiles = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase
                    )
                    {
                        ["SF Action Man Extended"] = Path.Combine(
                            contentRoot,
                            "Fonts",
                            "SFActionManExtended.ttf"
                        ),
                        ["SF Action Man Extended Italic"] = Path.Combine(
                            contentRoot,
                            "Fonts",
                            "SFActionManExtended-Italic.ttf"
                        ),
                    },
                    Lines = new List<LineParameters>
                    {
                        new LineParameters
                        {
                            Text = "first line",
                            Font = "SF Action Man Extended",
                            FontSize = 40,
                            Fill = MemeColor.FromArgb(255, 63, 63, 63),
                            Stroke = MemeColors.FromName("Black"),
                            StrokeWidth = -1,
                            FontStyle = MemeFontStyle.Italic,
                            Bounds = new MemeRectangle(10, 5, 180, 75),
                        },
                        new LineParameters
                        {
                            Text = "second line",
                            Font = "SF Action Man Extended",
                            FontSize = 40,
                            Fill = MemeColor.FromArgb(255, 63, 63, 63),
                            Stroke = MemeColors.FromName("Black"),
                            StrokeWidth = -1,
                            FontStyle = MemeFontStyle.Italic,
                            Bounds = new MemeRectangle(220, 5, 170, 75),
                        },
                    },
                }
            );

            AssertJpeg(bytes);
        }

        private static string GetContentRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "UpboatMe.sln")))
                {
                    return Path.Combine(current.FullName, "UpboatMe");
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Unable to locate the repository root from the test output directory."
            );
        }

        private static void AssertJpeg(byte[] bytes)
        {
            Assert.IsNotNull(bytes);
            Assert.IsTrue(bytes.Length > 4, "Expected encoded image bytes.");
            Assert.AreEqual(0xFF, bytes[0]);
            Assert.AreEqual(0xD8, bytes[1]);
        }
    }
}
