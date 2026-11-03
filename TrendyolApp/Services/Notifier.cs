using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace TrendyolApp.Services
{
    /// <summary>
    /// Sends phone notifications through ntfy (https://ntfy.sh): the phone app subscribes to the topic
    /// configured on the settings page and receives every message published to it.
    /// </summary>
    public static class Notifier
    {
        /// <summary>Publishes a notification; returns null on success or the error message.</summary>
        public static string Send(string title, string message, string clickUrl = null, string imageUrl = null, string tags = null)
        {
            var settings = SettingsStore.Get();
            if (string.IsNullOrWhiteSpace(settings.NtfyTopic)) return "Bildirim konusu (topic) ayarlanmamış.";

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var body = JsonConvert.SerializeObject(new
                {
                    topic = settings.NtfyTopic.Trim(),
                    title,
                    message,
                    click = clickUrl,
                    attach = imageUrl,
                    tags = tags == null ? null : tags.Split(','),
                    priority = 4
                }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                var request = (HttpWebRequest)WebRequest.Create(settings.NtfyServer.TrimEnd('/') + "/");
                request.Method = "POST";
                request.ContentType = "application/json; charset=utf-8";
                request.Timeout = 15000;

                var bytes = Encoding.UTF8.GetBytes(body);
                using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                using (request.GetResponse()) { }
                return null;
            }
            catch (WebException ex) when (ex.Response is HttpWebResponse response)
            {
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    var error = "ntfy HTTP " + (int)response.StatusCode + ": " + reader.ReadToEnd();
                    Trace.TraceWarning(error);
                    return error;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Notification failed: {0}", ex.Message);
                return ex.Message;
            }
        }
    }
}
