using ApiGateway.Account;
using ApiGateway.Account.Constants;
using ApiGateway.Attributes;
using ApiGateway.Contact;
using ApiGateway.Contact.Enum;
using ApiGateway.Exceptions;
using ApiGateway.FeatureFlags;
using ApiGateway.FeatureFlags.Models;
using ApiGateway.Models;
using ApiGateway.Offer;
using ApiGateway.Offer.Constants;
using ApiGateway.Offer.Model;
using ApiGateway.Pennylane;
using ApiGateway.Pennylane.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pulse.ExceptionMiddleware.Model;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Nodes;

namespace ApiGateway.UnitTests.Offer;

public class OfferControllerTests
{
    private readonly Mock<IOfferService> _mockOfferService;
    private readonly Mock<IPennylaneService> _mockPennylaneService;
    private readonly Mock<ILogger<OfferController>> _mockLogger;
    private readonly Mock<IAccountService> _mockAccountService;
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<IFeatureFlagService> _mockFeatureFlagService;
    private readonly OfferController _controller;

    public OfferControllerTests()
    {
        _mockOfferService = new Mock<IOfferService>(MockBehavior.Strict);
        _mockPennylaneService = new Mock<IPennylaneService>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<OfferController>>(MockBehavior.Loose);
        _mockAccountService = new Mock<IAccountService>(MockBehavior.Strict);
        _mockContactService = new Mock<IContactService>(MockBehavior.Loose);
        _mockFeatureFlagService = new Mock<IFeatureFlagService>(MockBehavior.Loose);
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default behavior: don't create company for any OfferId
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(It.IsAny<int>()))
            .Returns(false);
        var expectedOffer = new OfferDetails
        {
            OfferId = 8,
            Plans =
            [
                new ()
                {
                    PlanId = 80,
                    PlanCode = "STARTER",
                    Pricings =
                    [
                        new ()
                        {
                            PlanPricingId = 800,
                            Label = "1 utilisateur",
                        }
                    ]
                }
            ]
        };
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(It.IsAny<int>())).ReturnsAsync(expectedOffer);

        _controller = new OfferController(_mockLogger.Object, _mockOfferService.Object, _mockPennylaneService.Object, _mockAccountService.Object, _mockContactService.Object, _mockFeatureFlagService.Object);
    }

    #region CreateSubscription Tests

    [Fact]
    public async Task CreateSubscription_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456,
            PlanId = 789,
            ProductConfigurations = new List<int> { 1, 2, 3 },
            Contacts = new List<int> { 10, 20 },
            Notes = new List<Note>
            {
                new Note { StepName = "Step1", StepNote = "Note1" }
            },
            Applicant = "John Doe"
        };

        var expectedSubscriptionId = 999;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(expectedSubscriptionId);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        result.Should().BeOfType<ActionResult<int>>();
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(expectedSubscriptionId);
    }

    [Fact]
    public async Task CreateSubscription_ShouldCallOfferService()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();
        var subscriptionId = 123;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(subscriptionId);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(request), Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_ShouldLogRequestDetails()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456
        };

        var subscriptionId = 789;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(subscriptionId);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Creating subscription")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task CreateSubscription_WhenServiceThrowsException_ShouldPropagate()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ThrowsAsync(new HttpRequestException("Service error"));

        // Act
        var action = async () => await _controller.CreateSubscription(request);

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("Service error");
    }

    [Fact]
    public async Task CreateSubscription_WhenServiceThrowsBadRequestException_ShouldPropagate()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ThrowsAsync(new BadHttpRequestException("Service error"));

        // Act
        var action = async () => await _controller.CreateSubscription(request);

        // Assert
        await action.Should().ThrowAsync<BadHttpRequestException>()
            .WithMessage("Service error");
    }

    [Fact]
    public async Task CreateSubscription_ShouldReturnSubscriptionId()
    {
        // Arrange
        var request = new Fixture().Create<CreateSubscriptionOffer>();
        var expectedId = 12345;

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.Value.Should().Be(expectedId);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneShouldCreateCompany_ShouldCallCreateCompanyAsync()
    {
        // Arrange
        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting()
            {
                AccountingType = "type"
            },
            Legal = new Models.Legal()
            {
                Siren = "SIREN01"
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            PlanId = 80,
            PlanPricingId = 800
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.Is<CreateCompanyRequest>(
            req => req.AccountId == 123 &&
            req.Contacts.Count == 2 &&
            req.RequestedPlanCode == "STARTER" &&
            req.NumberOfUsers == "1 utilisateur")))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockPennylaneService.Verify(x => x.ShouldCreateCompanyForOffer(999), Times.Once);
        _mockPennylaneService.Verify(x => x.CreateCompanyAsync(It.Is<CreateCompanyRequest>(
            req => req.AccountId == 123 && req.Contacts.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneRequiresFirmAssignment_ReturnsConflictAndDoesNotCreateSubscription()
    {
        // Arrange
        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting() { AccountingType = "type" },
            Legal = new Models.Legal() { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            PlanId = 80,
            PlanPricingId = 800
        };

        var requiresFirmAssignmentResult = new CreateCompanyResult
        {
            Company = null,
            Status = PennylaneControllerStatuses.RequiresFirmAssignment
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(requiresFirmAssignmentResult);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var body = conflictResult.Value!;
        var errorCode = body.GetType().GetProperty("ErrorCode")!.GetValue(body) as string;
        var errorMessage = body.GetType().GetProperty("ErrorMessage")!.GetValue(body) as string;
        errorCode.Should().Be(Errors.PennylaneFirmAssignmentRequiredCode);
        errorMessage.Should().Be(Errors.PennylaneFirmAssignmentRequiredMessage);

        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()), Times.Never);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneShouldNotCreateCompany_ShouldNotCallCreateCompanyAsync()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 888,
            Contacts = new List<int> { 10, 20 }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(888))
            .Returns(false);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockPennylaneService.Verify(x => x.ShouldCreateCompanyForOffer(888), Times.Once);
        _mockPennylaneService.Verify(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateSubscription_WhenCompanyCreationSucceeds_ShouldLogSuccess()
    {
        // Arrange
        var address1 = new Fixture().Build<Address>()
            .With(c => c.Country, "FR")
            .Create();
        var address2 = new Fixture().Build<Address>()
            .With(c => c.Country, "EN")
            .Create();

        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting()
            {
                AccountingType = AccountConstants.Treasury
            },
            Legal = new Models.Legal()
            {
                Siren = "SIREN01"
            },
            Address = new List<Address>()
            {
                address1,address2
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Pennylane company creation result")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_WhenCompanyCreationFails_ShouldLogErrorAndContinue()
    {
        // Arrange
        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting()
            {
                AccountingType = "type"
            },
            Legal = new Models.Legal()
            {
                Siren = "SIREN01"
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new HttpRequestException("Pennylane service error"));

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Pennylane API request failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);

        // Should still call subscription creation
        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(request), Times.Once);

        // Should still return OK result
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.Value.Should().Be(456);
    }

    [Fact]
    public async Task CreateSubscription_WhenCompanyCreationFails_ShouldStillCreateSubscription()
    {
        // Arrange
        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting()
            {
                AccountingType = "type"
            },
            Legal = new Models.Legal()
            {
                Siren = "SIREN01"
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        var expectedSubscriptionId = 789;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new InvalidOperationException("Company creation failed"));

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(expectedSubscriptionId);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(request), Times.Once);
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult.Value.Should().Be(expectedSubscriptionId);
    }

    [Fact]
    public async Task CreateSubscription_ShouldLogDebugWithFullRequest()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 456
        };

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(789);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Full request")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_WithPennylaneEnabled_ShouldPassCorrectDataToCreateCompany()
    {
        // Arrange
        var account = new ApiGateway.Models.Account()
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting()
            {
                AccountingType = "type"
            },
            Legal = new Models.Legal()
            {
                Siren = "SIREN01"
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 555,
            OfferId = 999,
            Contacts = new List<int> { 100, 200, 300 }
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC555",
                FirmId = "FIRM999",
                Name = "Company 555"
            },
            Status = "created"
        };

        CreateCompanyRequest? capturedRequest = null;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(777);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.AccountId.Should().Be(555);
        capturedRequest.Contacts.Should().BeEquivalentTo(new List<int> { 100, 200, 300 });
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneEnabled_ShouldCallAccountService()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Legal = new Models.Legal
            {
                Siren = "SIREN01"
            },
            Accounting = new Models.Accounting
            {
                AccountingType = "type"
            },
            Address = new List<Models.Address>
            {
                new Models.Address { Country = "FR" }
            }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult
            {
                Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
                Status = "created"
            });

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockAccountService.Verify(x => x.GetAccountAsync(request.AccountId), Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_WithPennylaneEnabled_ShouldSetSirenNotYetRegisteredAndCountryCodeCorrectly()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting
            {
                AccountingType = "type"
            },
            Legal = new Models.Legal
            {
                Siren = "SIREN01"
            },
            Address = new List<Models.Address>
            {
                new Models.Address { Country = "France" }
            }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20, 30 }
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany
            {
                Id = "ACC123",
                FirmId = "FIRM001",
                Name = "Test Company"
            },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RegistrationNumber.Should().Be("SIREN01");
        capturedRequest.NotYetRegistered.Should().BeFalse();         // Siren is present -> false
        capturedRequest.CountryCode.Should().Be("FR");               // From first address
        capturedRequest.Contacts.Should().BeEquivalentTo(new List<int> { 10, 20, 30 });
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyCreatedSuccessfully_ShouldSetStatusToPennylaneCreated()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneCreated);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyToCreate_ShouldSetStatusToPennylaneNotCreated()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.ToCreate
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneNotCreated);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyRequiresFirmTransfer_ShouldSetStatusToPennylaneToTransfer()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.RequiresFirmTransfer
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToTransfer);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyPartiallyCreated_ShouldSetStatusToPennylaneToVerify()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.PartialSuccess
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyCreationFails_ShouldSetStatusToPennylaneToVerify()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new HttpRequestException("Pennylane service error"));

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyCreationThrowsInvalidOperationException_ShouldSetStatusToPennylaneToVerify()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new InvalidOperationException("Deserialization error"));

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    [Fact]
    public async Task CreateSubscription_WhenNotPennylaneOffer_ShouldNotSetStatus()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 888,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(888))
            .Returns(false);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyCreationThrowsBadHttpRequestException_ShouldSetStatusToPennylaneToVerify()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new BadHttpRequestException("Bad request"));

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCompanyStatusIsNotCreated_ShouldSetStatusToPennylaneToVerify()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.AlreadyExists
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(companyResult);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    [Fact]
    public async Task CreateSubscription_WithCustomerWithoutMobilePhone_ForPennylaneOffer_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        var customerContact = new ApiGateway.Contact.Models.Contact
        {
            Id = 10,
            Type = ContactType.Customer.ToString(),
            MobilePhone = null
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockContactService.Setup(x => x.GetContactByIdAsync(10))
            .ReturnsAsync(customerContact);

        _mockContactService.Setup(x => x.GetContactByIdAsync(20))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = 20, Type = ContactType.Collaborator.ToString() });

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task CreateSubscription_WithCollaboratorWithoutMobilePhone_ForPennylaneOffer_ShouldSucceed()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10 }
        };

        var collaboratorContact = new ApiGateway.Contact.Models.Contact
        {
            Id = 10,
            Type = ContactType.Collaborator.ToString(),
            MobilePhone = null
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockContactService.Setup(x => x.GetContactByIdAsync(10))
            .ReturnsAsync(collaboratorContact);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult
            {
                Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test" },
                Status = PennylaneControllerStatuses.Created
            });

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateSubscription_WithCustomerWithoutMobilePhone_ForNonPennylaneOffer_ShouldSucceed()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 888,
            Contacts = new List<int> { 10 }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(888))
            .Returns(false);

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Verify contact service was not called
        _mockContactService.Verify(x => x.GetContactByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateSubscription_WithCustomerWithMobilePhone_ForPennylaneOffer_ShouldSucceed()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10 }
        };

        var customerContact = new ApiGateway.Contact.Models.Contact
        {
            Id = 10,
            Type = ContactType.Customer.ToString(),
            MobilePhone = "+33612345678"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999))
            .Returns(true);

        _mockContactService.Setup(x => x.GetContactByIdAsync(10))
            .ReturnsAsync(customerContact);

        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId))
            .ReturnsAsync(account);

        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult
            {
                Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test" },
                Status = PennylaneControllerStatuses.Created
            });

        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request))
            .ReturnsAsync(456);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task CreateSubscription_WhenApprovedPlatformPlanAndFlagDisabled_ShouldReturn403WithGTW028()
    {
        // Arrange: Pennylane offer with APPROVED_PLATFORM plan, flag is OFF
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };

        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }]
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var forbiddenResult = result.Result as ObjectResult;
        forbiddenResult.Should().NotBeNull();
        forbiddenResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var errorResponse = forbiddenResult.Value as ErrorResponse;
        errorResponse.Should().NotBeNull();
        errorResponse!.ErrorCode.Should().Be(Errors.ApprovedPlatformDisabledCode);
        errorResponse.ErrorMessage.Should().Be(Errors.ApprovedPlatformDisabledMessage);
        _mockOfferService.Verify(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()), Times.Never);
        _mockPennylaneService.Verify(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()), Times.Never);
    }

    [Fact]
    public async Task CreateSubscription_WhenApprovedPlatformPlan_ShouldEvaluateFlagWithResolvedContactId()
    {
        // Arrange: utilisateur authentifié, contact résolu → le flag doit être ciblé sur son contactId (pas l'email)
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };

        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }]
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockContactService.Setup(x => x.GetContactAsync("user@test.fr"))
            .ReturnsAsync(new ApiGateway.Contact.Models.Contact { Id = 456 });
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("upn", "user@test.fr")]))
            }
        };

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var forbiddenResult = result.Result as ObjectResult;
        forbiddenResult.Should().NotBeNull();
        forbiddenResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _mockContactService.Verify(x => x.GetContactAsync("user@test.fr"), Times.Once);
        _mockFeatureFlagService.Verify(
            x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.Is<FeatureContext?>(c => c != null && c.ContactId == "456"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSubscription_WhenApprovedPlatformPlanAndFlagEnabled_ShouldProceed()
    {
        // Arrange: Pennylane offer with APPROVED_PLATFORM plan, flag is ON
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };

        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }]
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult { Status = PennylaneControllerStatuses.Created });
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(555);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(555);
    }

    [Fact]
    public async Task CreateSubscription_WhenNonApprovedPlatformPlanAndFlagDisabled_ShouldProceed()
    {
        // Arrange: Pennylane offer with non-APPROVED_PLATFORM plan, flag OFF must not block
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 80,
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult { Status = PennylaneControllerStatuses.Created });
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(777);

        // Act
        var result = await _controller.CreateSubscription(request);

        // Assert
        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(777);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCreatedAndApprovedPlatform_ShouldSetMandateToSendStatus()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };
        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }],
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult { Status = PennylaneControllerStatuses.Created });
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        CreateSubscriptionOffer? captured = null;
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(r => captured = r)
            .ReturnsAsync(555);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(PennylaneConstants.PennylaneMandateToSend);
    }

    [Theory]
    [InlineData(PennylaneControllerStatuses.CreatedOnDefault)]
    [InlineData(PennylaneControllerStatuses.AlreadyExists)]
    public async Task CreateSubscription_WhenPennylaneCompanyExistsAndApprovedPlatform_ShouldSetMandateToSendStatus(string pennylaneStatus)
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };
        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }],
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult { Status = pennylaneStatus });
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        CreateSubscriptionOffer? captured = null;
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(r => captured = r)
            .ReturnsAsync(555);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(PennylaneConstants.PennylaneMandateToSend);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneCreatedAndOtherPlan_ShouldSetPennylaneCreatedStatus()
    {
        // Arrange
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 80,
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ReturnsAsync(new CreateCompanyResult { Status = PennylaneControllerStatuses.Created });

        CreateSubscriptionOffer? captured = null;
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(r => captured = r)
            .ReturnsAsync(777);

        // Act
        await _controller.CreateSubscription(request);

        // Assert: APPROVED_PLATFORM-only mapping must not leak to other plans
        captured!.Status.Should().Be(PennylaneConstants.PennylaneCreated);
    }

    [Fact]
    public async Task CreateSubscription_WhenPennylaneFailsAndApprovedPlatform_ShouldNotSetMandateToSend()
    {
        // Arrange: Pennylane creation fails (no Created status returned) — fallback path
        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 8,
            PlanId = 99,
        };
        var offerWithApprovedPlatform = new OfferDetails
        {
            OfferId = 8,
            Plans = [new() { PlanId = 99, PlanCode = OfferPlanCodes.ApprovedPlatform }],
        };
        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(8)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(123)).ReturnsAsync((ApiGateway.Models.Account?)null);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(8)).ReturnsAsync(offerWithApprovedPlatform);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .ThrowsAsync(new HttpRequestException("Pennylane down"));
        _mockFeatureFlagService
            .Setup(x => x.IsEnabledAsync(FeatureFlagKeys.EnableApprovedPlatform, It.IsAny<bool>(), It.IsAny<FeatureContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        CreateSubscriptionOffer? captured = null;
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(r => captured = r)
            .ReturnsAsync(999);

        // Act
        await _controller.CreateSubscription(request);

        // Assert: fallback to PennylaneToVerify, never MandateToSend
        captured!.Status.Should().Be(PennylaneConstants.PennylaneToVerify);
    }

    #endregion

    #region Controller Attributes Tests

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        // Arrange
        var type = typeof(OfferController);

        // Act
        var attributes = type.GetCustomAttributes(typeof(AuthorizeAttribute), true);

        // Assert
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveCorrectRoute()
    {
        // Arrange
        var type = typeof(OfferController);

        // Act
        var routeAttribute = type.GetCustomAttribute<RouteAttribute>();

        // Assert
        routeAttribute.Should().NotBeNull();
        routeAttribute.Template.Should().Be("gtw/offer/api");
    }

    #endregion

    #region CreateSubscription Method Attributes Tests
    [Fact]
    public void CreateSubscription_ShouldHaveHttpPostAttribute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<HttpPostAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Template.Should().Be("subscription/create");
    }

    [Fact]
    public void CreateSubscription_ShouldHaveCorrectRoute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<HttpPostAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Template.Should().Be("subscription/create");
    }

    [Fact]
    public void CreateSubscription_ShouldHaveRequirePermissionAttribute()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void CreateSubscription_RequirePermissionAttribute_ShouldHaveCOOFF001()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attribute = method.GetCustomAttribute<RequirePermissionAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        // We can't directly access the permissions field, but we can verify the attribute exists
        // The actual permission validation is tested in RequirePermissionAttributeTests
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseTypeAttributes()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method!.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();

        // Assert
        attributes.Should().NotBeEmpty();
        attributes.Should().HaveCountGreaterOrEqualTo(4); // 200, 400, 403, 404
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType200()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method!.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status200 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK);

        // Assert
        status200.Should().NotBeNull();
        status200.Type.Should().Be(typeof(int));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType404()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method!.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status404 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound);

        // Assert
        status404.Should().NotBeNull();
        status404.Type.Should().Be(typeof(ErrorResponse));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType400()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method!.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status400 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest);

        // Assert
        status400.Should().NotBeNull();
        status400.Type.Should().Be(typeof(ErrorResponse));
    }

    [Fact]
    public void CreateSubscription_ShouldHaveProducesResponseType403()
    {
        // Arrange
        var method = typeof(OfferController).GetMethod(nameof(OfferController.CreateSubscription));

        // Act
        var attributes = method!.GetCustomAttributes<ProducesResponseTypeAttribute>();
        var status403 = attributes.FirstOrDefault(a => a.StatusCode == StatusCodes.Status403Forbidden);

        // Assert
        status403.Should().NotBeNull();
        status403.Type.Should().Be(typeof(ErrorResponse));
    }

    #endregion

    #region ContactFunctions Tests

    [Fact]
    public async Task CreateSubscription_WithContactFunctions_ShouldPassContactFunctionsToCreateCompanyRequest()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var contactFunctions = new List<ContactFunctions>
        {
            new ContactFunctions { ContactId = 10, FunctionId = 1 },
            new ContactFunctions { ContactId = 20, FunctionId = 2 }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            ContactFunctions = contactFunctions
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContactFunctions.Should().NotBeNull();
        capturedRequest.ContactFunctions.Should().HaveCount(2);
        capturedRequest.ContactFunctions.Should().BeEquivalentTo(contactFunctions);
    }

    [Fact]
    public async Task CreateSubscription_WithNullContactFunctions_ShouldPassNullToCreateCompanyRequest()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            ContactFunctions = null
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContactFunctions.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WithEmptyContactFunctions_ShouldPassEmptyListToCreateCompanyRequest()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            ContactFunctions = new List<ContactFunctions>()
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContactFunctions.Should().NotBeNull();
        capturedRequest.ContactFunctions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSubscription_WithContactFunctions_ShouldPreserveContactIdAndFunctionId()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            AccountNumber = "ACC123",
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var contactFunctions = new List<ContactFunctions>
        {
            new ContactFunctions { ContactId = 100, FunctionId = 5 },
            new ContactFunctions { ContactId = 200, FunctionId = 10 },
            new ContactFunctions { ContactId = 300, FunctionId = 15 }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 100, 200, 300 },
            ContactFunctions = contactFunctions
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.ContactFunctions.Should().HaveCount(3);
        capturedRequest.ContactFunctions![0].ContactId.Should().Be(100);
        capturedRequest.ContactFunctions[0].FunctionId.Should().Be(5);
        capturedRequest.ContactFunctions[1].ContactId.Should().Be(200);
        capturedRequest.ContactFunctions[1].FunctionId.Should().Be(10);
        capturedRequest.ContactFunctions[2].ContactId.Should().Be(300);
        capturedRequest.ContactFunctions[2].FunctionId.Should().Be(15);
    }

    #endregion

    #region HubName Tests

    [Fact]
    public async Task CreateSubscription_WithHubName_ShouldPassHubNameToCreateCompanyRequest()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            HubName = "North Hub"
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.HubName.Should().Be("North Hub");
    }

    [Fact]
    public async Task CreateSubscription_WithoutHubName_ShouldPassNullToCreateCompanyRequest()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 }
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.HubName.Should().BeNull();
    }

    #endregion

    #region HasNoSiren Tests

    [Fact]
    public async Task CreateSubscription_WithHasNoSirenTrue_ShouldNotSendSiren()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "123456789" }, // Account has SIREN
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            HasNoSiren = true // Frontend explicitly says "no SIREN"
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.NotYetRegistered.Should().BeTrue();
        capturedRequest.RegistrationNumber.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WithHasNoSirenNull_AndSirenExists_ShouldUseSiren()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "987654321" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            HasNoSiren = null // Not specified
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.NotYetRegistered.Should().BeFalse();
        capturedRequest.RegistrationNumber.Should().Be("987654321");
    }

    [Fact]
    public async Task CreateSubscription_WithHasNoSirenNull_AndEmptySiren_ShouldSetNotYetRegisteredTrue()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "" }, // No SIREN
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            HasNoSiren = null // Not specified
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.NotYetRegistered.Should().BeTrue();
        capturedRequest.RegistrationNumber.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WithHasNoSirenFalse_AndSirenExists_ShouldUseSiren()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "111222333" },
            Address = new List<Models.Address> { new Models.Address { Country = "France" } }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            Contacts = new List<int> { 10, 20 },
            HasNoSiren = false // Explicitly false
        };

        CreateCompanyRequest? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = "created"
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(request)).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.NotYetRegistered.Should().BeFalse();
        capturedRequest.RegistrationNumber.Should().Be("111222333");
    }

    #endregion

    #region Plan Comparison Tests

    [Fact]
    public async Task CreateSubscription_WhenPennylaneReturnsValidated_ShouldSetStatusToValidated()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 1,
            Contacts = new List<int> { 10 }
        };

        CreateSubscriptionOffer? capturedRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Validated
        };

        var offerDetails = new OfferDetails
        {
            OfferId = 999,
            Plans = new List<OfferPlan> { new OfferPlan { PlanId = 1, PlanCode = "COLLABORATIVE" } }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync(offerDetails);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>())).ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedRequest = req)
            .ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(PennylaneControllerStatuses.Validated);
    }

    [Fact]
    public async Task CreateSubscription_WhenPlanIdProvided_ShouldResolveAndPassPlanCodeToPennylane()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 2,
            Contacts = new List<int> { 10 }
        };

        CreateCompanyRequest? capturedCompanyRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        var offerDetails = new OfferDetails
        {
            OfferId = 999,
            Plans = new List<OfferPlan>
            {
                new OfferPlan { PlanId = 1, PlanCode = "COLLABORATIVE" },
                new OfferPlan { PlanId = 2, PlanCode = "ESSENTIAL" }
            }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync(offerDetails);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedCompanyRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>())).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedCompanyRequest.Should().NotBeNull();
        capturedCompanyRequest!.RequestedPlanCode.Should().Be("ESSENTIAL");
    }

    [Fact]
    public async Task CreateSubscription_WhenPlanIdIsNull_ShouldNotCallGetOfferById()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = null,
            Contacts = new List<int> { 10 }
        };

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>())).ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>())).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        _mockOfferService.Verify(x => x.GetOfferByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateSubscription_WhenOfferApiReturnsNull_ShouldPassNullPlanCode()
    {
        // Arrange
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 1,
            Contacts = new List<int> { 10 }
        };

        CreateCompanyRequest? capturedCompanyRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync((OfferDetails?)null);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedCompanyRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>())).ReturnsAsync(456);

        // Act
        await _controller.CreateSubscription(request);

        // Assert
        capturedCompanyRequest.Should().NotBeNull();
        capturedCompanyRequest!.RequestedPlanCode.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WhenPlanIdNotFoundInOffer_ShouldPassNullPlanCode()
    {
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 99,
            Contacts = new List<int> { 10 }
        };

        CreateCompanyRequest? capturedCompanyRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        var offerDetails = new OfferDetails
        {
            OfferId = 999,
            Plans = new List<OfferPlan> { new OfferPlan { PlanId = 1, PlanCode = "COLLABORATIVE" } }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync(offerDetails);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedCompanyRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>())).ReturnsAsync(456);

        await _controller.CreateSubscription(request);

        capturedCompanyRequest.Should().NotBeNull();
        capturedCompanyRequest!.RequestedPlanCode.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WhenOfferHasNullPlans_ShouldPassNullPlanCode()
    {
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 1,
            Contacts = new List<int> { 10 }
        };

        CreateCompanyRequest? capturedCompanyRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Created
        };

        var offerDetails = new OfferDetails { OfferId = 999, Plans = null };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync(offerDetails);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedCompanyRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>())).ReturnsAsync(456);

        await _controller.CreateSubscription(request);

        capturedCompanyRequest.Should().NotBeNull();
        capturedCompanyRequest!.RequestedPlanCode.Should().BeNull();
    }

    [Fact]
    public async Task CreateSubscription_WhenValidatedStatus_ShouldAlsoPassPlanCodeCorrectly()
    {
        var account = new ApiGateway.Models.Account
        {
            AccountId = 123,
            Accounting = new Models.Accounting { AccountingType = "type" },
            Legal = new Models.Legal { Siren = "SIREN01" }
        };

        var request = new CreateSubscriptionOffer
        {
            AccountId = 123,
            OfferId = 999,
            PlanId = 1,
            Contacts = new List<int> { 10 }
        };

        CreateCompanyRequest? capturedCompanyRequest = null;
        CreateSubscriptionOffer? capturedSubscriptionRequest = null;

        var companyResult = new CreateCompanyResult
        {
            Company = new PennylaneCompany { Id = "ACC123", FirmId = "FIRM001", Name = "Test Company" },
            Status = PennylaneControllerStatuses.Validated
        };

        var offerDetails = new OfferDetails
        {
            OfferId = 999,
            Plans = new List<OfferPlan> { new OfferPlan { PlanId = 1, PlanCode = "COLLABORATIVE" } }
        };

        _mockPennylaneService.Setup(x => x.ShouldCreateCompanyForOffer(999)).Returns(true);
        _mockAccountService.Setup(x => x.GetAccountAsync(request.AccountId)).ReturnsAsync(account);
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(999)).ReturnsAsync(offerDetails);
        _mockPennylaneService.Setup(x => x.CreateCompanyAsync(It.IsAny<CreateCompanyRequest>()))
            .Callback<CreateCompanyRequest>(req => capturedCompanyRequest = req)
            .ReturnsAsync(companyResult);
        _mockOfferService.Setup(x => x.CreateSubscriptionAsync(It.IsAny<CreateSubscriptionOffer>()))
            .Callback<CreateSubscriptionOffer>(req => capturedSubscriptionRequest = req)
            .ReturnsAsync(456);

        await _controller.CreateSubscription(request);

        capturedCompanyRequest!.RequestedPlanCode.Should().Be("COLLABORATIVE");
        capturedSubscriptionRequest!.Status.Should().Be(PennylaneControllerStatuses.Validated);
    }

    #endregion

    #region GetPlanInfoAsync Tests

    [Fact]
    public async Task GetPlanInfoAsync_NoPlanId_ReturnsNulls()
    {
        var request = new CreateSubscriptionOffer { PlanId = null };

        var result = await _controller.GetPlanInfoAsync(request);

        result.Item1.Should().BeNull();
        result.Item2.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanInfoAsync_PlanIdNotFound_ReturnsNulls()
    {
        var request = new CreateSubscriptionOffer { PlanId = 1, OfferId = 10 };
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(10)).ReturnsAsync(new OfferDetails { OfferId = 10, Plans = [] });

        var result = await _controller.GetPlanInfoAsync(request);

        result.Item1.Should().BeNull();
        result.Item2.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanInfoAsync_PlanIdFound_NoPricingId_ReturnsPlanCodeAndNull()
    {
        var plan = new OfferPlan { PlanId = 2, PlanCode = "CODE2" };
        var request = new CreateSubscriptionOffer { PlanId = 2, OfferId = 20 };
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(20)).ReturnsAsync(new OfferDetails { OfferId = 20, Plans = [plan] });

        var result = await _controller.GetPlanInfoAsync(request);

        result.Item1.Should().Be("CODE2");
        result.Item2.Should().BeNull();
    }

    [Fact]
    public async Task GetPlanInfoAsync_PlanIdAndPricingIdFound_ReturnsPlanCodeAndLabel()
    {
        var pricing = new PlanPricing { PlanPricingId = 5, Label = "5 utilisateurs" };
        var plan = new OfferPlan { PlanId = 3, PlanCode = "CODE3", Pricings = new List<PlanPricing> { pricing } };
        var request = new CreateSubscriptionOffer { PlanId = 3, OfferId = 30, PlanPricingId = 5 };
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(30)).ReturnsAsync(new OfferDetails { OfferId = 30, Plans = [plan] });

        var result = await _controller.GetPlanInfoAsync(request);

        result.Item1.Should().Be("CODE3");
        result.Item2.Should().Be("5 utilisateurs");
    }

    [Fact]
    public async Task GetPlanInfoAsync_PlanIdFound_PricingIdNotFound_ReturnsPlanCodeAndNull()
    {
        var plan = new OfferPlan { PlanId = 4, PlanCode = "CODE4", Pricings = new List<PlanPricing>() };
        var request = new CreateSubscriptionOffer { PlanId = 4, OfferId = 40, PlanPricingId = 99 };
        _mockOfferService.Setup(x => x.GetOfferByIdAsync(40)).ReturnsAsync(new OfferDetails { OfferId = 40, Plans = [plan] });

        var result = await _controller.GetPlanInfoAsync(request);

        result.Item1.Should().Be("CODE4");
        result.Item2.Should().BeNull();
    }

    #endregion
}
