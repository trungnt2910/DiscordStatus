using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace DiscordStatus
{
    public static class Helpers
    {
        public static HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string url)
        {
#if __WASM__
            url = "https://cors.bridged.cc/" + url;
#endif
            return new HttpRequestMessage(method, url);
        }
    }
}
