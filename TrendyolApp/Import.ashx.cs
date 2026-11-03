using System;
using System.Web;
using TrendyolApp.Services;

namespace TrendyolApp
{
    /// <summary>
    /// Receives the product links posted by the favorites bookmarklet from trendyol.com
    /// and redirects back to the product list with a summary.
    /// </summary>
    public class ImportHandler : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            var request = context.Request;
            if (request.HttpMethod != "POST" || request.Unvalidated.Form["key"] != SettingsStore.Get().ImportKey)
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.Write("Geçersiz istek. Yer imini uygulamadaki güncel butonla yeniden oluşturun.");
                return;
            }

            var result = ProductImport.Import(request.Unvalidated.Form["urls"]);
            context.Response.Redirect(string.Format("~/?found={0}&added={1}&existing={2}&failed={3}",
                result.Found, result.Added, result.AlreadyTracked, result.Failed), false);
        }
    }
}
