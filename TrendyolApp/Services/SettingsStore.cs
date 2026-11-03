using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace TrendyolApp.Services
{
    public class AppSettings
    {
        public string NtfyServer { get; set; } = "https://ntfy.sh";
        public string NtfyTopic { get; set; }
        public bool NotifyNewLowest { get; set; } = true;
        public bool NotifyTarget { get; set; } = true;
        public bool NotifyAnyDrop { get; set; }
        public bool NotifyBackInStock { get; set; } = true;
        public int CheckIntervalMinutes { get; set; } = 30;
        public bool TrendyolPlusMember { get; set; }

        /// <summary>Version of the stored product data, used for one-time fix-ups (see PriceTracker.Start).</summary>
        public int DataVersion { get; set; } = 1;

        /// <summary>Secret embedded in the favorites bookmarklet so other sites can't post to the import endpoint.</summary>
        public string ImportKey { get; set; }
    }

    /// <summary>User-editable settings kept in App_Data/settings.json.</summary>
    public static class SettingsStore
    {
        private static readonly object Sync = new object();
        private static AppSettings _settings;

        private static string FilePath => HostingEnvironment.MapPath("~/App_Data/settings.json");

        public static AppSettings Get()
        {
            lock (Sync)
            {
                return Clone(Load());
            }
        }

        public static void Save(AppSettings settings)
        {
            lock (Sync)
            {
                _settings = Clone(settings);
                Write();
            }
        }

        private static AppSettings Load()
        {
            if (_settings != null) return _settings;

            _settings = File.Exists(FilePath)
                ? JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new AppSettings()
                : new AppSettings();

            var changed = false;
            if (string.IsNullOrWhiteSpace(_settings.NtfyTopic)) { _settings.NtfyTopic = "trendyol-" + RandomToken(12); changed = true; }
            if (string.IsNullOrWhiteSpace(_settings.ImportKey)) { _settings.ImportKey = RandomToken(24); changed = true; }
            if (changed) Write();
            return _settings;
        }

        private static void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_settings, Formatting.Indented), Encoding.UTF8);
        }

        private static string RandomToken(int length)
        {
            const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
            var bytes = new byte[length];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        private static AppSettings Clone(AppSettings settings) =>
            JsonConvert.DeserializeObject<AppSettings>(JsonConvert.SerializeObject(settings));
    }
}
