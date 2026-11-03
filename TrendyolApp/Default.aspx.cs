using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using TrendyolApp.Models;
using TrendyolApp.Services;

namespace TrendyolApp
{
    public partial class _Default : Page
    {
        protected static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        protected static readonly Dictionary<string, string> Filters = new Dictionary<string, string>
        {
            { "all", "Tümü" },
            { "lowest", "En düşük fiyatında" },
            { "dropped", "Fiyatı düşenler" },
            { "target", "Hedefe ulaşanlar" },
            { "basket", "Sepette indirimli" },
            { "realdeal", "Gerçek indirim" },
            { "fake", "Sahte indirim" },
            { "outofstock", "Stokta yok" }
        };

        private const int PageSize = 48;

        protected static readonly Dictionary<string, string> Sorts = new Dictionary<string, string>
        {
            { "discount", "En yüksek fiyatına göre en ucuz" },
            { "most-discounted", "En çok indirime giren" },
            { "biggest-drop", "En büyük indirim (%)" },
            { "price", "Fiyat (artan)" },
            { "price-desc", "Fiyat (azalan)" },
            { "name", "İsim" },
            { "added", "Son eklenen" }
        };

        // Bookmarklet run on trendyol.com/hesabim/favoriler: collects every product link and posts it to Import.ashx
        // in the same tab (a new tab would be caught by the popup blocker). A status box shows progress, and the
        // links are copied to the clipboard so they can be pasted manually if the app can't be reached.
        protected string Bookmarklet =>
            "javascript:(function(){" +
            "var b=document.createElement('div');b.style.cssText='position:fixed;z-index:2147483647;top:20px;right:20px;" +
            "max-width:360px;background:#fff;color:#333;border:3px solid #f27a1a;border-radius:8px;padding:14px 16px;" +
            "font:15px/1.4 sans-serif;box-shadow:0 6px 24px rgba(0,0,0,.3)';document.body.appendChild(b);" +
            "var say=function(x){b.textContent=x;};var s={},n=0;" +
            // Other pages (home page, campaigns...) are full of product links; only collect from the favorites page.
            "if(!/favori/i.test(location.href)){say('Bu buton yalnızca Trendyol favoriler sayfasında çalışır. " +
            "Önce trendyol.com/hesabim/favoriler sayfasını açın.');setTimeout(function(){b.remove();},8000);return;}" +
            "document.querySelectorAll('a[href*=\\'-p-\\']').forEach(function(a){var m=a.href.match(/^[^?#]*-p-\\d+/);" +
            "if(m&&!s[m[0]]){s[m[0]]=1;n++;}});" +
            "if(!n){say('Bu sayfada ürün linki bulunamadı. Trendyol favoriler sayfasında olduğunuzdan emin olun.');" +
            "setTimeout(function(){b.remove();},8000);return;}" +
            "var t=Object.keys(s).join(' ');try{navigator.clipboard.writeText(t).catch(function(){});}catch(e){}" +
            "say(n+' ürün takip uygulamasına gönderiliyor...');" +
            "var f=document.createElement('form');f.method='POST';f.action='" + ImportUrl + "';" +
            "[['key','" + SettingsStore.Get().ImportKey + "'],['urls',t]].forEach(function(p){var i=document.createElement('input');" +
            "i.type='hidden';i.name=p[0];i.value=p[1];f.appendChild(i);});document.body.appendChild(f);f.submit();" +
            "setTimeout(function(){say('Uygulama açılamadı (" + ImportUrl + "). '+n+' ürün linki panoya kopyalandı; " +
            "uygulamadaki kutuya yapıştırıp Takibe Al deyin.');},6000);" +
            "})();";

        private string ImportUrl => Request.Url.GetLeftPart(UriPartial.Authority) + ResolveUrl("~/Import.ashx");

        protected string Filter => Filters.ContainsKey(Request.QueryString["filter"] ?? "") ? Request.QueryString["filter"] : "all";
        protected string Sort => Sorts.ContainsKey(Request.QueryString["sort"] ?? "") ? Request.QueryString["sort"] : "discount";
        /// <summary>Selected category ("" = all, "-" = products without a category).</summary>
        protected string CategoryFilter => (Request.QueryString["cat"] ?? "").Trim();
        protected string Search => (Request.QueryString["q"] ?? "").Trim();
        protected const string NoCategory = "-";

        /// <summary>Every category in use with its product count, user-defined ones first.</summary>
        protected List<CategoryCount> Categories { get; private set; }
        protected List<string> CategoryNames => Categories.Where(c => c.Name != NoCategory).Select(c => c.Name).ToList();
        protected List<Product> TopDiscounted { get; private set; }
        protected int ListedCount { get; private set; }

        protected class CategoryCount
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public bool IsCustom { get; set; }
        }

