namespace UserService.Core.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceMonthly  { get; set; }
    public decimal PriceAnnual   { get; set; }
    public int  MaxBranches   { get; set; } = 1;
    public int  MaxUsers      { get; set; } = 10;
    public bool IsActive      { get; set; } = true;
    // JSON array of feature slugs e.g. ["ticketing","operations","fleet"]
    public string FeaturesJson { get; set; } = "[]";

    // Stub fields for future Stripe integration
    public string? StripeProductId { get; set; }

    public virtual ICollection<CompanySubscription> Subscriptions { get; set; } = [];
}
