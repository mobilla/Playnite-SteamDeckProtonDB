using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDb : MetadataPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SteamDeckProtonDbSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("78f86cf5-abfd-47e9-b753-6b81c29132ed");

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Tags,
            MetadataField.Links
        };

        public override string Name => "Steam Deck & ProtonDB";

        public SteamDeckProtonDb(IPlayniteAPI api) : base(api)
        {
            settings = new SteamDeckProtonDbSettings(this);
            Properties = new MetadataPluginProperties
            {
                HasSettings = true
            };
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new SteamDeckProtonDbProvider(options, this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
                // Ensure the view has the plugin settings as DataContext so bindings work
                var view = new SteamDeckProtonDbSettingsView();
                view.DataContext = settings;
                return view;
        }
    }
}