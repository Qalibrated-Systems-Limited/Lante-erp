using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Entities;

// STAT-001: every recurring statutory item (PAYE 9th monthly, VAT 20th monthly, NSSF/SHA 9th
// monthly, WHT 20th monthly, corporate/instalment tax). StatutoryDeadlineBackgroundService rolls
// Monthly/Quarterly obligations forward automatically into dated StatutoryDeadline rows; Annually
// items (corporate tax, instalment tax) are tracked via manually-created StatutoryDeadline rows
// since their exact due date varies with the tax calendar rather than a fixed day-of-month.
public class StatutoryObligation : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public ObligationFrequency Frequency { get; set; }
    public int StatutoryDay { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<StatutoryDeadline> Deadlines { get; set; } = new List<StatutoryDeadline>();
}
