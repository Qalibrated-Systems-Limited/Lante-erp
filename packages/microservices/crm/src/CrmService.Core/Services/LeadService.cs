using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Leads;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C2 (P2) — lead capture &amp; qualification. See <see cref="ILeadService"/>.</summary>
public class LeadService(
    IGenericRepository<Lead> leads,
    IGenericRepository<LeadActivity> activities,
    IGenericRepository<Customer> customers,
    IOpportunityService opportunities,
    IMapper mapper) : ILeadService
{
    // A lead is "stale" when it's still open (not converted/unqualified) and has had no activity in
    // more than 2 days (CRM daily sweep flags these; computed live here for the read model too).
    public static bool IsStale(Lead l)
    {
        if (l.IsConverted || l.Status is LeadStatus.Unqualified or LeadStatus.Converted) return false;
        var last = l.LastActivityAt ?? l.CreatedAt;
        return (DateTime.UtcNow - last).TotalDays > 2;
    }
    private static bool IsOpen(Lead l) => !l.IsConverted && l.Status != LeadStatus.Unqualified && l.Status != LeadStatus.Converted;

    public async Task<LeadListResult> GetAllAsync(LeadFilterParams filter)
    {
        var query = leads.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(l => l.FirstName.ToLower().Contains(s) || l.LastName.ToLower().Contains(s)
                || (l.CompanyName != null && l.CompanyName.ToLower().Contains(s))
                || (l.Email != null && l.Email.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<LeadStatus>(filter.Status, true, out var st))
            query = query.Where(l => l.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.Source) && Enum.TryParse<LeadSourceType>(filter.Source, true, out var src))
            query = query.Where(l => l.Source == src);
        // Substring match: ProductRange holds a comma-joined list, so a lead selecting several
        // lines must still be found by any one of them.
        if (!string.IsNullOrWhiteSpace(filter.ProductRange))
        {
            var pr = filter.ProductRange.Trim().ToLower();
            query = query.Where(l => l.ProductRange != null && l.ProductRange.ToLower().Contains(pr));
        }
        if (!string.IsNullOrWhiteSpace(filter.Rating) && Enum.TryParse<LeadRating>(filter.Rating, true, out var rt))
            query = query.Where(l => l.Rating == rt);
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo))
            query = query.Where(l => l.AssignedTo == filter.AssignedTo);

        var all = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        if (filter.Stale == true) all = all.Where(IsStale).ToList();

        var openLeads = all.Where(IsOpen).ToList();
        var total = all.Count;
        var items = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        var dtos = items.Select(ToSummary).ToList();

        return new LeadListResult(dtos, total, openLeads.Count, openLeads.Sum(l => l.EstimatedValue));
    }

    public async Task<LeadDetailDto?> GetByIdAsync(string id)
    {
        var l = await leads.Query().Include(x => x.Activities).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return null;
        var dto = mapper.Map<LeadDetailDto>(l);
        dto.IsStale = IsStale(l);
        dto.Activities = dto.Activities.OrderByDescending(a => a.ActivityDate).ToList();
        return dto;
    }

    public async Task<LeadDetailDto> CreateAsync(CreateLeadDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.FirstName) && string.IsNullOrWhiteSpace(dto.CompanyName))
            throw new InvalidOperationException("A contact name or company is required.");

        var productRange = CrmFieldRules.NormaliseProductRange(dto.ProductRange);

        var email = dto.Email?.Trim().ToLowerInvariant();
        var phone = dto.Phone?.Trim();

        // Block a lead that already exists. Matching is on email or phone — the only fields that
        // actually identify a person; names collide constantly. Only *open* leads block: once a
        // lead is converted or written off, the same contact coming back is a genuinely new
        // enquiry and must be capturable.
        if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(phone))
        {
            var existing = await leads.Query().AsNoTracking().FirstOrDefaultAsync(l =>
                !l.IsConverted
                && l.Status != LeadStatus.Unqualified
                && ((email != null && l.Email != null && l.Email.ToLower() == email) ||
                    (phone != null && l.Phone != null && l.Phone == phone)));

            if (existing != null)
            {
                var who = string.IsNullOrWhiteSpace(existing.CompanyName)
                    ? $"{existing.FirstName} {existing.LastName}".Trim()
                    : existing.CompanyName;
                throw new InvalidOperationException(
                    $"An open lead for this contact already exists ({who}, {existing.Status}, owned by " +
                    $"{existing.AssignedToName ?? existing.AssignedTo}). Add an activity to that lead instead of capturing a duplicate.");
            }

            // Already a client? Then this is not a lead. Further business with a live account
            // belongs on an opportunity against it — a lead shadowing a customer splits the account
            // history and double-counts the pipeline. Rejected applications don't block: those
            // never became customers, so the contact is genuinely a fresh prospect again.
            var client = await customers.Query().AsNoTracking().FirstOrDefaultAsync(c =>
                c.Status != CustomerStatus.Rejected
                && ((email != null && c.Email != null && c.Email.ToLower() == email) ||
                    (phone != null && c.Phone != null && c.Phone == phone)));

            if (client != null)
                throw new Exceptions.ExistingCustomerLeadException(client.Id, client.Name,
                    $"{client.Name} is already a client. Raise an opportunity against their account " +
                    "rather than capturing a lead, so the work stays on their history.");
        }

        var lead = new Lead
        {
            Source = dto.Source,
            SourceName = dto.SourceName?.Trim(),
            CampaignId = dto.CampaignId,
            AssignedTo = string.IsNullOrWhiteSpace(dto.AssignedTo) ? userId : dto.AssignedTo,
            AssignedToName = dto.AssignedToName ?? userName,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            CompanyName = dto.CompanyName?.Trim(),
            Email = email,
            Phone = phone,
            Industry = dto.Industry?.Trim(),
            ProductRange = productRange,
            EstimatedValue = dto.EstimatedValue,
            Rating = dto.Rating,
            Notes = dto.Notes?.Trim(),
            Status = LeadStatus.New,
            LastActivityAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        var created = await leads.CreateAsync(lead);
        return (await GetByIdAsync(created.Id))!;
    }

    public async Task<LeadDetailDto> UpdateAsync(string id, UpdateLeadDto dto, string userId)
    {
        var l = await FindAsync(id);
        if (dto.Source.HasValue) l.Source = dto.Source.Value;
        if (dto.SourceName != null) l.SourceName = dto.SourceName.Trim();
        if (dto.CampaignId != null) l.CampaignId = dto.CampaignId;
        if (dto.AssignedTo != null) l.AssignedTo = dto.AssignedTo;
        if (dto.AssignedToName != null) l.AssignedToName = dto.AssignedToName;
        if (dto.FirstName != null) l.FirstName = dto.FirstName.Trim();
        if (dto.LastName != null) l.LastName = dto.LastName.Trim();
        if (dto.CompanyName != null) l.CompanyName = dto.CompanyName.Trim();
        if (dto.Email != null) l.Email = dto.Email.Trim().ToLowerInvariant();
        if (dto.Phone != null) l.Phone = dto.Phone.Trim();
        if (dto.Industry != null) l.Industry = dto.Industry.Trim();
        if (dto.ProductRange != null) l.ProductRange = CrmFieldRules.NormaliseProductRange(dto.ProductRange);
        if (dto.EstimatedValue.HasValue) l.EstimatedValue = dto.EstimatedValue.Value;
        if (dto.Rating.HasValue) l.Rating = dto.Rating.Value;
        if (dto.Notes != null) l.Notes = dto.Notes.Trim();
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return (await GetByIdAsync(id))!;
    }

    public async Task<LeadActivityDto> AddActivityAsync(string id, CreateLeadActivityDto dto, string userId)
    {
        var l = await FindAsync(id);
        var act = await activities.CreateAsync(new LeadActivity
        {
            LeadId = id,
            ActivityType = string.IsNullOrWhiteSpace(dto.ActivityType) ? "Note" : dto.ActivityType.Trim(),
            Subject = dto.Subject.Trim(),
            Description = dto.Description?.Trim(),
            PerformedBy = userId,
            ActivityDate = dto.ActivityDate ?? DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
        // Logging activity refreshes the lead and clears any stale flag; first contact advances New→Contacted.
        l.LastActivityAt = DateTime.UtcNow;
        l.StaleAlertedAt = null;
        if (l.Status == LeadStatus.New) l.Status = LeadStatus.Contacted;
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return mapper.Map<LeadActivityDto>(act);
    }

    public async Task<LeadActionResult> AssignAsync(string id, AssignLeadDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.AssignedTo)) throw new InvalidOperationException("Assignee is required.");
        var l = await FindAsync(id);
        l.AssignedTo = dto.AssignedTo;
        l.AssignedToName = dto.AssignedToName;
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return new LeadActionResult(l.Status.ToString(), "Lead reassigned.");
    }

    public async Task<LeadActionResult> QualifyAsync(string id, QualifyLeadDto dto, string userId)
    {
        var l = await FindAsync(id);
        if (l.IsConverted || l.Status == LeadStatus.Converted)
            throw new InvalidOperationException("This lead has already been converted.");
        l.Status = LeadStatus.Qualified;
        if (dto.Rating.HasValue) l.Rating = dto.Rating.Value;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) l.Notes = dto.Notes.Trim();
        l.LastActivityAt = DateTime.UtcNow;
        l.StaleAlertedAt = null;
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return new LeadActionResult(l.Status.ToString(), "Lead qualified — ready to convert to an opportunity.");
    }

    public async Task<LeadActionResult> UnqualifyAsync(string id, UnqualifyLeadDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new InvalidOperationException("A reason is required.");
        var l = await FindAsync(id);
        if (l.IsConverted) throw new InvalidOperationException("This lead has already been converted.");
        l.Status = LeadStatus.Unqualified;
        l.UnqualifiedReason = dto.Reason.Trim();
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return new LeadActionResult(l.Status.ToString(), "Lead marked unqualified.");
    }

    public async Task<LeadActionResult> ConvertAsync(string id, string? customerId, string userId)
    {
        var l = await FindAsync(id);
        if (l.IsConverted) throw new InvalidOperationException("This lead has already been converted.");
        if (l.Status != LeadStatus.Qualified)
            throw new InvalidOperationException($"Only a qualified lead can be converted (current: {l.Status}).");
        // Create the opportunity from the lead (P3), then mark the lead converted.
        var opp = await opportunities.CreateAsync(new DTOs.Opportunities.CreateOpportunityDto
        {
            Name = string.IsNullOrWhiteSpace(l.CompanyName) ? $"{l.FirstName} {l.LastName}".Trim() : l.CompanyName!,
            CustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId,
            CustomerName = l.CompanyName,
            LeadId = l.Id,
            AssignedTo = l.AssignedTo,
            AssignedToName = l.AssignedToName,
            EstimatedValue = l.EstimatedValue,
            Source = l.Source.ToString(),
        }, userId, l.AssignedToName);

        l.IsConverted = true;
        l.ConvertedAt = DateTime.UtcNow;
        l.ConvertedCustomerId = string.IsNullOrWhiteSpace(customerId) ? null : customerId;
        l.ConvertedOpportunityId = opp.Id;
        l.Status = LeadStatus.Converted;
        Touch(l, userId);
        await leads.UpdateAsync(l);
        return new LeadActionResult(l.Status.ToString(), $"Lead converted to opportunity {opp.OpportunityNumber}.");
    }

    private async Task<Lead> FindAsync(string id) =>
        await leads.Query().FirstOrDefaultAsync(l => l.Id == id)
        ?? throw new KeyNotFoundException($"Lead {id} not found.");

    private LeadSummaryDto ToSummary(Lead l)
    {
        var dto = mapper.Map<LeadSummaryDto>(l);
        dto.IsStale = IsStale(l);
        return dto;
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
