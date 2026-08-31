using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Customers;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C1 (P1) — customer master + 4-stage onboarding. See <see cref="ICustomerService"/>.
/// Throws KeyNotFoundException→404 / InvalidOperationException→400 (mapped by GlobalExceptionMiddleware).</summary>
public class CustomerService(
    IGenericRepository<Customer> customers,
    IGenericRepository<CustomerContact> contacts,
    IGenericRepository<ClientCreditLimit> creditLimits,
    IMapper mapper) : ICustomerService
{
    public async Task<CustomerListResult> GetAllAsync(CustomerFilterParams filter)
    {
        var query = customers.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Email != null && c.Email.ToLower().Contains(s)) ||
                (c.ClientReference != null && c.ClientReference.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<CustomerStatus>(filter.Status, true, out var st))
            query = query.Where(c => c.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.AccountTier) && Enum.TryParse<AccountTier>(filter.AccountTier, true, out var tier))
            query = query.Where(c => c.AccountTier == tier);
        if (!string.IsNullOrWhiteSpace(filter.AccountOwnerId))
            query = query.Where(c => c.AccountOwnerId == filter.AccountOwnerId);

        // Engagement state, distinct from the approval Status: a client can be approved (Active) yet
        // dormant. DormantSince is stamped by the C7 sweep after 90 days without interaction.
        if (!string.IsNullOrWhiteSpace(filter.Activity))
        {
            query = filter.Activity.Trim().ToLowerInvariant() switch
            {
                "dormant" => query.Where(c => c.DormantSince != null),
                "active"  => query.Where(c => c.DormantSince == null && c.Status == Core.Enums.CustomerStatus.Active),
                _         => query,
            };
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new CustomerListResult(mapper.Map<List<CustomerSummaryDto>>(items), total);
    }

    public async Task<CustomerDetailDto?> GetByIdAsync(string id)
    {
        var c = await customers.Query().Include(x => x.Contacts).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? null : mapper.Map<CustomerDetailDto>(c);
    }

    public async Task<DuplicateCheckResult> CheckDuplicateAsync(string? name, string? email, string? phone, string? kraPin = null)
    {
        var matches = await FindDuplicatesAsync(name, email, phone, kraPin);
        return new DuplicateCheckResult(matches.Count > 0, mapper.Map<List<CustomerSummaryDto>>(matches));
    }

    private async Task<List<Customer>> FindDuplicatesAsync(string? name, string? email, string? phone, string? kraPin = null)
    {
        var n = name?.Trim().ToLower();
        var em = email?.Trim().ToLower();
        var ph = phone?.Trim();
        var pin = kraPin?.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(n) && string.IsNullOrWhiteSpace(em)
            && string.IsNullOrWhiteSpace(ph) && string.IsNullOrWhiteSpace(pin))
            return new List<Customer>();

        return await customers.Query().AsNoTracking().Where(c =>
            (n != null && c.Name.ToLower() == n) ||
            (em != null && c.Email != null && c.Email.ToLower() == em) ||
            (ph != null && c.Phone != null && c.Phone == ph) ||
            (pin != null && c.KraPin != null && c.KraPin.ToUpper() == pin)).ToListAsync();
    }

    public async Task<CustomerDetailDto> CreateAsync(CreateCustomerDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Customer name is required.");

        // Throws on a malformed PIN — a mistyped one silently produces invalid tax invoices later.
        var kraPin = CrmFieldRules.NormaliseKraPin(dto.KraPin);

        var dupes = await FindDuplicatesAsync(dto.Name, dto.Email, dto.Phone, kraPin);
        if (dupes.Count > 0)
        {
            // Name the field that actually collided; "a matching name, email, or phone" leaves the
            // user guessing which of three values to change.
            var hit = dupes[0];
            var on =
                kraPin != null && string.Equals(hit.KraPin, kraPin, StringComparison.OrdinalIgnoreCase) ? "KRA PIN" :
                !string.IsNullOrWhiteSpace(dto.Email) && string.Equals(hit.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase) ? "email" :
                !string.IsNullOrWhiteSpace(dto.Phone) && hit.Phone == dto.Phone.Trim() ? "phone" : "name";
            throw new InvalidOperationException(
                $"A client with the same {on} already exists ({hit.Name}). Link to the existing record instead.");
        }

        var customer = new Customer
        {
            Name = dto.Name.Trim(),
            CustomerType = dto.CustomerType,
            Industry = dto.Industry?.Trim(),
            Segment = dto.Segment?.Trim(),
            AccountTier = dto.AccountTier,
            Geography = dto.Geography?.Trim(),
            BusinessLine = dto.BusinessLine?.Trim(),
            Email = dto.Email?.Trim().ToLowerInvariant(),
            Phone = dto.Phone?.Trim(),
            ClientReference = dto.ClientReference?.Trim(),
            KraPin = kraPin,
            AccountOwnerId = string.IsNullOrWhiteSpace(dto.AccountOwnerId) ? userId : dto.AccountOwnerId,
            AccountOwnerName = dto.AccountOwnerName ?? userName,
            IntroducedBy = string.IsNullOrWhiteSpace(dto.IntroducedBy) ? userId : dto.IntroducedBy,
            IntroducedByLocked = false,
            Status = CustomerStatus.PendingLineManager,
            SubmittedBy = userId,
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        };
        var created = await customers.CreateAsync(customer);

        var idx = 0;
        foreach (var ct in dto.Contacts)
        {
            var primary = ct.IsPrimary || idx == 0;   // first contact is primary by default
            await contacts.CreateAsync(new CustomerContact
            {
                CustomerId = created.Id,
                FirstName = ct.FirstName.Trim(),
                LastName = ct.LastName.Trim(),
                JobTitle = ct.JobTitle?.Trim(),
                Email = ct.Email?.Trim(),
                Phone = ct.Phone?.Trim(),
                IsPrimary = primary,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
            idx++;
        }

        return (await GetByIdAsync(created.Id))!;
    }

    public async Task<CustomerDetailDto> UpdateAsync(string id, UpdateCustomerDto dto, string userId)
    {
        var c = await FindAsync(id);
        if (dto.Name != null) c.Name = dto.Name.Trim();
        if (dto.CustomerType.HasValue) c.CustomerType = dto.CustomerType.Value;
        if (dto.Industry != null) c.Industry = dto.Industry.Trim();
        if (dto.Segment != null) c.Segment = dto.Segment.Trim();
        if (dto.AccountTier.HasValue) c.AccountTier = dto.AccountTier.Value;
        if (dto.Geography != null) c.Geography = dto.Geography.Trim();
        if (dto.BusinessLine != null) c.BusinessLine = dto.BusinessLine.Trim();
        if (dto.Email != null) c.Email = dto.Email.Trim().ToLowerInvariant();
        if (dto.Phone != null) c.Phone = dto.Phone.Trim();
        if (dto.ClientReference != null) c.ClientReference = dto.ClientReference.Trim();
        if (dto.KraPin != null)
        {
            var pin = CrmFieldRules.NormaliseKraPin(dto.KraPin);
            // A PIN identifies a taxpayer, so it must stay unique — otherwise two client records
            // can invoice under one PIN and the duplicate check stops meaning anything.
            if (pin != null && !string.Equals(pin, c.KraPin, StringComparison.OrdinalIgnoreCase))
            {
                var clash = await customers.Query().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id != c.Id && x.KraPin != null && x.KraPin.ToUpper() == pin);
                if (clash != null)
                    throw new InvalidOperationException($"KRA PIN {pin} already belongs to {clash.Name}.");
            }
            c.KraPin = pin;
        }
        if (dto.AccountOwnerId != null) c.AccountOwnerId = dto.AccountOwnerId;
        if (dto.AccountOwnerName != null) c.AccountOwnerName = dto.AccountOwnerName;
        // IntroducedBy is intentionally NOT editable here (locked-after-MD referral protection).
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return (await GetByIdAsync(id))!;
    }

    // ── Onboarding chain ──

    public async Task<CustomerActionResult> ApproveLineManagerAsync(string id, string userId)
    {
        var c = await FindAsync(id);
        Require(c, CustomerStatus.PendingLineManager, "line-manager approval");
        c.Status = CustomerStatus.PendingHeadBd;
        c.LineManagerApprovedBy = userId;
        c.LineManagerApprovedAt = DateTime.UtcNow;
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return new CustomerActionResult(c.Status.ToString(), "Approved by line manager. Awaiting Head of BD.");
    }

    public async Task<CustomerActionResult> ApproveHeadBdAsync(string id, string userId)
    {
        var c = await FindAsync(id);
        Require(c, CustomerStatus.PendingHeadBd, "Head of BD approval");
        c.Status = CustomerStatus.PendingCfo;
        c.HeadBdApprovedBy = userId;
        c.HeadBdApprovedAt = DateTime.UtcNow;
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return new CustomerActionResult(c.Status.ToString(), "Approved by Head of BD. Awaiting CFO credit review.");
    }

    public async Task<CustomerActionResult> CfoReviewAsync(string id, CfoReviewDto dto, string userId)
    {
        var c = await FindAsync(id);
        Require(c, CustomerStatus.PendingCfo, "CFO credit review");
        if (dto.CreditLimit < 0 || dto.CreditTermsDays < 0)
            throw new InvalidOperationException("Credit limit and terms must be non-negative.");

        c.CreditLimit = dto.CreditLimit;
        c.CreditTermsDays = dto.CreditTermsDays;
        c.Status = CustomerStatus.PendingMd;
        c.CfoApprovedBy = userId;
        c.CfoApprovedAt = DateTime.UtcNow;
        Touch(c, userId);
        await customers.UpdateAsync(c);

        await creditLimits.CreateAsync(new ClientCreditLimit
        {
            CustomerId = c.Id,
            CreditLimit = dto.CreditLimit,
            CreditTermsDays = dto.CreditTermsDays,
            ApprovedBy = userId,
            SetAt = DateTime.UtcNow,
            Notes = dto.Notes?.Trim(),
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        return new CustomerActionResult(c.Status.ToString(), "Credit terms set by CFO. Awaiting MD final approval.");
    }

    public async Task<CustomerActionResult> ApproveMdAsync(string id, string userId)
    {
        var c = await FindAsync(id);
        Require(c, CustomerStatus.PendingMd, "MD final approval");
        c.Status = CustomerStatus.Active;
        c.MdApprovedBy = userId;
        c.MdApprovedAt = DateTime.UtcNow;
        c.ActivatedAt = DateTime.UtcNow;
        c.IntroducedByLocked = true;   // referral attribution locked after MD approval (CRM-058)
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return new CustomerActionResult(c.Status.ToString(), "Client approved by MD and added to the master database.");
    }

    public async Task<CustomerActionResult> RejectAsync(string id, RejectCustomerDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("A rejection reason is required.");
        var c = await FindAsync(id);
        if (c.Status is not (CustomerStatus.PendingLineManager or CustomerStatus.PendingHeadBd
            or CustomerStatus.PendingCfo or CustomerStatus.PendingMd))
            throw new InvalidOperationException($"Cannot reject a client in status '{c.Status}'.");
        c.Status = CustomerStatus.Rejected;
        c.RejectedBy = userId;
        c.RejectedAt = DateTime.UtcNow;
        c.RejectionReason = dto.Reason.Trim();
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return new CustomerActionResult(c.Status.ToString(), "Client onboarding rejected.");
    }

    public async Task<CustomerActionResult> DeactivateAsync(string id, string userId)
    {
        var c = await FindAsync(id);
        if (c.Status != CustomerStatus.Active)
            throw new InvalidOperationException($"Only an active client can be deactivated (current: {c.Status}).");
        c.Status = CustomerStatus.Inactive;
        Touch(c, userId);
        await customers.UpdateAsync(c);
        return new CustomerActionResult(c.Status.ToString(), "Client deactivated.");
    }

    // ── Contacts ──

    public async Task<CustomerContactDto> AddContactAsync(string customerId, CreateCustomerContactDto dto, string userId)
    {
        _ = await FindAsync(customerId);   // ensure exists
        if (dto.IsPrimary) await ClearPrimaryAsync(customerId, userId);
        var contact = await contacts.CreateAsync(new CustomerContact
        {
            CustomerId = customerId,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            JobTitle = dto.JobTitle?.Trim(),
            Email = dto.Email?.Trim(),
            Phone = dto.Phone?.Trim(),
            IsPrimary = dto.IsPrimary,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
        return mapper.Map<CustomerContactDto>(contact);
    }

    public async Task<CustomerContactDto> UpdateContactAsync(string contactId, CreateCustomerContactDto dto, string userId)
    {
        var contact = await contacts.GetByIdAsync(contactId)
            ?? throw new KeyNotFoundException($"Contact {contactId} not found.");
        if (dto.IsPrimary && !contact.IsPrimary) await ClearPrimaryAsync(contact.CustomerId, userId);
        contact.FirstName = dto.FirstName.Trim();
        contact.LastName = dto.LastName.Trim();
        contact.JobTitle = dto.JobTitle?.Trim();
        contact.Email = dto.Email?.Trim();
        contact.Phone = dto.Phone?.Trim();
        contact.IsPrimary = dto.IsPrimary;
        Touch(contact, userId);
        await contacts.UpdateAsync(contact);
        return mapper.Map<CustomerContactDto>(contact);
    }

    private async Task ClearPrimaryAsync(string customerId, string userId)
    {
        var existing = await contacts.Query().Where(c => c.CustomerId == customerId && c.IsPrimary).ToListAsync();
        foreach (var e in existing) { e.IsPrimary = false; Touch(e, userId); await contacts.UpdateAsync(e); }
    }

    // ── Helpers ──
    private async Task<Customer> FindAsync(string id) =>
        await customers.Query().FirstOrDefaultAsync(c => c.Id == id)
        ?? throw new KeyNotFoundException($"Customer {id} not found.");

    private static void Require(Customer c, CustomerStatus expected, string action)
    {
        if (c.Status != expected)
            throw new InvalidOperationException($"Cannot perform {action}: client is in status '{c.Status}', expected '{expected}'.");
    }

    private static void Touch(BaseEntity e, string userId)
    {
        e.UpdatedBy = userId;
        e.UpdatedAt = DateTime.UtcNow;
    }
}
