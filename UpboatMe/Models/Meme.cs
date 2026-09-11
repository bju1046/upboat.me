using System.Collections.Generic;
using System.IO;
using UpboatMe.Imaging;

namespace UpboatMe.Models
{
    public class Meme
    {
        private const string ImagePathFormat = "~/Images/{0}";

        public string ImagePath { get; private set; }
        public string ImageType { get; private set; }
        public string ImageFileName { get; private set; }
        public string Description { get; private set; }
        public IList<string> Aliases { get; set; }
        public IReadOnlyList<LineConfig> Lines { get; set; }

        public string ImageFileNameWithoutExtension
        {
            get { return Path.GetFileNameWithoutExtension(ImageFileName); }
        }

        public Meme(
            string description,
            string imageFileName,
            IList<string> aliases,
            string imageType = "image/jpg"
        )
        {
            Description = description;
            ImagePath = string.Format(ImagePathFormat, imageFileName);
            ImageFileName = imageFileName;
            Aliases = aliases;
            ImageType = imageType;

            // Default meme has 2 lines, the second one hugs the bottom of the image
            Lines = new List<LineConfig> { new LineConfig(), new LineConfig(hugBottom: true) };
        }
    }

    public class LineConfig
    {
        public MemeColor Stroke { get; set; }
        public MemeColor Fill { get; set; }
        public string Font { get; set; }
        public int FontSize { get; set; }
        public MemeFontStyle FontStyle { get; set; }
        public int StrokeWidth { get; set; }
        public MemeTextAlignment TextAlignment { get; set; }
        public MemeTextAlignment LineAlignment { get; set; }
        public bool DoForceTextToAllCaps { get; set; }
        public float HeightPercent { get; set; }
        public MemeRectangle? Bounds { get; set; }
        public bool HugBottom { get; set; }

        public LineConfig(
            string stroke = "black",
            string fill = "white",
            string font = "Impact",
            MemeFontStyle fontStyle = MemeFontStyle.Regular,
            int fontSize = 40,
            int strokeWidth = 5,
            float heightPercent = 25,
            MemeTextAlignment textAlignment = MemeTextAlignment.Center,
            MemeTextAlignment lineAlignment = MemeTextAlignment.Near,
            bool doForceTextToAllCaps = true,
            bool hugBottom = false
        )
        {
            Stroke = MemeColors.FromName(stroke);
            Fill = MemeColors.FromName(fill);
            Font = font;
            FontSize = fontSize;
            StrokeWidth = strokeWidth;
            HeightPercent = heightPercent;
            TextAlignment = textAlignment;
            LineAlignment = lineAlignment;
            DoForceTextToAllCaps = doForceTextToAllCaps;
            FontStyle = fontStyle;
            HugBottom = hugBottom;
        }
    }
}
