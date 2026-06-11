# Beneficiary Identity Documents Gateway Contract

## Route mapping

| Direction | Method and path |
| --- | --- |
| Gateway upstream | `POST /gtw/prospect/api/prospects/{prospectId}/beneficiaries/{beneficiaryId}/identity-documents` |
| Prospect downstream | `POST /api/prospects/{prospectId}/beneficiaries/{beneficiaryId}/identity-documents` |

The Gateway is a transparent proxy for this endpoint:

- it preserves the `multipart/form-data` request body and content headers;
- it preserves the downstream response status, headers, and body;
- it does not deserialize files or apply business validation;
- `DownstreamExceptionHandler` logs unsuccessful downstream responses without replacing them.

## Gateway controls

- Accepted authentication schemes: `AAD` and `GIGYA MyPulse v2`.
- The `ProspectExperienceHandler` feature flag applies before forwarding.
- No `RouteClaimsRequirement` is configured because the functional source does not provide a permission code.
- Prospect ownership and resource authorization remain mandatory downstream controls.

When implemented, if the Prospect experience feature is disabled, Gateway should return:

```json
{
  "errorCode": "GTW026",
  "errorMessage": "L'expérience Prospect est désactivée."
}
```

with `403 Forbidden`.

Authentication failures generated before proxying use the existing Gateway authentication contract. All Prospect validation and technical errors are forwarded unchanged.

## Request and response

The canonical downstream schema, validation matrix, messages, and examples are documented in:

`Pulse.Back.Prospect/_docs/contracts/beneficiary-identity-documents-contract.md`

