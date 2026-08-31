using System.Security.Claims;
using FinanceService.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceService.Api.Controllers;

[ApiController]
// The FLOOR for every finance endpoint. It was a bare [Authorize], which required only a signed-in
// user — so a driver holding fleet.write could read the whole ledger, the chart of accounts, every
// customer and every VAT filing by calling the API directly. The frontend hides the module, but
// hiding is not a control (#277).
//
// Reads inherit this and add nothing. Mutations carry finance.write, and anything that commits money,
// posts to the ledger or closes a period carries finance.approve.
[Authorize(Policy = "finance.read")]
[Route("api/v{version:apiVersion}/finance")]
[Asp.Versioning.ApiVersion("1.0")]
public abstract class BaseFinanceController : ControllerBase
{
    protected string? CurrentUserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("nameid")?.Value;

    protected IReadOnlyCollection<string> CurrentRoles =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

    protected bool IsCompanyAdmin =>
        string.Equals(User.FindFirst("is_company_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    /// The caller's authorisation facts for the payment-authority matrix.
    protected ApprovalContext ApprovalCtx => new(CurrentRoles, IsCompanyAdmin);
}
