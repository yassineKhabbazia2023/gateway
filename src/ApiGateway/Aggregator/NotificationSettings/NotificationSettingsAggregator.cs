using ApiGateway.Aggregator.NotificationSettings.Models;
using ApiGateway.Aggregator.NotificationSettings.Models.ApiResponse;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocelot.Configuration;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Text;

namespace ApiGateway.Aggregator.NotificationSettings
{
    public class NotificationSettingsAggregator : IDefinedAggregator
    {
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var isTokenExpired = responses.Any(c => c.Items.Errors().Any(e => e.HttpStatusCode == (int)HttpStatusCode.Unauthorized));
            if (isTokenExpired)
            {
                return new DownstreamResponse(null, HttpStatusCode.Unauthorized,new List<Header>(), "reason");
            }
            var feedcenterSettings = await ExtractSettingsForKeyAsync(responses, "feedcenter");
            var emailSettings = await ExtractSettingsForKeyAsync(responses, "email");

            var mergedSettings = MergeSettings(feedcenterSettings, emailSettings);

            var json = JsonConvert.SerializeObject(mergedSettings, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            });

            return new DownstreamResponse(
                new StringContent(json, Encoding.UTF8, "application/json"),
                HttpStatusCode.OK,
                new List<Header>(),
                "OK");
        }

        private async Task<SettingsResult?> ExtractSettingsForKeyAsync(List<HttpContext> contexts, string key)
        {
            var context = contexts.FirstOrDefault(c =>
                c.Items.TryGetValue("DownstreamRoute", out var routeObj) &&
                routeObj is DownstreamRoute route &&
                route.Key.Equals(key,StringComparison.InvariantCultureIgnoreCase));

            if (context == null)
                return null;
            var downstreamResponse = context.Items.DownstreamResponse();

            if (downstreamResponse.StatusCode < HttpStatusCode.OK || downstreamResponse.StatusCode >= HttpStatusCode.MultipleChoices)
            {
                return null;
            }

            var body = await downstreamResponse.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body))
                return null;

            var rawResponse = JsonConvert.DeserializeObject<RawResponse>(body);

            if (rawResponse == null)
                return null;

            return ConvertRawResponseToSettingsResult(rawResponse, key);
        }

        private SettingsResult ConvertRawResponseToSettingsResult(RawResponse rawResponse, string sourceKey)
        {
            var domainSettings = rawResponse.DomainSettings
                .Select(domain => new DomainSetting(domain.Domain,
                    domain.Settings.Select(setting => new Setting
                    {
                        Id = setting.Id,
                        Label = setting.Label,
                        FeedcenterValue = sourceKey == "feedcenter" ? setting.Value : null,
                        EmailValue = sourceKey == "email" ? setting.Value : null
                    }).ToList()))
                .ToList();

            return new SettingsResult(domainSettings, rawResponse.PreferenceSettings);
        }

        private SettingsResult MergeSettings(SettingsResult? feedcenter, SettingsResult? email)
        {
            var mergedDomains = new Dictionary<string, DomainSetting>();

            AddOrMergeDomainSettings(mergedDomains, feedcenter, isFeedcenter: true);
            AddOrMergeDomainSettings(mergedDomains, email, isFeedcenter: false);

            var preferenceSettings = feedcenter?.PreferenceSettings ?? email?.PreferenceSettings ?? new PreferenceSettings(string.Empty,false);

            return new SettingsResult(mergedDomains.Values.ToList(), preferenceSettings);
        }

        private void AddOrMergeDomainSettings(Dictionary<string, DomainSetting> mergedDomains, SettingsResult? sourceSettings, bool isFeedcenter)
        {
            if (sourceSettings == null)
                return;

            foreach (var domain in sourceSettings.DomainSettings)
            {
                if (!mergedDomains.TryGetValue(domain.Domain, out var mergedDomain))
                {
                    mergedDomain = new DomainSetting(domain.Domain, new List<Setting>());
                    mergedDomains[domain.Domain] = mergedDomain;
                }

                foreach (var setting in domain.Settings)
                {
                    var existingSetting = mergedDomain.Settings.FirstOrDefault(s => s.Id == setting.Id);
                    if (existingSetting == null)
                    {
                        existingSetting = new Setting
                        {
                            Id = setting.Id,
                            Label = setting.Label,
                            FeedcenterValue = null,
                            EmailValue = null
                        };
                        mergedDomain.Settings.Add(existingSetting);
                    }

                    if (isFeedcenter)
                        existingSetting.FeedcenterValue = setting.FeedcenterValue;
                    else
                        existingSetting.EmailValue = setting.EmailValue;
                }
            }
        }
    }
}
