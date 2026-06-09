using ApiGateway.Account;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Constants;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Policies;
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

    /// <summary>
    /// Builds the expected retry fingerprint for the provided request.
    /// </summary>
    /// <param name="request">The request to fingerprint, or the default valid request when omitted.</param>
    /// <returns>The retry fingerprint expected from Prospect.</returns>
    private static string BuildResumeFingerprint(CreateProspectRequest? request = null)
    {
        return ProspectResumeRequestFingerprint.Build(request ?? BuildRequest());
    }

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
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((IncompleteProspectCreationState?)null);
        _prospect.Setup(p => p.GetInpiCompanyInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(BuildInpi());
        _prospect.Setup(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(prospectId);
        _registry.Setup(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AkuiteoCustomerCreated { AccountNumber = accountNumber });
        _registry.Setup(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _accountService.Setup(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountCreated { AccountId = accountId });
        _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ContactCreated { ContactId = contactId });
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(FinalizeProspectOutcome.Updated);
        _prospect.Setup(p => p.PrepareCreationResumeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _prospect.Setup(p => p.GetCreationRoleSynchronizationOutcomeAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(ProspectRoleSynchronizationOutcome.Synchronized);
        _prospect.Setup(p => p.UpdateCreationProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _prospect.Setup(p => p.MarkProspectCreationFailedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    /// <summary>
    /// Verifies that prospect creation patches identifiers before sending the account role batch.
    /// </summary>
    [Fact]
    public async Task CreateAsync_HappyPath_ReturnsProspectIdAndCallsAllStepsInOrder()
    {
        var prospectId = 42;
        var accountNumber = "AK-001";
        var accountId = 42;
        var contactId = 99;
        IReadOnlyCollection<CreateRolesBulkItem>? capturedAccountRoleItems = null;
        int? capturedAccountRolesAccountId = null;
        CreateContactRequest? capturedContactRequest = null;
        var executedSteps = new List<string>();
        SetupHappyPath(prospectId, accountNumber, accountId, contactId);
        _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateContactRequest, CancellationToken>((request, _) => capturedContactRequest = request)
            .ReturnsAsync(new ContactCreated { ContactId = contactId });
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
            .ReturnsAsync(FinalizeProspectOutcome.Updated);

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
        executedSteps.Should().Equal("patch", "accountRoles");
        capturedAccountRolesAccountId.Should().Be(accountId);
        capturedContactRequest.Should().NotBeNull();
        capturedContactRequest!.AccountNumber.Should().Be(accountNumber);
        capturedAccountRoleItems.Should().NotBeNull();
        capturedAccountRoleItems.Should().BeEquivalentTo(new[]
        {
            new CreateRolesBulkItem { ContactId = contactId, IsSignatory = true },
            new CreateRolesBulkItem { ContactId = 200, IsSignatory = false, RoleCode = "AM" },
            new CreateRolesBulkItem { ContactId = 100, IsSignatory = false, RoleCode = "CLP" }
        });
        _registry.Verify(r => r.SiretExistsInAkuiteoAsync("12345678901234", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetInpiCompanyInfoAsync("12345678901234", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.CreateAkuiteoContactAsync(accountNumber, It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.Is<CreateAccountRequest>(r => r.AccountNumber == accountNumber && r.Siret == "12345678901234" && r.AccountType == AccountType.PROSPECT), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.UpdateCreationProgressAsync(prospectId, 100, It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
        _prospect.Verify(p => p.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenSiretExistsInAkuiteo_ThrowsSiretAlreadyExistsAndSkipsAllOtherSteps()
    {
        _registry.Setup(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

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
                _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("8"));
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
        _prospect.Verify(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(
            p => p.MarkProspectCreationFailedAsync(
                42,
                100,
                It.Is<MarkProspectCreationFailedRequest>(r => r.CompletedMilestone == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
    /// Verifies that downstream creation failures stop the remaining orchestration and mark the prospect as failed.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task CreateAsync_WhenDownstreamCreationStepFails_SkipsRemainingStepsAndMarksProspectAsFailed(int failingStep)
    {
        SetupHappyPath(42, "AK-001", 43, 99);

        switch (failingStep)
        {
            case 5:
                _registry.Setup(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("step-5"));
                break;
            case 6:
                _accountService.Setup(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("step-6"));
                break;
            case 7:
                _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("step-7"));
                break;
        }

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ProspectOrchestrationException>();
        exception.Which.Step.Should().Be(failingStep);

        if (failingStep <= 5)
        {
            _accountService.Verify(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        if (failingStep <= 6)
        {
            _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        _prospect.Verify(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.MarkProspectCreationFailedAsync(42, 100, It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that finalization retries while Prospect is still waiting for synchronized stale data.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenFinalizationIsWaitingForSynchronization_RetriesBeforeAssigningRoles()
    {
        var executedSteps = new List<string>();
        var attempt = 0;
        SetupHappyPath(42, "AK-001", 43, 99);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedSteps.Add("accountRoles"))
            .ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.UpdateProspectIdsAsync(42, "AK-001", 43, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempt++;
                executedSteps.Add($"patch-attempt-{attempt}");
                return attempt == 1
                    ? FinalizeProspectOutcome.SynchronizationPending
                    : FinalizeProspectOutcome.Updated;
            });

        await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        executedSteps.Should().Equal("patch-attempt-1", "patch-attempt-2", "accountRoles");
        _prospect.Verify(p => p.UpdateProspectIdsAsync(42, "AK-001", 43, 99, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.MarkProspectCreationFailedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that finalization stops after the bounded retry window and marks the prospect as failed.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenFinalizationNeverBecomesReady_ThrowsAndMarksProspectAsFailed()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        _prospect.Setup(p => p.UpdateProspectIdsAsync(42, "AK-001", 43, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FinalizeProspectOutcome.SynchronizationPending);

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ProspectOrchestrationException>();
        exception.Which.Step.Should().Be(8);
        _prospect.Verify(p => p.UpdateProspectIdsAsync(42, "AK-001", 43, 99, It.IsAny<CancellationToken>()), Times.Exactly(3));
        _accountService.Verify(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.MarkProspectCreationFailedAsync(42, 100, It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that ACC021 role failures are treated as idempotent duplicates after prospect finalization.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRoleAssignmentReturnsOnlyExistingRoleFailures_IgnoresDuplicatesAndPersistsTerminalStep()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRolesBulkResult
            {
                Failed =
                [
                    new CreateRolesBulkItemResult { ContactId = 99, ErrorCode = AccountErrorCodes.ExistingRole },
                    new CreateRolesBulkItemResult { ContactId = 200, ErrorCode = AccountErrorCodes.ExistingRole },
                    new CreateRolesBulkItemResult { ContactId = 100, ErrorCode = AccountErrorCodes.ExistingRole }
                ]
            });

        var result = await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        result.AccountId.Should().Be(43);
        result.Signatory.ContactId.Should().Be(99);
        _prospect.Verify(
            p => p.UpdateCreationProgressAsync(
                42,
                100,
                It.Is<UpdateProspectCreationProgressRequest>(r => r.CompletedMilestone == ProspectCreationMilestone.RolesAssigned),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _prospect.Verify(
            p => p.MarkProspectCreationFailedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<MarkProspectCreationFailedRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that Gateway waits for Prospect role synchronization before persisting the terminal checkpoint.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRoleSynchronizationIsPending_RetriesBeforePersistingTerminalStep()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        var attempt = 0;
        _prospect.Setup(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                attempt++;
                return attempt == 1
                    ? ProspectRoleSynchronizationOutcome.SynchronizationPending
                    : ProspectRoleSynchronizationOutcome.Synchronized;
            });

        await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        _prospect.Verify(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(
            p => p.UpdateCreationProgressAsync(
                42,
                100,
                It.Is<UpdateProspectCreationProgressRequest>(r => r.CompletedMilestone == ProspectCreationMilestone.RolesAssigned),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that Gateway keeps the prospect resumable at step 8 when role synchronization never arrives.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRoleSynchronizationNeverArrives_ThrowsWithoutPersistingTerminalStep()
    {
        SetupHappyPath(42, "AK-001", 43, 99);
        _prospect.Setup(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProspectRoleSynchronizationOutcome.SynchronizationPending);

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ProspectOrchestrationException>();
        exception.Which.Step.Should().Be(ProspectOrchestrationDiagnosticSteps.ConfirmRoleSynchronization);
        _prospect.Verify(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()), Times.Exactly(3));
        _prospect.Verify(
            p => p.UpdateCreationProgressAsync(
                42,
                100,
                It.Is<UpdateProspectCreationProgressRequest>(r => r.CompletedMilestone == ProspectCreationMilestone.RolesAssigned),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospect.Verify(
            p => p.MarkProspectCreationFailedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<MarkProspectCreationFailedRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenIncompleteProspectAlreadyReachedStep4_ResumesWithoutRepeatingFreshSteps()
    {
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync("12345678901234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteProspectCreationState
            {
                ProspectId = 42,
                LegalName = "ACME SARL",
                CreationStatus = 2,
                LastCompletedStep = 4,
                CompletedMilestone = ProspectCreationMilestone.AkuiteoCustomerCreated,
                AkuiteoAccountNumber = "AK-001",
                ResumeRequestFingerprint = BuildResumeFingerprint()
            });
        _prospect.Setup(p => p.UpdateCreationProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.PrepareCreationResumeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.UpdateProspectIdsAsync(42, "AK-001", 43, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FinalizeProspectOutcome.Updated);
        _accountService.Setup(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountCreated { AccountId = 43 });
        _contactService.Setup(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCreated { ContactId = 99 });
        _registry.Setup(r => r.CreateAkuiteoContactAsync("AK-001", It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.UpdateCreationProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProspectRoleSynchronizationOutcome.Synchronized);
        _prospect.Setup(p => p.MarkProspectCreationFailedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        result.AccountNumber.Should().Be("AK-001");
        _registry.Verify(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.GetInpiCompanyInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CreateProspectAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.PrepareCreationResumeAsync(42, 100, It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.CreateAkuiteoContactAsync("AK-001", It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenResumePreparationFails_ThrowsDedicatedResumePreparationStep()
    {
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync("12345678901234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteProspectCreationState
            {
                ProspectId = 42,
                LegalName = "ACME SARL",
                CreationStatus = 2,
                LastCompletedStep = 4,
                CompletedMilestone = ProspectCreationMilestone.AkuiteoCustomerCreated,
                AkuiteoAccountNumber = "AK-001",
                ResumeRequestFingerprint = BuildResumeFingerprint()
            });
        _prospect.Setup(p => p.PrepareCreationResumeAsync(42, 100, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("resume"));

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ProspectOrchestrationException>();
        exception.Which.Step.Should().Be(ProspectOrchestrationDiagnosticSteps.PrepareCreationResume);
        exception.Which.StepName.Should().Be("PrepareCreationResume");
    }

    [Fact]
    public async Task CreateAsync_WhenResumePayloadDoesNotMatchCheckpoint_ThrowsConflictAndSkipsResume()
    {
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync("12345678901234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteProspectCreationState
            {
                ProspectId = 42,
                LegalName = "ACME SARL",
                CreationStatus = 2,
                LastCompletedStep = 4,
                CompletedMilestone = ProspectCreationMilestone.AkuiteoCustomerCreated,
                AkuiteoAccountNumber = "AK-001",
                ResumeRequestFingerprint = "DIFFERENT"
            });

        Func<Task> act = () => _service.CreateAsync(BuildRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<ProspectResumePayloadMismatchException>();
        _prospect.Verify(p => p.PrepareCreationResumeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenIncompleteProspectAlreadyReachedFinalization_ResumesAtRoleAssignment()
    {
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync("12345678901234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteProspectCreationState
            {
                ProspectId = 42,
                LegalName = "ACME SARL",
                CreationStatus = 1,
                LastCompletedStep = 8,
                CompletedMilestone = ProspectCreationMilestone.RydgeContactCreated,
                AkuiteoAccountNumber = "AK-001",
                PendingAccountId = 43,
                PendingSignatoryContactId = 99,
                ResumeRequestFingerprint = BuildResumeFingerprint()
            });
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRolesBulkResult());
        _prospect.Setup(p => p.UpdateCreationProgressAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.GetCreationRoleSynchronizationOutcomeAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProspectRoleSynchronizationOutcome.Synchronized);
        _prospect.Setup(p => p.MarkProspectCreationFailedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<MarkProspectCreationFailedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        result.AccountId.Should().Be(43);
        result.Signatory.ContactId.Should().Be(99);
        _registry.Verify(r => r.SiretExistsInAkuiteoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.CreateAkuiteoCustomerAsync(It.IsAny<CreateProspectRequest>(), It.IsAny<InpiCompanyInfo>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.CreateAkuiteoContactAsync(It.IsAny<string>(), It.IsAny<SignatoryDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateAccountForProspectAsync(It.IsAny<CreateAccountRequest>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        _contactService.Verify(c => c.CreateContactForProspectAsync(It.IsAny<CreateContactRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.UpdateProspectIdsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _accountService.Verify(a => a.CreateRolesAsync(43, It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.UpdateCreationProgressAsync(42, 100, It.Is<UpdateProspectCreationProgressRequest>(r => r.CompletedMilestone == ProspectCreationMilestone.RolesAssigned), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.PrepareCreationResumeAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a signatory with COFFRE_FORT_NUMERIQUE contact type gets ContactFlagPortailFactures set to true in the role bulk.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenSignatoryHasCoffreFortNumerique_SetsContactFlagPortailFacturesToTrue()
    {
        var contactId = 99;
        IReadOnlyCollection<CreateRolesBulkItem>? capturedItems = null;
        SetupHappyPath(42, "AK-001", 43, contactId);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<CreateRolesBulkItem>, int?, CancellationToken>((_, items, _, _) => capturedItems = items)
            .ReturnsAsync(new CreateRolesBulkResult());

        var request = BuildRequest();
        request.Signatory.ContactTypes = ["COFFRE_FORT_NUMERIQUE"];

        await _service.CreateAsync(request, CancellationToken.None);

        capturedItems.Should().NotBeNull();
        capturedItems!.Single(i => i.ContactId == contactId).ContactFlagPortailFactures.Should().BeTrue();
        capturedItems!.Where(i => i.ContactId != contactId).Should().AllSatisfy(i => i.ContactFlagPortailFactures.Should().BeNull());
    }

    /// <summary>
    /// Verifies that COFFRE_FORT_NUMERIQUE detection is case-insensitive.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenSignatoryHasCoffreFortNumeriqueInLowercase_SetsContactFlagPortailFacturesToTrue()
    {
        var contactId = 99;
        IReadOnlyCollection<CreateRolesBulkItem>? capturedItems = null;
        SetupHappyPath(42, "AK-001", 43, contactId);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<CreateRolesBulkItem>, int?, CancellationToken>((_, items, _, _) => capturedItems = items)
            .ReturnsAsync(new CreateRolesBulkResult());

        var request = BuildRequest();
        request.Signatory.ContactTypes = ["coffre_fort_numerique"];

        await _service.CreateAsync(request, CancellationToken.None);

        capturedItems.Should().NotBeNull();
        capturedItems!.Single(i => i.ContactId == contactId).ContactFlagPortailFactures.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that ContactFlagPortailFactures stays null when COFFRE_FORT_NUMERIQUE is absent from contact types.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenSignatoryDoesNotHaveCoffreFortNumerique_LeavesContactFlagPortailFacturesNull()
    {
        var contactId = 99;
        IReadOnlyCollection<CreateRolesBulkItem>? capturedItems = null;
        SetupHappyPath(42, "AK-001", 43, contactId);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<CreateRolesBulkItem>, int?, CancellationToken>((_, items, _, _) => capturedItems = items)
            .ReturnsAsync(new CreateRolesBulkResult());

        var request = BuildRequest();
        request.Signatory.ContactTypes = ["SIGNATAIRE", "RECOUVREMENT"];

        await _service.CreateAsync(request, CancellationToken.None);

        capturedItems.Should().NotBeNull();
        capturedItems!.Should().AllSatisfy(i => i.ContactFlagPortailFactures.Should().BeNull());
    }
}
