using System;
using System.Web;
using System.Web.UI;
using TrendyolApp.Models;
using TrendyolApp.Services;

namespace TrendyolApp
{
    public partial class Settings : Page
    {
        protected string CurrentTopic => SettingsStore.Get().NtfyTopic;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            var settings = SettingsStore.Get();
            chkNewLowest.Checked = settings.NotifyNewLowest;
            chkTarget.Checked = settings.NotifyTarget;
            chkAnyDrop.Checked = settings.NotifyAnyDrop;
            chkBackInStock.Checked = settings.NotifyBackInStock;
            chkPlus.Checked = settings.TrendyolPlusMember;
            var interval = ddlInterval.Items.FindByValue(settings.CheckIntervalMinutes.ToString());
            if (interval != null) ddlInterval.SelectedValue = interval.Value;
            txtTopic.Text = settings.NtfyTopic;
            txtServer.Text = settings.NtfyServer;
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (Save()) ShowMessage("success", "Ayarlar kaydedildi.");
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            if (!Save()) return;
            var error = Notifier.Send("Trendyol Fiyat Takip", "Bildirimler çalışıyor! Fiyat düştüğünde buraya haber vereceğim.", tags: "tada");
            if (error == null) ShowMessage("success", "Test bildirimi gönderildi. Telefonunuza birkaç saniye içinde gelmesi gerekir.");
            else ShowMessage("danger", "Bildirim gönderilemedi: " + error);
        }

        private bool Save()
        {
            var topic = txtTopic.Text.Trim();
            var server = txtServer.Text.Trim();
            if (topic.Length < 6 || topic.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(topic, "^[A-Za-z0-9_-]+$"))
            {
                ShowMessage("warning", "Konu adı 6-64 karakter olmalı ve yalnızca harf, rakam, - ve _ içermeli.");
                return false;
            }
            if (!Uri.TryCreate(server, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            {
                ShowMessage("warning", "Sunucu adresi geçersiz. Varsayılan: https://ntfy.sh");
                return false;
            }

            var settings = SettingsStore.Get();
            settings.NotifyNewLowest = chkNewLowest.Checked;
            settings.NotifyTarget = chkTarget.Checked;
            settings.NotifyAnyDrop = chkAnyDrop.Checked;
            settings.NotifyBackInStock = chkBackInStock.Checked;
            settings.TrendyolPlusMember = chkPlus.Checked;
            PricePoint.IncludePlusPrice = chkPlus.Checked;
            settings.CheckIntervalMinutes = int.Parse(ddlInterval.SelectedValue);
            settings.NtfyTopic = topic;
            settings.NtfyServer = server;
            SettingsStore.Save(settings);
            return true;
        }

        private void ShowMessage(string type, string text)
        {
            litMessage.Text = "<div class=\"alert alert-" + type + "\">" + HttpUtility.HtmlEncode(text) + "</div>";
        }
    }
}
