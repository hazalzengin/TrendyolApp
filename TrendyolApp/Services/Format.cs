using System.Globalization;

namespace TrendyolApp.Services
{
    public static class Format
    {
        public static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        public static string Money(decimal? value) => value.HasValue ? value.Value.ToString("#,0.##", Tr) + " TL" : "—";
    }
}
