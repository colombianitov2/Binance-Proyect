using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CryptoSelfBot.Wpf.Services
{
    public class CoolorsService
    {
        private readonly HttpClient _client = new();

        public async Task<List<Color>> GetRandomPaletteAsync()
        {
            try
            {
                string url = "https://coolors.co/api/v1/palettes/random?count=1";
                var response = await _client.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<CoolorsResponse>(response);
                if (data?.Palettes != null && data.Palettes.Count > 0)
                {
                    return data.Palettes[0].Colors
                        .Select(hex => (Color)ColorConverter.ConvertFromString(hex))
                        .ToList();
                }
            }
            catch { /* fallback */ }
            return GetFallbackPalette();
        }

        private List<Color> GetFallbackPalette()
        {
            return new List<Color>
            {
                Color.FromRgb(24, 26, 32),
                Color.FromRgb(33, 39, 49),
                Color.FromRgb(240, 185, 11),
                Color.FromRgb(234, 236, 239),
                Color.FromRgb(51, 59, 71)
            };
        }

        private class CoolorsResponse
        {
            public List<Palette> Palettes { get; set; } = new();
        }

        private class Palette
        {
            public List<string> Colors { get; set; } = new();
        }
    }
}