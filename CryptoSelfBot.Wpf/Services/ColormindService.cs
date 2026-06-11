using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CryptoSelfBot.Wpf.Services
{
    public class ColormindService
    {
        private readonly HttpClient _client;

        public ColormindService()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        }

        public async Task<List<Color>?> GetRandomPaletteAsync()
        {
            try
            {
                var body = JsonSerializer.Serialize(new { model = "default" });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var resp = await _client.PostAsync("http://colormind.io/api/", content);
                if (!resp.IsSuccessStatusCode) return null;
                var txt = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(txt);
                if (!doc.RootElement.TryGetProperty("result", out var res)) return null;
                var list = new List<Color>();
                foreach (var item in res.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Array) continue;
                    int[] rgb = new int[3];
                    int i = 0;
                    foreach (var c in item.EnumerateArray())
                    {
                        if (i < 3 && c.TryGetInt32(out var v)) rgb[i++] = v;
                    }
                    if (i == 3)
                    {
                        list.Add(Color.FromRgb((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]));
                    }
                }
                return list.Count > 0 ? list : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
