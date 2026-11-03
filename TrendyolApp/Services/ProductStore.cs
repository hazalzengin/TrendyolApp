using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Hosting;
using Newtonsoft.Json;
using TrendyolApp.Models;

namespace TrendyolApp.Services
{
    /// <summary>
    /// Thread-safe JSON file storage (App_Data/products.json). Readers get copies,
    /// so the background tracker and pages never share mutable objects.
    /// </summary>
    public static class ProductStore
    {
        private static readonly object Sync = new object();
        private static List<Product> _products;

        private static string FilePath => HostingEnvironment.MapPath("~/App_Data/products.json");

        public static List<Product> GetAll()
        {
            lock (Sync)
            {
                return Clone(Load());
            }
        }

        public static Product Get(string id)
        {
            lock (Sync)
            {
                var product = Load().FirstOrDefault(p => p.Id == id);
                return product == null ? null : Clone(new List<Product> { product })[0];
            }
        }

        /// <summary>Adds products that aren't tracked yet and returns the ids that were added.</summary>
        public static List<string> AddRange(IEnumerable<Product> products)
        {
            lock (Sync)
            {
                var list = Load();
                var added = new List<string>();
                foreach (var product in products)
                {
                    if (list.Any(p => p.Id == product.Id)) continue;
                    list.Add(product);
                    added.Add(product.Id);
                }
                if (added.Count > 0) Save();
                return added;
            }
        }

        public static void Update(string id, Action<Product> change)
        {
            lock (Sync)
            {
                var product = Load().FirstOrDefault(p => p.Id == id);
                if (product == null) return;
                change(product);
                Save();
            }
        }

        public static void UpdateAll(Action<Product> change)
        {
            lock (Sync)
            {
                var list = Load();
                if (list.Count == 0) return;
                list.ForEach(change);
                Save();
            }
        }

        public static void Remove(string id) => RemoveWhere(p => p.Id == id);

        /// <summary>Removes every product matching the condition and returns how many were removed.</summary>
        public static int RemoveWhere(Predicate<Product> match)
        {
            lock (Sync)
            {
                var removed = Load().RemoveAll(match);
                if (removed > 0) Save();
                return removed;
            }
        }

        private static List<Product> Load()
        {
            if (_products != null) return _products;
            _products = File.Exists(FilePath)
                ? JsonConvert.DeserializeObject<List<Product>>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new List<Product>()
                : new List<Product>();
            return _products;
        }

        private static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(_products, Formatting.Indented), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null);
            else File.Move(temp, FilePath);
        }

        private static List<Product> Clone(List<Product> products) =>
            JsonConvert.DeserializeObject<List<Product>>(JsonConvert.SerializeObject(products));
    }
}
