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

namespace SteamDeckProtonDb
{
    public partial class SteamDeckProtonDbSettingsView : UserControl
    {
        public SteamDeckProtonDbSettingsView()
        {
            // Provide a safe default DataContext so bindings in XAML can be evaluated during design/runtime load
            try
            {
                this.DataContext = new SteamDeckProtonDbSettings();
            }
            catch { }

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                try { Playnite.SDK.LogManager.GetLogger().Error("Settings view ctor failed: " + ex.ToString()); } catch { }
                throw;
            }
        }
    }
}