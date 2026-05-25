using ApiGateway.Account;
using ApiGateway.Account.Constants;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ApiGateway.UnitTests.ProspectExperience.Services;

public class ProspectOrchestrationServiceTests
{
    private readonly Mock<IRegistryProspectClient> _registry;
    private readonly Mock<IProspectApiClient> _prospect;
    private readonly Mock<IAccountService> _accountService;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<ILogger<ProspectOrchestrationService>> _logger;
    private readonly ProspectOrchestrationService _service;

    public ProspectOrchestrationServiceTests()
    {
        _registry = new Mock<IRegistryProspectClient>();
        _prospect = new Mock<IProspectApiClient>();
        _accountService = new Mock<IAccountService>();
        _contactService = new Mock<IContactService>();
        _logger = new Mock<ILogger<ProspectOrchestrationService>>();
        _service = new ProspectOrchestrationService(
            _registry.Object,
            _prospect.Object,
            _accountService.Object,
            _contactService.Object,
            _logger.Object);
    }

    private static CreateProspectRequest BuildRequest(string siret = "12345678901234") => new()
    {
        Siret = siret,
        LegalForm = "SARL",
        LegalStructure = "PERSONNE_MORALE",
        CaseManagerContactId = 100,
        AccountManagerContactId = 200,
        OfficeCode = "PAR",
        Department = "75",
        Region = "11",
        Country = "FR",
        Signatory = new SignatoryDto
        {
            Title = "M.",
            LastName = "Dupont",
            FirstName = "Jean",
            JobTitle = "Dirigeant",
            Department = "Direction",
            CompanyRole = "Gérant",
            ContactTypes = new[] { "Signataire" },
            Email = "jean.dupont@test.fr",
            MobilePhone = "+33612345678"
        }
    };

    private static InpiCompanyInfo BuildInpi(string siret = "12345678901234") => new()
    {
        Siret = siret,
        Siren = siret[..9],
        LegalName = "ACME SARL",
        NafCode = "6201Z",
        Address = "10 rue des Lilas",
        ZipCode = "75001",
        City = "Paris",
        LegalForm = "SARL",
        ShareCapital = 10000
    };

