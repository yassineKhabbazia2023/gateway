using ApiGateway.Authorization;
using ApiGateway.Constants;
using ApiGateway.UnitTests.Mocks;
using System.Net;

namespace ApiGateway.UnitTests.DelegatingHandlers
{
    public class FeedCenterSettingsHandlerTest
    {
        private readonly FakeFeedCenterSettingsHandler _fakeFeedCenterSettingsHandler;
        private readonly MockHttpMessageHandler _mockHttpMessageHandler;
        private readonly Mock<IAuthorizationService> mockAuthorizationService = new Mock<IAuthorizationService>();

        public FeedCenterSettingsHandlerTest()
        {
            var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Test response")
            };
            _mockHttpMessageHandler = new MockHttpMessageHandler(expectedResponse);
            _fakeFeedCenterSettingsHandler = new FakeFeedCenterSettingsHandler(_mockHttpMessageHandler, mockAuthorizationService.Object);
        }

        [Fact]
        public async Task Should_Add_CurrentUserPermissionCode_IntoHeader_WhenContactIdIsValid()
        {
            //Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "https://contact-domain.api/contacts?contactId=2");
           
            var expectedPermissions = "CODE001,CODE002";
            var apiResponse = expectedPermissions.Split(",").ToList();
            mockAuthorizationService.Setup(m => m.GetAllContactAuthorizationsAsync(2)).ReturnsAsync(apiResponse);
            //Act
            var response = await _fakeFeedCenterSettingsHandler.FakeSendAsync(request, CancellationToken.None);

            //Assert
            request.Headers.Contains(GlobalsConstants.UserPermissionsHeader).Should().BeTrue($"the header {GlobalsConstants.UserPermissionsHeader} should be present");
            request.Headers.GetValues(GlobalsConstants.UserPermissionsHeader).FirstOrDefault().Should().Be(expectedPermissions, $"the '{GlobalsConstants.UserPermissionsHeader}' header should match the specified value : {expectedPermissions}");
        }
    }
}
