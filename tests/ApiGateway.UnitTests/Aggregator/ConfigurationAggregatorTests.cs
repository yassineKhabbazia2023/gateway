using ApiGateway.Aggregator;
using ApiGateway.Aggregator.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocelot.Middleware;
using System.Net;
using Action = ApiGateway.Aggregator.Models.Action;

namespace ApiGateway.UnitTests.Aggregator
{
    public class ConfigurationAggregatorTests
    {
      
        [Fact]
        public async Task Aggregate_ShouldAggregateResponsesCorrectly()
        {
            // Arrange
            var httpContext1 = new DefaultHttpContext();
            var httpContext2 = new DefaultHttpContext();

            var permissions1 = new List<ApiGateway.Aggregator.Models.Configuration>
        {
            new ApiGateway.Aggregator.Models.Configuration
            {
                Category = "Category1",
                Actions = new List<Action>
                {
                    new Action { ActionId = 1, Code = "A1", Label = "Action 1", Enabled = true },
                    new Action { ActionId = 2, Code = "A2", Label = "Action 2", Enabled = false }
                }
            }
        };

            var permissions2 = new List<ApiGateway.Aggregator.Models.Configuration>
        {
            new ApiGateway.Aggregator.Models.Configuration
            {
                Category = "Category2",
                Actions = new List<Action>
                {
                    new Action { ActionId = 3, Code = "A3", Label = "Action 3", Enabled = true },
                    new Action { ActionId = 4, Code = "A4", Label = "Action 4", Enabled = false }
                }
            }
        };

            var response1 = new DownstreamResponse
            (
                new StringContent(JsonConvert.SerializeObject(permissions1)),
                HttpStatusCode.OK,
                new List<Header>{},
                "reason"
            );
            var response2 = new DownstreamResponse
            (
                new StringContent(JsonConvert.SerializeObject(permissions2)),
                HttpStatusCode.OK,
                new List<Header> { },
                "reason"
            );

            httpContext1.Items["DownstreamResponse"] = response1;
            httpContext2.Items["DownstreamResponse"] = response2;

            var permissionAggregator = new ConfigurationAggregator();

            // Act
            var result = await permissionAggregator.Aggregate(new List<HttpContext> { httpContext1, httpContext2 });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            var resultPermissions = JsonConvert.DeserializeObject<List<ApiGateway.Aggregator.Models.Configuration>>(resultContent);

            Assert.Equal(2, resultPermissions.Count);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            Assert.Equal(4, resultPermissions.SelectMany(p => p.Actions).Count());
        }

        [Fact]
        public async Task Aggregate_ShouldHandleInvalidJsonGracefully()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            var invalidResponse = new DownstreamResponse
            (
                 new StringContent("Invalid JSON"),
                HttpStatusCode.OK,
                new List<Header> { },
                "reason"
            );

            httpContext.Items["DownstreamResponse"] = invalidResponse;

            var permissionAggregator = new ConfigurationAggregator();

            // Act & Assert
            await Assert.ThrowsAsync<JsonReaderException>(() => permissionAggregator.Aggregate(new List<HttpContext> { httpContext }));
        }
    }
}
