using System;
using System.Net;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;

namespace AcManager.Tools.Helpers.Api {
    public class IpGeoEntry {
        [JsonProperty(@"ip")]
        public string Ip;

        [JsonProperty(@"city")]
        public string City;

        [JsonProperty(@"region")]
        public string Region;

        [JsonProperty(@"country")]
        public string Country;

        [JsonProperty(@"loc")]
        public string Location;

        [JsonProperty(@"postal")]
        public string Postal;
    }

    public static class IpGeoProvider {
        private const string RequestUri = "http://ipinfo.io/geo";

        public static IpGeoEntry Get() {
            const string requestUri = RequestUri;
            try {
                var httpRequest = WebRequest.Create(requestUri);
                httpRequest.Method = "GET";
                using (var response = (HttpWebResponse)httpRequest.GetResponse()) {
                    return response.StatusCode != HttpStatusCode.OK
                            ? null : JsonConvert.DeserializeObject<IpGeoEntry>(response.GetResponseStream()?.ReadAsStringAndDispose());
                }
            } catch (Exception e) {
                Logging.Warning($"Cannot determine location: {requestUri}\n{e}");
                return null;
            }
        }
    }
}