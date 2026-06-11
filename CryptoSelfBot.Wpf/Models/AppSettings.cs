using System;

namespace CryptoSelfBot.Wpf.Models
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = "";

        public string SecretKey { get; set; } = "";

        public string StoragePath { get; set; } = "";

        public string ThemeUrl { get; set; } = "";

        public DateTime LastSaved { get; set; } =
            DateTime.Now;
    }
}