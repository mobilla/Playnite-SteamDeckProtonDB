using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDbProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly SteamDeckProtonDb plugin;

        public override List<MetadataField> AvailableFields => throw new NotImplementedException();

        public SteamDeckProtonDbProvider(MetadataRequestOptions options, SteamDeckProtonDb plugin)
        {
            this.options = options;
            this.plugin = plugin;
        }

        // Override additional methods based on supported metadata fields.
        public override string GetDescription(GetMetadataFieldArgs args)
        {
            return options.GameData.Name + " description";
        }
    }
}