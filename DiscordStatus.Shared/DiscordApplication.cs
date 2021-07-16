using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordStatus
{
    public class DiscordApplication
    {
        [JsonProperty(PropertyName="id")]
        public string ID { get; set; }
        [JsonProperty(PropertyName="name")]
        public string Name { get; set; }

        public override string ToString()
        {
            return $"{ID}: {Name}";
        }
    }
}