    private void SetupHappyPath(int prospectId, string accountNumber, int accountId, int contactId)
    {
        _registry.Setup(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _prospect.Setup(p => p.GetInpiCompanyInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildInpi());
        _prospect.Setup(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(prospectId);
        _registry.Setup(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AkuiteoCustomerCreated { AccountNumber = accountNumber });
        _registry.Setup(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accountService.Setup(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreated { AccountId = accountId });
        _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ContactCreated { ContactId = contactId });
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _prospect.Setup(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Verifies that prospect creation patches identifiers before sending one batched role-assignment call.
    /// </summary>
    [Fact]
    public async Task CreateAsync_HappyPath_ReturnsProspectIdAndCallsAllStepsInOrder()
    {
        var prospectId = 42;
        var accountNumber = "AK-001";
        var accountId = 42;
        var contactId = 99;
        IReadOnlyCollection<CreateRoleAssignmentRequest>? capturedAssignments = null;
        IReadOnlyCollection<CreateRolesBulkItem>? capturedAccountRoleItems = null;
        int? capturedAccountRolesAccountId = null;
        var executedSteps = new List<string>();
        SetupHappyPath(prospectId, accountNumber, accountId, contactId);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<CreateRolesBulkItem>, int?, CancellationToken>((id, items, _, _) =>
            {
                capturedAccountRolesAccountId = id;
                capturedAccountRoleItems = items;
                executedSteps.Add("accountRoles");
            })
            .ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, It.IsAny<CancellationToken>()))
            .Callback(() => executedSteps.Add("patch"))
            .Returns(Task.CompletedTask);
        _prospect.Setup(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<CreateRoleAssignmentRequest>, CancellationToken>((requests, _) =>
            {
                capturedAssignments = requests;
                executedSteps.Add("roles");
            })
            .Returns(Task.CompletedTask);

        var result = await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        result.Should().NotBeNull();
        result.AccountId.Should().Be(accountId);
        result.AccountNumber.Should().Be(accountNumber);
        result.AccountType.Should().Be(AccountType.PROSPECT);
        result.LegalName.Should().Be("ACME SARL");
        result.StepCode.Should().Be(ProspectStepCode.InProgress);
        result.Signatory.Should().NotBeNull();
        result.Signatory.ContactId.Should().Be(contactId);
        result.Signatory.FirstName.Should().Be("Jean");
        result.Signatory.LastName.Should().Be("Dupont");
        result.Signatory.Email.Should().Be("jean.dupont@test.fr");
        executedSteps.Should().Equal("patch", "roles", "accountRoles");
        capturedAccountRolesAccountId.Should().Be(accountId);
        capturedAccountRoleItems.Should().NotBeNull();
        capturedAccountRoleItems.Should().BeEquivalentTo(new[]
        {
            new CreateRolesBulkItem { ContactId = contactId, IsSignatory = true },
            new CreateRolesBulkItem { ContactId = 100, IsSignatory = false, RoleCode = RoleCodes.CaseManager },
            new CreateRolesBulkItem { ContactId = 200, IsSignatory = false, RoleCode = RoleCodes.AccountManager }
        });
        _registry.Verify(r => r.SiretExistsInAkuiteoAsync("12345678901234", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetInpiCompanyInfoAsync("12345678901234", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.CreateAkuiteoContactAsync(accountNumber, It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.Is<CreateAccountRequest>(r => r.AccountNumber == accountNumber && r.Siret == "12345678901234" && r.AccountType == AccountType.PROSPECT), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>()), Times.Once);
        capturedAssignments.Should().NotBeNull();
        capturedAssignments.Should().BeEquivalentTo(new[]
        {
            new CreateRoleAssignmentRequest { ContactId = contactId, AccountId = accountId, IsSignatory = true },
            new CreateRoleAssignmentRequest { ContactId = 100, AccountId = accountId, IsSignatory = false },
            new CreateRoleAssignmentRequest { ContactId = 200, AccountId = accountId, IsSignatory = false }
        });
    }

    [Fact]
    public async Task CreateAsync_WhenSiretExistsInAkuiteo_ThrowsSiretAlreadyExistsAndSkipsAllOtherSteps()
    {
        // Arrange
        _registry.Setup(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<SiretAlreadyExistsException>();
        _prospect.Verify(p => p.GetInpiCompanyInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that each orchestration failure is wrapped with the corresponding step identifier.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task CreateAsync_WhenStepFails_ThrowsProspectOrchestrationExceptionWithCorrectStep(int failingStep)
    {
        SetupHappyPath(42, "AK-001", 43, 99);

        switch (failingStep)
        {
            case 1:
                _registry.Setup(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("1"));
                break;
            case 2:
                _prospect.Setup(p => p.GetInpiCompanyInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("2"));
                break;
            case 3:
                _prospect.Setup(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("3"));
                break;
            case 4:
                _registry.Setup(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("4"));
                break;
            case 5:
                _registry.Setup(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("5"));
                break;
            case 6:
                _accountService.Setup(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("6"));
                break;
            case 7:
                _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("7"));
                break;
            case 8:
                _prospect.Setup(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("8"));
                break;
            case 9:
                _prospect.Setup(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("9"));
                break;
            case 10:
                _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("10"));
                break;
        }

        ProspectOrchestrationException? caught = null;
        try
        {
            await _service.CreateAsync(BuildRequest(), CancellationToken.None);
        }
        catch (ProspectOrchestrationException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.Step.Should().Be(failingStep);
        caught.Code.Should().Be(ApiGateway.Exceptions.Errors.ProspectOrchestrationFailedCode);
        caught.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>
    /// Verifies that a step 4 failure stops the remaining downstream calls.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenStep4Fails_DoesNotCallSubsequentSteps()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        _registry.Setup(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ProspectOrchestrationException>();
        _registry.Verify(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a step 4 failure is logged with the orchestration context.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenStep4Fails_LogsErrorWithStepAndContext()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        _registry.Setup(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("boom"));

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);
        await act.Should().ThrowAsync<ProspectOrchestrationException>();

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[Step]: 4")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that duplicate manager contacts are sent only once in the batch payload.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenManagersShareTheSameContact_BuildsADeduplicatedBatch()
    {
        var request = BuildRequest();
        request.CaseManagerContactId = 100;
        request.AccountManagerContactId = 100;

        IReadOnlyCollection<CreateRoleAssignmentRequest>? capturedAssignments = null;
        SetupHappyPath(42, "AK-001", 43, 99);
        _prospect.Setup(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<CreateRoleAssignmentRequest>, CancellationToken>((requests, _) => capturedAssignments = requests)
            .Returns(Task.CompletedTask);

        await _service.CreateAsync(request, CancellationToken.None);

        capturedAssignments.Should().NotBeNull();
        capturedAssignments.Should().BeEquivalentTo(new[]
        {
            new CreateRoleAssignmentRequest { ContactId = 99, AccountId = 43, IsSignatory = true },
            new CreateRoleAssignmentRequest { ContactId = 100, AccountId = 43, IsSignatory = false }
        });
    }

    /// <summary>
    /// Verifies that signatory priority is preserved when the signatory also appears as a manager.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenSignatoryMatchesManager_KeepsSignatoryRoleInBatch()
    {
        var request = BuildRequest();
        request.CaseManagerContactId = 99;
        request.AccountManagerContactId = 200;

        IReadOnlyCollection<CreateRoleAssignmentRequest>? capturedAssignments = null;
        SetupHappyPath(42, "AK-001", 43, 99);
        _prospect.Setup(p => p.CreateRoleAsync(It.IsAny<IReadOnlyCollection<CreateRoleAssignmentRequest>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<CreateRoleAssignmentRequest>, CancellationToken>((requests, _) => capturedAssignments = requests)
            .Returns(Task.CompletedTask);

        await _service.CreateAsync(request, CancellationToken.None);

        capturedAssignments.Should().NotBeNull();
        capturedAssignments.Should().BeEquivalentTo(new[]
        {
            new CreateRoleAssignmentRequest { ContactId = 99, AccountId = 43, IsSignatory = true },
            new CreateRoleAssignmentRequest { ContactId = 200, AccountId = 43, IsSignatory = false }
        });
    }
}
