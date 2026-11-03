using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace TrendyolApp.Services
{
    public class ProductSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        /// <summary>Null when the page has no price (usually because the product is sold out).</summary>
        public decimal? Price { get; set; }
        public decimal? ListPrice { get; set; }
        public decimal? BasketPrice { get; set; }
        public decimal? PlusPrice { get; set; }
        public bool InStock { get; set; }
        /// <summary>Trendyol category path, e.g. "Ev &amp; Mobilya/Ev/Sofra &amp; Mutfak/Sofra/Kase".</summary>
        public string Category { get; set; }
    }

    /// <summary>
    /// Reads product data from Trendyol product pages.
    /// Trendyol (Cloudflare) rejects HTTP/1.1 clients, and HttpWebRequest on .NET Framework
    /// cannot speak HTTP/2, so pages are downloaded with the curl.exe that ships with Windows 10+.
    /// </summary>
    public static class TrendyolClient
    {
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

        private static readonly Regex IdRegex = new Regex(@"-p-(\d+)", RegexOptions.Compiled);
        private static readonly Regex UrlRegex = new Regex(@"(https?://)?(www\.|m\.)?(trendyol\.com|ty\.gl)/[^\s""'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DiscountedPriceRegex = new Regex(@"""discountedPrice"":\{""value"":([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex SellingPriceRegex = new Regex(@"""sellingPrice"":\{""value"":([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex OriginalPriceRegex = new Regex(@"""originalPrice"":\{""value"":([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex CouponPriceRegex = new Regex(@"""couponApplicablePrice"":\{""value"":([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex PlusPriceRegex = new Regex(@"""tyPlusCouponApplicablePrice"":\{""value"":([\d.]+)", RegexOptions.Compiled);
        private static readonly Regex LdJsonPriceRegex = new Regex(@"""price""\s*:\s*""([\d.]+)""", RegexOptions.Compiled);
        private static readonly Regex InStockRegex = new Regex(@"""inStock"":(true|false)", RegexOptions.Compiled);
        private static readonly Regex TitleRegex = new Regex(@"og:title""\s+content=""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex ImageRegex = new Regex(@"og:image""\s+content=""([^""]*)""", RegexOptions.Compiled);
        private static readonly Regex CategoryRegex = new Regex(@"""category"":\{""id"":\d+,""name"":""[^""]*"",""hierarchy"":""([^""]+)""", RegexOptions.Compiled);
        private static readonly Regex CanonicalRegex = new Regex(@"(?:canonical""\s+href|og:url""\s+content)=""([^""]*-p-\d+[^""]*)""", RegexOptions.Compiled);

        /// <summary>Finds every Trendyol link in free text (one per line, space separated, pasted lists...).</summary>
        public static List<string> ExtractUrls(string text)
        {
            return UrlRegex.Matches(text ?? "")
                .Cast<Match>()
                .Select(m => NormalizeUrl(m.Value))
                .Where(u => u != null)
                .Distinct()
                .ToList();
        }

        public static string NormalizeUrl(string url)
        {
            url = (url ?? "").Trim();
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

            var host = uri.Host.ToLowerInvariant();
            if (host == "ty.gl") return uri.AbsoluteUri; // short links are resolved when the page is fetched
            if (host != "trendyol.com" && !host.EndsWith(".trendyol.com")) return null;

            return "https://www.trendyol.com" + uri.AbsolutePath;
        }

        public static string ExtractId(string url)
        {
            var match = IdRegex.Match(url ?? "");
            return match.Success ? match.Groups[1].Value : null;
        }

        public static ProductSnapshot Fetch(string url)
        {
            var html = Download(url);
            var snapshot = Parse(html);
            snapshot.Id = ExtractId(url) ?? snapshot.Id;
            if (snapshot.Id == null) throw new InvalidOperationException("Ürün numarası bulunamadı.");
            return snapshot;
        }

        public static ProductSnapshot Parse(string html)
        {
            // The page also lists other sellers and variants; read the offer Trendyol shows by default ("winnerVariant").
            var start = html.IndexOf("\"winnerVariant\"", StringComparison.Ordinal);
            var offer = start >= 0 ? html.Substring(start, Math.Min(5000, html.Length - start)) : html;

            var price = ReadDecimal(DiscountedPriceRegex, offer) ?? ReadDecimal(LdJsonPriceRegex, html);
            var inStockMatch = InStockRegex.Match(offer);
            if (!inStockMatch.Success) inStockMatch = InStockRegex.Match(html);
            var inStock = !inStockMatch.Success || inStockMatch.Groups[1].Value == "true";

            if (price == null && inStock)
                throw new InvalidOperationException("Sayfada fiyat bulunamadı (ürün kaldırılmış veya sayfa yapısı değişmiş olabilir).");

            var listPrice = new[] { ReadDecimal(SellingPriceRegex, offer), ReadDecimal(OriginalPriceRegex, offer) }.Max();
            var basketPrice = ReadDecimal(CouponPriceRegex, offer);
            var plusPrice = ReadDecimal(PlusPriceRegex, offer);
            var canonical = CanonicalRegex.Match(html);

            return new ProductSnapshot
            {
                Id = canonical.Success ? ExtractId(canonical.Groups[1].Value) : null,
                Name = CleanTitle(ReadText(TitleRegex, html)),
                ImageUrl = ReadText(ImageRegex, html),
                Price = price,
                ListPrice = listPrice > price ? listPrice : null,
                BasketPrice = basketPrice < price ? basketPrice : null,
                PlusPrice = plusPrice < (basketPrice ?? price) ? plusPrice : null,
                InStock = inStock,
                Category = ReadCategory(html)
            };
        }

        private static string Download(string url)
        {
            var normalized = NormalizeUrl(url) ?? throw new ArgumentException("Geçersiz Trendyol linki: " + url);

            var curl = ConfigurationManager.AppSettings["CurlPath"];
            if (string.IsNullOrWhiteSpace(curl)) curl = Path.Combine(Environment.SystemDirectory, "curl.exe");

            var startInfo = new ProcessStartInfo
            {
                FileName = curl,
                Arguments = "-s -L --compressed --max-time 30 " +
                            "-A \"" + UserAgent + "\" " +
                            "-H \"Accept-Language: tr-TR,tr;q=0.9\" " +
                            "-w \"\\n%{http_code}\" \"" + normalized + "\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            string output;
            using (var process = Process.Start(startInfo))
            {
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Sayfa indirilemedi (curl çıkış kodu " + process.ExitCode + ").");
            }

            var split = output.LastIndexOf('\n');
            var status = split >= 0 ? output.Substring(split + 1).Trim() : "";
            if (status != "200")
                throw new InvalidOperationException("Trendyol HTTP " + status + " döndürdü.");
            return output.Substring(0, split);
        }

        private static decimal? ReadDecimal(Regex regex, string html)
        {
            var match = regex.Match(html);
            return match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : (decimal?)null;
        }

        private static string ReadText(Regex regex, string html)
        {
            var match = regex.Match(html);
            return match.Success ? HttpUtility.HtmlDecode(match.Groups[1].Value).Trim() : null;
        }

        private static string ReadCategory(string html)
        {
            var match = CategoryRegex.Match(html);
            if (!match.Success) return null;
            // The hierarchy is a JSON string, so "/" is written as /.
            var path = Regex.Unescape(match.Groups[1].Value).Trim();
            return path.Length == 0 ? null : path;
        }

        private static string CleanTitle(string title)
        {
            if (title == null) return null;
            var suffix = title.LastIndexOf(" - Fiyatı", StringComparison.Ordinal);
            return suffix > 0 ? title.Substring(0, suffix).Trim() : title;
        }
    }
}
