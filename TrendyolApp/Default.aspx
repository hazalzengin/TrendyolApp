<%@ Page Title="Favorilerim" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="TrendyolApp._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">

    <asp:Literal ID="litMessage" runat="server" />

    <div class="row stats">
        <div class="col-xs-6 col-md-3"><div class="stat"><span class="stat-value"><%= Stats.Total %></span><span class="stat-label">Takip edilen ürün</span></div></div>
        <div class="col-xs-6 col-md-3"><div class="stat stat-lowest"><span class="stat-value"><%= Stats.AtLowest %></span><span class="stat-label">En düşük fiyatında</span></div></div>
        <div class="col-xs-6 col-md-3"><div class="stat stat-drop"><span class="stat-value"><%= Stats.Dropped %></span><span class="stat-label">Fiyatı düşen</span></div></div>
        <div class="col-xs-6 col-md-3"><div class="stat stat-target"><span class="stat-value"><%= Stats.TargetReached %></span><span class="stat-label">Hedef fiyata ulaşan</span></div></div>
    </div>

    <div class="panel panel-default">
        <div class="panel-heading">
            <strong>Ürün ekle</strong>
            <a class="pull-right" data-toggle="collapse" href="#importHelp">Favorilerimi nasıl toplu eklerim?</a>
        </div>
        <div class="panel-body">
            <div id="importHelp" class="collapse import-help">
                <ol>
                    <li>Yer imini bir kez oluşturun. İki yolu var:
                        <ul>
                            <li><strong>Sürükleyerek:</strong> bu butonu yer imleri çubuğuna sürükleyin (çubuk görünmüyorsa <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>B</kbd>):
                                <a class="btn btn-warning btn-xs bookmarklet" href="<%= Bookmarklet %>" onclick="alert('Bu butonu yer imleri çubuğuna sürükleyin, sonra Trendyol favoriler sayfasında tıklayın.'); return false;">Favorileri Takibe Al</a>
                            </li>
                            <li><strong>Elle (sürükleme olmazsa):</strong>
                                <button type="button" class="btn btn-default btn-xs" id="copyBookmarklet">Yer imi kodunu kopyala</button>
                                <span id="copyResult" class="text-success small"></span>
                                <ol class="manual-steps">
                                    <li><kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>O</kbd> ile Chrome'un <em>Yer İşareti Yöneticisi</em>'ni açın.</li>
                                    <li>Sol taraftan <em>Yer işareti çubuğu</em>'nu seçin, sağ üstteki <strong>⋮</strong> menüsünden <em>Yeni yer işareti ekle</em>'ye tıklayın.</li>
                                    <li><em>Ad</em> kutusuna <code>Favorileri Takibe Al</code> yazın, <em>URL</em> kutusuna kopyaladığınız kodu yapıştırın (<kbd>Ctrl</kbd>+<kbd>V</kbd>) ve <em>Kaydet</em>'e basın.</li>
                                </ol>
                                <textarea id="bookmarkletCode" class="form-control bookmarklet-code" rows="2" readonly><%: Bookmarklet %></textarea>
                            </li>
                        </ul>
                    </li>
                    <li><a href="https://www.trendyol.com/hesabim/favoriler" target="_blank" rel="noopener">Trendyol favoriler sayfanızı</a> açın (kendi hesabınızla giriş yapmış olun) ve tüm ürünler yüklenene kadar sayfayı en alta kaydırın.</li>
                    <li>Yer imleri çubuğundaki <em>Favorileri Takibe Al</em>'a tıklayın. Sağ üstte bir bilgi kutusu çıkar, ardından bu uygulama açılır ve favorilerinizdeki yeni ürünler takibe alınır.</li>
                </ol>
                <p class="text-muted">Favorilerinize yeni ürün ekledikçe aynı butona tekrar tıklamanız yeterli; zaten takip edilen ürünler tekrar eklenmez.
                    Uygulama açılamazsa linkler panoya kopyalanmıştır, aşağıdaki kutuya yapıştırabilirsiniz. Şifrenize ihtiyaç yoktur.</p>
            </div>
            <asp:TextBox ID="txtUrls" runat="server" TextMode="MultiLine" Rows="3" CssClass="form-control url-input"
                placeholder="Trendyol ürün linklerini yapıştırın (her satıra bir link veya favorilerden kopyalanan liste)" />
            <div class="add-actions">
                <asp:Button ID="btnAdd" runat="server" Text="Takibe Al" CssClass="btn btn-primary" OnClick="btnAdd_Click" />
                <asp:Button ID="btnRefreshAll" runat="server" Text="Tüm Fiyatları Güncelle" CssClass="btn btn-default" OnClick="btnRefreshAll_Click" CausesValidation="false" />
                <span class="text-muted small">Fiyatlar uygulama açıkken otomatik olarak her <%= IntervalText %> bir kontrol edilir (<a runat="server" href="~/Settings">değiştir</a>).</span>
            </div>
        </div>
    </div>

    <% if (Stats.Pending > 0) { %>
        <div class="progress-box">
            <div class="small">Fiyatlar alınıyor: <strong><%= Stats.Total - Stats.Pending %> / <%= Stats.Total %></strong> ürün
                (kalan süre yaklaşık <%= RemainingText %>). Sayfa kendiliğinden yenilenir.</div>
            <div class="progress"><div class="progress-bar progress-bar-warning" style="width: <%= (Stats.Total - Stats.Pending) * 100 / Math.Max(1, Stats.Total) %>%"></div></div>
        </div>
    <% } %>

    <asp:Panel ID="pnlBatches" runat="server" CssClass="panel panel-default batches">
        <div class="panel-heading">
            <a data-toggle="collapse" href="#batchList"><strong>Aktarımlar</strong> <span class="text-muted small">(yanlışlıkla eklenen ürünleri toplu silmek için)</span></a>
        </div>
        <div id="batchList" class="collapse <%= ShowBatchesOpen ? "in" : "" %>">
            <table class="table table-condensed batch-table">
                <asp:Repeater ID="rptBatches" runat="server" ItemType="TrendyolApp.ImportBatch" OnItemCommand="rptBatches_ItemCommand">
                    <ItemTemplate>
                        <tr>
                            <td><%# Item.Time.ToString("dd.MM.yyyy HH:mm", Tr) %></td>
                            <td><%# Item.Count %> ürün</td>
                            <td class="small text-muted"><%#: Item.Sample %></td>
                            <td class="text-right">
                                <asp:LinkButton runat="server" CssClass="btn btn-danger btn-xs" CommandName="deleteBatch" CommandArgument='<%# Item.Key %>'
                                    OnClientClick='<%# "return confirm(\"Bu aktarımla eklenen " + Item.Count + " ürün takipten çıkarılsın mı?\");" %>'>Bu aktarımı sil</asp:LinkButton>
                            </td>
                        </tr>
                    </ItemTemplate>
                </asp:Repeater>
            </table>
        </div>
    </asp:Panel>

    <div class="category-bar">
        <div class="category-pills">
            <a class="btn btn-sm <%= CategoryFilter == "" ? "btn-primary" : "btn-default" %>" href="<%= ListUrl(Filter, Sort, 1, "") %>">Tüm kategoriler <span class="badge"><%= Stats.Total %></span></a>
            <% foreach (var c in Categories) { %>
                <a class="btn btn-sm <%= string.Compare(c.Name, CategoryFilter, Tr, System.Globalization.CompareOptions.IgnoreCase) == 0 ? "btn-primary" : c.IsCustom ? "btn-info" : "btn-default" %>"
                   href="<%= ListUrl(Filter, Sort, 1, c.Name) %>"><%: c.Name == NoCategory ? "Kategorisiz" : c.Name %> <span class="badge"><%= c.Count %></span></a>
            <% } %>
        </div>
        <div class="category-tools">
            <input type="search" id="searchBox" class="form-control input-sm" placeholder="Ürün ara (ör. nevresim, havlu, elbise)" value="<%: Search %>" />
            <span class="bulk-category">
                <asp:TextBox ID="txtBulkCategory" runat="server" CssClass="form-control input-sm" placeholder="Kategori (ör. Çeyizlik)" list="categoryList" />
                <asp:Button ID="btnBulkCategory" runat="server" CssClass="btn btn-default btn-sm" Text="Listelenenlere uygula" OnClick="btnBulkCategory_Click"
                    OnClientClick="return confirm('Şu an listelenen tüm ürünlere (tüm sayfalar) bu kategori verilsin mi? Boş bırakırsanız kendi kategorileri kaldırılır.');" />
            </span>
            <span class="small text-muted"><%= ListedCount %> ürün listeleniyor</span>
        </div>
        <datalist id="categoryList">
            <option value="Çeyizlik"></option>
            <% foreach (var name in CategoryNames) { %><option value="<%: name %>"></option><% } %>
        </datalist>
    </div>

    <% if (TopDiscounted.Count > 0) { %>
        <div class="panel panel-default top-discounted">
            <div class="panel-heading"><strong>En çok indirime girenler</strong>
                <span class="text-muted small"><%: CategoryFilter == "" ? "tüm ürünler" : CategoryFilter == NoCategory ? "kategorisiz ürünler" : CategoryFilter %></span>
                <a class="pull-right small" href="<%= ListUrl(Filter, "most-discounted") %>">Tümünü sırala</a>
            </div>
            <ol class="top-discounted-list">
                <% foreach (var p in TopDiscounted) { %>
                    <li><a href="<%= DetailUrl(p) %>"><%: p.Name ?? p.Url %></a>
                        <span class="text-muted small">— <strong><%= p.DiscountCount %> kez</strong><%= p.BiggestDropPercent.HasValue ? ", en büyük %" + p.BiggestDropPercent.Value.ToString("0.#", Tr) : "" %>, şimdi <%= Money(p.CurrentPrice) %></span></li>
                <% } %>
            </ol>
        </div>
    <% } %>

    <div class="toolbar clearfix">
        <div class="btn-group filter-group">
            <% foreach (var f in Filters) { %>
                <a class="btn btn-sm <%= f.Key == Filter ? "btn-primary" : "btn-default" %>" href="<%= ListUrl(f.Key, Sort) %>"><%= f.Value %> <span class="badge"><%= FilterCounts[f.Key] %></span></a>
            <% } %>
        </div>
        <div class="pull-right sort-box">
            <label for="sortSelect" class="small text-muted">Sırala:</label>
            <select id="sortSelect" class="form-control input-sm" onchange="location.href=this.value">
                <% foreach (var s in Sorts) { %>
                    <option value="<%= ListUrl(Filter, s.Key) %>" <%= s.Key == Sort ? "selected" : "" %>><%= s.Value %></option>
                <% } %>
            </select>
        </div>
    </div>

    <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
        <asp:Literal ID="litEmpty" runat="server" />
    </asp:Panel>

    <div class="row product-grid">
        <asp:Repeater ID="rptProducts" runat="server" ItemType="TrendyolApp.Models.Product" OnItemCommand="rptProducts_ItemCommand">
            <ItemTemplate>
                <div class="col-xs-12 col-sm-6 col-md-4 col-lg-3">
                    <div class='product-card <%# Item.TargetReached ? "is-target" : Item.IsAtLowest ? "is-lowest" : "" %>'>
                        <a class="product-image" href='<%# DetailUrl(Item) %>'>
                            <%# Item.ImageUrl != null ? "<img src=\"" + HttpUtility.HtmlAttributeEncode(Item.ImageUrl) + "\" alt=\"\" loading=\"lazy\" />" : "<span class=\"no-image\">Görsel yok</span>" %>
                            <span class="badges"><%# Badges(Item) %></span>
                        </a>
                        <div class="product-body">
                            <h4 class="product-name"><a href='<%# DetailUrl(Item) %>' title='<%#: Item.Name %>'><%#: Item.Name ?? (Item.LastChecked == null ? "Bilgiler yükleniyor…" : Item.Url) %></a></h4>

                            <div class="price-now">
                                <%# Money(Item.CurrentPrice) %>
                                <%# Item.BasketPrice.HasValue ? "<span class=\"basket-tag\">Sepette</span>" : "" %>
                                <%# Item.ListPrice.HasValue ? "<s>" + Money(Item.ListPrice) + "</s>" : "" %>
                                <%# Change(Item) %>
                            </div>
                            <%# Item.BasketPrice.HasValue ? "<div class=\"small text-muted\">Sayfadaki fiyat: " + Money(Item.ShownPrice) + "</div>" : "" %>
                            <%# DiscountNote(Item) %>
                            <%# DiscountHistory(Item) %>
                            <div class="product-category small text-muted" title='<%#: Item.TrendyolCategory %>'>
                                <%#: Item.TrendyolSubCategory ?? "" %>
                            </div>

                            <table class="price-stats">
                                <tr><th>En düşük</th><td class="lowest"><%# Money(Item.LowestPrice) %> <small><%# Day(Item.LowestPriceDate) %></small></td></tr>
                                <tr><th>Önceki 30 gün en düşük</th><td><%# Money(Item.Lowest30Days) %></td></tr>
                                <tr><th>En yüksek</th><td><%# Money(Item.HighestPrice) %></td></tr>
                                <tr><th>Ortalama</th><td><%# Money(Item.AveragePrice) %></td></tr>
                            </table>

                            <div class="input-group input-group-sm target-box">
                                <span class="input-group-addon">Hedef</span>
                                <asp:TextBox ID="txtTarget" runat="server" CssClass="form-control" placeholder="Fiyat (TL)" Text='<%# Item.TargetPrice.HasValue ? Item.TargetPrice.Value.ToString("0.##", Tr) : "" %>' />
                                <span class="input-group-btn">
                                    <asp:LinkButton runat="server" CssClass="btn btn-default" CommandName="target" CommandArgument='<%# Item.Id %>'>Kaydet</asp:LinkButton>
                                </span>
                            </div>

                            <div class="input-group input-group-sm category-box">
                                <span class="input-group-addon">Kategori</span>
                                <asp:TextBox ID="txtCategory" runat="server" CssClass="form-control" list="categoryList" Text='<%# Item.CustomCategory %>'
                                    placeholder='<%# Item.Category ?? "ör. Çeyizlik" %>' />
                                <span class="input-group-btn">
                                    <asp:LinkButton runat="server" CssClass="btn btn-default" CommandName="category" CommandArgument='<%# Item.Id %>'>Kaydet</asp:LinkButton>
                                </span>
                            </div>

                            <div class="card-actions">
                                <a href='<%#: Item.Url %>' target="_blank" rel="noopener">Trendyol'da aç</a>
                                <asp:LinkButton runat="server" CommandName="refresh" CommandArgument='<%# Item.Id %>'>Güncelle</asp:LinkButton>
                                <asp:LinkButton runat="server" CssClass="text-danger" CommandName="delete" CommandArgument='<%# Item.Id %>'
                                    OnClientClick="return confirm('Bu ürün takipten çıkarılsın mı?');">Sil</asp:LinkButton>
                            </div>
                            <div class="checked small text-muted">
                                <%# Item.LastChecked.HasValue ? "Son kontrol: " + Item.LastChecked.Value.ToString("dd.MM.yyyy HH:mm", Tr) : "Henüz kontrol edilmedi" %>
                                <%# Item.LastError != null ? "<br /><span class=\"text-danger\">" + HttpUtility.HtmlEncode(Item.LastError) + "</span>" : "" %>
                            </div>
                        </div>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </div>

    <% if (PageCount > 1) { %>
        <nav class="text-center">
            <ul class="pagination">
                <% for (var i = 1; i <= PageCount; i++) { %>
                    <li class="<%= i == PageNumber ? "active" : "" %>"><a href="<%= ListUrl(Filter, Sort, i) %>"><%= i %></a></li>
                <% } %>
            </ul>
        </nav>
    <% } %>

    <script>
        // Search: Enter navigates (the page is one WebForms form, so a nested GET form isn't possible).
        document.getElementById('searchBox').addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;
            e.preventDefault();
            var params = new URLSearchParams(location.search);
            params.delete('page');
            if (this.value.trim()) params.set('q', this.value.trim()); else params.delete('q');
            location.search = params.toString();
        });
        document.getElementById('searchBox').addEventListener('search', function () {
            if (!this.value) { var params = new URLSearchParams(location.search); params.delete('q'); params.delete('page'); location.search = params.toString(); }
        });

        document.getElementById('copyBookmarklet').addEventListener('click', function () {
            var box = document.getElementById('bookmarkletCode');
            var done = function () { document.getElementById('copyResult').textContent = 'Kopyalandı ✓'; };
            box.select();
            if (navigator.clipboard) navigator.clipboard.writeText(box.value).then(done, function () { document.execCommand('copy'); done(); });
            else { document.execCommand('copy'); done(); }
        });
    </script>

    <% if (AutoReload) { %>
        <script>
            // Prices are being fetched in the background; reload to show them unless the user is typing.
            setInterval(function () {
                var active = document.activeElement;
                var typing = active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA');
                var urls = document.getElementById('<%= txtUrls.ClientID %>');
                if (!typing && !(urls && urls.value)) location.href = location.href;
            }, 30000);
        </script>
    <% } %>

</asp:Content>
