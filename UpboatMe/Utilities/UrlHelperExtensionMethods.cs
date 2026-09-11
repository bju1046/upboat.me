using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UpboatMe.Utilities
{
    public static class UrlHelperExtensionMethods
    {
        public static string AbsoluteAction(this IUrlHelper url, string path)
        {
            var context = url.ActionContext.HttpContext;
            var request = context.Request;

            var protoHeaderValue = request.Headers["X-FORWARDED-PROTO"].ToString();
            var visitorHeaderValue = request.Headers["CF-Visitor"].ToString();

            var isSsl =
                request.IsHttps
                || string.Equals(protoHeaderValue, "https", StringComparison.OrdinalIgnoreCase)
                || visitorHeaderValue.IndexOf(
                    "\"scheme\":\"https\"",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            var absoluteAction = string.Format(
                "{0}://{1}{2}",
                isSsl ? "https" : "http",
                request.Host,
                path
            );

            return absoluteAction;
        }

        public static string VersionedContent(this IUrlHelper url, string path)
        {
            var env =
                url.ActionContext.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            var relativePath = path.TrimStart('~', '/').Replace('/', Path.DirectorySeparatorChar);
            var filepath = Path.Combine(env.ContentRootPath, relativePath);
            var lastWriteTime = File.GetLastWriteTimeUtc(filepath);
            var cacheBuster = lastWriteTime.Ticks;

            return string.Format("{0}?v={1}", url.Content(path), cacheBuster);
        }
    }
}
