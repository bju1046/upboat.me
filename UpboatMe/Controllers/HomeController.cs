using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using UpboatMe.Models;
using UpboatMe.Utilities;

namespace UpboatMe.Controllers
{
    public class HomeController : Controller
    {
        // Note: don't forget to explicitly enable new actions in the route config
        [OutputCache(Duration = 60)]
        public IActionResult Index()
        {
            var memes = GlobalMemeConfiguration.Memes.GetMemes();

            // note: since this method uses output caching, you should really only expect
            // the value to change once per minute
            var randomIndex = ThreadRandom.Next(0, memes.Count);
            var randomMeme = memes[randomIndex];

            var root = Request.PathBase.Value ?? "/";
            if (!root.EndsWith("/"))
            {
                root += "/";
            }

            var viewModel = new IndexViewModel()
            {
                RecentMemes = new List<RecentMeme>
                {
                    new RecentMeme
                    {
                        Url = root + "sap/wears-suit-and-tie-to-interview/phone-interview.jpg",
                        Alt =
                            "socially awkward penguin: wears suit and tie to interview"
                            + Environment.NewLine
                            + "phone interview",
                        Title = "socially awkward penguin",
                    },
                    new RecentMeme
                    {
                        Url = root + "fry/not-sure-if-sunny-outside/or-hungover.jpg",
                        Alt =
                            "futurama fry: not sure if sunny outside"
                            + Environment.NewLine
                            + "or hungover",
                        Title = "futurama fry",
                    },
                    new RecentMeme
                    {
                        Url =
                            root
                            + "scc/i-don't-buy-things-with-money/i-buy-them-with-hours-of-my-life.jpg",
                        Alt =
                            "sudden clarity clarence: i don't buy things with money"
                            + Environment.NewLine
                            + "i buy them with hours of my life",
                        Title = "sudden clarity clarence",
                    },
                    new RecentMeme
                    {
                        Url = root + "blb/has-pet-rock/it-runs-away.jpg",
                        Alt = "bad luck brian: has pet rock" + Environment.NewLine + "it runs away",
                        Title = "bad luck brian",
                    },
                },
                BuilderViewModel = new BuilderViewModel
                {
                    Memes = memes,
                    Lines = Enumerable.Repeat("", randomMeme.Lines.Count).ToList(),
                    SelectedMeme = randomMeme.Aliases.First(),
                    ApplicationPath = root,
                },
            };

            return View(viewModel);
        }

        public IActionResult HowTo()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult Pricing()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
