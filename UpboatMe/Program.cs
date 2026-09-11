using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using UpboatMe.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddOutputCache();

var app = builder.Build();

GlobalMemeConfiguration.Initialize(app.Environment.ContentRootPath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Index");
}

app.Use(
    async (context, next) =>
    {
        context.Response.Headers["Server"] = "Memeverse/1.0";
        context.Response.Headers.Remove("X-Powered-By");
        await next();
    }
);

void MapStaticDirectory(string physicalDirectory, string requestPath)
{
    if (!Directory.Exists(physicalDirectory))
    {
        return;
    }

    app.UseStaticFiles(
        new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(physicalDirectory),
            RequestPath = new PathString(requestPath),
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public,max-age=31536000";
            },
        }
    );
}

MapStaticDirectory(Path.Combine(app.Environment.ContentRootPath, "Content"), "/Content");
MapStaticDirectory(Path.Combine(app.Environment.ContentRootPath, "Scripts"), "/Scripts");
MapStaticDirectory(Path.Combine(app.Environment.ContentRootPath, "Images"), "/Images");
MapStaticDirectory(Path.Combine(app.Environment.ContentRootPath, "Fonts"), "/Fonts");

app.MapGet(
    "/favicon.ico",
    async context =>
    {
        var iconPath = Path.Combine(app.Environment.ContentRootPath, "favicon.ico");
        if (File.Exists(iconPath))
        {
            await context.Response.SendFileAsync(iconPath);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
);

app.MapGet(
    "/robots.txt",
    async context =>
    {
        var robotsPath = Path.Combine(app.Environment.ContentRootPath, "robots.txt");
        if (File.Exists(robotsPath))
        {
            await context.Response.SendFileAsync(robotsPath);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
);

app.UseRouting();
app.UseOutputCache();

app.MapControllers();

app.MapControllerRoute(
    name: "debug",
    pattern: "Debug/{top?}/{bottom?}",
    defaults: new { controller = "Meme", action = "Debug" }
);

app.MapControllerRoute(
    name: "meme-actions",
    pattern: "{action}/{*url}",
    defaults: new { controller = "Meme" },
    constraints: new { action = "Builder|Debug|List" }
);

app.MapControllerRoute(
    name: "home-pages",
    pattern: "{action}",
    defaults: new { controller = "Home" },
    constraints: new { action = "Index|HowTo|Terms|Privacy|Pricing" }
);

app.MapControllerRoute(
    name: "meme-catchall",
    pattern: "{*url}",
    defaults: new { controller = "Meme", action = "Make" },
    constraints: new
    {
        url = "^(?!api/|content/|scripts/|images/|fonts/|favicon\\.ico|robots\\.txt).*",
    }
);

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
