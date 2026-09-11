using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UpboatMe.Imaging;
using UpboatMe.Models;
using UpboatMe.Utilities;

namespace UpboatMe.App_Start
{
    public static class MemeConfig
    {
        private static readonly Regex StripCharactersToCollapseWords = new Regex(@"[']");
        private static readonly Regex NonWordStripCharacters = new Regex(
            @"[_' -]",
            RegexOptions.Compiled
        );
        private static readonly Regex ShouldBeDisplayedAsWhitespaceCharacters = new Regex(@"[_-]");

        public static void AutoRegisterMemesByFile(MemeConfiguration memes, string[] filenames)
        {
            var usedAliases = new HashSet<string>();

            foreach (var filename in filenames.OrderBy(f => f))
            {
                var lowerFilename = filename.ToLowerInvariant();

                var name = Path.GetFileNameWithoutExtension(lowerFilename);

                if (name == null)
                {
                    throw new NullReferenceException("name cannot be null");
                }
                // strip off number prefix (used for ordering)
                name = name.Substring(lowerFilename.IndexOf("-", StringComparison.Ordinal) + 1);

                var extension = Path.GetExtension(lowerFilename);

                if (extension == null)
                {
                    throw new NullReferenceException("extension cannot be null");
                }
                extension = extension.Substring(1); // strip off the dot

                var aliases = new List<string>
                {
                    name.ToInitialism(),
                    NonWordStripCharacters.Replace(name, ""),
                };

                var memeName = name.ToTitleString();

                var filteredAliases = aliases
                    .Where(a => a.Length > 1) // don't use single character aliases
                    .Distinct()
                    .ToList();

                var survivingAliases = new List<string>();
                // Note: don't follow resharper's advice on this loop. It be cray cray
                foreach (var alias in filteredAliases)
                {
                    if (usedAliases.Add(alias))
                    {
                        survivingAliases.Add(alias);
                    }
                }

                // TODO: image/jpg isn't acutally valid. Fix this or get rid of it
                var imageType = "image/" + extension;

                memes.Add(new Meme(memeName, filename, survivingAliases, imageType));
            }
        }

        private static readonly Regex DigitPrefixRegex = new Regex(
            @"^(\d+)",
            RegexOptions.Compiled
        );

        public static string ToInitialism(this string input)
        {
            var collapsedWords = StripCharactersToCollapseWords.Replace(input, "");
            var words = NonWordStripCharacters.Split(collapsedWords);
            var wordParts = words.Select(w =>
            {
                // if the prefix is a number, take the whole number
                var possibleNumberPrefix = DigitPrefixRegex.Match(w);
                if (possibleNumberPrefix.Captures.Count > 0)
                {
                    return possibleNumberPrefix.Captures[0].Value;
                }
                // otherwise just take the first character
                return w.Substring(0, 1);
            });

            return string.Join("", wordParts);
        }

        public static string ToTitleString(this string name)
        {
            // first drop names from something like "I'll-have-you-know" to "Ill-have-you-know"
            // then fix the separators so it becomes "I'll have you know"
            var memeName = ShouldBeDisplayedAsWhitespaceCharacters.Replace(name, " ");

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(memeName);
        }

        public static readonly Dictionary<string, string> PrivateFontFiles = new Dictionary<
            string,
            string
        >(StringComparer.OrdinalIgnoreCase);

