namespace UserService.Core.Entities;

public enum SubscriptionStatus { Trial, Active, Suspended, Cancelled }
public enum BillingCycle       { Monthly, Annual }

public class CompanySubscription : BaseEntity
{
    public string             TenantId      { get; set; } = string.Empty;
    public string             PlanId        { get; set; } = string.Empty;
    public SubscriptionStatus Status        { get; set; } = SubscriptionStatus.Trial;
    public BillingCycle       BillingCycle  { get; set; } = BillingCycle.Monthly;
    public DateTime           StartDate     { get; set; }
    public DateTime?          EndDate       { get; set; }
    public DateTime?          TrialEndsAt   { get; set; }

    // Stub fields for future Stripe integration
    public string? StripeCustomerId     { get; set; }
    public string? StripeSubscriptionId { get; set; }

    public virtual Tenant           Tenant { get; set; } = null!;
    public virtual SubscriptionPlan Plan   { get; set; } = null!;
}
