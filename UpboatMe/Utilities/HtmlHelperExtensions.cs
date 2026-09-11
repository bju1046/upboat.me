using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UpboatMe.Utilities
{
    public static class HtmlHelperExtensions
    {
        public static IHtmlContent TopNavLink(
            this IHtmlHelper helper,
            string text,
            string action,
            string controller,
            object routeValues,
            object htmlAttributes
        )
        {
            var isCurrent =
                string.Equals(
                    helper.ViewContext.RouteData.Values["controller"].ToString(),
                    controller,
                    System.StringComparison.OrdinalIgnoreCase
                )
                && string.Equals(
                    helper.ViewContext.RouteData.Values["action"].ToString(),
                    action,
                    System.StringComparison.OrdinalIgnoreCase
                );

            var htmlAttributesCollection = HtmlHelper.AnonymousObjectToHtmlAttributes(
                htmlAttributes ?? new object()
            );

            if (isCurrent)
            {
                if (htmlAttributesCollection.ContainsKey("class"))
                {
                    htmlAttributesCollection["class"] =
                        htmlAttributesCollection["class"] + " success";
                }
                else
                {
                    htmlAttributesCollection.Add("class", "success");
                }
            }

            var routeValuesDictionary = new RouteValueDictionary(routeValues);
            IDictionary<string, object> attributesDictionary =
                htmlAttributesCollection.ToDictionary(k => k.Key, v => v.Value);

            return helper.ActionLink(
                text,
                action,
                controller,
                routeValuesDictionary,
                attributesDictionary
            );
        }

        public static IHtmlContent ThumbImage(
            this IHtmlHelper helper,
            string imageNameWithoutExtension
        )
        {
            var builder = new TagBuilder("img");
            builder.Attributes["src"] = $"/Images/{imageNameWithoutExtension}.jpg";
            builder.Attributes["alt"] = imageNameWithoutExtension;
            builder.AddCssClass("thumb");
            return builder;
        }

        public static string LastUpdated(this IHtmlHelper helper)
        {
            var env =
                helper.ViewContext.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            var filePath = Path.Combine(env.ContentRootPath, "App_Data", "version.txt");
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }
            return "";
        }
    }
}
