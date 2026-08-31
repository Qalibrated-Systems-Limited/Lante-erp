namespace ComplianceService.Core.Entities;

// ICM-002/003: an Inter-Company Services Agreement — scope and recharge rate governing
// recurring recharges to one related party. EndDate null = open-ended/current agreement.
public class Icsa : BaseEntity
{
    public string RelatedPartyId { get; set; } = string.Empty;
    public RelatedParty? RelatedParty { get; set; }
    public string Scope { get; set; } = string.Empty;
    public decimal RechargeRate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
