using ApiGateway.Account;
using ApiGateway.Contact;
using ApiGateway.Exceptions;
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
    private readonly Mock<IAdditionalSupportingDocumentUploadStrategy> _additionalSupportingDocumentUploadStrategy;
    private readonly Mock<ILogger<ProspectOrchestrationService>> _logger;
    private readonly List<IProspectStepCompletionStrategy> _stepCompletionStrategies;
    private readonly ProspectOrchestrationService _service;

    public ProspectOrchestrationServiceTests()
    {
        _registry = new Mock<IRegistryProspectClient>();
        _prospect = new Mock<IProspectApiClient>();
        _accountService = new Mock<IAccountService>();
        _contactService = new Mock<IContactService>();
        _additionalSupportingDocumentUploadStrategy = new Mock<IAdditionalSupportingDocumentUploadStrategy>();
        _logger = new Mock<ILogger<ProspectOrchestrationService>>();
        _stepCompletionStrategies =
        [
            new BeneficiaryStepCompletionStrategy(
                _registry.Object,
                _prospect.Object,
                Mock.Of<ILogger<BeneficiaryStepCompletionStrategy>>()),
            new DefaultStepCompletionStrategy(
                _prospect.Object,
                Mock.Of<ILogger<DefaultStepCompletionStrategy>>())
        ];
        _service = new ProspectOrchestrationService(
            _registry.Object,
            _prospect.Object,
            _stepCompletionStrategies,
            _additionalSupportingDocumentUploadStrategy.Object,
            _accountService.Object,
            _contactService.Object,
            _logger.Object);
        _prospect
            .Setup(p => p.PersistBeneficiariesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
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
        _prospect.Setup(p => p.PersistBeneficiariesAsync(prospectId, It.IsAny<CancellationToken>()))
            .Callback(() => executedSteps.Add("Beneficiary"))
            .ReturnsAsync(true);

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
        executedSteps.Should().Equal("patch", "accountRoles", "Beneficiary");
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
        _prospect.Verify(p => p.UpdateCreationProgressAsync(prospectId, 100, It.IsAny<UpdateProspectCreationProgressRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
        _prospect.Verify(p => p.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.PersistBeneficiariesAsync(prospectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the same collaborator can receive both prospect collaborator roles.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenSameCollaboratorIsAccountManagerAndCaseManager_SendsBothRoleCodes()
    {
        IReadOnlyCollection<CreateRolesBulkItem>? capturedItems = null;
        var request = BuildRequest();
        request.CaseManagerContactId = request.AccountManagerContactId;

        SetupHappyPath(42, "AK-001", 43, 99);
        _accountService.Setup(a => a.CreateRolesAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyCollection<CreateRolesBulkItem>, int?, CancellationToken>((_, items, _, _) => capturedItems = items)
            .ReturnsAsync(new CreateRolesBulkResult());

        await _service.CreateAsync(request, CancellationToken.None);

        capturedItems.Should().NotBeNull();
        capturedItems!.Where(item => item.ContactId == request.AccountManagerContactId)
            .Should()
            .BeEquivalentTo(new[]
            {
                new CreateRolesBulkItem { ContactId = request.AccountManagerContactId, IsSignatory = false, RoleCode = "AM" },
                new CreateRolesBulkItem { ContactId = request.AccountManagerContactId, IsSignatory = false, RoleCode = "CLP" }
            });
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
    [InlineData(12)]
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
            case 12:
                _prospect.Setup(p => p.PersistBeneficiariesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new HttpRequestException("12"));
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
            p => p.UpdateCreationProgressAsync(
                42,
                100,
                It.Is<UpdateProspectCreationProgressRequest>(r => r.CompletedMilestone == ProspectCreationMilestone.BeneficiariesPersisted),
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
    /// Verifies that a retry from the role checkpoint executes only the final beneficiary step.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenRolesAreAlreadyAssigned_ResumesAtBeneficiaryPersistence()
    {
        _prospect.Setup(p => p.GetIncompleteProspectBySiretAsync("12345678901234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncompleteProspectCreationState
            {
                ProspectId = 42,
                LegalName = "ACME SARL",
                CreationStatus = ProspectCreationStatus.Completed,
                LastCompletedStep = 9,
                CompletedMilestone = ProspectCreationMilestone.RolesAssigned,
                AkuiteoAccountNumber = "AK-001",
                PendingAccountId = 43,
                PendingSignatoryContactId = 99,
                ResumeRequestFingerprint = BuildResumeFingerprint()
            });
        _prospect.Setup(p => p.UpdateCreationProgressAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<UpdateProspectCreationProgressRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.CreateAsync(BuildRequest(), CancellationToken.None);

        result.AccountId.Should().Be(43);
        _accountService.Verify(
            service => service.CreateRolesAsync(
                It.IsAny<int>(),
                It.IsAny<IReadOnlyCollection<CreateRolesBulkItem>>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _prospect.Verify(p => p.PersistBeneficiariesAsync(42, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(
            p => p.UpdateCreationProgressAsync(
                42,
                100,
                It.Is<UpdateProspectCreationProgressRequest>(
                    request => request.CompletedMilestone == ProspectCreationMilestone.BeneficiariesPersisted),
                It.IsAny<CancellationToken>()),
            Times.Once);
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

    /// <summary>
    /// Verifies that the beneficiary completion flow short-circuits when Prospect returns no documents to upload.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenNoDocumentsArePending_ReturnsEmptyResultWithoutRegistryCalls()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [],
                DocumentsToUploadToExternalServiceStatus.StepAlreadyCompleted));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
        _prospect.Verify(p => p.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(It.IsAny<int>(), It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the beneficiary completion flow short-circuits when the step is already completed.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenStepIsAlreadyCompleted_ReturnsEmptyResultWithoutSideEffects()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [],
                DocumentsToUploadToExternalServiceStatus.StepAlreadyCompleted));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
        _prospect.Verify(p => p.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(It.IsAny<int>(), It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the orchestrator downloads each document, uploads it to Registry, persists the result in Prospect, and finalizes the step.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenDocumentsAreUploadedSuccessfully_ConsolidatesAndCompletesStep()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"));
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request => request.StepName == "Beneficiary" && request.SucceededDocumentIds.Count == 1 && request.SucceededDocumentIds[0] == 7 && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([7], []));
        _prospect.Setup(p => p.CompleteStepAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEquivalentTo([7]);
        result.FailedDocumentIds.Should().BeEmpty();
        _prospect.Verify(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(42, It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CompleteStepAsync(42, "Beneficiary", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that each successful document upload is persisted before processing the next document.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenMultipleDocumentsSucceed_PersistsEachSuccessImmediately()
    {
        var operations = new List<string>();
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7, 8],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "doc-7.pdf"));
        _prospect.Setup(p => p.GetDocumentAsync(42, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([4, 5, 6], "application/pdf", "doc-8.pdf"));
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "doc-7.pdf"),
                It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("upload-7"))
            .ReturnsAsync(true);
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync(
                "AK-001",
                It.Is<ProspectDocumentContentResponse>(document => document.FileName == "doc-8.pdf"),
                It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("upload-8"))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.SucceededDocumentIds.SequenceEqual(new[] { 7 })
                    && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("persist-7"))
            .ReturnsAsync(new DocumentUploadResultResponse([7], []));
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.SucceededDocumentIds.SequenceEqual(new[] { 8 })
                    && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("persist-8"))
            .ReturnsAsync(new DocumentUploadResultResponse([8], []));
        _prospect.Setup(p => p.CompleteStepAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .Callback(() => operations.Add("complete"))
            .Returns(Task.CompletedTask);

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().Equal(7, 8);
        result.FailedDocumentIds.Should().BeEmpty();
        operations.Should().Equal("upload-7", "persist-7", "upload-8", "persist-8", "complete");
    }

    /// <summary>
    /// Verifies that beneficiary step matching is case-insensitive and still uses the specialized upload strategy.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenBeneficiaryStepNameUsesDifferentCasing_UsesBeneficiaryStrategy()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [],
                DocumentsToUploadToExternalServiceStatus.StepAlreadyCompleted));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
        _prospect.Verify(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "beneficiary", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a missing upload plan from Prospect stops the beneficiary flow with a 404.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenUploadPlanIsMissing_Throws404WithoutSideEffects()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentsToUploadToExternalServiceResponse?)null);

        Func<Task> act = () => _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _prospect.Verify(p => p.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(It.IsAny<int>(), It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a missing Akuitéo account number stops before any document download.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenAkuiteoAccountNumberIsMissing_Throws400WithoutDownloadingDocuments()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                " ",
                [7],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));

        Func<Task> act = () => _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        _prospect.Verify(p => p.GetDocumentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(It.IsAny<int>(), It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a document download failure is collected as failed and does not stop remaining document uploads.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenOneDocumentCannotBeDownloaded_RegistersFailureAndContinues()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7, 8],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProspectDocumentContentResponse?)null);
        _prospect.Setup(p => p.GetDocumentAsync(42, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"));
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.StepName == "Beneficiary"
                    && request.SucceededDocumentIds.SequenceEqual(new[] { 8 })
                    && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([8], []));
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.StepName == "Beneficiary"
                    && request.SucceededDocumentIds.Count == 0
                    && request.FailedDocumentIds.SequenceEqual(new[] { 7 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([], [7]));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().Equal(8);
        result.FailedDocumentIds.Should().Equal(7);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(42, It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that Registry upload failures are collected and prevent final step completion.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenRegistryUploadFails_RegistersFailureAndDoesNotCompleteStep()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"));
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.SucceededDocumentIds.Count == 0
                    && request.FailedDocumentIds.SequenceEqual(new[] { 7 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([], [7]));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().Equal(7);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a mixed Registry result immediately persists successes, persists failures at the end, and prevents step completion.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenSomeDocumentsFail_RegistersSuccessImmediatelyAndDoesNotCompleteStep()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7, 8],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"));
        _registry.SetupSequence(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.SucceededDocumentIds.SequenceEqual(new[] { 7 })
                    && request.FailedDocumentIds.Count == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([7], []));
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.Is<DocumentUploadResultRequest>(request =>
                    request.SucceededDocumentIds.Count == 0
                    && request.FailedDocumentIds.SequenceEqual(new[] { 8 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentUploadResultResponse([], [8]));

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().Equal(7);
        result.FailedDocumentIds.Should().Equal(8);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.RegisterDocumentUploadResultAsync(42, It.IsAny<DocumentUploadResultRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that the beneficiary flow fails if Prospect cannot persist the consolidated upload result.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenUploadResultCannotBePersisted_Throws404AndDoesNotCompleteStep()
    {
        _prospect.Setup(p => p.GetDocumentsToUploadToExternalServiceAsync(42, "Beneficiary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentsToUploadToExternalServiceResponse(
                "AK-001",
                [7],
                DocumentsToUploadToExternalServiceStatus.PendingDocuments));
        _prospect.Setup(p => p.GetDocumentAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProspectDocumentContentResponse([1, 2, 3], "application/pdf", "PASSEPORT_DUPONT_Jean"));
        _registry.Setup(r => r.UploadAkuiteoDocumentAsync("AK-001", It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _prospect.Setup(p => p.RegisterDocumentUploadResultAsync(
                42,
                It.IsAny<DocumentUploadResultRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentUploadResultResponse?)null);

        Func<Task> act = () => _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "Beneficiary" }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that a non-specialized step is completed through the fallback strategy without document uploads.
    /// </summary>
    [Fact]
    public async Task CompleteStepAsync_WhenStepHasNoSpecializedStrategy_CompletesStepWithoutDocumentUpload()
    {
        _prospect.Setup(p => p.CompleteStepAsync(42, "SupportingDocuments", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.CompleteStepAsync(42, new CompleteStepRequest { StepName = "SupportingDocuments" }, CancellationToken.None);

        result.SucceededDocumentIds.Should().BeEmpty();
        result.FailedDocumentIds.Should().BeEmpty();
        _prospect.Verify(p => p.CompleteStepAsync(42, "SupportingDocuments", It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentsToUploadToExternalServiceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.UploadAkuiteoDocumentAsync(It.IsAny<string>(), It.IsAny<ProspectDocumentContentResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #region UploadSupportingDocumentAsync Tests

    /// <summary>
    /// Verifies that when upload succeeds and all mandatory documents are uploaded, the step is completed automatically.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadSucceeds_AllMandatoryDocsUploaded_CompletesStep()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "AUTRES", MaxFiles = 3, Documents = [] }
                ]
            });

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(prospectId, "SupportingDocuments", It.IsAny<CancellationToken>(), currentUserId), Times.Once);
    }

    /// <summary>
    /// Verifies that complementary supporting documents are sent directly to Akuitéo without completing the step.
    /// </summary>
    [Theory]
    [InlineData("AUTRES")]
    [InlineData("Autres")]
    public async Task UploadSupportingDocumentAsync_WhenDocumentTypeIsAutres_UploadsDirectlyWithoutStepCompletion(string documentType)
    {
        var prospectId = 42;
        var currentUserId = 100;
        var documentId = 99;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = documentType,
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(documentId));
        _additionalSupportingDocumentUploadStrategy
            .Setup(strategy => strategy.UploadAsync(prospectId, documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentExternalUploadBatchResult([documentId], []));

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _additionalSupportingDocumentUploadStrategy.Verify(
            strategy => strategy.UploadAsync(prospectId, documentId, It.IsAny<CancellationToken>()),
            Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
    }

    /// <summary>
    /// Verifies that complementary document Akuitéo upload failures do not fail the stored document upload.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenAutresAkuiteoUploadFails_DoesNotThrowOrCompleteStep()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var documentId = 99;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "AUTRES",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(documentId));
        _additionalSupportingDocumentUploadStrategy
            .Setup(strategy => strategy.UploadAsync(prospectId, documentId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Akuitéo unavailable"));

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.GetDocumentRequirementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to upload complementary supporting document")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that concurrent supporting document uploads for the same prospect serialize automatic step completion.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenConcurrentUploadsBecomeReady_SerializesStepCompletion()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var firstRequest = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };
        var secondRequest = new UploadSupportingDocumentRequest
        {
            DocumentType = "STATUTS",
            File = Mock.Of<IFormFile>()
        };
        var completionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeCompletions = 0;
        var maximumConcurrentCompletions = 0;
        var completionCalls = 0;
        var stepCompletionStrategy = new Mock<IProspectStepCompletionStrategy>();

        stepCompletionStrategy.SetupGet(s => s.Priority).Returns(100);
        stepCompletionStrategy.Setup(s => s.CanHandle("SupportingDocuments")).Returns(true);
        stepCompletionStrategy
            .Setup(s => s.CompleteAsync(prospectId, "SupportingDocuments", It.IsAny<CancellationToken>(), currentUserId))
            .Returns<int, string, CancellationToken, int?>(async (_, _, _, _) =>
            {
                var callNumber = Interlocked.Increment(ref completionCalls);
                var active = Interlocked.Increment(ref activeCompletions);
                maximumConcurrentCompletions = Math.Max(maximumConcurrentCompletions, active);

                try
                {
                    if (callNumber == 1)
                    {
                        completionStarted.SetResult();
                        await releaseFirstCompletion.Task;
                    }

                    return new DocumentExternalUploadBatchResult([], []);
                }
                finally
                {
                    Interlocked.Decrement(ref activeCompletions);
                }
            });

        var service = new ProspectOrchestrationService(
            _registry.Object,
            _prospect.Object,
            [stepCompletionStrategy.Object],
            _additionalSupportingDocumentUploadStrategy.Object,
            _accountService.Object,
            _contactService.Object,
            _logger.Object);

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, firstRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(41));
        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, secondRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));
        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] }
                ]
            });

        var firstUpload = service.UploadSupportingDocumentAsync(prospectId, currentUserId, firstRequest, CancellationToken.None);
        await completionStarted.Task;
        var secondUpload = service.UploadSupportingDocumentAsync(prospectId, currentUserId, secondRequest, CancellationToken.None);

        await Task.Delay(50);
        completionCalls.Should().Be(1);

        releaseFirstCompletion.SetResult();
        await Task.WhenAll(firstUpload, secondUpload);

        completionCalls.Should().Be(2);
        maximumConcurrentCompletions.Should().Be(1);
        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, It.IsAny<UploadSupportingDocumentRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Exactly(4));
        stepCompletionStrategy.Verify(s => s.CompleteAsync(prospectId, "SupportingDocuments", It.IsAny<CancellationToken>(), currentUserId), Times.Exactly(2));
    }

    /// <summary>
    /// Verifies that automatic completion is skipped when the in-lock requirements refresh fails.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenRefreshedRequirementsAreNull_DoesNotCompleteStep()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.SetupSequence(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] }
                ]
            })
            .ReturnsAsync((DocumentRequirementsResponse?)null);

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Could not retrieve document requirements")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that automatic completion is skipped when the in-lock requirements refresh is no longer complete.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_WhenRefreshedRequirementsAreNotComplete_DoesNotCompleteStep()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.SetupSequence(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] }
                ]
            })
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [] }
                ]
            });

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int?>()), Times.Never);
    }

    /// <summary>
    /// Verifies that when upload succeeds but not all mandatory documents are uploaded, the step is not completed.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadSucceeds_NotAllMandatoryDocsUploaded_DoesNotCompleteStep()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [] }
                ]
            });

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that when upload succeeds but requirements check fails, step completed is false.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadSucceeds_RequirementsCheckFails_ReturnsStepCompletedFalse()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentRequirementsResponse?)null);

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Could not retrieve document requirements")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when upload succeeds and all mandatory documents are uploaded but no strategy is found, step completed is false.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadSucceeds_StrategyNotFound_ReturnsStepCompletedFalse()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        var serviceWithoutSupportingDocsStrategy = new ProspectOrchestrationService(
            _registry.Object,
            _prospect.Object,
            [],
            _additionalSupportingDocumentUploadStrategy.Object,
            _accountService.Object,
            _contactService.Object,
            _logger.Object);

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] }
                ]
            });

        await serviceWithoutSupportingDocsStrategy.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.CompleteStepAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("No strategy found for SupportingDocuments")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that when upload succeeds and strategy throws, step completed is false but document ID is still returned.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadSucceeds_StrategyThrows_ReturnsStepCompletedFalse()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.Success(42));

        _prospect.Setup(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentRequirementsResponse
            {
                Documents =
                [
                    new RequiredDocumentItem { Type = "KBIS", MaxFiles = 1, Documents = [new object()] },
                    new RequiredDocumentItem { Type = "STATUTS", MaxFiles = 1, Documents = [new object()] }
                ]
            });

        _prospect.Setup(p => p.CompleteStepAsync(prospectId, "SupportingDocuments", It.IsAny<CancellationToken>(), currentUserId))
            .ThrowsAsync(new HttpRequestException("Strategy failed"));

        await _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        _prospect.Verify(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()), Times.Once);
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(prospectId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _prospect.Verify(p => p.CompleteStepAsync(prospectId, "SupportingDocuments", It.IsAny<CancellationToken>(), currentUserId), Times.Once);
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to complete supporting documents step")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that upload validation errors throw GatewayException with 400 status.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadFails_ValidationError_ThrowsGatewayException400()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.ValidationError("DocumentType", "INVALID_TYPE", "Invalid document type"));

        Func<Task> act = () => _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Code.Should().Be("INVALID_TYPE");
        exception.Which.Message.Should().Be("Invalid document type");
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that file too large errors throw GatewayException with 413 status.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadFails_FileTooLarge_ThrowsGatewayException413()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.FileTooLarge("FILE_TOO_LARGE", "File exceeds maximum size of 10MB"));

        Func<Task> act = () => _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status413RequestEntityTooLarge);
        exception.Which.Code.Should().Be("FILE_TOO_LARGE");
        exception.Which.Message.Should().Be("File exceeds maximum size of 10MB");
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that prospect not found errors throw GatewayException with 404 status.
    /// </summary>
    [Fact]
    public async Task UploadSupportingDocumentAsync_UploadFails_ProspectNotFound_ThrowsGatewayException404()
    {
        var prospectId = 42;
        var currentUserId = 100;
        var request = new UploadSupportingDocumentRequest
        {
            DocumentType = "KBIS",
            File = Mock.Of<IFormFile>()
        };

        _prospect.Setup(p => p.UploadSupportingDocumentAsync(prospectId, currentUserId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UploadSupportingDocumentResult.ProspectNotFound());

        Func<Task> act = () => _service.UploadSupportingDocumentAsync(prospectId, currentUserId, request, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<GatewayException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.Code.Should().Be(Errors.NullArgumentCode);
        exception.Which.Message.Should().Contain($"Prospect {prospectId} not found");
        _prospect.Verify(p => p.GetDocumentRequirementsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
