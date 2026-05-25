using ApiGateway.Account;
using ApiGateway.Account.Constants;
using ApiGateway.Contact;
using ApiGateway.ProspectExperience.Enum;
using ApiGateway.ProspectExperience.Exceptions;
using ApiGateway.ProspectExperience.Models.Internal;
using ApiGateway.ProspectExperience.Models.Requests;
using ApiGateway.ProspectExperience.Models.Responses;
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
        int? prospectId = null;
        string? accountNumber = null;
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

        await CheckSiretNotInAkuiteoAsync(siret, sanitizedPayload, ct);
        var inpi = await FetchInpiAsync(siret, sanitizedPayload, ct);
        prospectId = await CreateProspectAsync(request, inpi, sanitizedPayload, ct);
        accountNumber = await CreateAkuiteoCustomerAsync(request, inpi, prospectId.Value, sanitizedPayload, ct);
        await CreateAkuiteoContactAsync(accountNumber, request.Signatory, siret, prospectId.Value, sanitizedPayload, ct);
        var accountId = await CreateRydgeAccountAsync(request, inpi, accountNumber, prospectId.Value, sanitizedPayload, ct);
        var contactId = await CreateRydgeContactAsync(request, siret, prospectId.Value, accountNumber, sanitizedPayload, ct);
        var roleAssignmentContext = new ProspectOrchestrationContext
        {
            Siret = siret,
            ProspectId = prospectId,
            AccountNumber = accountNumber
        };
        await PatchProspectIdsAsync(prospectId.Value, accountNumber, accountId, contactId, siret, sanitizedPayload, ct);
        await AssignProspectRolesAsync(contactId, request.CaseManagerContactId, request.AccountManagerContactId, accountId, roleAssignmentContext, sanitizedPayload, ct);
        await AssignAccountRolesAsync(contactId, request.CaseManagerContactId, request.AccountManagerContactId, accountId, roleAssignmentContext, sanitizedPayload, ct);

        return new ProspectListItem
        {
            AccountId = accountId,
            AccountNumber = accountNumber,
            AccountType = AccountType.PROSPECT,
            LegalName = inpi.LegalName,
            Signatory = new ProspectSignatoryListItem
            {
                ContactId = contactId,
                FirstName = request.Signatory.FirstName,
                LastName = request.Signatory.LastName,
                Email = request.Signatory.Email
            },
            StepCode = ProspectStepCode.InProgress
        };
    }

    private async Task CheckSiretNotInAkuiteoAsync(string siret, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 1;
        try
        {
            var exists = await registryClient.SiretExistsInAkuiteoAsync(siret, ct);
            if (exists)
            {
                logger.LogInformation("[Orchestration]: stopped - [Step]: {Step} - [Siret]: {Siret} - [Reason]: SIRET already present in Akuiteo", step, siret);
                throw new SiretAlreadyExistsException(siret);
            }
        }
        catch (SiretAlreadyExistsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogStepFailure(step, siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: siret, inner: ex);
        }
    }

    private async Task<InpiCompanyInfo> FetchInpiAsync(string siret, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 2;
        try
        {
            return await prospectClient.GetInpiCompanyInfoAsync(siret, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(step, siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: siret, inner: ex);
        }
    }

    private async Task<int> CreateProspectAsync(CreateProspectRequest request, InpiCompanyInfo inpi, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 3;
        try
        {
            return await prospectClient.CreateProspectAsync(request, inpi, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(step, request.Siret, null, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: request.Siret, inner: ex);
        }
    }

    private async Task<string> CreateAkuiteoCustomerAsync(CreateProspectRequest request, InpiCompanyInfo inpi, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 4;
        try
        {
            var result = await registryClient.CreateAkuiteoCustomerAsync(request, inpi, ct);
            return result.AccountNumber;
        }
        catch (Exception ex)
        {
            LogStepFailure(step, request.Siret, prospectId, null, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: request.Siret, prospectId: prospectId, inner: ex);
        }
    }

    private async Task CreateAkuiteoContactAsync(string accountNumber, SignatoryDto signatory, string siret, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 5;
        try
        {
            await registryClient.CreateAkuiteoContactAsync(accountNumber, signatory, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(step, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task<int> CreateRydgeAccountAsync(CreateProspectRequest request, InpiCompanyInfo inpi, string accountNumber, int prospectId, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 6;
        try
        {
            var payload = new CreateAccountRequest
            {
                AccountNumber = accountNumber,
                LegalName = inpi.LegalName,
                Siret = inpi.Siret,
                AccountType = AccountType.PROSPECT
            };
            var result = await accountService.CreateAccountForProspectAsync(payload, request.CaseManagerContactId, ct);
            return result.AccountId;
        }
        catch (Exception ex)
        {
            LogStepFailure(step, request.Siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: request.Siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
    }

    private async Task<int> CreateRydgeContactAsync(CreateProspectRequest request, string siret, int prospectId, string accountNumber, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 7;
        try
        {
            var payload = CreateContactRequest.FromSignatory(request.Signatory, request.OfficeCode);
            var result = await contactService.CreateContactForProspectAsync(payload, ct);

            return result.ContactId;
        }
        catch (Exception ex)
        {
            LogStepFailure(step, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
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
        const int step = 10;
        try
        {
            var contacts = BuildAccountRoleItems(signatoryContactId, caseManagerContactId, accountManagerContactId);

            logger.LogInformation(
                "[Orchestration]: assigning {RoleCount} account roles - [Step]: {Step} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountId]: {AccountId}",
                contacts.Count,
                step,
                context.Siret,
                context.ProspectId,
                accountId);

            var result = await accountService.CreateRolesAsync(accountId, contacts, caseManagerContactId, ct);

            if (result.Failed.Count > 0)
            {
                var failures = string.Join(", ", result.Failed.Select(f => $"contactId={f.ContactId}:{f.ErrorCode}"));
                throw new InvalidOperationException($"Account bulk role creation returned failures: {failures}");
            }
        }
        catch (Exception ex)
        {
            LogStepFailure(step, context.Siret, context.ProspectId, context.AccountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: context.Siret, prospectId: context.ProspectId, accountNumber: context.AccountNumber, inner: ex);
        }
    }

    /// <summary>
    /// Assigns the created account roles through a single batch call to the prospect service.
    /// </summary>
    /// <param name="signatoryContactId">The signatory contact identifier.</param>
    /// <param name="caseManagerContactId">The case manager contact identifier.</param>
    /// <param name="accountManagerContactId">The account manager contact identifier.</param>
    /// <param name="accountId">The created account identifier.</param>
    /// <param name="context">The orchestration context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task AssignProspectRolesAsync(
        int signatoryContactId,
        int? caseManagerContactId,
        int accountManagerContactId,
        int accountId,
        ProspectOrchestrationContext context,
        string sanitizedPayload,
        CancellationToken ct)
    {
        const int step = 9;
        try
        {
            var roleAssignments = BuildRoleAssignments(signatoryContactId, caseManagerContactId, accountManagerContactId, accountId);

            logger.LogInformation(
                "[Orchestration]: assigning {RoleCount} roles - [Step]: {Step} - [Siret]: {Siret} - [ProspectId]: {ProspectId} - [AccountId]: {AccountId}",
                roleAssignments.Count,
                step,
                context.Siret,
                context.ProspectId,
                accountId);

            await prospectClient.CreateRoleAsync(roleAssignments, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(step, context.Siret, context.ProspectId, context.AccountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: context.Siret, prospectId: context.ProspectId, accountNumber: context.AccountNumber, inner: ex);
        }
    }

    private async Task PatchProspectIdsAsync(int prospectId, string accountNumber, int accountId, int contactId, string siret, string sanitizedPayload, CancellationToken ct)
    {
        const int step = 8;
        try
        {
            await prospectClient.UpdateProspectIdsAsync(prospectId, accountNumber, accountId, contactId, ct);
        }
        catch (Exception ex)
        {
            LogStepFailure(step, siret, prospectId, accountNumber, sanitizedPayload, ex);
            throw new ProspectOrchestrationException(step, siret: siret, prospectId: prospectId, accountNumber: accountNumber, inner: ex);
        }
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

    /// <summary>
    /// Builds the distinct role assignments to send to the prospect role endpoint.
    /// </summary>
    /// <param name="signatoryContactId">The signatory contact identifier.</param>
    /// <param name="caseManagerContactId">The case manager contact identifier.</param>
    /// <param name="accountManagerContactId">The account manager contact identifier.</param>
    /// <param name="accountId">The created account identifier.</param>
    /// <returns>The distinct role assignments for the account.</returns>
    private static IReadOnlyCollection<CreateRoleAssignmentRequest> BuildRoleAssignments(
        int signatoryContactId,
        int? caseManagerContactId,
        int accountManagerContactId,
        int accountId)
    {
        var assignments = new List<CreateRoleAssignmentRequest>
        {
            new()
            {
                ContactId = signatoryContactId,
                AccountId = accountId,
                IsSignatory = true
            },
            new()
            {
                ContactId = accountManagerContactId,
                AccountId = accountId,
                IsSignatory = false
            }
        };
        if (caseManagerContactId.HasValue)
        {
            assignments.Add(new CreateRoleAssignmentRequest
            {
                ContactId = caseManagerContactId.Value,
                AccountId = accountId,
                IsSignatory = false
            });
        }
        return assignments
            .GroupBy(request => new { request.ContactId, request.AccountId })
            .Select(group => new CreateRoleAssignmentRequest
            {
                ContactId = group.Key.ContactId,
                AccountId = group.Key.AccountId,
                IsSignatory = group.Any(request => request.IsSignatory)
            })
            .ToArray();
    }
}
