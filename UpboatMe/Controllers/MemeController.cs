using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UpboatMe.App_Start;
using UpboatMe.Imaging;
using UpboatMe.Models;
using UpboatMe.Utilities;

namespace UpboatMe.Controllers;

public class MemeController : Controller
{
    private static string ResolveWatermarkPath(string contentRoot)
    {
        var legacyPath = Path.Combine(contentRoot, "Content", "UpBoatWatermark.png");
        if (System.IO.File.Exists(legacyPath))
        {
            return legacyPath;
        }

        return Path.Combine(contentRoot, "wwwroot", "Content", "UpBoatWatermark.png");
    }

    [OutputCache(Duration = 60 * 60)]
    public IActionResult Make()
    {
        var requestPath = Request.Path.Value ?? "/";
        var requestUrl = requestPath + Request.QueryString;
        var memeRequest = MemeRequest.FromUrl(requestUrl, Request.PathBase.Value ?? "/");
        var meme = MemeUtilities.FindMeme(GlobalMemeConfiguration.Memes, memeRequest.Name);
        if (meme == null)
        {
            meme = GlobalMemeConfiguration.NotFoundMeme;
            memeRequest.Lines = new List<string> { "404", "Y U NO USE VALID MEME NAME?" };
        }

        var hasExtension =
            requestPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || requestPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || requestPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        if (!hasExtension)
        {
            var encodedPathAndQuery = Request.GetEncodedPathAndQuery();
            var queryStart = encodedPathAndQuery.IndexOf('?');
            var encodedPath = queryStart >= 0
                ? encodedPathAndQuery[..queryStart]
                : encodedPathAndQuery;
            var query = queryStart >= 0 ? encodedPathAndQuery[queryStart..] : string.Empty;

            return Redirect(encodedPath + Path.GetExtension(meme.ImageFileName) + query);
        }

        var contentRoot = HttpContext
            .RequestServices.GetRequiredService<IHostEnvironment>()
            .ContentRootPath;
        var renderParameters = new RenderParameters
        {
            FullImagePath = Path.Combine(
                contentRoot,
                meme.ImagePath.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar)
            ),
            DebugMode = memeRequest.IsDebugMode,
            FullWatermarkImageFilePath = ResolveWatermarkPath(contentRoot),
            WatermarkImageHeight = 25,
            WatermarkImageWidth = 25,
            WatermarkText = "upboat.me",
            WatermarkFont = "Arial",
            WatermarkFontStyle = MemeFontStyle.Regular,
            WatermarkFontSize = 9,
            WatermarkStroke = MemeColors.FromName("Black"),
            WatermarkFill = MemeColors.FromName("White"),
            WatermarkStrokeWidth = 1,
            PrivateFontFiles = MemeConfig.PrivateFontFiles,
            Lines = meme
                .Lines.Select(l => new LineParameters
                {
                    Bounds = l.Bounds,
                    DoForceTextToAllCaps = l.DoForceTextToAllCaps,
                    Fill = l.Fill,
                    Font = l.Font,
                    FontSize = l.FontSize,
                    FontStyle = l.FontStyle,
                    HeightPercent = l.HeightPercent,
                    Stroke = l.Stroke,
                    StrokeWidth = l.StrokeWidth,
                    TextAlignment = l.TextAlignment,
                    HugBottom = l.HugBottom,
                })
                .ToList(),
        };

        for (var x = 0; x < renderParameters.Lines.Count; x++)
        {
            if (x < memeRequest.Lines.Count)
            {
                renderParameters.Lines[x].Text = memeRequest
                    .Lines[x]
                    .SanitizeMemeText(renderParameters.Lines[x].DoForceTextToAllCaps);
            }
        }

        var renderer = new Renderer();
        var bytes = renderer.Render(renderParameters);
        return File(bytes, meme.ImageType == "image/jpg" ? "image/jpeg" : meme.ImageType);
    }

    public IActionResult Debug(string top, string bottom)
    {
        var viewModel = new MemeDebugViewModel
        {
            DebugImages = GlobalMemeConfiguration
                .Memes.GetMemeNames()
                .Select(m => $"/{m}/{top}/{bottom}?debugMode=true")
                .ToList(),
        };

        return View(viewModel);
    }

    [OutputCache(Duration = 60)]
    public IActionResult List(string query)
    {
        var list = GlobalMemeConfiguration.Memes.GetMemes();
        if (string.IsNullOrEmpty(query))
        {
            return View(list);
        }

        var filteredList = list.AsQueryable();
        var keywords = query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var keyword in keywords)
        {
            var k = keyword.Trim();

            if (k.StartsWith("-"))
            {
                k = k.TrimStart('-');
                if (k == string.Empty)
                {
                    continue;
                }

                filteredList = filteredList.Where(m =>
                    m.Description.IndexOf(k, StringComparison.OrdinalIgnoreCase) == -1
                    && m.Aliases.All(a => a.IndexOf(k, StringComparison.OrdinalIgnoreCase) == -1)
                );
            }
            else
            {
                filteredList = filteredList.Where(m =>
                    m.Description.IndexOf(k, StringComparison.OrdinalIgnoreCase) != -1
                    || m.Aliases.Any(a => a.IndexOf(k, StringComparison.OrdinalIgnoreCase) != -1)
                );
            }
        }

        return View(filteredList.ToList());
    }

    [OutputCache(Duration = 60)]
    public IActionResult Builder()
    {
        var url = Request.Path + Request.QueryString;
        var memeRequest = MemeRequest.FromUrl(url, Request.PathBase.Value ?? "/");
        var meme = MemeUtilities.FindMeme(GlobalMemeConfiguration.Memes, memeRequest.Name);

        int memeLineCount;
        if (meme == null)
        {
            memeRequest.Name = "ihyk";
            memeRequest.Lines = new List<string>
            {
                "I'll have you know I tried other meme generators",
                "and only wasted hours and hours of my life",
            };
            memeLineCount = GlobalMemeConfiguration.Memes[memeRequest.Name].Lines.Count;
        }
        else
        {
            memeLineCount = meme.Lines.Count;
        }

        var lines = Enumerable
            .Range(0, memeLineCount)
            .Select(
                (item, index) =>
                    memeRequest.Lines.Count > index ? memeRequest.Lines[index] : string.Empty
            )
            .ToList();

        var viewModel = new BuilderViewModel
        {
            Memes = GlobalMemeConfiguration.Memes.GetMemes(),
            SelectedMeme = memeRequest.Name,
            Lines = lines,
            ApplicationPath = Request.PathBase.Value ?? "/",
        };

        return View(viewModel);
    }
}