        protected string IntervalText => PriceTracker.DescribeInterval(PriceTracker.CheckInterval);
        protected int PageNumber => int.TryParse(Request.QueryString["page"], out var page) && page > 0 ? page : 1;
        protected int PageCount { get; private set; }
        protected bool AutoReload { get; private set; }
        protected bool ShowBatchesOpen { get; private set; }
        protected SummaryStats Stats { get; private set; }
        protected Dictionary<string, int> FilterCounts { get; private set; }

        // A full pass checks one product every ~4.5 seconds (request + polite pause).
        protected string RemainingText
        {
            get
            {
                var minutes = (int)Math.Ceiling(Stats.Pending * 4.5 / 60);
                return minutes < 60 ? minutes + " dakika" : (minutes / 60) + " saat " + (minutes % 60) + " dakika";
            }
        }

        protected class SummaryStats
        {
            public int Total, AtLowest, Dropped, TargetReached, Pending;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Summary passed back by Import.ashx after a bookmarklet import.
            if (!IsPostBack && int.TryParse(Request.QueryString["found"], out var found))
            {
                var result = new ImportResult
                {
                    Found = found,
                    Added = ReadInt("added"),
                    AlreadyTracked = ReadInt("existing"),
                    Failed = ReadInt("failed")
                };
                ShowMessage(result.Added > 0 ? "success" : "info", "Favoriler aktarıldı: " + ProductImport.Describe(result));
            }
        }

        private int ReadInt(string key) => int.TryParse(Request.QueryString[key], out var value) ? value : 0;

        protected void Page_PreRender(object sender, EventArgs e)
        {
            var all = ProductStore.GetAll();
            Stats = new SummaryStats
            {
                Total = all.Count,
                AtLowest = all.Count(p => p.IsAtLowest),
                Dropped = all.Count(p => p.PriceDropped),
                TargetReached = all.Count(p => p.TargetReached),
                Pending = all.Count(p => p.LastChecked == null)
            };
            AutoReload = PriceTracker.IsRunning || Stats.Pending > 0;
            var comparer = StringComparer.Create(Tr, true);
            Categories = all
                .GroupBy(p => p.Category ?? NoCategory, comparer)
                .Select(g => new CategoryCount { Name = g.Key, Count = g.Count(), IsCustom = g.Any(p => !string.IsNullOrWhiteSpace(p.CustomCategory)) })
                .OrderBy(c => c.Name == NoCategory)
                .ThenByDescending(c => c.IsCustom)
                .ThenBy(c => c.Name, comparer)
                .ToList();

            // Filter buttons count within the selected category and search.
            var scoped = ApplyScope(all).ToList();
            FilterCounts = Filters.Keys.ToDictionary(key => key, key => ApplyFilter(scoped, key).Count());

            TopDiscounted = scoped.Where(p => p.DiscountCount > 0)
                .OrderByDescending(p => p.DiscountCount)
                .ThenByDescending(p => p.BiggestDropPercent ?? 0)
                .Take(5)
                .ToList();

            var products = ApplySort(ApplyFilter(scoped, Filter)).ToList();
            ListedCount = products.Count;
            PageCount = Math.Max(1, (products.Count + PageSize - 1) / PageSize);
            rptProducts.DataSource = products.Skip((Math.Min(PageNumber, PageCount) - 1) * PageSize).Take(PageSize);
            rptProducts.DataBind();

            var batches = all
                .GroupBy(p => p.AddedAt.ToString("yyyyMMddHHmm"))
                .Select(g => new ImportBatch
                {
                    Key = g.Key,
                    Time = g.Min(p => p.AddedAt),
                    Count = g.Count(),
                    Sample = string.Join(", ", g.Where(p => p.Name != null).Take(3).Select(p => p.Name.Length > 40 ? p.Name.Substring(0, 40) + "…" : p.Name))
                })
                .OrderByDescending(b => b.Time)
                .ToList();
            pnlBatches.Visible = batches.Count > 0;
            ShowBatchesOpen = batches.Any(b => b.Count > 200);
            rptBatches.DataSource = batches;
            rptBatches.DataBind();

            pnlEmpty.Visible = products.Count == 0;
            litEmpty.Text = all.Count == 0
                ? "Henüz takip ettiğiniz ürün yok. Yukarıdan Trendyol ürün linki ekleyin veya favorilerinizi toplu olarak aktarın."
                : "Bu filtreye uyan ürün yok.";
        }

