using System;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using Newtonsoft.Json;
using TrendyolApp.Models;
using TrendyolApp.Services;

namespace TrendyolApp
{
    public partial class ProductDetail : Page
    {
        protected Product Product { get; private set; }

        protected string HistoryJson => JsonConvert.SerializeObject(Product.History.Select(h => new
        {
            date = h.Date.ToString("dd.MM.yy HH:mm", CultureInfo.InvariantCulture),
            price = h.Effective,
            shown = h.BasketPrice.HasValue ? h.Price : (decimal?)null
        }));

        protected void Page_Load(object sender, EventArgs e)
        {
            Product = ProductStore.Get(Request.QueryString["id"]);
            if (Product?.Name != null) Title = Product.Name;
        }
    }
}
