using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Windows.Markup;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Playnite.SDK;

namespace SteamDeckProtonDb
{
    public partial class SteamDeckProtonDbSettingsView : UserControl
    {
        private readonly SteamDeckProtonDb plugin;

        public SteamDeckProtonDbSettingsView(SteamDeckProtonDb plugin = null)
        {
            this.plugin = plugin;
            // Try to load via pack URI (compiled resource)
            try
            {
                var uri = new Uri("/SteamDeckProtonDb;component/SteamDeckProtonDbSettingsView.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
                return;
            }
            catch
            {
                // Fallback: load raw XAML from output directory (works with dotnet build)
                try
                {
                    var asmLocation = typeof(SteamDeckProtonDbSettingsView).GetTypeInfo().Assembly.Location;
                    var asmDir = System.IO.Path.GetDirectoryName(asmLocation);
                    var xamlPath = System.IO.Path.Combine(asmDir ?? string.Empty, "SteamDeckProtonDbSettingsView.xaml");
                    if (File.Exists(xamlPath))
                    {
                        var xamlText = File.ReadAllText(xamlPath);
                        // Remove x:Class to prevent recursion and type binding issues when loading at runtime
                        try { xamlText = Regex.Replace(xamlText, "\\s+x:Class=\\\"[^\\\"]*\\\"", string.Empty); } catch { }

                        using (var sr = new StringReader(xamlText))
                        using (var xr = XmlReader.Create(sr))
                        {
                            var loaded = XamlReader.Load(xr);
                            if (loaded is UserControl uc && uc.Content is UIElement ui)
                            {
                                Content = ui;
                                WireUpEventHandlers(ui);
                                return;
                            }
                            else if (loaded is UIElement el)
                            {
                                Content = el;
                                WireUpEventHandlers(el);
                                return;
                            }
                        }
                    }
                }
                catch
                {
                }

                // As a last resort, create minimal placeholder to avoid crashing settings
                var fallbackButton = new Button
                {
                    Content = "Open Cache Directory",
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 5, 10, 5)
                };
                fallbackButton.Click += OpenCacheDirectory_Click;

                Content = new StackPanel
                {
                    Margin = new Thickness(12),
                    Children =
                    {
                        new TextBlock { Text = "Failed to load settings view.", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) },
                        new TextBlock { Text = "Please rebuild or ensure XAML file is present in output folder.", FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0,0,0,12) },
                        new TextBlock { Text = "Cache Directory", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0,8,0,8) },
                        fallbackButton
                    }
                };
            }
        }

        private void WireUpEventHandlers(UIElement root)
        {
            // Find the buttons by name in the visual tree (should work within TabControl)
            var openButton = FindVisualChild<Button>(root, b => 
                (b as FrameworkElement)?.Name == "OpenCacheDirectoryButton");
            if (openButton != null)
            {
                openButton.Click += OpenCacheDirectory_Click;
            }

            var clearButton = FindVisualChild<Button>(root, b => 
                (b as FrameworkElement)?.Name == "ClearCacheButton");
            if (clearButton != null)
            {
                clearButton.Click += ClearCache_Click;
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> predicate = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild && (predicate == null || predicate(typedChild)))
                {
                    return typedChild;
                }

                var result = FindVisualChild(child, predicate);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OpenCacheDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (plugin == null)
                {
                    MessageBox.Show("Plugin reference not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var cacheDir = System.IO.Path.Combine(plugin.GetPluginUserDataPath(), "cache");
                
                // Create directory if it doesn't exist
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                // Open directory in file explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = cacheDir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open cache directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (plugin == null)
                {
                    MessageBox.Show("Plugin reference not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show(
                    "Are you sure you want to clear the cache? This will delete all cached Steam Deck and ProtonDB data.",
                    "Clear Cache",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                var cacheDir = System.IO.Path.Combine(plugin.GetPluginUserDataPath(), "cache");
                
                if (Directory.Exists(cacheDir))
                {
                    int filesDeleted = 0;
                    foreach (var file in Directory.GetFiles(cacheDir, "*.json"))
                    {
                        File.Delete(file);
                        filesDeleted++;
                    }

                    MessageBox.Show($"Cache cleared successfully. {filesDeleted} file(s) deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Cache directory does not exist.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