        /// <summary>Applies the category and name search from the query string.</summary>
        private IEnumerable<Product> ApplyScope(IEnumerable<Product> products)
        {
            var category = CategoryFilter;
            if (category == NoCategory)
                products = products.Where(p => p.Category == null);
            else if (category != "")
                products = products.Where(p => p.Category != null && string.Compare(p.Category, category, Tr, CompareOptions.IgnoreCase) == 0);

            var words = Search.ToLower(Tr).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
                products = products.Where(p =>
                {
                    var text = ((p.Name ?? "") + " " + (p.TrendyolCategory ?? "") + " " + (p.CustomCategory ?? "")).ToLower(Tr);
                    return words.All(w => text.Contains(w));
                });
            return products;
        }

        /// <summary>Gives every product in the current list (all pages) the typed category.</summary>
        protected void btnBulkCategory_Click(object sender, EventArgs e)
        {
            var category = CleanCategory(txtBulkCategory.Text);
            var ids = new HashSet<string>(ApplyFilter(ApplyScope(ProductStore.GetAll()), Filter).Select(p => p.Id));
            if (ids.Count == 0)
            {
                ShowMessage("warning", "Listede ürün yok.");
                return;
            }
            ProductStore.UpdateAll(p => { if (ids.Contains(p.Id)) p.CustomCategory = category; });
            txtBulkCategory.Text = "";
            ShowMessage("success", category == null
                ? ids.Count + " ürünün kendi kategorisi kaldırıldı (Trendyol kategorisi kullanılacak)."
                : ids.Count + " ürün \"" + category + "\" kategorisine alındı.");
        }

