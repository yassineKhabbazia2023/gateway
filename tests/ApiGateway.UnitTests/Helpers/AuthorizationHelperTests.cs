using ApiGateway.Helpers;
using ApiGateway.Authorization;
using ApiGateway.Constants;
using Microsoft.AspNetCore.Http;
using ApiGateway.Account;
using System.Collections.Specialized;

namespace ApiGateway.UnitTests.Helpers
{
  public class AuthorizationHelperTests
  {
    private readonly Mock<IAuthorizationSevice> _mockAuthorizationService;
    private readonly Mock<IAccountService> _mockAccountService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly HttpContext _httpContext;

    public AuthorizationHelperTests()
    {
      // Initialize mocks
      _mockAuthorizationService = new Mock<IAuthorizationSevice>();
      _mockAccountService = new Mock<IAccountService>();
      _mockServiceProvider = new Mock<IServiceProvider>();

      // Set up HttpContext with mocked service provider
      _httpContext = new DefaultHttpContext
      {
        RequestServices = _mockServiceProvider.Object
      };
    }

    // Helper method to set up mock for authorization service
    private void SetupAuthorizationServiceReturn(List<string> permissions)
    {
      _mockAuthorizationService
          .Setup(x => x.GetContactAuthorizationAsync(It.IsAny<int>(), It.IsAny<int?>()))
          .ReturnsAsync(permissions);

      _mockServiceProvider
          .Setup(x => x.GetService(typeof(IAuthorizationSevice)))
          .Returns(_mockAuthorizationService.Object);
    }

    // Helper method to set up mock for account service
    private void SetupAccountServiceReturn(bool hasRole)
    {
      _mockAccountService
          .Setup(x => x.CheckContactRoleAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>()))
          .ReturnsAsync(hasRole);

      _mockServiceProvider
          .Setup(x => x.GetService(typeof(IAccountService)))
          .Returns(_mockAccountService.Object);
    }

    [Fact]
    public async Task SkipRoleCheck_ShouldReturnTrue_WhenPermissionsContainNoRoleCheckPermission()
    {
      // Arrange
      var permissions = new List<string> { GlobalsConstants.NoRoleCheckPermissions[0] };
      SetupAuthorizationServiceReturn(permissions);

      // Act
      var result = await AuthorizationHelper.SkipRoleCheck(1, 1, _httpContext);

      // Assert
      Assert.True(result);
    }

    [Fact]
    public async Task SkipRoleCheck_ShouldReturnFalse_WhenPermissionsDoNotContainNoRoleCheckPermission()
    {
      // Arrange
      var permissions = new List<string> { "OtherPermission" };
      SetupAuthorizationServiceReturn(permissions);

      // Act
      var result = await AuthorizationHelper.SkipRoleCheck(1, 1, _httpContext);

      // Assert
      Assert.False(result);
    }

    [Fact]
    public async Task CheckRoles_ShouldReturnTrue_WhenPermissionsContainNoAccountCheckPermissions()
    {
      // Arrange
      var permissions = new List<string> { GlobalsConstants.NoAccountCheckPermissions[0] };
      SetupAuthorizationServiceReturn(permissions);

      // Act
      var result = await AuthorizationHelper.CheckRoles(1, null, 1, _httpContext);

      // Assert
      Assert.True(result);
    }

    [Fact]
    public async Task CheckRoles_ShouldCallAccountService_WhenPermissionsDoNotContainNoAccountCheckPermissions()
    {
      // Arrange
      var permissions = new List<string> { "OtherPermission" };
      SetupAuthorizationServiceReturn(permissions);
      SetupAccountServiceReturn(true);

      // Act
      var result = await AuthorizationHelper.CheckRoles(1, null, 1, _httpContext);

      // Assert
      Assert.True(result);
      _mockAccountService.Verify(x => x.CheckContactRoleAsync(1, 1, null), Times.Once);
    }

    [Fact]
    public async Task CheckContactsCommonAccountRole_ShouldReturnTrue_WhenPermissionsContainNoAccountCheckPermissions()
    {
      // Arrange
      var permissions = new List<string> { GlobalsConstants.NoAccountCheckPermissions[0] };
      SetupAuthorizationServiceReturn(permissions);

      // Act
      var result = await AuthorizationHelper.CheckContactsCommonAccountRole(1, 1, _httpContext);

      // Assert
      Assert.True(result);
    }

    [Fact]
    public async Task ParseQueryParameter_ShouldReturnParsedValue_WhenValidParameterPassed()
    {
      // Arrange
      var queryParameters = new NameValueCollection { { "param", "123" } };

      // Act
      var result = AuthorizationHelper.ParseQueryParameter<int>("param", queryParameters);

      // Assert
      Assert.Equal(123, result);
    }

    [Fact]
    public async Task ParseQueryParameter_ShouldReturnNull_WhenInvalidParameterPassed()
    {
      // Arrange
      var queryParameters = new NameValueCollection { { "param", "invalid" } };

      // Act
      var result = AuthorizationHelper.ParseQueryParameter<int>("param", queryParameters);

      // Assert
      Assert.Null(result);
    }

    [Fact]
    public async Task ParseQueryParameter_ShouldReturnNull_WhenParameterNotPresent()
    {
      // Arrange
      var queryParameters = new NameValueCollection();

      // Act
      var result = AuthorizationHelper.ParseQueryParameter<int>("param", queryParameters);

      // Assert
      Assert.Null(result);
    }
  }
}
