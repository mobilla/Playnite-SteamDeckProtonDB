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

        private SteamDeckProtonDbSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("78f86cf5-abfd-47e9-b753-6b81c29132ed");

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Description
            // Include addition fields if supported by the metadata source
        };

        // Change to something more appropriate
        public override string Name => "Custom Metadata";

        public SteamDeckProtonDb(IPlayniteAPI api) : base(api)
        {
            settings = new SteamDeckProtonDbSettingsViewModel(this);
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
            return new SteamDeckProtonDbSettingsView();
        }
    }
}