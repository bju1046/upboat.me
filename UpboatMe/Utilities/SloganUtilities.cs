using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UpboatMe.Utilities
{
    public static class SloganUtilities
    {
        private static string[] _Slogans { get; set; }
        private static readonly object _syncLock = new object();

        private static string[] Slogans(string contentRootPath)
        {
            if (_Slogans == null)
            {
                lock (_syncLock)
                {
                    if (_Slogans == null)
                    {
                        var filePath = Path.Combine(contentRootPath, "App_Data", "slogans.txt");
                        _Slogans = File.ReadAllLines(filePath);
                    }
                }
            }

            return _Slogans;
        }

        public static string GetRandomSlogan(string contentRootPath)
        {
            var slogans = Slogans(contentRootPath);
            var sloganCount = slogans.Length;
            var random = new Random();
            var sloganIndex = random.Next(0, sloganCount);

            return slogans[sloganIndex];
        }
    }
}
