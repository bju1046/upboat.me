using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UpboatMe.App_Start;
using UpboatMe.Models;

namespace UpboatMe.Models
{
    public class GlobalMemeConfiguration
    {
        private const string NotFoundMemeName = "yuno";

        private static MemeConfiguration _memes;
        public static MemeConfiguration Memes
        {
            get { return _memes; }
        }

        public static Meme NotFoundMeme
        {
            get { return _memes[NotFoundMemeName]; }
        }

        static GlobalMemeConfiguration()
        {
            _memes = new MemeConfiguration();
        }

        public static void Initialize(string contentRootPath)
        {
            _memes = new MemeConfiguration();
            var imagesDirectory = Path.Combine(contentRootPath, "Images");
            var filepaths = Directory.GetFiles(imagesDirectory, "*.jpg");
            var filenames = filepaths
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Select(f => f!)
                .ToArray();
            MemeConfig.AutoRegisterMemesByFile(_memes, filenames);
            MemeConfig.RegisterManualMemes(_memes, contentRootPath);
        }
    }
}
