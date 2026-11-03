<%@ Page Title="Ürün" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ProductDetail.aspx.cs" Inherits="TrendyolApp.ProductDetail" %>
<%@ Import Namespace="TrendyolApp" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <p><a runat="server" href="~/">&larr; Tüm ürünler</a></p>

    <% if (Product == null) { %>
        <div class="alert alert-warning">Ürün bulunamadı.</div>
    <% } else { %>
        <div class="row detail">
            <div class="col-sm-4">
                <% if (Product.ImageUrl != null) { %><img class="detail-image" src="<%: Product.ImageUrl %>" alt="" /><% } %>
            </div>
            <div class="col-sm-8">
                <h2><%: Product.Name ?? Product.Url %></h2>
                <p>
                    <a class="btn btn-warning btn-sm" href="<%: Product.Url %>" target="_blank" rel="noopener">Trendyol'da aç</a>
                    <% if (!Product.InStock) { %><span class="label label-default">Stokta yok</span><% } %>
                    <% if (Product.TargetReached) { %><span class="label label-success">Hedef fiyatta</span><% } %>
                    <% if (Product.IsAtLowest) { %><span class="label label-warning">Şu an en düşük fiyatında</span><% } %>
                </p>

                <div class="row stats">
                    <div class="col-xs-6 col-md-3"><div class="stat"><span class="stat-value"><%= _Default.Money(Product.CurrentPrice) %></span><span class="stat-label">Şu anki fiyat</span></div></div>
                    <div class="col-xs-6 col-md-3"><div class="stat stat-lowest"><span class="stat-value"><%= _Default.Money(Product.LowestPrice) %></span><span class="stat-label">En düşük<%= Product.LowestPriceDate.HasValue ? " (" + Product.LowestPriceDate.Value.ToString("dd.MM.yyyy") + ")" : "" %></span></div></div>
                    <div class="col-xs-6 col-md-3"><div class="stat"><span class="stat-value"><%= _Default.Money(Product.HighestPrice) %></span><span class="stat-label">En yüksek</span></div></div>
                    <div class="col-xs-6 col-md-3"><div class="stat stat-target"><span class="stat-value"><%= _Default.Money(Product.TargetPrice) %></span><span class="stat-label">Hedef fiyat</span></div></div>
                </div>

                <% if (Product.BasketPrice.HasValue) { %>
                    <p><span class="basket-tag">Sepette</span> Kupon/sepet indirimiyle ödenecek fiyat <strong><%= _Default.Money(Product.BasketPrice) %></strong>, sayfada görünen fiyat <%= _Default.Money(Product.ShownPrice) %>. Tüm hesaplamalarda sepet fiyatı kullanılır.</p>
                <% } %>
                <p>
                    Önceki 30 günün en düşük fiyatı: <strong><%= Product.Lowest30Days.HasValue ? _Default.Money(Product.Lowest30Days) : "henüz yeterli veri yok" %></strong>
                    <% if (Product.ListPrice.HasValue) { %> · Sayfada üstü çizili fiyat: <s><%= _Default.Money(Product.ListPrice) %></s><% } %>
                </p>
                <%= _Default.DiscountNote(Product) %>

                <p class="text-muted small">
                    Takibe alındı: <%= Product.AddedAt.ToString("dd.MM.yyyy HH:mm") %> ·
                    Son kontrol: <%= Product.LastChecked.HasValue ? Product.LastChecked.Value.ToString("dd.MM.yyyy HH:mm") : "—" %>
                    <% if (Product.LastError != null) { %><br /><span class="text-danger"><%: Product.LastError %></span><% } %>
                </p>
            </div>
        </div>

        <h3>Fiyat geçmişi</h3>
        <% if (Product.History.Count == 0) { %>
            <p class="text-muted">Henüz fiyat bilgisi yok.</p>
        <% } else { %>
            <div class="chart-box"><canvas id="priceChart"></canvas></div>

            <table class="table table-condensed table-striped history-table">
                <thead><tr><th>Tarih</th><th>Ödenecek</th><th>Sayfa fiyatı</th><th>Üstü çizili</th></tr></thead>
                <tbody>
                    <% foreach (var point in Product.History.AsEnumerable().Reverse()) { %>
                        <tr class="<%= point.Effective == Product.LowestPrice ? "success" : "" %>">
                            <td><%= point.Date.ToString("dd.MM.yyyy HH:mm") %></td>
                            <td><strong><%= _Default.Money(point.Effective) %></strong></td>
                            <td><%= _Default.Money(point.Price) %></td>
                            <td><%= point.ListPrice.HasValue ? _Default.Money(point.ListPrice) : "" %></td>
                        </tr>
                    <% } %>
                </tbody>
            </table>

            <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
            <script>
                (function () {
                    var data = <%= HistoryJson %>;
                    var target = <%= Product.TargetPrice.HasValue ? Product.TargetPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null" %>;
                    var tl = function (v) { return v.toLocaleString('tr-TR') + ' TL'; };
                    var datasets = [{
                        label: 'Ödenecek fiyat',
                        data: data.map(function (p) { return p.price; }),
                        borderColor: '#f27a1a',
                        backgroundColor: 'rgba(242,122,26,0.12)',
                        fill: true,
                        stepped: true,
                        pointRadius: 3
                    }];
                    if (target !== null) {
                        datasets.push({
                            label: 'Hedef',
                            data: data.map(function () { return target; }),
                            borderColor: '#2e9e5b',
                            borderDash: [6, 4],
                            pointRadius: 0
                        });
                    }
                    new Chart(document.getElementById('priceChart'), {
                        type: 'line',
                        data: { labels: data.map(function (p) { return p.date; }), datasets: datasets },
                        options: {
                            maintainAspectRatio: false,
                            interaction: { mode: 'index', intersect: false },
                            scales: { y: { ticks: { callback: tl } } },
                            plugins: { tooltip: { callbacks: { label: function (c) { return c.dataset.label + ': ' + tl(c.parsed.y); } } } }
                        }
                    });
                })();
            </script>
        <% } %>
    <% } %>
</asp:Content>
