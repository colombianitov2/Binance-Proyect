using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CryptoSelfBot.Engine.Services
{
    public class SourceMonitorService
    {
        private readonly List<MonitoredSource> _sources = new();
        private readonly DatabaseService _databaseService;
        private readonly string _configPath;

        public SourceMonitorService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sources.json");
            LoadSources();
        }

        private void LoadSources()
        {
            // Fuentes por defecto
            _sources.AddRange(new List<MonitoredSource>
            {
                new MonitoredSource
                {
                    Name = "Binance REST API",
                    Url = "https://api.binance.com/api/v3/ping",
                    Type = SourceType.Api,
                    Status = SourceStatus.Active,
                    CheckIntervalHours = 6
                },
                new MonitoredSource
                {
                    Name = "Binance WebSocket",
                    Url = "wss://stream.binance.com:9443/ws",
                    Type = SourceType.WebSocket,
                    Status = SourceStatus.Active,
                    CheckIntervalHours = 6
                },
                new MonitoredSource
                {
                    Name = "NewsAPI",
                    Url = "https://newsapi.org/v2/everything",
                    Type = SourceType.Api,
                    Status = SourceStatus.Active,
                    CheckIntervalHours = 6
                },
                new MonitoredSource
                {
                    Name = "Coolors",
                    Url = "https://coolors.co/api/v1/palettes/random",
                    Type = SourceType.Api,
                    Status = SourceStatus.Active,
                    CheckIntervalHours = 6
                },
                new MonitoredSource
                {
                    Name = "FRED API (placeholder)",
                    Url = "https://api.stlouisfed.org/fred/series/observations",
                    Type = SourceType.Api,
                    Status = SourceStatus.TemporarySuspended,
                    CheckIntervalHours = 6,
                    LastError = "API Key no configurada",
                    NextRetryUtc = DateTime.UtcNow.AddDays(7)
                },
                new MonitoredSource
                {
                    Name = "ECB Scraper (placeholder)",
                    Url = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml",
                    Type = SourceType.Scraper,
                    Status = SourceStatus.TemporarySuspended,
                    CheckIntervalHours = 6,
                    LastError = "Timeout de conexión",
                    NextRetryUtc = DateTime.UtcNow.AddDays(7)
                }
            });

            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    var savedSources = JsonSerializer.Deserialize<List<MonitoredSource>>(json);
                    if (savedSources != null)
                    {
                        _sources.Clear();
                        _sources.AddRange(savedSources);
                    }
                }
                catch { /* mantener defaults */ }
            }
        }

        public List<MonitoredSource> GetAllSources() => _sources;

        public async Task CheckAllSourcesAsync()
        {
            foreach (var source in _sources)
            {
                await CheckSourceAsync(source);
            }
            SaveSources();
        }

        private async Task CheckSourceAsync(MonitoredSource source)
        {
            // Si está deshabilitado permanentemente, no hacer nada
            if (source.Status == SourceStatus.PermanentlyDisabled)
                return;

            // Si está suspendido temporalmente, verificar si ya pasaron 7 días
            if (source.Status == SourceStatus.TemporarySuspended)
            {
                if (source.NextRetryUtc.HasValue && DateTime.UtcNow < source.NextRetryUtc.Value)
                    return; // aún no toca reintentar

                // Intentar reconectar
                bool success = await TestConnectionAsync(source.Url, source.Type);
                if (success)
                {
                    source.Status = SourceStatus.Active;
                    source.LastError = null;
                    source.NextRetryUtc = null;
                    source.ConsecutiveFailures = 0;
                    LogToDatabase(source.Name, "Recuperada", "Conexión restablecida");
                }
                else
                {
                    source.Status = SourceStatus.PermanentlyDisabled;
                    source.LastError = "Fallo tras reintento a los 7 días";
                    LogToDatabase(source.Name, "Deshabilitada", source.LastError);
                }
                return;
            }

            // Fuente activa: verificar conectividad
            bool active = await TestConnectionAsync(source.Url, source.Type);
            if (!active)
            {
                source.ConsecutiveFailures++;
                source.LastError = "Error de conexión o timeout";
                if (source.ConsecutiveFailures >= 3)
                {
                    source.Status = SourceStatus.TemporarySuspended;
                    source.NextRetryUtc = DateTime.UtcNow.AddDays(7);
                    LogToDatabase(source.Name, "Suspendida temporalmente", source.LastError);
                }
            }
            else
            {
                source.ConsecutiveFailures = 0;
                source.LastError = null;
            }
        }

        private async Task<bool> TestConnectionAsync(string url, SourceType type)
        {
            try
            {
                if (type == SourceType.WebSocket)
                {
                    // Verificación simple de conectividad (ping HTTP a la API REST como proxy)
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var response = await client.GetAsync("https://api.binance.com/api/v3/ping");
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var response = await client.GetAsync(url);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private void LogToDatabase(string sourceName, string status, string? errorMessage)
        {
            // Insertar en tabla de logs de fuentes (si existe) o simplemente loguear en consola
            Console.WriteLine($"[SourceMonitor] {sourceName}: {status} - {errorMessage}");
        }

        private void SaveSources()
        {
            try
            {
                string json = JsonSerializer.Serialize(_sources, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { /* ignorar errores de escritura */ }
        }
    }

    public class MonitoredSource
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public SourceType Type { get; set; } = SourceType.Api;
        public SourceStatus Status { get; set; } = SourceStatus.Active;
        public string? LastError { get; set; }
        public DateTime? NextRetryUtc { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int CheckIntervalHours { get; set; } = 6;
    }

    public enum SourceType
    {
        Api,
        WebSocket,
        Scraper,
        Other
    }

    public enum SourceStatus
    {
        Active,
        TemporarySuspended,
        PermanentlyDisabled
    }
}