using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CryptoSelfBot.Wpf.Services;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CryptoSelfBot.Wpf.Views
{
    public partial class ThemeWindow : Window
    {
        public ThemeWindow()
        {
            InitializeComponent();
            PreviewColors = new System.Collections.ObjectModel.ObservableCollection<SolidColorBrush>();
            DataContext = this;
            // Save current resources as fallback for revert
            CaptureCurrentResources();
            LoadSavedThemes();
        }

        public System.Collections.ObjectModel.ObservableCollection<SolidColorBrush> PreviewColors { get; set; }

        private Color _savedBg, _savedCard, _savedPrimary, _savedText, _savedMuted;

        private void CaptureCurrentResources()
        {
            var app = Application.Current;
            _savedBg = ((SolidColorBrush?)app.Resources["WindowBackgroundBrush"])?.Color ?? Color.FromRgb(5,11,26);
            _savedCard = ((SolidColorBrush?)app.Resources["CardBackgroundBrush"])?.Color ?? Color.FromRgb(12,21,43);
            _savedPrimary = ((SolidColorBrush?)app.Resources["PrimaryBrush"])?.Color ?? Color.FromRgb(0,157,220);
            _savedText = ((SolidColorBrush?)app.Resources["TextBrush"])?.Color ?? Color.FromRgb(242,242,242);
            _savedMuted = ((SolidColorBrush?)app.Resources["MutedTextBrush"])?.Color ?? Color.FromRgb(148,163,184);
        }

        private void ImportFromText_Click(object sender, RoutedEventArgs e)
        {
            string input = PaletteInputText.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Introduce una URL de Coolors o pega el contenido exportado.", "Importar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Try as URL
            var parsed = ParseColorsFromCoolorsUrl(input);
            if (parsed != null && parsed.Length >= 5)
            {
                SaveAndApplyPalette(parsed);
                MessageBox.Show("Paleta importada desde URL y aplicada.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Try as raw hex list
            var hexes = ExtractHexColors(input);
            if (hexes.Length >= 5)
            {
                var cols = hexes.Take(5).Select(h => (Color)ColorConverter.ConvertFromString(h)).ToArray();
                SaveAndApplyPalette(cols);
                MessageBox.Show("Paleta importada desde texto y aplicada.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show("No se pudieron extraer colores del texto proporcionado.", "Importar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SaveTheme_Click(object sender, RoutedEventArgs e)
        {
            // Persist current in-memory colors as theme file
            try
            {
                var app = Application.Current;
                var wb = (SolidColorBrush?)app.Resources["WindowBackgroundBrush"] ?? new SolidColorBrush(Color.FromRgb(5,11,26));
                var cb = (SolidColorBrush?)app.Resources["CardBackgroundBrush"] ?? new SolidColorBrush(Color.FromRgb(12,21,43));
                var pb = (SolidColorBrush?)app.Resources["PrimaryBrush"] ?? new SolidColorBrush(Color.FromRgb(0,157,220));
                var tb = (SolidColorBrush?)app.Resources["TextBrush"] ?? new SolidColorBrush(Color.FromRgb(242,242,242));
                var mb = (SolidColorBrush?)app.Resources["MutedTextBrush"] ?? new SolidColorBrush(Color.FromRgb(148,163,184));

                var cols = new Color[] { wb.Color, cb.Color, pb.Color, tb.Color, mb.Color };
                SaveAndApplyPalette(cols);
                MessageBox.Show("Tema guardado y persistido.", "Tema", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error guardando tema: " + ex.Message, "Tema", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Enhanced parsing: try to detect simple ASE/ACO like exports by hex sequences in file (handled earlier by ExtractHexColors)
        // If binary ASE/ACO support required, implement specialized parser or use third-party lib.

        private Color GetReadableTextColor(Color background)
        {
            // Compute relative luminance, choose white or black
            double r = background.R / 255.0;
            double g = background.G / 255.0;
            double b = background.B / 255.0;
            double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            return lum < 0.6 ? Colors.White : Colors.Black;
        }

        private void ApplyTheme(Color background, Color card, Color accent, Color text, Color muted)
        {
            var app = Application.Current;
            app.Resources["WindowBackgroundBrush"] = new SolidColorBrush(background);
            app.Resources["CardBackgroundBrush"] = new SolidColorBrush(card);
            app.Resources["BorderBrushColor"] = new SolidColorBrush(Color.FromRgb(
                (byte)(card.R + 30 > 255 ? 255 : card.R + 30),
                (byte)(card.G + 30 > 255 ? 255 : card.G + 30),
                (byte)(card.B + 30 > 255 ? 255 : card.B + 30)));
            app.Resources["PrimaryAccent"] = new SolidColorBrush(accent);
            app.Resources["TextDark"] = new SolidColorBrush(text);
            app.Resources["TextMuted"] = new SolidColorBrush(muted);
            app.Resources["CardBackground"] = new SolidColorBrush(card);
            app.Resources["CardBorder"] = app.Resources["BorderBrushColor"];
            app.Resources["ContentBackground"] = new SolidColorBrush(Color.FromRgb(
                (byte)(background.R + 20 > 255 ? 255 : background.R + 20),
                (byte)(background.G + 20 > 255 ? 255 : background.G + 20),
                (byte)(background.B + 20 > 255 ? 255 : background.B + 20)));
            app.Resources["HeaderBorder"] = app.Resources["BorderBrushColor"];

            MessageBox.Show("Tema aplicado correctamente.", "Tema", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplyBinanceTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0x0B, 0x0E, 0x11), // background
                Color.FromRgb(0x1E, 0x23, 0x29), // card
                Color.FromRgb(0xF0, 0xB9, 0x0B), // accent
                Color.FromRgb(0xEA, 0xEC, 0xEF), // text
                Color.FromRgb(0x92, 0x9A, 0xA5)  // muted
            );
        }

        private void ApplyCyberpunkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0x11, 0x11, 0x27),
                Color.FromRgb(0x00, 0xFF, 0xF7),
                Color.FromRgb(0xFF, 0x00, 0xFF),
                Color.FromRgb(0x00, 0xFF, 0x99),
                Color.FromRgb(0xFF, 0x00, 0xFF)
            );
        }

        private void ApplyCoolorsTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0x2A, 0x2D, 0x34),
                Color.FromRgb(0x00, 0x9D, 0xDC),
                Color.FromRgb(0xF2, 0x64, 0x30),
                Color.FromRgb(0x67, 0x61, 0xA8),
                Color.FromRgb(0x00, 0x9B, 0x72)
            );
        }

        private void ApplyDarkTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0x11, 0x11, 0x11),
                Color.FromRgb(0x22, 0x22, 0x22),
                Color.FromRgb(0xFF, 0x55, 0x00),
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0x88, 0x88, 0x88)
            );
        }

        private void ApplySciFiTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0x0B, 0x12, 0x20),
                Color.FromRgb(0x00, 0xFF, 0x99),
                Color.FromRgb(0x00, 0xB4, 0xFF),
                Color.FromRgb(0x1E, 0x29, 0x3B),
                Color.FromRgb(0x00, 0xFF, 0x99)
            );
        }

        private void ApplyMinimalTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(
                Color.FromRgb(0xFF, 0xFF, 0xFF),
                Color.FromRgb(0xE2, 0xE8, 0xF0),
                Color.FromRgb(0x38, 0xBD, 0xF8),
                Color.FromRgb(0x11, 0x11, 0x11),
                Color.FromRgb(0x6B, 0x72, 0x80)
            );
        }

        private void OpenCoolors_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://coolors.co",
                UseShellExecute = true
            });
        }

        private void ImportColors_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar paleta (Coolors export, CSS, SVG, ASE, TXT)",
                Filter = "All supported|*.ase;*.css;*.svg;*.txt;*.xml;*.json;*.aco;*.ase;*.aseprite;*.*|Text files (*.txt)|*.txt|SVG (*.svg)|*.svg|CSS (*.css)|*.css|ASE (*.ase)|*.ase|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string file = dlg.FileName;
                byte[] raw = File.ReadAllBytes(file);

                // Try binary ASE/ACO parsing first
                var binColors = TryParseBinaryPalette(raw);
                if (binColors != null && binColors.Length >= 5)
                {
                    var cols = binColors.Take(5).ToArray();
                    PreviewColors.Clear();
                    foreach (var c in cols) PreviewColors.Add(new SolidColorBrush(c));
                    MessageBox.Show("Paleta binaria detectada y cargada en preview. Usa 'Aplicar paleta (preview)' para aplicar.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string content = File.ReadAllText(file);
                var parsed = ExtractHexColors(content);
                if (parsed.Length >= 5)
                {
                    var cols = parsed.Take(5).Select(hex => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)).ToArray();
                    PreviewColors.Clear();
                    foreach (var c in cols) PreviewColors.Add(new SolidColorBrush(c));
                    MessageBox.Show("Paleta detectada y cargada en preview. Usa 'Aplicar paleta (preview)' para aplicar.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Try as Coolors URL stored in file
                var maybe = ParseColorsFromCoolorsUrl(content.Trim());
                if (maybe != null && maybe.Length >= 5)
                {
                    PreviewColors.Clear();
                    foreach (var c in maybe.Take(5)) PreviewColors.Add(new SolidColorBrush(c));
                    MessageBox.Show("Paleta URL detectada y cargada en preview. Usa 'Aplicar paleta (preview)' para aplicar.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show("No se encontraron suficientes colores en el archivo.", "Importar", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Error importando paleta: " + ex.Message, "Importar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Color[]? TryParseBinaryPalette(byte[] raw)
        {
            // Try to parse Adobe ACO (Adobe Color) files (v1/v2)
            try
            {
                using var ms = new MemoryStream(raw);
                using var br = new BinaryReader(ms);

                // Read version (big-endian ushort)
                ushort version = ReadUInt16BE(br);
                if (version == 1 || version == 2)
                {
                    ushort count = ReadUInt16BE(br);
                    var cols = new List<Color>();
                    for (int i = 0; i < count; i++)
                    {
                        ushort colorSpace = ReadUInt16BE(br);
                        // Read four components (ushort each)
                        ushort c1 = ReadUInt16BE(br);
                        ushort c2 = ReadUInt16BE(br);
                        ushort c3 = ReadUInt16BE(br);
                        ushort c4 = ReadUInt16BE(br);

                        if (colorSpace == 0) // RGB
                        {
                            byte r = (byte)Math.Round(c1 / 65535.0 * 255);
                            byte g = (byte)Math.Round(c2 / 65535.0 * 255);
                            byte b = (byte)Math.Round(c3 / 65535.0 * 255);
                            cols.Add(Color.FromRgb(r, g, b));
                        }
                        else if (colorSpace == 2) // Lab - approximate skip
                        {
                            // Not supported: skip
                        }
                        else if (colorSpace == 7) // Gray
                        {
                            byte v = (byte)Math.Round(c1 / 65535.0 * 255);
                            cols.Add(Color.FromRgb(v, v, v));
                        }
                    }

                    // If v2, there may be names following; ignore
                    if (cols.Count >= 5) return cols.ToArray();
                }
            }
            catch { }

            // Try ASE (.ase) parsing
            try
            {
                if (raw.Length >= 4 && raw[0] == (byte)'A' && raw[1] == (byte)'S' && raw[2] == (byte)'E' && raw[3] == (byte)'F')
                {
                    var aseColors = ParseAse(raw);
                    if (aseColors != null && aseColors.Length >= 5) return aseColors;
                }
            }
            catch { }

            return null;
        }

        private static ushort ReadUInt16BE(BinaryReader br)
        {
            var data = br.ReadBytes(2);
            if (data.Length < 2) return 0;
            return (ushort)((data[0] << 8) | data[1]);
        }

        private static uint ReadUInt32BE(BinaryReader br)
        {
            var data = br.ReadBytes(4);
            if (data.Length < 4) return 0;
            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        }

        private static float ReadSingleBE(BinaryReader br)
        {
            var data = br.ReadBytes(4);
            if (data.Length < 4) return 0f;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }

        private Color[]? ParseAse(byte[] raw)
        {
            try
            {
                using var ms = new MemoryStream(raw);
                using var br = new BinaryReader(ms);
                // Header 'ASEF'
                var header = br.ReadBytes(4);
                if (header.Length < 4) return null;
                if (header[0] != (byte)'A' || header[1] != (byte)'S' || header[2] != (byte)'E' || header[3] != (byte)'F') return null;

                // version
                ushort major = ReadUInt16BE(br);
                ushort minor = ReadUInt16BE(br);
                uint blockCount = ReadUInt32BE(br);
                var colors = new List<Color>();

                for (uint i = 0; i < blockCount; i++)
                {
                    ushort blockType = ReadUInt16BE(br);
                    uint blockLength = ReadUInt32BE(br);
                    long blockStart = ms.Position;

                    if (blockType == 0x0001 || blockType == 0x0002) // color entry or group
                    {
                        if (blockType == 0x0001)
                        {
                            // name: uint16 = length in chars including null
                            ushort nameLen = ReadUInt16BE(br);
                            string name = "";
                            if (nameLen > 0)
                            {
                                var nameBytes = br.ReadBytes(nameLen * 2);
                                try { name = Encoding.BigEndianUnicode.GetString(nameBytes).TrimEnd('\0'); } catch { }
                            }

                            // color model 4 bytes ASCII
                            var modelBytes = br.ReadBytes(4);
                            string model = Encoding.ASCII.GetString(modelBytes);

                            // components depend on model
                            if (model == "RGB ")
                            {
                                // 3 floats (BE)
                                float r = ReadSingleBE(br);
                                float g = ReadSingleBE(br);
                                float b = ReadSingleBE(br);
                                // ASE RGB floats are 0..1
                                byte R = (byte)Math.Round(Math.Max(0, Math.Min(1, r)) * 255);
                                byte G = (byte)Math.Round(Math.Max(0, Math.Min(1, g)) * 255);
                                byte B = (byte)Math.Round(Math.Max(0, Math.Min(1, b)) * 255);
                                colors.Add(Color.FromRgb(R, G, B));
                            }
                            else if (model == "CMYK")
                            {
                                // 4 floats, convert roughly
                                float c = ReadSingleBE(br);
                                float m = ReadSingleBE(br);
                                float y = ReadSingleBE(br);
                                float k = ReadSingleBE(br);
                                byte R = (byte)Math.Round((1 - Math.Min(1, c * (1 - k) + k)) * 255);
                                byte G = (byte)Math.Round((1 - Math.Min(1, m * (1 - k) + k)) * 255);
                                byte B = (byte)Math.Round((1 - Math.Min(1, y * (1 - k) + k)) * 255);
                                colors.Add(Color.FromRgb(R, G, B));
                            }
                            else if (model == "Gray")
                            {
                                float v = ReadSingleBE(br);
                                byte V = (byte)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
                                colors.Add(Color.FromRgb(V, V, V));
                            }
                            else
                            {
                                // unknown model: try to scan for three floats
                                try
                                {
                                    float a = ReadSingleBE(br);
                                    float b = ReadSingleBE(br);
                                    float c2 = ReadSingleBE(br);
                                    byte R = (byte)Math.Round(Math.Max(0, Math.Min(1, a)) * 255);
                                    byte G = (byte)Math.Round(Math.Max(0, Math.Min(1, b)) * 255);
                                    byte B = (byte)Math.Round(Math.Max(0, Math.Min(1, c2)) * 255);
                                    colors.Add(Color.FromRgb(R, G, B));
                                }
                                catch { }
                            }

                            // color type (1 byte)
                            try { br.ReadByte(); } catch { }
                        }
                        else
                        {
                            // group or other: skip content
                        }
                    }

                    // advance to next block
                    try { ms.Position = blockStart + blockLength; } catch { break; }
                }

                if (colors.Count >= 5) return colors.ToArray();
            }
            catch { }
            return null;
        }

        private void ApplyPreview_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewColors.Count < 5)
            {
                MessageBox.Show("No hay paleta en preview para aplicar.", "Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var cols = PreviewColors.Take(5).Select(b => b.Color).ToArray();
            SaveAndApplyPalette(cols);
            MessageBox.Show("Paleta aplicada.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RevertTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(_savedBg, _savedCard, _savedPrimary, _savedText, _savedMuted);
            MessageBox.Show("Tema revertido al estado anterior.", "Revertir", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdatePreviewFromHex_Click(object sender, RoutedEventArgs e)
        {
            var boxes = new[] { ColorBox0, ColorBox1, ColorBox2, ColorBox3, ColorBox4 };
            var parsed = new List<Color>();
            foreach (var tb in boxes)
            {
                if (tb == null) continue;
                string text = tb.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(text)) continue;
                try
                {
                    if (!text.StartsWith("#")) text = "#" + text;
                    var c = (Color)ColorConverter.ConvertFromString(text);
                    parsed.Add(c);
                }
                catch { }
            }
            if (parsed.Count >= 1)
            {
                PreviewColors.Clear();
                foreach (var c in parsed) PreviewColors.Add(new SolidColorBrush(c));
                MessageBox.Show($"Preview actualizado con {parsed.Count} color(es).", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show("No se detectaron hex válidos en los campos.", "Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void CheckContrast_Click(object sender, RoutedEventArgs e)
        {
            var app = Application.Current;
            var bgBrush = (SolidColorBrush?)app.Resources["WindowBackgroundBrush"] ?? new SolidColorBrush(_savedBg);
            var textBrush = (SolidColorBrush?)app.Resources["TextBrush"] ?? new SolidColorBrush(_savedText);
            double ratio = ContrastRatio(bgBrush.Color, textBrush.Color);
            MessageBox.Show($"Contraste (fondo vs texto): {ratio:F2}:1\nRecomendado >= 4.5 para texto normal.", "Contraste WCAG", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AdjustTextForContrast_Click(object sender, RoutedEventArgs e)
        {
            var app = Application.Current;
            var bgBrush = (SolidColorBrush?)app.Resources["WindowBackgroundBrush"] ?? new SolidColorBrush(_savedBg);
            // Choose black or white depending ratio
            var white = Colors.White; var black = Colors.Black;
            double rWhite = ContrastRatio(bgBrush.Color, white);
            double rBlack = ContrastRatio(bgBrush.Color, black);
            var chosen = rWhite >= rBlack ? white : black;
            app.Resources["TextBrush"] = new SolidColorBrush(chosen);
            MessageBox.Show($"Texto ajustado a {(chosen==Colors.White?"blanco":"negro")} (contraste {Math.Max(rWhite,rBlack):F2}:1)", "Ajuste contraste", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static double LinearizeChannel(byte c)
        {
            double v = c / 255.0;
            if (v <= 0.03928) return v / 12.92;
            return Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static double RelativeLuminance(Color color)
        {
            double r = LinearizeChannel(color.R);
            double g = LinearizeChannel(color.G);
            double b = LinearizeChannel(color.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double ContrastRatio(Color a, Color b)
        {
            double L1 = RelativeLuminance(a);
            double L2 = RelativeLuminance(b);
            double brighter = Math.Max(L1, L2);
            double darker = Math.Min(L1, L2);
            return (brighter + 0.05) / (darker + 0.05);
        }

        private System.Windows.Media.Color[]? ParseColorsFromCoolorsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var uri = new System.Uri(url);
                string[] parts = uri.Segments.Last().Split('-', StringSplitOptions.RemoveEmptyEntries);
                var list = new List<System.Windows.Media.Color>();
                foreach (var p in parts)
                {
                    string hex = p.Trim();
                    if (!hex.StartsWith("#")) hex = "#" + hex;
                    var conv = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                    list.Add(conv);
                }
                return list.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private string[] ExtractHexColors(string input)
        {
            if (string.IsNullOrEmpty(input)) return Array.Empty<string>();
            var list = new List<string>();
            try
            {
                // Buscar códigos hex de 6 dígitos con o sin #
                var rx = new System.Text.RegularExpressions.Regex("#?[0-9A-Fa-f]{6}");
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(input))
                {
                    string hex = m.Value;
                    if (!hex.StartsWith("#")) hex = "#" + hex;
                    if (!list.Contains(hex, StringComparer.OrdinalIgnoreCase))
                        list.Add(hex.ToUpperInvariant());
                }
            }
            catch { }
            return list.ToArray();
        }

        private void SaveAndApplyPalette(System.Windows.Media.Color[] colors)
        {
            if (colors == null || colors.Length < 5) return;
            // Apply in-memory
            ApplyTheme(colors[0], colors[1], colors[2], colors[3], colors[4]);
            // Persist as XAML ResourceDictionary in StoragePath/Themes
            try
            {
                string themesDir = System.IO.Path.Combine(AppDataService.Configuration.StoragePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Themes");
                System.IO.Directory.CreateDirectory(themesDir);
                string fileName = $"palette_{DateTime.Now:yyyyMMddHHmmss}.xaml";
                string path = System.IO.Path.Combine(themesDir, fileName);
                SavePaletteToXaml(colors, path);
                // Persist selection
                AppDataService.Configuration.Theme = path;
                AppDataService.Save();
            }
            catch { }
        }

        private void SavePaletteToXaml(System.Windows.Media.Color[] colors, string path)
        {
            // Create a ResourceDictionary XAML using the same keys used in Colors.xaml
            string ToHex(System.Windows.Media.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

            // Compute a border color variant from the card color
            byte brR = (byte)(colors[1].R + 30 > 255 ? 255 : colors[1].R + 30);
            byte brG = (byte)(colors[1].G + 30 > 255 ? 255 : colors[1].G + 30);
            byte brB = (byte)(colors[1].B + 30 > 255 ? 255 : colors[1].B + 30);
            var borderColor = System.Windows.Media.Color.FromRgb(brR, brG, brB);

            var sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"> ");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"WindowBackgroundBrush\" Color=\"{ToHex(colors[0])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"CardBackgroundBrush\" Color=\"{ToHex(colors[1])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"CardBrush\" Color=\"{ToHex(colors[1])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"PrimaryBrush\" Color=\"{ToHex(colors[2])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"TextBrush\" Color=\"{ToHex(colors[3])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"MutedTextBrush\" Color=\"{ToHex(colors[4])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"BackgroundBrush\" Color=\"{ToHex(colors[0])}\"/>");
            sb.AppendLine($"  <SolidColorBrush x:Key=\"BorderBrushColor\" Color=\"{ToHex(borderColor)}\"/>");
            sb.AppendLine("</ResourceDictionary>");

            System.IO.File.WriteAllText(path, sb.ToString());
        }

        private void LoadSavedThemes()
        {
            try
            {
                string themesDir = System.IO.Path.Combine(AppDataService.Configuration.StoragePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Themes");
                if (!Directory.Exists(themesDir)) return;
                var files = Directory.GetFiles(themesDir, "*.xaml");
                SavedThemesListBox.ItemsSource = files.Select(f => System.IO.Path.GetFileName(f)).ToList();
            }
            catch { }
        }

        private void LoadSelectedTheme_Click(object sender, RoutedEventArgs e)
        {
            if (SavedThemesListBox.SelectedItem == null) return;
            string file = SavedThemesListBox.SelectedItem.ToString()!;
            string themesDir = System.IO.Path.Combine(AppDataService.Configuration.StoragePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Themes");
            string path = System.IO.Path.Combine(themesDir, file);
            if (File.Exists(path))
            {
                ThemeManager.ApplyTheme(path);
                AppDataService.Configuration.Theme = path;
                AppDataService.Save();
                MessageBox.Show("Tema cargado.", "Tema", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteSelectedTheme_Click(object sender, RoutedEventArgs e)
        {
            if (SavedThemesListBox.SelectedItem == null) return;
            string file = SavedThemesListBox.SelectedItem.ToString()!;
            string themesDir = System.IO.Path.Combine(AppDataService.Configuration.StoragePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Themes");
            string path = System.IO.Path.Combine(themesDir, file);
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
                LoadSavedThemes();
            }
        }

        private void RenameSelectedTheme_Click(object sender, RoutedEventArgs e)
        {
            if (SavedThemesListBox.SelectedItem == null) return;
            string current = SavedThemesListBox.SelectedItem.ToString()!;
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = current, Filter = "XAML files (*.xaml)|*.xaml" };
            string themesDir = System.IO.Path.Combine(AppDataService.Configuration.StoragePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Themes");
            dlg.InitialDirectory = themesDir;
            if (dlg.ShowDialog() == true)
            {
                try { File.Move(System.IO.Path.Combine(themesDir, current), dlg.FileName); } catch { }
                LoadSavedThemes();
            }
        }
    }
}