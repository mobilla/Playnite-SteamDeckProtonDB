using System;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.IO;
using System.Windows.Markup;
using System.Xml;
using System.Xaml;
using Playnite.SDK;

namespace SteamDeckProtonDb
{
    public partial class SteamDeckProtonDbSettingsView
    {
        // Fallback InitializeComponent to load the XAML at runtime when XAML compilation
        // doesn't generate the usual method during build in some environments.
        public void InitializeComponent()
        {
            try
            {
                var uri = new Uri("/SteamDeckProtonDb;component/SteamDeckProtonDbSettingsView.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
                return;
            }
            catch (Exception ex)
            {
                try { LogManager.GetLogger().Error("Failed to load Settings XAML (primary): " + ex.ToString()); } catch { }
                // Try alternative pack URIs and log available resources for diagnosis
                try
                {
                    var asm = typeof(SteamDeckProtonDbSettingsView).GetTypeInfo().Assembly;
                    var names = asm.GetManifestResourceNames();
                    try { LogManager.GetLogger().Debug("Assembly resources: " + string.Join(",", names)); } catch { }

                    var candidates = new[] {
                        "/SteamDeckProtonDb;component/SteamDeckProtonDbSettingsView.xaml",
                        "/steamdeckprotondb;component/SteamDeckProtonDbSettingsView.xaml",
                        "/" + asm.GetName().Name + ";component/SteamDeckProtonDbSettingsView.xaml",
                        "/" + asm.GetName().Name.ToLowerInvariant() + ";component/SteamDeckProtonDbSettingsView.xaml"
                    };

                    foreach (var c in candidates)
                    {
                        try
                        {
                            var uri = new Uri(c, UriKind.Relative);
                            Application.LoadComponent(this, uri);
                            try { LogManager.GetLogger().Debug("Loaded Settings XAML using alternative URI: " + c); } catch { }
                            return;
                        }
                        catch (Exception inner) { try { LogManager.GetLogger().Debug("Alt URI failed: " + c + " -> " + inner.Message); } catch { } }
                    }

                    // As a last resort try loading the XAML file from the assembly directory (useful during development)
                    try
                    {
                        var asmLocation = typeof(SteamDeckProtonDbSettingsView).GetTypeInfo().Assembly.Location;
                        var asmDir = Path.GetDirectoryName(asmLocation);
                        var xamlPath = Path.Combine(asmDir, "SteamDeckProtonDbSettingsView.xaml");
                        if (File.Exists(xamlPath))
                        {
                            try
                            {
                                var xamlText = File.ReadAllText(xamlPath);
                                // Remove x:Class attribute to avoid class mismatch when loading at runtime
                                try
                                {
                                    xamlText = System.Text.RegularExpressions.Regex.Replace(xamlText, @"\s+x:Class=""[^"" ]*""", string.Empty);
                                }
                                catch { }

                                using (var sr = new StringReader(xamlText))
                                using (var xr = XmlReader.Create(sr))
                                {
                                    var loaded = System.Windows.Markup.XamlReader.Load(xr);
                                    if (loaded is UserControl uc)
                                    {
                                        this.Content = uc.Content as UIElement;
                                    }
                                    else if (loaded is UIElement el)
                                    {
                                        this.Content = el;
                                    }
                                    try { LogManager.GetLogger().Debug("Loaded Settings XAML from sanitized file: " + xamlPath); } catch { }
                                    return;
                                }
                            }
                            catch (Exception fileEx) { try { LogManager.GetLogger().Debug("Loading XAML from file failed: " + fileEx.ToString()); } catch { } }
                        }
                        else
                        {
                            try { LogManager.GetLogger().Debug("XAML file not found at: " + xamlPath); } catch { }
                        }
                    }
                    catch { }
                }
                catch { }

                throw;
            }
        }
    }
}
