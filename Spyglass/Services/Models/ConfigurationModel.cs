using System.ComponentModel;
using Newtonsoft.Json;

namespace Spyglass.Services.Models
{
    public class ConfigurationModel
    {
        [JsonProperty]
        [DefaultValue("")]
        public string Token { get; private set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong MainGuildId { get; set; }
        
        [JsonProperty]
        [DefaultValue(false)]
        public bool ReactionRolesEnabled { get; set; }
        
        [JsonProperty]
        [DefaultValue(false)]
        public bool EntryGateEnabled { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong EntryGateRoleId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong EntryGateChannelId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong EntryGateMessageId { get; set; }
        
        [JsonProperty]
        [DefaultValue(false)]
        public bool ModMailEnabled { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong ModMailServerId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong ModMailUnansweredCategoryId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong ModMailAnsweredCategoryId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong InfractionLogChannelId { get; set; }
        
        [JsonProperty]
        [DefaultValue(0)]
        public ulong MutedRoleId { get; set; }
    }
}