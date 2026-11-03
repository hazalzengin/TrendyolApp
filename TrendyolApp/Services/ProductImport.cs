using System;
using System.Collections.Generic;
using System.Linq;
using TrendyolApp.Models;

namespace TrendyolApp.Services
{
    public class ImportResult
    {
        public int Found { get; set; }
        public int Added { get; set; }
        public int AlreadyTracked { get; set; }
        public int Failed { get; set; }
    }

    /// <summary>Turns pasted text / bookmarklet posts into tracked products.</summary>
    public static class ProductImport
    {
        public static ImportResult Import(string text)
        {
            var urls = TrendyolClient.ExtractUrls(text);
            var result = new ImportResult { Found = urls.Count };

            var products = new List<Product>();
            foreach (var url in urls)
            {
                var id = TrendyolClient.ExtractId(url);
                if (id == null)
                {
                    // Short links (ty.gl/...) only reveal the product id after the page is opened.
                    try { id = TrendyolClient.Fetch(url).Id; }
                    catch { result.Failed++; continue; }
                }
                if (products.All(p => p.Id != id))
                    products.Add(new Product { Id = id, Url = url, AddedAt = DateTime.Now });
            }

            var added = ProductStore.AddRange(products);
            if (added.Count > 0) PriceTracker.QueueRefresh(added);

            result.Added = added.Count;
            result.AlreadyTracked = products.Count - added.Count;
            return result;
        }

        public static string Describe(ImportResult result)
        {
            if (result.Found == 0) return "Geçerli bir Trendyol ürün linki bulunamadı.";
            var message = result.Added + " yeni ürün takibe alındı" + (result.Added > 0 ? ", fiyatlar arka planda alınıyor." : ".");
            if (result.AlreadyTracked > 0) message += " " + result.AlreadyTracked + " ürün zaten takip ediliyordu.";
            if (result.Failed > 0) message += " " + result.Failed + " link açılamadı.";
            return message;
        }
    }
}
