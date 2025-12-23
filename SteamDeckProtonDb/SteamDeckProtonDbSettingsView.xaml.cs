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

namespace SteamDeckProtonDb
{
    public partial class SteamDeckProtonDbSettingsView : UserControl
    {
        public SteamDeckProtonDbSettingsView()
        {
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
                                return;
                            }
                            else if (loaded is UIElement el)
                            {
                                Content = el;
                                return;
                            }
                        }
                    }
                }
                catch
                {
                }

                // As a last resort, create minimal placeholder to avoid crashing settings
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "Failed to load settings view.", FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) },
                        new TextBlock { Text = "Please rebuild or ensure XAML file is present in output folder.", FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray }
                    }
                };
            }
        }
    }
}
