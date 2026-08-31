namespace CrmService.Core.Services;

/// <summary>
/// Rounds a currency amount to the cent using commercial (AwayFromZero) rounding — the convention
/// a Kenyan accountant expects on a payslip, and what payroll already uses. .NET's bare
/// <c>Math.Round</c> defaults to banker's rounding (ToEven), which differs at exact half-cents —
/// precisely what percentage arithmetic (discounts, VAT) produces. See #380.
/// </summary>
public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