        public static void RegisterManualMemes(MemeConfiguration memes, string contentRootPath)
        {
            var sfActionManExtendedFontPath = Path.Combine(
                contentRootPath,
                "Fonts",
                "SFActionManExtended.ttf"
            );
            PrivateFontFiles["SF Action Man Extended"] = sfActionManExtendedFontPath;
            PrivateFontFiles["SF Action Man Extended Italic"] = Path.Combine(
                contentRootPath,
                "Fonts",
                "SFActionManExtended-Italic.ttf"
            );

            // overrides
            memes["10guy"].Aliases.Add("tenguy");
            memes["allthethings"].Aliases.Add("xallthey");
            memes["angrywalter"].Aliases.Add("amitheonlyone", "aitoo");
            memes["annoyedpicard"].Aliases.Add("whythefuck");
            memes["braceyourselves"].Aliases.Add("imminentned", "iscoming");
            memes["ermahgerd"].Aliases.Add("emg");
            memes["everyonelosestheirminds"].Aliases.Add("joker");
            memes["futuramafry"].Aliases.Add("notsureif");
            memes["grumpycat"].Aliases.Add("sadcat");
            memes["idontwanttoliveonthisplanetanymore"].Aliases.Add("farnsworth");
            memes["illhaveyouknow"].Aliases.Add("spongebob");
            memes["officespacelumbergh"].Aliases.Add("thatdbegreat", "yeah");
            memes["onedoesnotsimply"].Aliases.Add("boromir", "mordor");
            memes["schrutefacts"].Aliases.Add("schrute", "dwight", "correction", "false");
            memes["technologicallyimpairedduck"].Aliases.Add("technologyduck");
            memes["boythatescalatedquickly"].Aliases.Add("wteq", "ronburgundy");
            memes["themostinterestingmanintheworld"].Aliases.Add("idontalways");
            memes["toystoryeverywhere"].Aliases.Add("buzzwoody", "xxeverywhere");
            memes["unhelpfulhighschoolteacher"].Aliases.Add("scumbagteacher");
            memes["mckaylamaroneynotimpressed"].Aliases.Add("nim", "um", "unimpressedmckayla");
            memes["csisunglasses"].Aliases.Add("csi", "sunglasses", "yeeaaaahh");

            var batman = memes["batmanslappingrobin"];
            foreach (var line in batman.Lines)
            {
                line.Font = "SF Action Man Extended";
                line.Fill = MemeColor.FromArgb(255, 63, 63, 63);
                line.StrokeWidth = -1;
                line.FontStyle = MemeFontStyle.Italic;
            }
            batman.Lines[0].Bounds = new MemeRectangle(10, 5, 180, 75);
            batman.Lines[1].Bounds = new MemeRectangle(220, 5, 170, 75);
            batman.Lines[1].HugBottom = false;

            var resharper = memes["resharpertip"];
            foreach (var line in resharper.Lines)
            {
                line.Font = "Segoe UI";
                line.TextAlignment = MemeTextAlignment.Near;
                line.Fill = MemeColors.FromName("WhiteSmoke");
                line.StrokeWidth = -1;
                line.DoForceTextToAllCaps = false;
            }
            resharper.Lines[0].Bounds = new MemeRectangle(78, 72, 481, 22);
            resharper.Lines[1].Bounds = new MemeRectangle(78, 97, 462, 22);
            resharper.Lines[1].HugBottom = false;

            var doge = memes["doge"];
            doge.Lines = Enumerable.Range(0, 6).Select(i => new LineConfig()).ToList();
            foreach (var line in doge.Lines)
            {
                line.Font = "Comic Sans MS";
                line.TextAlignment = MemeTextAlignment.Near;
                line.StrokeWidth = -1;
                //line.FontStyle = FontStyle.Bold;
            }
            doge.Lines[0].Bounds = new MemeRectangle(30, 30, 400, 100);
            doge.Lines[0].Fill = MemeColors.FromName("HotPink");
            doge.Lines[0].FontSize = 50;

            doge.Lines[1].Bounds = new MemeRectangle(20, 120, 550, 100);
            doge.Lines[1].TextAlignment = MemeTextAlignment.Far;
            doge.Lines[1].Fill = MemeColors.FromName("ForestGreen");
            doge.Lines[1].FontSize = 32;

            doge.Lines[2].Bounds = new MemeRectangle(50, 460, 400, 100);
            doge.Lines[2].Fill = MemeColors.FromName("Yellow");
            doge.Lines[2].FontSize = 24;

            doge.Lines[3].Bounds = new MemeRectangle(20, 530, 580, 100);
            doge.Lines[3].TextAlignment = MemeTextAlignment.Far;
            doge.Lines[3].Fill = MemeColors.FromName("Blue");
            doge.Lines[3].FontSize = 20;

            doge.Lines[4].Bounds = new MemeRectangle(20, 350, 510, 100);
            doge.Lines[4].TextAlignment = MemeTextAlignment.Far;
            doge.Lines[4].Fill = MemeColors.FromName("Orange");
            doge.Lines[4].FontSize = 36;

            doge.Lines[5].Bounds = new MemeRectangle(20, 220, 560, 100);
            doge.Lines[5].TextAlignment = MemeTextAlignment.Far;
            doge.Lines[5].Fill = MemeColors.FromName("Red");
            doge.Lines[5].FontSize = 22;

            var csi = memes["csisunglasses"];
            csi.Lines = Enumerable.Range(0, 4).Select(i => new LineConfig()).ToList();
            foreach (var line in csi.Lines)
            {
                line.Fill = MemeColors.FromName("Black");
                line.Font = "SF Action Man Extended";
                line.StrokeWidth = -1;
                line.TextAlignment = MemeTextAlignment.Center;
            }
            csi.Lines[0].Bounds = new MemeRectangle(12, 18, 231, 58);
            csi.Lines[1].Bounds = new MemeRectangle(190, 94, 78, 18);
            csi.Lines[2].Bounds = new MemeRectangle(311, 33, 207, 61);
            csi.Lines[3].Bounds = new MemeRectangle(35, 342, 196, 49);
        }
    }
}
