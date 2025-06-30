using ApiGateway.Aggregator.NotificationSettings;
using ApiGateway.Aggregator.NotificationSettings.Models;
using ApiGateway.Aggregator.NotificationSettings.Models.ApiResponse;
using Microsoft.AspNetCore.Http;
using Ocelot.Configuration;
using Ocelot.Configuration.Creator;
using Ocelot.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ApiGateway.UnitTests.Aggregator
{
    public class NotificationSettingsAggregatorTests
    {
        private JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        [Fact]
        public async Task Aggregate_ShouldMergeFeedcenterWithEmailProperly()
        {
            // Arrange
            var httpContextFeedcenter = new DefaultHttpContext();
            var httpContextEmail = new DefaultHttpContext();

            var feedcenterPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting (Id: 2, Label: "Doc A", Value: true)
            })
                },
                new PreferenceSettings("label",false)
            );

            var emailPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting (Id: 2, Label: "Doc A", Value: false)
            })
                },
                new PreferenceSettings("label",false)
            );

            httpContextFeedcenter.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(feedcenterPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextFeedcenter.Items["DownstreamRoute"] = CreateRouteWithKey("feedcenter");

            httpContextEmail.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(emailPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextEmail.Items["DownstreamRoute"] = CreateRouteWithKey("email");

            var aggregator = new NotificationSettingsAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextFeedcenter, httpContextEmail });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            var final = JsonSerializer.Deserialize<SettingsResult>(resultContent, jsonOptions);

            Assert.NotNull(final);
            Assert.Single(final.DomainSettings);
            Assert.Equal("Documents", final.DomainSettings[0].Domain);
            Assert.Single(final.DomainSettings[0].Settings);

            var setting = final.DomainSettings[0].Settings[0];
            Assert.Equal(2, setting.Id);
            Assert.True(setting.FeedcenterValue);
            Assert.False(setting.EmailValue);
        }

        [Fact]
        public async Task Aggregate_ShouldNotAddEmailValue_WhenIdMissingInEmailDownstream()
        {
            // Arrange
            var httpContextFeedcenter = new DefaultHttpContext();
            var httpContextEmail = new DefaultHttpContext();

            var feedcenterPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting(Id: 2, Label: "Doc A", Value: true),
                new RawSetting(Id: 3, Label: "Doc B", Value: true)
            })
                },
                new PreferenceSettings("label", false)
            );

            var emailPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting(Id: 2, Label: "Doc A", Value: false)
            })
                },
                new PreferenceSettings("label", false)
            );

            httpContextFeedcenter.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(feedcenterPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextFeedcenter.Items["DownstreamRoute"] = CreateRouteWithKey("feedcenter");

            httpContextEmail.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(emailPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextEmail.Items["DownstreamRoute"] = CreateRouteWithKey("email");

            var aggregator = new NotificationSettingsAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextFeedcenter, httpContextEmail });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            var final = JsonSerializer.Deserialize<SettingsResult>(resultContent, jsonOptions);

            Assert.NotNull(final);
            Assert.Single(final.DomainSettings);
            Assert.Equal("Documents", final.DomainSettings[0].Domain);
            Assert.Equal(2, final.DomainSettings[0].Settings.Count);

            var setting2 = final.DomainSettings[0].Settings.First(s => s.Id == 2);
            Assert.True(setting2.FeedcenterValue);
            Assert.False(setting2.EmailValue);

            var setting3 = final.DomainSettings[0].Settings.First(s => s.Id == 3);
            Assert.True(setting3.FeedcenterValue);
            Assert.Null(setting3.EmailValue); // Email ne contient pas cet ID
        }


        [Fact]
        public async Task Aggregate_ShouldIncludeSettingsFromEmailOnlyWithNullFeedcenterValue()
        {
            // Arrange
            var httpContextFeedcenter = new DefaultHttpContext();
            var httpContextEmail = new DefaultHttpContext();

            var feedcenterPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting(Id: 1, Label: "Doc A", Value: true)
            })
                },
                new PreferenceSettings("label", false)
            );

            var emailPayload = new RawResponse(
                new List<RawDomainSetting>
                {
            new("Documents", new List<RawSetting>
            {
                new RawSetting(Id: 1, Label: "Doc A", Value: false),
                new RawSetting(Id: 99, Label: "Doc B", Value: true) // N'existe que dans email
            })
                },
                new PreferenceSettings("label",false)
            );

            httpContextFeedcenter.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(feedcenterPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextFeedcenter.Items["DownstreamRoute"] = CreateRouteWithKey("feedcenter");

            httpContextEmail.Items["DownstreamResponse"] = new DownstreamResponse(
                new StringContent(JsonSerializer.Serialize(emailPayload, jsonOptions), Encoding.UTF8, "application/json"),
                HttpStatusCode.OK, new List<Header>(), "OK");

            httpContextEmail.Items["DownstreamRoute"] = CreateRouteWithKey("email");

            var aggregator = new NotificationSettingsAggregator();

            // Act
            var result = await aggregator.Aggregate(new List<HttpContext> { httpContextFeedcenter, httpContextEmail });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            var final = JsonSerializer.Deserialize<SettingsResult>(resultContent, jsonOptions);

            Assert.NotNull(final);
            Assert.Single(final.DomainSettings);
            Assert.Equal("Documents", final.DomainSettings[0].Domain);
            Assert.Equal(2, final.DomainSettings[0].Settings.Count);

            var setting1 = final.DomainSettings[0].Settings.First(s => s.Id == 1);
            Assert.True(setting1.FeedcenterValue);
            Assert.False(setting1.EmailValue);

            var setting99 = final.DomainSettings[0].Settings.First(s => s.Id == 99);
            Assert.Null(setting99.FeedcenterValue); // N'existe pas dans feedcenter
            Assert.True(setting99.EmailValue);
        }


        private DownstreamRoute CreateRouteWithKey(string key)
        {
            return new DownstreamRoute(
                key,
                null,
                new List<HeaderFindAndReplace>(),
                new List<HeaderFindAndReplace>(),
                new List<DownstreamHostAndPort>(),
                null, null,
                null,
                false,
                false,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                new Dictionary<string, string>(),
                new List<ClaimToThing>(),
                new List<ClaimToThing>(),
                new List<ClaimToThing>(),
                new List<ClaimToThing>(),
                false,
                false,
                null,
                null,
                null,
                new List<string>(),
                new List<AddHeader>(),
                new List<AddHeader>(),
                false,
                null,
                null,
                new Version(1, 1)
            );
        }
    }
}
