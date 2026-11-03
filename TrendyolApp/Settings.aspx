<%@ Page Title="Bildirimler" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="TrendyolApp.Settings" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Telefon bildirimleri</h2>
    <asp:Literal ID="litMessage" runat="server" />

    <div class="row">
        <div class="col-md-6">
            <div class="panel panel-default">
                <div class="panel-heading"><strong>1. Telefonu bağlayın</strong></div>
                <div class="panel-body">
                    <ol>
                        <li>Telefonunuza ücretsiz <strong>ntfy</strong> uygulamasını kurun:
                            <a href="https://play.google.com/store/apps/details?id=io.heckel.ntfy" target="_blank" rel="noopener">Android</a> ·
                            <a href="https://apps.apple.com/app/ntfy/id1625396347" target="_blank" rel="noopener">iPhone</a>
                        </li>
                        <li>Uygulamada <em>+</em> (Abone ol / Subscribe) butonuna basın ve aşağıdaki konu adını aynen yazın:
                            <div class="topic-box"><code><%: CurrentTopic %></code></div>
                        </li>
                        <li><em>Test bildirimi gönder</em>'e basarak telefonunuza bildirim geldiğini kontrol edin.</li>
                    </ol>
                    <p class="text-muted small">Konu adı rastgele oluşturuldu; bilen herkes bildirimlerinizi görebileceği için kimseyle paylaşmayın.</p>
                </div>
            </div>
        </div>

        <div class="col-md-6">
            <div class="panel panel-default">
                <div class="panel-heading"><strong>2. Ne zaman bildirim gelsin?</strong></div>
                <div class="panel-body settings-form">
                    <div class="checkbox"><label><asp:CheckBox ID="chkNewLowest" runat="server" /> Ürün şimdiye kadarki <strong>en düşük fiyatına</strong> indiğinde</label></div>
                    <div class="checkbox"><label><asp:CheckBox ID="chkTarget" runat="server" /> Ürün belirlediğim <strong>hedef fiyata</strong> ulaştığında</label></div>
                    <div class="checkbox"><label><asp:CheckBox ID="chkBackInStock" runat="server" /> Tükenen ürün <strong>tekrar stoğa girdiğinde</strong></label></div>
                    <div class="checkbox"><label><asp:CheckBox ID="chkAnyDrop" runat="server" /> Fiyat <strong>her düştüğünde</strong> (küçük düşüşler dahil)</label></div>

                    <div class="checkbox"><label><asp:CheckBox ID="chkPlus" runat="server" /> <strong>Trendyol Plus üyesiyim</strong> (Plus'a özel fiyatlar hesaba katılsın)</label></div>
                    <div class="form-group">
                        <label for="<%= ddlInterval.ClientID %>">Fiyatlar ne sıklıkla kontrol edilsin?</label>
                        <asp:DropDownList ID="ddlInterval" runat="server" CssClass="form-control">
                            <asp:ListItem Value="15">15 dakikada bir</asp:ListItem>
                            <asp:ListItem Value="30">30 dakikada bir</asp:ListItem>
                            <asp:ListItem Value="60">Saatte bir</asp:ListItem>
                            <asp:ListItem Value="180">3 saatte bir</asp:ListItem>
                            <asp:ListItem Value="360">6 saatte bir</asp:ListItem>
                            <asp:ListItem Value="720">12 saatte bir</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="form-group">
                        <label for="<%= txtTopic.ClientID %>">Konu adı (topic)</label>
                        <asp:TextBox ID="txtTopic" runat="server" CssClass="form-control" />
                    </div>
                    <div class="form-group">
                        <label for="<%= txtServer.ClientID %>">ntfy sunucusu</label>
                        <asp:TextBox ID="txtServer" runat="server" CssClass="form-control" />
                    </div>

                    <asp:Button ID="btnSave" runat="server" Text="Kaydet" CssClass="btn btn-primary" OnClick="btnSave_Click" />
                    <asp:Button ID="btnTest" runat="server" Text="Test bildirimi gönder" CssClass="btn btn-default" OnClick="btnTest_Click" />
                </div>
            </div>
        </div>
    </div>
</asp:Content>
