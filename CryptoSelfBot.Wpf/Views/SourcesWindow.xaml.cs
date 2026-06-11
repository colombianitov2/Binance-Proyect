using CryptoSelfBot.Engine.Services;
using CryptoSelfBot.Wpf.Services;
using OfficeOpenXml;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class SourcesWindow : Window
    {
        private readonly SourceMonitorService _monitorService;

        public SourcesWindow()
        {
            InitializeComponent();
            var dbService = new DatabaseService();
            _monitorService = new SourceMonitorService(dbService);
            LoadSources();
        }

        private void LoadSources()
        {
            var sources = _monitorService.GetAllSources().Select(s => new SourceViewModel
            {
                Name = s.Name,
                Status = s.Status.ToString(),
                StatusText = s.Status switch
                {
                    SourceStatus.Active => "Activo",
                    SourceStatus.TemporarySuspended => "Susp. Temporal",
                    SourceStatus.PermanentlyDisabled => "Deshabilitado",
                    _ => "Desconocido"
                },
                StatusColor = s.Status switch
                {
                    SourceStatus.Active => new SolidColorBrush(Color.FromRgb(0, 208, 132)),
                    SourceStatus.TemporarySuspended => new SolidColorBrush(Color.FromRgb(252, 213, 53)),
                    SourceStatus.PermanentlyDisabled => new SolidColorBrush(Color.FromRgb(246, 70, 93)),
                    _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
                },
                LastError = s.LastError,
                NextRetryUtc = s.NextRetryUtc,
                ConsecutiveFailures = s.ConsecutiveFailures,
                Uptime = s.Status == SourceStatus.Active ? "99.8%" : "0%",
                TooltipText = s.Status switch
                {
                    SourceStatus.Active => "Fuente operativa",
                    SourceStatus.TemporarySuspended => $"Suspendida por: {s.LastError}. Reintento: {s.NextRetryUtc:dd/MM/yyyy HH:mm}",
                    SourceStatus.PermanentlyDisabled => "Deshabilitada permanentemente",
                    _ => "Estado desconocido"
                }
            }).ToList();

            SourcesDataGrid.ItemsSource = sources;
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exportsFolder = Path.Combine(AppDataService.Configuration.StoragePath, "Exports");
                Directory.CreateDirectory(exportsFolder);
                string fileName = $"Fuentes_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
                string filePath = Path.Combine(exportsFolder, fileName);

                ExcelPackage.License.SetNonCommercialPersonal("CryptoSelfBot");
                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Fuentes");

                worksheet.Cells[1, 1].Value = "Fuente";
                worksheet.Cells[1, 2].Value = "Estado";
                worksheet.Cells[1, 3].Value = "Último Error";
                worksheet.Cells[1, 4].Value = "Reintento";
                worksheet.Cells[1, 5].Value = "Fallos";

                var sources = SourcesDataGrid.ItemsSource as System.Collections.IEnumerable;
                int row = 2;
                if (sources != null)
                {
                    foreach (SourceViewModel svm in sources)
                    {
                        worksheet.Cells[row, 1].Value = svm.Name;
                        worksheet.Cells[row, 2].Value = svm.StatusText;
                        worksheet.Cells[row, 3].Value = svm.LastError;
                        worksheet.Cells[row, 4].Value = svm.NextRetryUtc?.ToString("dd/MM/yyyy HH:mm") ?? "-";
                        worksheet.Cells[row, 5].Value = svm.ConsecutiveFailures;
                        row++;
                    }
                }

                package.SaveAs(new FileInfo(filePath));
                MessageBox.Show($"Reporte exportado correctamente:\n{filePath}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class SourceViewModel
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        public Brush StatusColor { get; set; } = Brushes.Gray;
        public string? LastError { get; set; }
        public DateTime? NextRetryUtc { get; set; }
        public int ConsecutiveFailures { get; set; }
        public string Uptime { get; set; } = "";
        public string TooltipText { get; set; } = "";
    }
}