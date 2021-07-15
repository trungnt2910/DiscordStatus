using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace DiscordStatus
{
    public class Asset
    {
        [JsonProperty(PropertyName = "id")]
        public string ID { get; set; }
        [JsonProperty(PropertyName = "type")]
        public int Type { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        public override string ToString()
        {
            return $"{ID}: {Name}";
        }
    }
}
