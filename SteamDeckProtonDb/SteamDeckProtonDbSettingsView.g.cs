using System;
using System.Windows;

namespace SteamDeckProtonDb
{
    public partial class SteamDeckProtonDbSettingsView
    {
        // Fallback InitializeComponent to load the XAML at runtime when XAML compilation
        // doesn't generate the usual method during build in some environments.
        public void InitializeComponent()
        {
            var uri = new Uri("/SteamDeckProtonDb;component/SteamDeckProtonDbSettingsView.xaml", UriKind.Relative);
            Application.LoadComponent(this, uri);
        }
    }
}
