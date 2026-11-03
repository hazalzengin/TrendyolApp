using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web.Hosting;
using TrendyolApp.Models;

namespace TrendyolApp.Services
{
    /// <summary>
    /// Periodically re-reads the price of every tracked product while the web app is running.
    /// </summary>
    public static class PriceTracker
    {
        private static Timer _timer;
        private static int _running;
        private static readonly Random Jitter = new Random();

        private static readonly TimeSpan TickPeriod = TimeSpan.FromMinutes(5);

        public static TimeSpan CheckInterval => TimeSpan.FromMinutes(Math.Max(15, SettingsStore.Get().CheckIntervalMinutes));

        public static bool IsRunning => _running == 1;

        /// <summary>Bumped when stored data must be fixed up once at startup.</summary>
        private const int CurrentDataVersion = 2;

        public static void Start()
        {
            var settings = SettingsStore.Get();
            PricePoint.IncludePlusPrice = settings.TrendyolPlusMember;

            if (settings.DataVersion < CurrentDataVersion)
            {
                // Version 1 read the first price on the page, which on multi-seller pages often belonged to
                // another seller or a recommended product. Drop those readings and re-check everything.
                ProductStore.UpdateAll(p =>
                {
                    p.History.Clear();
                    p.LastChecked = null;
                    p.LastError = null;
                });
                settings.DataVersion = CurrentDataVersion;
                SettingsStore.Save(settings);
            }

            // Wake up every few minutes and refresh only the products whose last check is older than the interval.
            _timer = new Timer(_ => CheckDue(), null, TimeSpan.FromSeconds(30), TickPeriod);
        }

        public static void Stop() => _timer?.Dispose();

        public static void CheckDue()
        {
            // Products checked during one run finish a few minutes apart; the tolerance keeps them in the same batch.
            var dueBefore = DateTime.Now - CheckInterval + TickPeriod;
            Run(ProductStore.GetAll()
                .Where(p => p.LastChecked == null || p.LastChecked.Value <= dueBefore)
                .Select(p => p.Id));
        }

        public static string DescribeInterval(TimeSpan interval) =>
            interval.TotalMinutes < 60 ? interval.TotalMinutes + " dakikada" : interval.TotalHours.ToString("0.#", Format.Tr) + " saatte";

        /// <summary>Starts a background refresh; returns false if one is already in progress.</summary>
        public static bool QueueRefresh(IEnumerable<string> ids)
        {
            var list = ids.ToList();
            if (IsRunning) return false;
            HostingEnvironment.QueueBackgroundWorkItem(_ => Run(list));
            return true;
        }

        public static string RefreshNow(string id)
        {
            var product = ProductStore.Get(id);
            return product == null ? "Ürün bulunamadı." : Refresh(product);
        }

        private static void Run(IEnumerable<string> ids)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) return;
            try
            {
                var done = new HashSet<string>();
                foreach (var id in ids.Concat(PendingIds()))
                {
                    if (!done.Add(id)) continue;
                    var product = ProductStore.Get(id);
                    if (product == null) continue;

                    // Be polite to Trendyol: a short random pause between requests.
                    if (done.Count > 1) Thread.Sleep(TimeSpan.FromSeconds(2 + Jitter.NextDouble() * 3));

                    Refresh(product);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        /// <summary>Products added while a run was in progress (never checked yet), evaluated lazily at the end of the run.</summary>
        private static IEnumerable<string> PendingIds()
        {
            foreach (var product in ProductStore.GetAll().Where(p => p.LastChecked == null))
                yield return product.Id;
        }

        private static string Refresh(Product product)
        {
            try
            {
                var snapshot = TrendyolClient.Fetch(product.Url);
                PriceAlert alert = null;
                ProductStore.Update(product.Id, p => alert = Apply(p, snapshot));
                if (alert != null)
                    Notifier.Send(alert.Title, alert.Message, product.Url, snapshot.ImageUrl, alert.Tags);
                return null;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Price check failed for {0}: {1}", product.Id, ex.Message);
                ProductStore.Update(product.Id, p =>
                {
                    p.LastChecked = DateTime.Now;
                    p.LastError = ex.Message;
                });
                return ex.Message;
            }
        }

        private class PriceAlert
        {
            public string Title, Message, Tags;
        }

        /// <summary>Stores the new reading and returns the notification it triggers, if any.</summary>
        private static PriceAlert Apply(Product product, ProductSnapshot snapshot)
        {
            var now = DateTime.Now;
            var wasInStock = product.InStock;
            var name = snapshot.Name ?? product.Name;
            product.Name = name;
            product.ImageUrl = snapshot.ImageUrl ?? product.ImageUrl;
            product.InStock = snapshot.InStock;
            product.TrendyolCategory = snapshot.Category ?? product.TrendyolCategory;
            product.LastChecked = now;
            product.LastError = null;

            // Sold-out pages may have no price; keep the history as it is.
            if (snapshot.Price == null) return null;

            var point = new PricePoint
            {
                Date = now,
                Price = snapshot.Price.Value,
                ListPrice = snapshot.ListPrice,
                BasketPrice = snapshot.BasketPrice,
                PlusPrice = snapshot.PlusPrice
            };
            var alert = BuildAlert(product, name, point.Effective, point.Discounted.HasValue, !wasInStock && snapshot.InStock);

            // Record a point when the price changes, plus one per day so the chart shows how long a price lasted.
            var last = product.Last;
            if (last == null || last.Price != point.Price || last.ListPrice != point.ListPrice ||
                last.BasketPrice != point.BasketPrice || last.PlusPrice != point.PlusPrice || last.Date.Date != now.Date)
            {
                product.History.Add(point);
            }
            return alert;
        }

        private static PriceAlert BuildAlert(Product product, string name, decimal price, bool isBasketPrice, bool backInStock)
        {
            // The very first reading has nothing to compare with.
            if (!product.HasPrice) return null;

            var settings = SettingsStore.Get();
            var previous = product.CurrentPrice.Value;
            var lowest = product.LowestPrice.Value;
            var target = product.TargetPrice;

            var reasons = new List<string>();
            string title = null, tags = null;

            if (settings.NotifyBackInStock && backInStock)
            {
                title = "Stoğa girdi: " + Format.Money(price);
                tags = "package";
                reasons.Add("Ürün tekrar satışta.");
            }
            if (settings.NotifyNewLowest && price < lowest)
            {
                title = title ?? "En düşük fiyat: " + Format.Money(price);
                tags = tags ?? "chart_with_downwards_trend";
                reasons.Add("Şimdiye kadarki en düşük fiyat (önceki en düşük: " + Format.Money(lowest) + ").");
            }
            if (settings.NotifyTarget && target.HasValue && price <= target && previous > target)
            {
                title = title ?? "Hedef fiyata ulaştı: " + Format.Money(price);
                tags = tags ?? "dart";
                reasons.Add("Hedefiniz: " + Format.Money(target) + ".");
            }
            if (settings.NotifyAnyDrop && price < previous && reasons.Count == 0)
            {
                title = "Fiyat düştü: " + Format.Money(price);
                tags = "arrow_down";
            }
            if (title == null) return null;

            var message = name + "\n" + Format.Money(previous) + " → " + Format.Money(price) +
                          (isBasketPrice ? " (sepette)" : "");
            if (reasons.Count > 0) message += "\n" + string.Join("\n", reasons);
            return new PriceAlert { Title = title, Message = message, Tags = tags };
        }
    }
}
