using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using UpboatMe.Utilities;

namespace UpboatMe.Models
{
    public class BuilderViewModel
    {
        public string SelectedMeme { get; set; }
        public IList<Meme> Memes { get; set; }
        public List<string> Lines { get; set; }
        public string ApplicationPath { get; set; }

        private readonly Regex _previewUrlStripper = new Regex(@"\s+", RegexOptions.Compiled);

        public string GetPreviewUrl(IUrlHelper url)
        {
            var absoluteBaseUrl = url.AbsoluteAction("");
            var root = string.IsNullOrEmpty(ApplicationPath) ? "/" : ApplicationPath;
            if (!root.EndsWith("/"))
            {
                root += "/";
            }

            var path = string.Format("{0}{1}/{2}", root, SelectedMeme, string.Join("/", Lines));
            var strippedPath = _previewUrlStripper.Replace(path, "-");
            var trimmedPath = strippedPath.TrimEnd('/');
            var pathWithExtension = trimmedPath + ".jpg";

            return string.Concat(absoluteBaseUrl.TrimEnd('/'), pathWithExtension);
        }

        public string GetAltText()
        {
            return string.Format(
                "{0}:{1}{2}",
                SelectedMeme,
                Environment.NewLine,
                string.Join(Environment.NewLine, Lines)
            );
        }

        public string GetShareText()
        {
            return string.Format("{0}", SelectedMeme);
        }
    }
}
