using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OfficeOpenXml;

namespace CryptoSelfBot.Engine.Services
{
    public class StorageManager
    {
        private readonly string _dataFolder;
        private readonly string _connectionString;
        private readonly double _criticalFillRatio = 0.95;

        public StorageManager(string dataFolder)
        {
            _dataFolder = dataFolder ?? throw new ArgumentNullException(nameof(dataFolder));
            _connectionString = $"Data Source={Path.Combine(_dataFolder, "Database", "cryptoselfbot.db")}";
        }

        private string SafePathRoot => Path.GetPathRoot(_dataFolder) ?? string.Empty;

        public long GetAvailableSpace()
        {
            if (string.IsNullOrEmpty(SafePathRoot)) return 0;
            DriveInfo drive = new DriveInfo(SafePathRoot);
            return drive.AvailableFreeSpace;
        }

        public long GetTotalSpace()
        {
            if (string.IsNullOrEmpty(SafePathRoot)) return 0;
            DriveInfo drive = new DriveInfo(SafePathRoot);
            return drive.TotalSize;
        }

        public double GetFillRatio()
        {
            long total = GetTotalSpace();
            if (total == 0) return 0;
            return 1.0 - (double)GetAvailableSpace() / total;
        }

        public bool IsCritical() => GetFillRatio() >= _criticalFillRatio;

        public async Task<List<DataRecord>> GetLeastImportantRecordsAsync(int count = 100)
        {
            var records = new List<DataRecord>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"SELECT id, source, symbol, timestamp, importance_score 
                                   FROM market_data 
                                   ORDER BY importance_score ASC, timestamp ASC 
                                   LIMIT @count";
            command.Parameters.AddWithValue("@count", count);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                records.Add(new DataRecord
                {
                    Id = reader.GetInt32(0),
                    Source = reader.GetString(1),
                    Symbol = reader.GetString(2),
                    Timestamp = reader.GetDateTime(3),
                    ImportanceScore = reader.GetDouble(4)
                });
            }
            return records;
        }

        public async Task ExportAndDeleteAsync(List<DataRecord> records)
        {
            if (!records.Any()) return;

            var firstDate = records.Min(r => r.Timestamp).ToString("yyyy-MM-dd");
            var lastDate = records.Max(r => r.Timestamp).ToString("yyyy-MM-dd");
            var fileName = $"{firstDate}_a_{lastDate}_purga.xlsx";
            var filePath = Path.Combine(_dataFolder, "Exports", fileName);

            // EPPlus 8 license
            ExcelPackage.License.SetNonCommercialPersonal("CryptoSelfBot");

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Datos Eliminados");

            worksheet.Cells[1, 1].Value = "ID";
            worksheet.Cells[1, 2].Value = "Fuente";
            worksheet.Cells[1, 3].Value = "Símbolo";
            worksheet.Cells[1, 4].Value = "Fecha";
            worksheet.Cells[1, 5].Value = "Importancia";

            for (int i = 0; i < records.Count; i++)
            {
                worksheet.Cells[i + 2, 1].Value = records[i].Id;
                worksheet.Cells[i + 2, 2].Value = records[i].Source;
                worksheet.Cells[i + 2, 3].Value = records[i].Symbol;
                worksheet.Cells[i + 2, 4].Value = records[i].Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[i + 2, 5].Value = records[i].ImportanceScore;
            }

            await package.SaveAsAsync(new FileInfo(filePath));

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            foreach (var record in records)
            {
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM market_data WHERE id = @id";
                command.Parameters.AddWithValue("@id", record.Id);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        public async Task MaintainStorageAsync()
        {
            if (IsCritical())
            {
                var recordsToDelete = await GetLeastImportantRecordsAsync(500);
                await ExportAndDeleteAsync(recordsToDelete);
            }
        }
    }

    public class DataRecord
    {
        public int Id { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double ImportanceScore { get; set; }
    }
}