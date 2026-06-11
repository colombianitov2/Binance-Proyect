using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CryptoSelfBot.Engine.Models;

namespace CryptoSelfBot.Engine.Services
{
    public class NewsArchiveService
    {
        private readonly DatabaseService _databaseService;
        private readonly string _archiveFolder;

        public NewsArchiveService(DatabaseService databaseService, string storagePath)
        {
            _databaseService = databaseService;
            _archiveFolder = Path.Combine(storagePath, "NewsArchive");
            Directory.CreateDirectory(_archiveFolder);
        }

        public async Task CleanupOldNewsAsync(int retentionDays = 365)
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var oldTrades = await _databaseService.GetTradesAsync();
            var oldNewsTrades = oldTrades
                .Where(t => t.Notes?.Contains("NewsAPI") == true && t.TimestampUtc < cutoff)
                .ToList();

            if (!oldNewsTrades.Any()) return;

            string fileName = $"Noticias_{cutoff:yyyy-MM-dd}_a_{DateTime.UtcNow:yyyy-MM-dd}.txt";
            string filePath = Path.Combine(_archiveFolder, fileName);

            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"ARCHIVO DE NOTICIAS - CryptoSelfBot");
            writer.WriteLine($"Período: {cutoff:yyyy-MM-dd} a {DateTime.UtcNow:yyyy-MM-dd}");
            writer.WriteLine(new string('-', 80));

            foreach (var trade in oldNewsTrades)
            {
                writer.WriteLine($"Fecha: {trade.TimestampUtc:dd/MM/yyyy HH:mm:ss}");
                writer.WriteLine($"Par: {trade.Symbol}");
                writer.WriteLine($"Tipo: {trade.Side}");
                writer.WriteLine($"Precio: {trade.Price}");
                writer.WriteLine($"Cantidad: {trade.Quantity}");
                writer.WriteLine($"Notas: {trade.Notes}");
                writer.WriteLine(new string('-', 40));
            }

            writer.WriteLine($"Total de registros archivados: {oldNewsTrades.Count}");
        }

        public async Task ExportNewsToExcelAsync(string? outputPath = null)
        {
            var trades = await _databaseService.GetTradesAsync();
            var newsTrades = trades
                .Where(t => t.Notes?.Contains("NewsAPI") == true)
                .OrderByDescending(t => t.TimestampUtc)
                .ToList();

            if (!newsTrades.Any()) return;

            string exportsFolder = outputPath ?? Path.Combine(_archiveFolder, "..", "Exports");
            Directory.CreateDirectory(exportsFolder);
            string fileName = $"Noticias_Export_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
            string filePath = Path.Combine(exportsFolder, fileName);

            OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("CryptoSelfBot");
            using var package = new OfficeOpenXml.ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Noticias");

            worksheet.Cells[1, 1].Value = "Fecha";
            worksheet.Cells[1, 2].Value = "Par";
            worksheet.Cells[1, 3].Value = "Tipo";
            worksheet.Cells[1, 4].Value = "Precio";
            worksheet.Cells[1, 5].Value = "Cantidad";
            worksheet.Cells[1, 6].Value = "Sentimiento";

            for (int i = 0; i < newsTrades.Count; i++)
            {
                worksheet.Cells[i + 2, 1].Value = newsTrades[i].TimestampUtc.ToString("dd/MM/yyyy HH:mm:ss");
                worksheet.Cells[i + 2, 2].Value = newsTrades[i].Symbol;
                worksheet.Cells[i + 2, 3].Value = newsTrades[i].Side;
                worksheet.Cells[i + 2, 4].Value = newsTrades[i].Price;
                worksheet.Cells[i + 2, 5].Value = newsTrades[i].Quantity;
                worksheet.Cells[i + 2, 6].Value = newsTrades[i].Notes;
            }

            await package.SaveAsAsync(new FileInfo(filePath));
        }
    }
}