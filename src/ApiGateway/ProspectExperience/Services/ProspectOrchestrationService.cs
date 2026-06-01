using ApiGateway.Account;
using ApiGateway.Account.Constants;
using ApiGateway.Configuration;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Constants;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
using ApiGateway.ProspectExperience.Policies;
using Newtonsoft.Json;

namespace ApiGateway.ProspectExperience.Services;

public class ProspectOrchestrationService(
    IRegistryProspectClient registryClient,
    IProspectApiClient prospectClient,
    IAccountService accountService,
    IContactService contactService,
    ILogger<ProspectOrchestrationService> logger) : IProspectService
{
    public async Task<ProspectListItem> CreateAsync(CreateProspectRequest request, CancellationToken ct)
    {
        var siret = request.Siret;
        var runtimeState = new ProspectCreationRuntimeState();
        var auditUserId = ResolveAuditUserId(request);
        var requestFingerprint = ProspectResumeRequestFingerprint.Build(request);
        var sanitizedPayload = JsonConvert.SerializeObject(new
        {
            request.Siret,
            request.LegalForm,
            request.LegalStructure,
            request.CaseManagerContactId,
            request.AccountManagerContactId,
            request.Department,
            request.Region,
            request.Country,
            Signatory = new
            {
                request.Signatory.Title,
                request.Signatory.LastName,
                request.Signatory.FirstName,
                request.Signatory.JobTitle,
                request.Signatory.Department,
                request.Signatory.CompanyRole,
                request.Signatory.ContactTypes,
                request.Signatory.Email
            }
        });

        var resumeState = await prospectClient.GetIncompleteProspectBySiretAsync(siret, ct);
        var canResume = ProspectResumePolicy.CanResumeFromCheckpoint(resumeState);

        if (!canResume)
        {
            await CheckSiretNotInAkuiteoAsync(siret, sanitizedPayload, ct);
            var inpi = await FetchInpiAsync(siret, sanitizedPayload, ct);
            runtimeState.LegalName = inpi.LegalName;
            runtimeState.ProspectId = await CreateProspectAsync(request, inpi, sanitizedPayload, ct);

            try
            {
                runtimeState.AccountNumber = await CreateAkuiteoCustomerAsync(request, inpi, runtimeState.ProspectId.Value, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.AkuiteoCustomerCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.AkuiteoCustomerCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateAkuiteoCustomer,
                    siret,
                    sanitizedPayload,
                    ct);

                await CreateAkuiteoContactAsync(runtimeState.AccountNumber, request.Signatory, siret, runtimeState.ProspectId.Value, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.AkuiteoContactCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.AkuiteoContactCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateAkuiteoContact,
                    siret,
                    sanitizedPayload,
                    ct);

                runtimeState.AccountId = await CreateRydgeAccountAsync(request, runtimeState.LegalName, siret, runtimeState.AccountNumber, runtimeState.ProspectId.Value, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.RydgeAccountCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.RydgeAccountCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateRydgeAccount,
                    siret,
                    sanitizedPayload,
                    ct);

                runtimeState.ContactId = await CreateRydgeContactAsync(request, siret, runtimeState.ProspectId.Value, runtimeState.AccountNumber, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.RydgeContactCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.RydgeContactCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateRydgeContact,
                    siret,
                    sanitizedPayload,
                    ct);

                await PatchProspectIdsAsync(runtimeState.ProspectId.Value, runtimeState.AccountNumber, runtimeState.AccountId.Value, runtimeState.ContactId.Value, siret, sanitizedPayload, ct);
                runtimeState.FinalizationCompleted = true;

                await AssignRolesAndPersistAsync(request, runtimeState, auditUserId, siret, sanitizedPayload, ct);

                return BuildCreatedProspect(runtimeState.AccountId.Value, runtimeState.AccountNumber, runtimeState.LegalName, request.Signatory, runtimeState.ContactId.Value);
            }
            catch (ProspectOrchestrationException) when (runtimeState.ProspectId.HasValue)
            {
                await TryMarkProspectCreationFailedAsync(
                    runtimeState,
                    auditUserId,
                    siret,
                    ct);
                throw;
            }
        }

        var resumableState = resumeState!;

        if (!ProspectResumePolicy.HasMatchingPayload(resumableState, requestFingerprint))
        {
            logger.LogWarning(
                "[Orchestration]: cannot resume incomplete prospect because the retry payload changed - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [LastCompletedStep]: {LastCompletedStep}",
                siret,
                resumableState.ProspectId,
                resumableState.LastCompletedStep);
            throw new ProspectResumePayloadMismatchException(siret);
        }

        if (ProspectResumePolicy.RequiresResumePreparation(resumableState))
        {
            await PrepareProspectCreationResumeAsync(resumableState.ProspectId, auditUserId, siret, sanitizedPayload, ct);
        }

        runtimeState.ProspectId = resumableState.ProspectId;
        runtimeState.LegalName = resumableState.LegalName;
        runtimeState.LastCompletedMilestone = resumableState.CompletedMilestone;
        runtimeState.AccountNumber = resumableState.AkuiteoAccountNumber;
        runtimeState.AccountId = resumableState.PendingAccountId;
        runtimeState.ContactId = resumableState.PendingSignatoryContactId;
        runtimeState.FinalizationCompleted = resumableState.CreationStatus == ProspectCreationStatus.Completed;

        logger.LogInformation(
            "[Orchestration]: resuming incomplete prospect - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [LastCompletedStep]: {LastCompletedStep}",
            siret,
            runtimeState.ProspectId,
            resumableState.LastCompletedStep);

        try
        {
            runtimeState.AccountNumber = ProspectResumePolicy.RequireAccountNumber(resumableState, siret);

            if (runtimeState.LastCompletedMilestone == ProspectCreationMilestone.AkuiteoCustomerCreated)
            {
                await CreateAkuiteoContactAsync(runtimeState.AccountNumber, request.Signatory, siret, runtimeState.ProspectId.Value, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.AkuiteoContactCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.AkuiteoContactCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateAkuiteoContact,
                    siret,
                    sanitizedPayload,
                    ct);
            }

            if (runtimeState.LastCompletedMilestone == ProspectCreationMilestone.AkuiteoContactCreated)
            {
                runtimeState.AccountId = await CreateRydgeAccountAsync(request, runtimeState.LegalName, siret, runtimeState.AccountNumber, runtimeState.ProspectId.Value, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.RydgeAccountCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.RydgeAccountCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateRydgeAccount,
                    siret,
                    sanitizedPayload,
                    ct);
            }
            else if (runtimeState.LastCompletedMilestone is ProspectCreationMilestone.RydgeAccountCreated or ProspectCreationMilestone.RydgeContactCreated or ProspectCreationMilestone.RolesAssigned)
            {
                runtimeState.AccountId = ProspectResumePolicy.RequireAccountId(resumableState, siret, resumableState.LastCompletedStep);
            }

            if (runtimeState.LastCompletedMilestone == ProspectCreationMilestone.RydgeAccountCreated)
            {
                runtimeState.ContactId = await CreateRydgeContactAsync(request, siret, runtimeState.ProspectId.Value, runtimeState.AccountNumber, sanitizedPayload, ct);
                runtimeState.LastCompletedMilestone = ProspectCreationMilestone.RydgeContactCreated;
                await PersistRuntimeStateAsync(
                    runtimeState,
                    auditUserId,
                    ProspectCreationMilestone.RydgeContactCreated,
                    ProspectOrchestrationDiagnosticSteps.CreateRydgeContact,
                    siret,
                    sanitizedPayload,
                    ct);
            }
            else if (runtimeState.LastCompletedMilestone is ProspectCreationMilestone.RydgeContactCreated or ProspectCreationMilestone.RolesAssigned)
            {
                runtimeState.ContactId = ProspectResumePolicy.RequireSignatoryContactId(resumableState, siret, resumableState.LastCompletedStep);
            }

            if (!runtimeState.FinalizationCompleted)
            {
                await PatchProspectIdsAsync(runtimeState.ProspectId.Value, runtimeState.AccountNumber, runtimeState.AccountId.Value, runtimeState.ContactId.Value, siret, sanitizedPayload, ct);
                runtimeState.FinalizationCompleted = true;
            }

            await AssignRolesAndPersistAsync(request, runtimeState, auditUserId, siret, sanitizedPayload, ct);

            return BuildCreatedProspect(runtimeState.AccountId.Value, runtimeState.AccountNumber, runtimeState.LegalName, request.Signatory, runtimeState.ContactId.Value);
        }
        catch (ProspectOrchestrationException) when (runtimeState.ProspectId.HasValue)
        {
            await TryMarkProspectCreationFailedAsync(
                runtimeState,
                auditUserId,
                siret,
                ct);
            throw;
        }
    }

    private async Task CheckSiretNotInAkuiteoAsync(string siret, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            var exists = await registryClient.SiretExistsInAkuiteoAsync(siret, ct);
            if (exists)
            {
                logger.LogInformation("[Orchestration]: stopped - [Step]: {Step} - [Siret]: {Siret} - [Reason]: SIRET already present in Akuiteo", ProspectOrchestrationDiagnosticSteps.CheckAkuiteoSiret, siret);
                throw new SiretAlreadyExistsException(siret);
            }
        }
        catch (SiretAlreadyExistsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CheckAkuiteoSiret, siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CheckAkuiteoSiret, siret: siret, inner: ex);
        }
    }

    private async Task<InpiCompanyInfo> FetchInpiAsync(string siret, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            return await prospectClient.GetInpiCompanyInfoAsync(siret, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.FetchInpi, siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.FetchInpi, siret: siret, inner: ex);
        }
    }

    private async Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            return await prospectClient.CreateProspectAsync(request, inpi, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CreateProspect, request.Siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CreateProspect, siret: request.Siret, inner: ex);
        }
    }

    private async Task<string> CreateAkuiteoCustomerAsync(CreateProspectRequest request, InpiCompanyInfo inpi, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            var result = await registryClient.CreateAkuiteoCustomerAsync(request, inpi, ct);
            return result.AccountNumber;
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CreateAkuiteoCustomer, request.Siret, prospectId, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CreateAkuiteoCustomer, siret: request.Siret, prospectId: prospectId, inner: ex);
        }
    }

    private async Task CreateAkuiteoContactAsync(string accountNumber, SignatoryDto signatory, string siret, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            await registryClient.CreateAkuiteoContactAsync(accountNumber, signatory, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CreateAkuiteoContact, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CreateAkuiteoContact, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task<int> CreateRydgeAccountAsync(CreateProspectRequest request, string legalName, string siret, string accountNumber, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            var payload = new CreateAccountRequest
            {
                AccountNumber = accountNumber,
                LegalName = legalName,
                Siret = siret,
                AccountType = AccountType.PROSPECT
            };
            var result = await accountService.CreateAccountForProspectAsync(payload, request.CaseManagerContactId, ct);
            return result.AccountId;
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CreateRydgeAccount, request.Siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CreateRydgeAccount, siret: request.Siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task<int> CreateRydgeContactAsync(CreateProspectRequest request, string siret, int prospectId, string accountNumber, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            var payload = CreateContactRequest.FromSignatory(request.Signatory, request.OfficeCode);
            var result = await contactService.CreateContactForProspectAsync(payload, ct);

            return result.ContactId;
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.CreateRydgeContact, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.CreateRydgeContact, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task AssignAccountRolesAsync(
        int signatoryContactId,
        int? caseManagerContactId,
        int accountManagerContactId,
        int accountId,
        ProspectOrchestrationContext context,
        string sanitizedPayload,
        CancellationToken ct)
    {
        try
        {
            var contacts = BuildAccountRoleItems(signatoryContactId, caseManagerContactId, accountManagerContactId);

            logger.LogInformation(
                "[Orchestration]: assigning {RoleCount} account roles - [Step]: {Step} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountId]: {AccountId}",
                contacts.Count,
                ProspectOrchestrationDiagnosticSteps.AssignRoles,
                context.Siret,
                context.ProspectId,
                accountId);

            var result = await accountService.CreateRolesAsync(accountId, contacts, caseManagerContactId, ct);

            var nonDuplicateFailures = result.Failed
                .Where(failure => !string.Equals(failure.ErrorCode, AccountErrorCodes.ExistingRole, StringComparison.Ordinal))
                .ToList();

            if (nonDuplicateFailures.Count > 0)
            {
                var failures = string.Join(", ", nonDuplicateFailures.Select(f => $"contactId={f.ContactId}:{f.ErrorCode}"));
                throw new InvalidOperationException($"Account bulk role creation returned failures: {failures}");
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.AssignRoles, context.Siret, context.ProspectId, context.AccountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.AssignRoles, siret: context.Siret, prospectId: context.ProspectId, accountNumber: context.AccountNumber, inner: ex);
        }
    }

    private async Task PatchProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, string siret, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            for (var attempt = 1; attempt <= ConfigConstants.ProspectFinalizationRetryAttempt; attempt++)
            {
                var outcome = await prospectClient.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, ct);
                if (outcome == FinalizeProspectOutcome.Updated)
                {
                    return;
                }

                if (outcome == FinalizeProspectOutcome.SynchronizationPending)
                {
                    logger.LogWarning(
                        "[Orchestration]: prospect finalization postponed - [Step]: {Step} - [Attempt]: {Attempt}/{MaxAttempts} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                        ProspectOrchestrationDiagnosticSteps.FinalizeProspect,
                        attempt,
                        ConfigConstants.ProspectFinalizationRetryAttempt,
                        siret,
                        prospectId,
                        accountNumber);

                    if (attempt == ConfigConstants.ProspectFinalizationRetryAttempt)
                    {
                        throw new TimeoutException("Prospect synchronized account/contact rows are still unavailable after the bounded retry window.");
                    }

                    await Task.Delay(ConfigConstants.ProspectFinalizationRetryDelayMilliseconds, ct);
                    continue;
                }

                if (outcome == FinalizeProspectOutcome.ProspectNotFound)
                {
                    throw new InvalidOperationException($"Prospect {prospectId} could not be finalized because it was not found.");
                }

                if (outcome == FinalizeProspectOutcome.InvalidCreationStatus)
                {
                    throw new InvalidOperationException($"Prospect {prospectId} could not be finalized because its creation status is invalid.");
                }
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.FinalizeProspect, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.FinalizeProspect, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task PrepareProspectCreationResumeAsync(int prospectId, int currentUserId, string siret, string sanitizedPayload, CancellationToken ct)
    {
        try
        {
            var updated = await prospectClient.PrepareCreationResumeAsync(prospectId, currentUserId, ct);
            if (!updated)
            {
                throw new InvalidOperationException($"Prospect {prospectId} could not be prepared for resume because it was not found.");
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.PrepareCreationResume, siret, prospectId, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.PrepareCreationResume, siret: siret, prospectId: prospectId, inner: ex);
        }
    }

    /// <summary>
    /// Waits for the expected role rows to become visible in Prospect before persisting the terminal checkpoint.
    /// </summary>
    /// <param name="prospectId">The prospect identifier.</param>
    /// <param name="accountNumber">The account number used for diagnostics.</param>
    /// <param name="siret">The target SIRET.</param>
    /// <param name="sanitizedPayload">The sanitized payload used for diagnostic logs.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ConfirmProspectRoleSynchronizationAsync(
        int prospectId,
        string? accountNumber,
        string siret,
        string sanitizedPayload,
        CancellationToken ct)
    {
        try
        {
            for (var attempt = 1; attempt <= ConfigConstants.ProspectRoleSynchronizationRetryAttempt; attempt++)
            {
                var outcome = await prospectClient.GetCreationRoleSynchronizationOutcomeAsync(prospectId, ct);
                if (outcome == ProspectRoleSynchronizationOutcome.Synchronized)
                {
                    return;
                }

                if (outcome == ProspectRoleSynchronizationOutcome.SynchronizationPending)
                {
                    logger.LogWarning(
                        "[Orchestration]: prospect role synchronization still pending - [Step]: {Step} - [Attempt]: {Attempt}/{MaxAttempts} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                        ProspectOrchestrationDiagnosticSteps.ConfirmRoleSynchronization,
                        attempt,
                        ConfigConstants.ProspectRoleSynchronizationRetryAttempt,
                        siret,
                        prospectId,
                        accountNumber ?? "n/a");

                    if (attempt == ConfigConstants.ProspectRoleSynchronizationRetryAttempt)
                    {
                        throw new TimeoutException("Prospect synchronized role rows are still unavailable after the bounded retry window.");
                    }

                    await Task.Delay(ConfigConstants.ProspectRoleSynchronizationRetryDelayMilliseconds, ct);
                    continue;
                }

                throw new InvalidOperationException($"Prospect {prospectId} could not confirm role synchronization because it was not found.");
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(ProspectOrchestrationDiagnosticSteps.ConfirmRoleSynchronization, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(ProspectOrchestrationDiagnosticSteps.ConfirmRoleSynchronization, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task PersistCreationProgressAsync(
        int prospectId,
        int currentUserId,
        UpdateProspectCreationProgressRequest request,
        int diagnosticStep,
        string siret,
        string? accountNumber,
        string sanitizedPayload,
        CancellationToken ct)
    {
        try
        {
            var updated = await prospectClient.UpdateCreationProgressAsync(prospectId, currentUserId, request, ct);
            if (!updated)
            {
                throw new InvalidOperationException($"Prospect {prospectId} could not persist creation progress milestone {request.CompletedMilestone} because it was not found.");
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(diagnosticStep, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(diagnosticStep, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    /// <summary>
    /// Persists the current orchestration runtime checkpoint for the provided milestone.
    /// </summary>
    /// <param name="runtimeState">The in-memory orchestration runtime state.</param>
    /// <param name="currentUserId">The audit user identifier.</param>
    /// <param name="completedMilestone">The completed Gateway-owned milestone to persist in Prospect.</param>
    /// <param name="diagnosticStep">The local diagnostic step used for logs, exceptions, and telemetry.</param>
    /// <param name="siret">The target SIRET.</param>
    /// <param name="sanitizedPayload">The sanitized payload used for diagnostic logs.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task PersistRuntimeStateAsync(
        ProspectCreationRuntimeState runtimeState,
        int currentUserId,
        ProspectCreationMilestone completedMilestone,
        int diagnosticStep,
        string siret,
        string sanitizedPayload,
        CancellationToken ct)
    {
        return PersistCreationProgressAsync(
            runtimeState.ProspectId!.Value,
            currentUserId,
            new UpdateProspectCreationProgressRequest
            {
                CompletedMilestone = completedMilestone,
                AkuiteoAccountNumber = runtimeState.AccountNumber,
                PendingAccountId = runtimeState.AccountId,
                PendingSignatoryContactId = runtimeState.ContactId
            },
            diagnosticStep,
            siret,
            runtimeState.AccountNumber,
            sanitizedPayload,
            ct);
    }

    /// <summary>
    /// Assigns account roles for the current runtime state and persists the terminal checkpoint.
    /// </summary>
    /// <param name="request">The prospect creation request.</param>
    /// <param name="runtimeState">The in-memory orchestration runtime state.</param>
    /// <param name="currentUserId">The audit user identifier.</param>
    /// <param name="siret">The target SIRET.</param>
    /// <param name="sanitizedPayload">The sanitized payload used for diagnostic logs.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AssignRolesAndPersistAsync(
        CreateProspectRequest request,
        ProspectCreationRuntimeState runtimeState,
        int currentUserId,
        string siret,
        string sanitizedPayload,
        CancellationToken ct)
    {
        var roleAssignmentContext = new ProspectOrchestrationContext
        {
            Siret = siret,
            ProspectId = runtimeState.ProspectId,
            AccountNumber = runtimeState.AccountNumber
        };

        await AssignAccountRolesAsync(
            runtimeState.ContactId!.Value,
            request.CaseManagerContactId,
            request.AccountManagerContactId,
            runtimeState.AccountId!.Value,
            roleAssignmentContext,
            sanitizedPayload,
            ct);

        await ConfirmProspectRoleSynchronizationAsync(
            runtimeState.ProspectId!.Value,
            runtimeState.AccountNumber,
            siret,
            sanitizedPayload,
            ct);

        runtimeState.LastCompletedMilestone = ProspectCreationMilestone.RolesAssigned;
        await PersistRuntimeStateAsync(
            runtimeState,
            currentUserId,
            ProspectCreationMilestone.RolesAssigned,
            ProspectOrchestrationDiagnosticSteps.AssignRoles,
            siret,
            sanitizedPayload,
            ct);
    }

    private async Task TryMarkProspectCreationFailedAsync(
        ProspectCreationRuntimeState runtimeState,
        int currentUserId,
        string siret,
        CancellationToken ct)
    {
        if (!runtimeState.ProspectId.HasValue)
        {
            return;
        }

        if (runtimeState.FinalizationCompleted)
        {
            logger.LogInformation(
                "[Orchestration]: skipping failed lifecycle mark because Prospect finalization already succeeded - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                siret,
                runtimeState.ProspectId.Value,
                runtimeState.AccountNumber ?? "n/a");
            return;
        }

        try
        {
            var request = new MarkProspectCreationFailedRequest
            {
                CompletedMilestone = runtimeState.LastCompletedMilestone,
                AkuiteoAccountNumber = runtimeState.AccountNumber,
                PendingAccountId = runtimeState.AccountId,
                PendingSignatoryContactId = runtimeState.ContactId
            };

            var updated = await prospectClient.MarkProspectCreationFailedAsync(runtimeState.ProspectId.Value, currentUserId, request, ct);
            if (updated)
            {
                logger.LogInformation(
                    "[Orchestration]: prospect marked as failed - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                    siret,
                    runtimeState.ProspectId.Value,
                    runtimeState.AccountNumber ?? "n/a");
            }
            else
            {
                logger.LogWarning(
                    "[Orchestration]: prospect could not be marked as failed because it was not found - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                    siret,
                    runtimeState.ProspectId.Value,
                    runtimeState.AccountNumber ?? "n/a");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "[Orchestration]: could not mark prospect as failed - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber}",
                siret,
                runtimeState.ProspectId.Value,
                runtimeState.AccountNumber ?? "n/a");
        }
    }

    private static ProspectListItem BuildCreatedProspect(int accountId, string accountNumber, string legalName, SignatoryDto signatory, int contactId)
    {
        return new ProspectListItem
        {
            AccountId = accountId,
            AccountNumber = accountNumber,
            AccountType = AccountType.PROSPECT,
            LegalName = legalName,
            Signatory = new ProspectSignatoryListItem
            {
                ContactId = contactId,
                FirstName = signatory.FirstName,
                LastName = signatory.LastName,
                Email = signatory.Email
            },
            StepCode = ProspectStepCode.InProgress
        };
    }

    private static int ResolveAuditUserId(CreateProspectRequest request)
    {
        return request.CaseManagerContactId ?? request.AccountManagerContactId;
    }

    private void LogStepFailure(int step, string siret, int? prospectId, string? accountNumber, string sanitizedPayload, Exception ex)
    {
        logger.LogError(
            ex,
            "[Orchestration]: failed - [Step]: {Step} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountNumber]: {AccountNumber} - [Payload]: {Payload} - [Reason]: {Reason}",
            step,
            siret,
            prospectId?.ToString() ?? "n/a",
            accountNumber ?? "n/a",
            sanitizedPayload,
            ex.Message);
    }

    private static IReadOnlyCollection<CreateRolesBulkItem> BuildAccountRoleItems(
        int signatoryContactId,
        int? caseManagerContactId,
        int accountManagerContactId)
    {
        var items = new List<CreateRolesBulkItem>
        {
            new() { ContactId = signatoryContactId, IsSignatory = true },
            new() { ContactId = accountManagerContactId, IsSignatory = false, RoleCode = RoleCodes.AccountManager }
        };
        if (caseManagerContactId.HasValue)
        {
            items.Add(new CreateRolesBulkItem
            {
                ContactId = caseManagerContactId.Value,
                IsSignatory = false,
                RoleCode = RoleCodes.CaseManager
            });
        }

        return items
            .GroupBy(item => item.ContactId)
            .Select(group => new CreateRolesBulkItem
            {
                ContactId = group.Key,
                IsSignatory = group.Any(item => item.IsSignatory == true),
                RoleCode = group.Select(item => item.RoleCode).FirstOrDefault(code => code is not null)
            })
            .ToArray();
    }
}
