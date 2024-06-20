using ApiGateway.Aggregator.Models;
using ApiGateway.Aggregrator;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Ocelot.Middleware;
using System.Net;
using Action = ApiGateway.Aggregator.Models.Action;

namespace ApiGateway.UnitTests.Aggregator
{
    public class PermissionAggregatorTests
    {
        [Fact]
        public async Task Aggregate_ShouldAggregateResponsesCorrectly()
        {
            // Arrange
            var httpContext1 = new DefaultHttpContext();
            var httpContext2 = new DefaultHttpContext();

            var permissions1 = new string[] { "abc", "xyz" };

            var permissions2 = new string[] { "toto", "titi" };

            var response1 = new DownstreamResponse
            (
                new StringContent(JsonConvert.SerializeObject(permissions1)),
                HttpStatusCode.OK,
                new List<Header> { },
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

            var permissionAggregator = new PermissionAggregator();

            // Act
            var result = await permissionAggregator.Aggregate(new List<HttpContext> { httpContext1, httpContext2 });

            // Assert
            var resultContent = await result.Content.ReadAsStringAsync();
            var resultPermissions = JsonConvert.DeserializeObject<List<string>>(resultContent);

            Assert.Equal(4, resultPermissions.Count);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
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
