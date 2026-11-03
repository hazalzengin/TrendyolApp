using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TrendyolApp.Models
{
    public class PricePoint
    {
        public DateTime Date { get; set; }

        /// <summary>Price shown on the product page.</summary>
        public decimal Price { get; set; }

        /// <summary>Crossed-out "before discount" price claimed by the page, if higher than <see cref="Price"/>.</summary>
        public decimal? ListPrice { get; set; }

        /// <summary>Price after coupons/basket discounts, if lower than <see cref="Price"/>.</summary>
        public decimal? BasketPrice { get; set; }

        /// <summary>Price for Trendyol Plus members, if lower than <see cref="Price"/>.</summary>
        public decimal? PlusPrice { get; set; }

        /// <summary>Set from the settings page: whether Trendyol Plus prices apply to the user.</summary>
        public static bool IncludePlusPrice { get; set; }

        /// <summary>The cheapest of the basket and (for members) Plus prices, when one applies.</summary>
        [JsonIgnore]
        public decimal? Discounted
        {
            get
            {
                var plus = IncludePlusPrice ? PlusPrice : null;
                if (BasketPrice == null) return plus;
                if (plus == null) return BasketPrice;
                return Math.Min(BasketPrice.Value, plus.Value);
            }
        }

        /// <summary>What the customer actually pays.</summary>
        [JsonIgnore]
        public decimal Effective => Discounted ?? Price;
    }

    public class Product
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public decimal? TargetPrice { get; set; }
        public bool InStock { get; set; } = true;
        public DateTime AddedAt { get; set; }
        public DateTime? LastChecked { get; set; }
        public string LastError { get; set; }
        public List<PricePoint> History { get; set; } = new List<PricePoint>();

        /// <summary>Category path read from the Trendyol page, e.g. "Ev &amp; Mobilya/Ev/Sofra &amp; Mutfak/Sofra/Kase".</summary>
        public string TrendyolCategory { get; set; }

        /// <summary>The user's own category (e.g. "Çeyizlik"); overrides the Trendyol category when set.</summary>
        public string CustomCategory { get; set; }

        /// <summary>Category used for filtering: the user's own one, else the top level of the Trendyol category.</summary>
        [JsonIgnore]
        public string Category =>
            !string.IsNullOrWhiteSpace(CustomCategory) ? CustomCategory.Trim()
            : string.IsNullOrWhiteSpace(TrendyolCategory) ? null
            : TrendyolCategory.Split('/')[0].Trim();

        /// <summary>Most specific level of the Trendyol category, e.g. "Kase".</summary>
        [JsonIgnore]
        public string TrendyolSubCategory => string.IsNullOrWhiteSpace(TrendyolCategory) ? null : TrendyolCategory.Split('/').Last().Trim();

        /// <summary>How many times the recorded price went down (each drop counts as one discount).</summary>
        [JsonIgnore]
        public int DiscountCount
        {
            get
            {
                var count = 0;
                for (var i = 1; i < History.Count; i++)
                    if (History[i].Effective < History[i - 1].Effective) count++;
                return count;
            }
        }

        /// <summary>The biggest single price drop recorded, as a percentage of the price before it.</summary>
        [JsonIgnore]
        public decimal? BiggestDropPercent
        {
            get
            {
                decimal? best = null;
                for (var i = 1; i < History.Count; i++)
                {
                    var before = History[i - 1].Effective;
                    var after = History[i].Effective;
                    if (before <= 0 || after >= before) continue;
                    var percent = Math.Round((before - after) / before * 100, 1);
                    if (best == null || percent > best) best = percent;
                }
                return best;
            }
        }

        [JsonIgnore]
        public bool HasPrice => History.Count > 0;

        [JsonIgnore]
        public PricePoint Last => HasPrice ? History[History.Count - 1] : null;

        /// <summary>Price actually paid (basket price when there is one). All statistics use this value.</summary>
        [JsonIgnore]
        public decimal? CurrentPrice => Last?.Effective;

        [JsonIgnore]
        public decimal? ShownPrice => Last?.Price;

        [JsonIgnore]
        public decimal? BasketPrice => Last?.Discounted;

        [JsonIgnore]
        public decimal? ListPrice => Last?.ListPrice;

        [JsonIgnore]
        public decimal? LowestPrice => HasPrice ? History.Min(h => h.Effective) : (decimal?)null;

        [JsonIgnore]
        public decimal? HighestPrice => HasPrice ? History.Max(h => h.Effective) : (decimal?)null;

        [JsonIgnore]
        public decimal? AveragePrice => HasPrice ? Math.Round(History.Average(h => h.Effective), 2) : (decimal?)null;

        /// <summary>The most recent date the product was sold at its lowest recorded price.</summary>
        [JsonIgnore]
        public DateTime? LowestPriceDate => HasPrice ? History.Where(h => h.Effective == LowestPrice).Max(h => h.Date) : (DateTime?)null;

        [JsonIgnore]
        public decimal? PreviousPrice
        {
            get
            {
                if (!HasPrice) return null;
                var current = CurrentPrice.Value;
                var previous = History.Take(History.Count - 1).LastOrDefault(h => h.Effective != current);
                return previous?.Effective;
            }
        }

        [JsonIgnore]
        public bool IsAtLowest => HasPrice && History.Select(h => h.Effective).Distinct().Count() > 1 && CurrentPrice <= LowestPrice;

        [JsonIgnore]
        public bool TargetReached => TargetPrice.HasValue && CurrentPrice.HasValue && CurrentPrice <= TargetPrice;

        [JsonIgnore]
        public bool PriceDropped => PreviousPrice.HasValue && CurrentPrice < PreviousPrice;

        [JsonIgnore]
        public bool PriceRaised => PreviousPrice.HasValue && CurrentPrice > PreviousPrice;

        /// <summary>Percentage of the current price relative to the highest recorded price (negative = cheaper).</summary>
        [JsonIgnore]
        public decimal? ChangeFromHighestPercent =>
            HasPrice && HighestPrice > 0 ? Math.Round((CurrentPrice.Value - HighestPrice.Value) / HighestPrice.Value * 100, 1) : (decimal?)null;

        /// <summary>
        /// Lowest price we recorded during the 30 days before the current reading (readings from the last
        /// 12 hours are ignored so a fresh discount is compared with what came before it). Null without enough history.
        /// </summary>
        [JsonIgnore]
        public decimal? Lowest30Days
        {
            get
            {
                if (!HasPrice) return null;
                var from = Last.Date.AddDays(-30);
                var until = Last.Date.AddHours(-12);
                var earlier = History.Where(h => h.Date >= from && h.Date <= until).ToList();
                return earlier.Count == 0 ? (decimal?)null : earlier.Min(h => h.Effective);
            }
        }

        /// <summary>The page advertises a discount, but the product sold at this price or cheaper in the last 30 days.</summary>
        [JsonIgnore]
        public bool FakeDiscount => ListPrice > ShownPrice && Lowest30Days.HasValue && CurrentPrice >= Lowest30Days;

        /// <summary>How much cheaper the product is compared with its lowest price of the previous 30 days.</summary>
        [JsonIgnore]
        public decimal? RealDiscountPercent =>
            Lowest30Days > 0 && CurrentPrice < Lowest30Days
                ? Math.Round((Lowest30Days.Value - CurrentPrice.Value) / Lowest30Days.Value * 100, 1)
                : (decimal?)null;
    }
}
