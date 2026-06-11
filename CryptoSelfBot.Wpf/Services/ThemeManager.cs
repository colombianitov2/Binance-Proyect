using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Windows.Media;

namespace CryptoSelfBot.Wpf.Services
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themePath)
        {
            if (string.IsNullOrEmpty(themePath)) return;

            try
            {
                // Intentar diferentes UriKinds y formatos (pack URI para recursos embebidos o rutas relativas)
                Uri uri;
                if (themePath.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) ||
                    themePath.StartsWith("/") || themePath.Contains(".xaml"))
                {
                    uri = new Uri(themePath, UriKind.RelativeOrAbsolute);
                }
                else
                {
                    uri = new Uri(themePath, UriKind.Relative);
                }

                var theme = new System.Windows.ResourceDictionary { Source = uri };
                ApplyCanonicalBrushAliases(theme);

                // Mantener recursos existentes y añadir el nuevo al inicio para permitir overrides
                var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
                // No limpiar todo: reemplazar solo diccionarios con la misma Source si existen
                try
                {
                    if (uri != null && !string.IsNullOrEmpty(uri.OriginalString))
                    {
                        var toRemove = merged.FirstOrDefault(d => d.Source != null && string.Equals(d.Source.OriginalString, uri.OriginalString, StringComparison.OrdinalIgnoreCase));
                        if (toRemove != null) merged.Remove(toRemove);
                    }
                    merged.Add(theme);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Warning applying theme dictionary: " + ex.Message);
                }
                Debug.WriteLine($"Theme applied: {themePath}");
            }
            catch (Exception ex)
            {
                // Reportar a salida de depuración y consola para facilitar detección de errores en runtime
                Debug.WriteLine($"Failed to apply theme '{themePath}': {ex}");
                try
                {
                    Console.WriteLine($"[ThemeManager] Failed to apply theme '{themePath}': {ex.Message}");
                }
                catch { }
            }
        }

        private static void ApplyCanonicalBrushAliases(System.Windows.ResourceDictionary theme)
        {
            var app = System.Windows.Application.Current;
            SolidColorBrush? Brush(params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (theme.Contains(key) && theme[key] is SolidColorBrush brush)
                        return brush;
                }
                return null;
            }

            void Set(string targetKey, SolidColorBrush? source)
            {
                if (source == null) return;
                app.Resources[targetKey] = new SolidColorBrush(source.Color);
            }

            Set("WindowBackgroundBrush", Brush("WindowBackgroundBrush", "PrimaryBackgroundBrush", "PrimaryBackground"));
            Set("BackgroundBrush", Brush("BackgroundBrush", "PrimaryBackgroundBrush", "PrimaryBackground"));
            Set("CardBackgroundBrush", Brush("CardBackgroundBrush", "SecondaryBackgroundBrush", "SecondaryBackground", "CardBrush"));
            Set("CardBrush", Brush("CardBrush", "SecondaryBackgroundBrush", "SecondaryBackground"));
            Set("PrimaryBrush", Brush("PrimaryBrush", "AccentBlueBrush", "AccentColor"));
            Set("AccentBrush", Brush("AccentBrush", "AccentOrangeBrush", "AccentColor"));
            Set("TextBrush", Brush("TextBrush", "PrimaryTextBrush", "TextPrimary"));
            Set("MutedTextBrush", Brush("MutedTextBrush", "SecondaryTextBrush", "TextSecondary"));
            Set("BorderBrushColor", Brush("BorderBrushColor", "BorderColor", "AccentColor"));
        }

        // Persistir/leer temas simples en %AppData%/CryptoSelfBot/themes
        public static string GetThemesFolder()
        {
            try
            {
                // Prefer user-selected storage path if configured
                string? storage = AppDataService.Configuration?.StoragePath;
                if (!string.IsNullOrEmpty(storage))
                {
                    var dir = Path.Combine(storage, "Themes");
                    Directory.CreateDirectory(dir);
                    return dir;
                }
            }
            catch { }

            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptoSelfBot", "themes");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        public static void SaveThemePalette(string name, Color[] palette)
        {
            try
            {
                var dir = GetThemesFolder();
                // Save JSON metadata
                var obj = new ThemePalette { Name = name, Colors = palette.Select(c => c.ToString()).ToArray() };
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(dir, name + ".json"), json);

                // Also emit a XAML ResourceDictionary for direct application
                string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n");
                sb.AppendLine($"  <SolidColorBrush x:Key=\"WindowBackgroundBrush\" Color=\"{ToHex(palette[0])}\"/>");
                sb.AppendLine($"  <SolidColorBrush x:Key=\"CardBackgroundBrush\" Color=\"{ToHex(palette[1])}\"/>");
                sb.AppendLine($"  <SolidColorBrush x:Key=\"PrimaryBrush\" Color=\"{ToHex(palette[2])}\"/>");
                sb.AppendLine($"  <SolidColorBrush x:Key=\"TextBrush\" Color=\"{ToHex(palette[3])}\"/>");
                sb.AppendLine($"  <SolidColorBrush x:Key=\"MutedTextBrush\" Color=\"{ToHex(palette[4])}\"/>");
                sb.AppendLine("</ResourceDictionary>");
                File.WriteAllText(Path.Combine(dir, name + ".xaml"), sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save theme: " + ex.Message);
            }
        }

        public static List<string> LoadSavedThemes()
        {
            var list = new List<string>();
            try
            {
                var dir = GetThemesFolder();
                // Prefer XAML theme files for direct application
                foreach (var f in Directory.EnumerateFiles(dir, "*.xaml")) list.Add(f);
                // Also include any JSON metadata (optional)
                foreach (var f in Directory.EnumerateFiles(dir, "*.json")) if (!list.Contains(f)) list.Add(f);
            }
            catch { }
            return list;
        }

        private class ThemePalette { public string Name { get; set; } = ""; public string[] Colors { get; set; } = Array.Empty<string>(); }
    }
}