        private static string CleanCategory(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length > 50) text = text.Substring(0, 50).Trim();
            return text.Length == 0 || text == NoCategory ? null : text;
        }

        protected void btnAdd_Click(object sender, EventArgs e)
        {
            var result = ProductImport.Import(txtUrls.Text);
            if (result.Found > 0) txtUrls.Text = "";
            ShowMessage(result.Found == 0 ? "warning" : result.Added > 0 ? "success" : "info", ProductImport.Describe(result));
        }

        protected void btnRefreshAll_Click(object sender, EventArgs e)
        {
            var started = PriceTracker.QueueRefresh(ProductStore.GetAll().Select(p => p.Id));
            ShowMessage("info", started
                ? "Tüm fiyatlar arka planda güncelleniyor. Sayfa kendiliğinden yenilenecek."
                : "Zaten bir güncelleme devam ediyor.");
        }

        protected void rptProducts_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            var id = (string)e.CommandArgument;
            switch (e.CommandName)
            {
                case "refresh":
                    var error = PriceTracker.RefreshNow(id);
                    if (error == null) ShowMessage("success", "Fiyat güncellendi.");
                    else ShowMessage("danger", "Fiyat alınamadı: " + error);
                    break;

                case "delete":
                    ProductStore.Remove(id);
                    ShowMessage("info", "Ürün takipten çıkarıldı.");
                    break;

                case "category":
                    var category = CleanCategory(((TextBox)e.Item.FindControl("txtCategory")).Text);
                    ProductStore.Update(id, p => p.CustomCategory = category);
                    ShowMessage("success", category == null ? "Kendi kategorisi kaldırıldı." : "Kategori \"" + category + "\" olarak kaydedildi.");
                    break;

                case "target":
                    var text = ((TextBox)e.Item.FindControl("txtTarget")).Text.Trim().Replace("TL", "").Trim();
                    if (text == "")
                    {
                        ProductStore.Update(id, p => p.TargetPrice = null);
                        ShowMessage("info", "Hedef fiyat kaldırıldı.");
                    }
                    else if (decimal.TryParse(text, NumberStyles.Number, Tr, out var target) && target > 0)
                    {
                        ProductStore.Update(id, p => p.TargetPrice = target);
                        ShowMessage("success", "Hedef fiyat " + Money(target) + " olarak kaydedildi.");
                    }
                    else
                    {
                        ShowMessage("warning", "Hedef fiyat anlaşılamadı. Örnek: 1499,90");
                    }
                    break;
            }
        }

        protected void rptBatches_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            if (e.CommandName != "deleteBatch") return;
            var key = (string)e.CommandArgument;
            var removed = ProductStore.RemoveWhere(p => p.AddedAt.ToString("yyyyMMddHHmm") == key);
            ShowMessage("info", removed + " ürün takipten çıkarıldı.");
        }

        private static IEnumerable<Product> ApplyFilter(IEnumerable<Product> products, string filter)
        {
            switch (filter)
            {
                case "lowest": return products.Where(p => p.IsAtLowest);
                case "dropped": return products.Where(p => p.PriceDropped);
                case "target": return products.Where(p => p.TargetReached);
                case "basket": return products.Where(p => p.BasketPrice.HasValue);
                case "realdeal": return products.Where(p => p.RealDiscountPercent.HasValue);
                case "fake": return products.Where(p => p.FakeDiscount);
                case "outofstock": return products.Where(p => !p.InStock);
                default: return products;
            }
        }

        private IEnumerable<Product> ApplySort(IEnumerable<Product> products)
        {
            switch (Sort)
            {
                case "price": return products.OrderBy(p => p.CurrentPrice ?? decimal.MaxValue);
                case "price-desc": return products.OrderByDescending(p => p.CurrentPrice ?? 0);
                case "name": return products.OrderBy(p => p.Name, StringComparer.Create(Tr, true));
                case "added": return products.OrderByDescending(p => p.AddedAt);
                case "most-discounted": return products.OrderByDescending(p => p.DiscountCount).ThenByDescending(p => p.BiggestDropPercent ?? 0);
                case "biggest-drop": return products.OrderByDescending(p => p.BiggestDropPercent ?? 0).ThenByDescending(p => p.DiscountCount);
                default: return products.OrderBy(p => p.ChangeFromHighestPercent ?? 0).ThenBy(p => p.Name);
            }
        }

        private void ShowMessage(string type, string text)
        {
            litMessage.Text = "<div class=\"alert alert-" + type + " alert-dismissible\">" +
                              "<button type=\"button\" class=\"close\" data-dismiss=\"alert\">&times;</button>" +
                              HttpUtility.HtmlEncode(text) + "</div>";
        }

        /// <summary>List link that keeps the selected category and search unless <paramref name="category"/> is given.</summary>
        protected string ListUrl(string filter, string sort, int page = 1, string category = null) =>
            "?filter=" + filter + "&amp;sort=" + sort +
            ((category ?? CategoryFilter) != "" ? "&amp;cat=" + HttpUtility.UrlEncode(category ?? CategoryFilter) : "") +
            (Search != "" ? "&amp;q=" + HttpUtility.UrlEncode(Search) : "") +
            (page > 1 ? "&amp;page=" + page : "");

        protected static string DiscountHistory(Product product)
        {
            if (product.DiscountCount == 0) return "";
            return "<div class=\"discount-count\" title=\"Takip başladığından beri fiyatın kaç kez düştüğü\">↓ " + product.DiscountCount +
                   " kez indirime girdi" + (product.BiggestDropPercent.HasValue ? ", en büyük %" + product.BiggestDropPercent.Value.ToString("0.#", Tr) : "") + "</div>";
        }

        /// <summary>Explains whether the advertised discount is real, based on our own 30-day price history.</summary>
        public static string DiscountNote(Product product)
        {
            if (product.FakeDiscount)
                return "<div class=\"discount-note fake\" title=\"Sayfada indirim gösteriliyor ama ürün önceki 30 günde bu fiyata veya daha ucuza satıldı.\">" +
                       "⚠ Sahte indirim: 30 gün içinde " + Money(product.Lowest30Days) + "</div>";
            if (product.RealDiscountPercent.HasValue)
                return "<div class=\"discount-note real\" title=\"Önceki 30 günün en düşük fiyatına göre.\">" +
                       "✓ Gerçek indirim: %" + product.RealDiscountPercent.Value.ToString("0.#", Tr) + "</div>";
            return "";
        }

        protected string DetailUrl(Product product) => ResolveUrl("~/ProductDetail?id=" + HttpUtility.UrlEncode(product.Id));

        public static string Money(decimal? value) => Format.Money(value);

        protected static string Day(DateTime? date) => date.HasValue ? date.Value.ToString("dd.MM.yy", Tr) : "";

        protected static string Change(Product product)
        {
            if (product.PriceDropped)
                return "<span class=\"change down\">▼ " + Money(product.PreviousPrice - product.CurrentPrice) + "</span>";
            if (product.PriceRaised)
                return "<span class=\"change up\">▲ " + Money(product.CurrentPrice - product.PreviousPrice) + "</span>";
            return "";
        }

        protected static string Badges(Product product)
        {
            var badges = new List<string>();
            if (product.TargetReached) badges.Add("<span class=\"label label-success\">Hedef fiyatta</span>");
            if (product.IsAtLowest) badges.Add("<span class=\"label label-warning\">En düşük fiyat</span>");
            if (product.FakeDiscount) badges.Add("<span class=\"label label-danger\">Sahte indirim</span>");
            if (product.ChangeFromHighestPercent < 0) badges.Add("<span class=\"label label-info\">%" + (-product.ChangeFromHighestPercent.Value).ToString("0.#", Tr) + " ucuz</span>");
            if (!product.InStock) badges.Add("<span class=\"label label-default\">Stokta yok</span>");
            return string.Join(" ", badges);
        }
    }

    /// <summary>Products added by the same import (same minute), so an accidental import can be undone at once.</summary>
    public class ImportBatch
    {
        public string Key { get; set; }
        public DateTime Time { get; set; }
        public int Count { get; set; }
        public string Sample { get; set; }
    }
}
