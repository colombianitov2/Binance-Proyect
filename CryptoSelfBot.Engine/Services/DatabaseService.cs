using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;
using Microsoft.Data.Sqlite;

namespace CryptoSelfBot.Engine.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;

        public DatabaseService()
        {
            try
            {
                // Intentar usar la ruta configurada en AppDataService si está disponible
                string basePath = null!;
                try
                {
                    // Evitar referencia directa al assembly WPF en proyectos de engine; usar reflexión para obtener la ruta si existe
                    var appDataServiceType = Type.GetType("CryptoSelfBot.Wpf.Services.AppDataService, CryptoSelfBot.Wpf");
                    if (appDataServiceType != null)
                    {
                        var prop = appDataServiceType.GetProperty("Configuration");
                        var config = prop?.GetValue(null);
                        if (config != null)
                        {
                            var storageProp = config.GetType().GetProperty("StoragePath");
                            var sp = storageProp?.GetValue(config) as string;
                            if (!string.IsNullOrEmpty(sp)) basePath = sp;
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(basePath))
                {
                    basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CryptoSelfBot");
                }

                // Evitar usar disco del sistema (C:) por seguridad y espacio
                try
                {
                    var root = Path.GetPathRoot(basePath);
                    var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                    if (string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        // Si basePath está en C:, usar Documents por defecto
                        basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CryptoSelfBot");
                    }
                }
                catch { }

                var folder = Path.Combine(basePath, "Database");
                Directory.CreateDirectory(folder);

                _dbPath = Path.Combine(folder, "trading.db");
            }
            catch
            {
                // Fallback: carpeta en AppData local si todo falla
                try
                {
                    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoSelfBot");
                    Directory.CreateDirectory(folder);
                    _dbPath = Path.Combine(folder, "trading.db");
                }
                catch
                {
                    // Ultimo recurso: archivo temporal
                    _dbPath = Path.Combine(Path.GetTempPath(), "trading.db");
                }
            }
        }

        // =====================================================
        // INIT
        // =====================================================

        public async Task InitializeAsync()
        {
            try
            {
                await using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS Trades
                (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Symbol TEXT NOT NULL,
                    Side TEXT NOT NULL,
                    Price REAL NOT NULL,
                    Quantity REAL NOT NULL,
                    TimestampUtc TEXT NOT NULL,
                    Strategy TEXT NOT NULL,
                    Notes TEXT NOT NULL
                );
                ";

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Registrar y rethrow para que el caller decida
                System.Diagnostics.Debug.WriteLine("Database Initialize error: " + ex.Message);
                throw;
            }
        }

        // =====================================================
        // INSERT TRADE
        // =====================================================

        public async Task InsertTradeAsync(
            TradeRecord trade)
        {
            try
            {
                await using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
                INSERT INTO Trades
                (
                    Symbol,
                    Side,
                    Price,
                    Quantity,
                    TimestampUtc,
                    Strategy,
                    Notes
                )
                VALUES
                (
                    $symbol,
                    $side,
                    $price,
                    $quantity,
                    $timestamp,
                    $strategy,
                    $notes
                );
                ";

                command.Parameters.AddWithValue("$symbol", trade.Symbol);
                command.Parameters.AddWithValue("$side", trade.Side);
                command.Parameters.AddWithValue("$price", trade.Price);
                command.Parameters.AddWithValue("$quantity", trade.Quantity);
                command.Parameters.AddWithValue("$timestamp", trade.TimestampUtc.ToString("O"));
                command.Parameters.AddWithValue("$strategy", trade.Strategy);
                command.Parameters.AddWithValue("$notes", trade.Notes);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("InsertTradeAsync error: " + ex.Message);
                // No rethrow para no romper el bucle de trading; logging suficiente
            }
        }

        // =====================================================
        // GET TRADES
        // =====================================================

        public async Task<List<TradeRecord>>
            GetTradesAsync()
        {
            var trades =
                new List<TradeRecord>();

            try
            {
                await using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM Trades ORDER BY Id DESC";

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    trades.Add(new TradeRecord
                    {
                        Id = reader.GetInt32(0),
                        Symbol = reader.GetString(1),
                        Side = reader.GetString(2),
                        Price = reader.GetDecimal(3),
                        Quantity = reader.GetDecimal(4),
                        TimestampUtc = DateTime.Parse(reader.GetString(5)),
                        Strategy = reader.GetString(6),
                        Notes = reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetTradesAsync error: " + ex.Message);
            }

            return trades;
        }
    }
}