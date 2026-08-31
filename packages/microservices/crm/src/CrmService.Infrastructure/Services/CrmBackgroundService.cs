using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CrmService.Core.Entities;
using CrmService.Infrastructure.Data;

namespace CrmService.Infrastructure.Services;

/// <summary>
/// Per-tenant-schema background sweep for CRM &amp; Sales — mirrors the operations background service.
/// Background jobs have no HTTP request to resolve a tenant schema from, so this discovers every
/// provisioned tenant_* schema directly from Postgres and runs each check pinned to that schema's
/// search_path (otherwise the interceptor falls back to "public" and never sees migrated tenants).
/// SCAFFOLD (C0): the individual check methods are placeholders that later phases fill in — lead
/// stale (C2), opportunity stale (C3), contract/tender/bid-bond alerts (C5/C6), dormant client (C7),
/// revenue snapshot + RAG (C9), campaign budget (C10), service-contract + legal-doc expiry (C11/C12),
/// payment/debtor cadences (C13).
/// </summary>
public class CrmBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<CrmBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CRM background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CRM background sweep failed.");
            }
            await Task.Delay(SweepInterval, stoppingToken);
        }

        logger.LogInformation("CRM background service stopped.");
    }

    private async Task RunChecksAsync(CancellationToken ct)
    {
        foreach (var schema in await GetTenantSchemasAsync())
        {
            try
            {
                await RunChecksForSchemaAsync(schema, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CRM checks failed for schema {Schema}", schema);
            }
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        return await context.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    // Runs all CRM engines for one tenant schema. Engines wired per phase.
    private async Task RunChecksForSchemaAsync(string schema, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var finance = scope.ServiceProvider.GetRequiredService<CrmService.Core.Interfaces.Services.IFinanceReadClient>();

        // Pin this scope's connection to the tenant schema and keep it open for the whole method —
        // background scopes have no HTTP context, so without this the interceptor falls back to "public".
        await db.Database.OpenConnectionAsync(ct);
        try
        {
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (filtered to ^tenant_) above, not user input; identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public", ct);
#pragma warning restore EF1002
            await CheckStaleLeadsAsync(db, ct);          // C2
            await CheckStaleOpportunitiesAsync(db, ct);  // C3
            await CheckContractRenewalsAsync(db, ct);    // C5
            await CheckTenderDeadlinesAsync(db, ct);     // C6
            await CheckDormantClientsAsync(db, ct);      // C7
            await CheckOverdueTasksAsync(db, ct);        // C7
            await SnapshotDailyAsync(db, ct);            // C9
            await CheckCampaignBudgetsAsync(db, ct);     // C10
            await CheckServiceContractRenewalsAsync(db, ct); // C11
            await CheckLegalDocExpiryAsync(db, ct);          // C12
            await CheckPaymentDueAlertsAsync(db, finance, schema, ct); // C13
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    // C2 (P2, TRK-003) — flag open leads with no activity in >2 days to their assigned SE (once).
    private async Task CheckStaleLeadsAsync(CrmDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-2);
        var stale = await db.Leads
            .Where(l => !l.IsConverted
                        && l.Status != Core.Enums.LeadStatus.Unqualified
                        && l.Status != Core.Enums.LeadStatus.Converted
                        && l.StaleAlertedAt == null
                        && (l.LastActivityAt ?? l.CreatedAt) < cutoff)
            .ToListAsync(ct);

        foreach (var l in stale)
        {
            l.StaleAlertedAt = DateTime.UtcNow;
            l.UpdatedAt = DateTime.UtcNow;
            db.Leads.Update(l);
            logger.LogInformation("Stale lead {LeadId} ({Name}) flagged to SE {SE}", l.Id, l.CompanyName ?? $"{l.FirstName} {l.LastName}", l.AssignedTo);
        }
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
    }

    // C3 (P3, CRM-012) — escalate open opportunities with no stage movement in >14 days to Head of BD (once).
    private async Task CheckStaleOpportunitiesAsync(CrmDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);
        var stale = await db.Opportunities
            .Where(o => o.Status == Core.Enums.OpportunityStatus.Open
                        && o.StaleAlertedAt == null
                        && o.StageMovedAt < cutoff)
            .ToListAsync(ct);

        foreach (var o in stale)
        {
            o.StaleAlertedAt = DateTime.UtcNow;
            o.UpdatedAt = DateTime.UtcNow;
            db.Opportunities.Update(o);
            logger.LogInformation("Stale opportunity {Num} ({Name}) — no stage movement in >14d, escalated to Head of BD", o.OpportunityNumber, o.Name);
        }
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
    }

    // C5 (P6, CRM-041) — contract renewal alerts at 60 and 30 days before end date (each once).
    private async Task CheckContractRenewalsAsync(CrmDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.Contracts
            .Where(c => c.Status == Core.Enums.ContractStatus.Active && c.EndDate != null && c.EndDate > now)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var c in active)
        {
            var days = (c.EndDate!.Value - now).TotalDays;
            if (days <= 60 && c.RenewalAlert60SentAt == null)
            {
                c.RenewalAlert60SentAt = now; changed++;
                logger.LogInformation("Contract {Num} renewal due in <=60d → Account Owner", c.ContractNumber);
            }
            if (days <= 30 && c.RenewalAlert30SentAt == null)
            {
                c.RenewalAlert30SentAt = now; changed++;
                logger.LogInformation("Contract {Num} renewal due in <=30d → Account Owner", c.ContractNumber);
            }
            if (c.RenewalAlert60SentAt == now || c.RenewalAlert30SentAt == now) { c.UpdatedAt = now; db.Contracts.Update(c); }
        }
        if (changed > 0) await db.SaveChangesAsync(ct);
    }

    // C6 (P5, CRM-019/020) — tender submission-deadline alerts (14/7/3/1d, each once) + bid-bond 14d expiry.
    private async Task CheckTenderDeadlinesAsync(CrmDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var open = await db.Tenders
            .Where(t => (t.Status == Core.Enums.TenderStatus.Registered || t.Status == Core.Enums.TenderStatus.Submitted)
                        && t.SubmissionDeadline > now)
            .ToListAsync(ct);
        var changed = 0;
        foreach (var t in open)
        {
            var days = (t.SubmissionDeadline - now).TotalDays;
            bool hit = false;
            if (days <= 14 && t.Alert14SentAt == null) { t.Alert14SentAt = now; hit = true; }
            if (days <= 7 && t.Alert7SentAt == null) { t.Alert7SentAt = now; hit = true; }
            if (days <= 3 && t.Alert3SentAt == null) { t.Alert3SentAt = now; hit = true; }
            if (days <= 1 && t.Alert1SentAt == null) { t.Alert1SentAt = now; hit = true; }
            if (hit) { t.UpdatedAt = now; db.Tenders.Update(t); changed++;
                logger.LogInformation("Tender {Num} deadline in {Days:0}d → alert SE {SE} + Head of BD", t.TenderNumber, days, t.AssignedTo); }
        }

        var bonds = await db.TenderBidBonds
            .Where(b => b.Status == Core.Enums.BidBondStatus.Active && b.ExpiryAlertSentAt == null && b.ValidityDate > now)
            .ToListAsync(ct);
        foreach (var b in bonds)
        {
            if ((b.ValidityDate - now).TotalDays <= 14)
            {
                b.ExpiryAlertSentAt = now; b.UpdatedAt = now; db.TenderBidBonds.Update(b); changed++;
                logger.LogInformation("Bid bond {Guar} expires in <=14d", b.GuaranteeNumber);
            }
        }
        if (changed > 0) await db.SaveChangesAsync(ct);
    }

    // C7 (P7, CRM-007) — flag active clients with no interaction in >90 days (dormant) and auto-create
    // a follow-up task for the Account Owner. (Purchase-date check via Finance INVOICE deferred to O10 seam.)
    private async Task CheckDormantClientsAsync(CrmDbContext db, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var dormant = await db.Customers
            .Where(c => c.Status == Core.Enums.CustomerStatus.Active
                        && c.DormantSince == null
                        && (c.LastInteractionAt ?? c.CreatedAt) < cutoff)
            .ToListAsync(ct);
        foreach (var c in dormant)
        {
            c.DormantSince = DateTime.UtcNow; c.UpdatedAt = DateTime.UtcNow; db.Customers.Update(c);
            db.ActivityTasks.Add(new ActivityTask
            {
                CustomerId = c.Id, AssignedTo = c.AccountOwnerId, TaskType = "DormantFollowUp",
                Subject = $"Re-engage dormant client: {c.Name} (no activity in 90+ days)",
                DueDate = DateTime.UtcNow.AddDays(7), Status = Core.Enums.ActivityTaskStatus.Open,
                IsAutoCreated = true, CreatedBy = "system", UpdatedBy = "system",
            });
            logger.LogInformation("Dormant client {Name} flagged; follow-up task created for owner {Owner}", c.Name, c.AccountOwnerId);
        }
        if (dormant.Count > 0) await db.SaveChangesAsync(ct);
    }

    // C7 (P7) — flag open tasks past their due date (once).
    private async Task CheckOverdueTasksAsync(CrmDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var overdue = await db.ActivityTasks
            .Where(t => t.Status == Core.Enums.ActivityTaskStatus.Open && t.OverdueAlertedAt == null && t.DueDate < now)
            .ToListAsync(ct);
        foreach (var t in overdue)
        {
            t.OverdueAlertedAt = now; t.UpdatedAt = now; db.ActivityTasks.Update(t);
            logger.LogInformation("Overdue task '{Subject}' → SE {SE}", t.Subject, t.AssignedTo);
        }
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);
    }

    // C9 (P9/P10, CRM-025/045) — write a daily pipeline + per-SE revenue snapshot for trend (once/day).
    // Revenue proxy = closed-deal contract value (real source = Finance INVOICE, O10 seam).
    private async Task SnapshotDailyAsync(CrmDbContext db, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        if (await db.PipelineSnapshots.AnyAsync(s => s.SnapshotDate == today, ct)) return;

        var open = await db.Opportunities.Where(o => o.Status == Core.Enums.OpportunityStatus.Open).ToListAsync(ct);
        var byStage = open.GroupBy(o => o.StageName).ToDictionary(g => g.Key,
            g => new { count = g.Count(), weighted = g.Sum(o => o.EstimatedValue * o.Probability) });
        db.PipelineSnapshots.Add(new PipelineSnapshot
        {
            SnapshotDate = today, OpenCount = open.Count,
            TotalPipelineValue = open.Sum(o => o.EstimatedValue),
            WeightedPipelineValue = open.Sum(o => o.EstimatedValue * o.Probability),
            ByStageJson = System.Text.Json.JsonSerializer.Serialize(byStage),
            CreatedBy = "system", UpdatedBy = "system",
        });

        var yearStart = new DateTime(today.Year, 1, 1);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var closed = await db.Deals.Where(d => d.Status == Core.Enums.DealStatus.Closed && d.WonBy != null && d.ClosedAt != null).ToListAsync(ct);
        foreach (var g in closed.GroupBy(d => d.WonBy!))
        {
            var ded = g.ToList();
            db.RevenueSnapshots.Add(new RevenueSnapshot
            {
                EmployeeId = g.Key, SnapshotDate = today,
                DailyRevenue = ded.Where(d => d.ClosedAt!.Value.Date == today).Sum(d => d.ContractValue),
                WtdRevenue = ded.Where(d => d.ClosedAt!.Value.Date >= weekStart).Sum(d => d.ContractValue),
                MtdRevenue = ded.Where(d => d.ClosedAt!.Value.Date >= monthStart).Sum(d => d.ContractValue),
                YtdRevenue = ded.Where(d => d.ClosedAt!.Value.Date >= yearStart).Sum(d => d.ContractValue),
                CreatedBy = "system", UpdatedBy = "system",
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("CRM daily snapshot written ({Open} open opps, {SE} SE revenue rows)", open.Count, closed.Select(d => d.WonBy).Distinct().Count());
    }

    // C10 (P11, CRM-047) — alert Head of BD + CFO once a campaign consumes >=80% of its approved budget (once).
    private async Task CheckCampaignBudgetsAsync(CrmDbContext db, CancellationToken ct)
    {
        var campaigns = await db.Campaigns
            .Where(c => c.Status != Core.Enums.CampaignStatus.Cancelled
                        && c.Alert80SentAt == null
                        && c.Budget > 0
                        && c.ActualSpend >= c.Budget * 0.8m)
            .ToListAsync(ct);

        foreach (var c in campaigns)
        {
            c.Alert80SentAt = DateTime.UtcNow;
            c.UpdatedAt = DateTime.UtcNow;
            db.Campaigns.Update(c);
            var pct = Math.Round(c.ActualSpend / c.Budget * 100, 1);
            logger.LogInformation("Campaign {Name} at {Pct}% of budget (>=80%) → alert Head of BD + CFO", c.Name, pct);
        }
        if (campaigns.Count > 0) await db.SaveChangesAsync(ct);
    }

    // C11 (P12, CRM-056) — service-contract renewal alerts at 60 and 30 days before end date (each once).
    private async Task CheckServiceContractRenewalsAsync(CrmDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await db.ServiceContracts
            .Where(c => c.Status == Core.Enums.ServiceContractStatus.Active && c.EndDate > now)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var c in active)
        {
            var days = (c.EndDate - now).TotalDays;
            if (days <= 60 && c.RenewalAlert60SentAt == null)
            {
                c.RenewalAlert60SentAt = now; changed++;
                logger.LogInformation("Service contract {Num} ({Type}) renewal due in <=60d → Account Owner", c.ContractNumber, c.ContractType);
            }
            if (days <= 30 && c.RenewalAlert30SentAt == null)
            {
                c.RenewalAlert30SentAt = now; changed++;
                logger.LogInformation("Service contract {Num} ({Type}) renewal due in <=30d → Account Owner", c.ContractNumber, c.ContractType);
            }
            if (c.RenewalAlert60SentAt == now || c.RenewalAlert30SentAt == now) { c.UpdatedAt = now; db.ServiceContracts.Update(c); }
        }
        if (changed > 0) await db.SaveChangesAsync(ct);
    }

    // C12 (P13, CRM-059..062) — consolidated legal-doc expiry sweep. NDA 60d → Legal + Head of BD;
    // framework review-due + renewal 60/30 → Account Owner; subcontractor insurance + renewal 60/30;
    // carrier NTSA licence / goods-in-transit insurance / vehicle inspection expiry (30d) → Procurement + CFO.
    private async Task CheckLegalDocExpiryAsync(CrmDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var changed = 0;

        // NDAs — 60-day expiry warning (once).
        foreach (var n in await db.NdaRegisters.Where(x => x.Status == Core.Enums.NdaStatus.Active && x.ExpiryAlert60SentAt == null && x.ExpiryDate > now).ToListAsync(ct))
        {
            if ((n.ExpiryDate - now).TotalDays <= 60)
            {
                n.ExpiryAlert60SentAt = now; n.UpdatedAt = now; db.NdaRegisters.Update(n); changed++;
                logger.LogInformation("NDA {Num} ({Party}) expires in <=60d → Legal + Head of BD", n.NdaNumber, n.CounterpartyName);
            }
        }

        // Framework agreements — performance-review due + renewal 60/30.
        foreach (var f in await db.FrameworkAgreements.Where(x => x.Status == Core.Enums.FrameworkStatus.Active).ToListAsync(ct))
        {
            var hit = false;
            if (f.PerformanceReviewDate != null && f.PerformanceReviewDate <= now && f.ReviewAlertSentAt == null)
            { f.ReviewAlertSentAt = now; hit = true; logger.LogInformation("Framework {Num} performance review due → Account Owner", f.AgreementNumber); }
            if (f.EndDate > now)
            {
                var days = (f.EndDate - now).TotalDays;
                if (days <= 60 && f.RenewalAlert60SentAt == null) { f.RenewalAlert60SentAt = now; hit = true; logger.LogInformation("Framework {Num} renewal due in <=60d → Account Owner", f.AgreementNumber); }
                if (days <= 30 && f.RenewalAlert30SentAt == null) { f.RenewalAlert30SentAt = now; hit = true; logger.LogInformation("Framework {Num} renewal due in <=30d → Account Owner", f.AgreementNumber); }
            }
            if (hit) { f.UpdatedAt = now; db.FrameworkAgreements.Update(f); changed++; }
        }

        // Subcontractor agreements — insurance expiry + renewal 60/30.
        foreach (var s in await db.SubcontractorAgreements.Where(x => x.Status == Core.Enums.SubcontractStatus.Active).ToListAsync(ct))
        {
            var hit = false;
            if (s.InsuranceExpiryDate != null && s.InsuranceAlertSentAt == null && (s.InsuranceExpiryDate.Value - now).TotalDays <= 30)
            { s.InsuranceAlertSentAt = now; hit = true; logger.LogInformation("Subcontractor {Num} ({Name}) insurance expiring → Procurement + CFO", s.AgreementNumber, s.SubcontractorName); }
            if (s.EndDate > now)
            {
                var days = (s.EndDate - now).TotalDays;
                if (days <= 60 && s.RenewalAlert60SentAt == null) { s.RenewalAlert60SentAt = now; hit = true; logger.LogInformation("Subcontractor {Num} renewal due in <=60d → Account Owner", s.AgreementNumber); }
                if (days <= 30 && s.RenewalAlert30SentAt == null) { s.RenewalAlert30SentAt = now; hit = true; logger.LogInformation("Subcontractor {Num} renewal due in <=30d → Account Owner", s.AgreementNumber); }
            }
            if (hit) { s.UpdatedAt = now; db.SubcontractorAgreements.Update(s); changed++; }
        }

        // Carriers — NTSA licence / insurance / inspection expiry (30d, each once) → Procurement + CFO.
        foreach (var c in await db.CarrierAgreements.Where(x => x.Status == Core.Enums.CarrierStatus.Active).ToListAsync(ct))
        {
            var hit = false;
            if (c.NtsaLicenceExpiry != null && c.NtsaAlertSentAt == null && (c.NtsaLicenceExpiry.Value - now).TotalDays <= 30)
            { c.NtsaAlertSentAt = now; hit = true; logger.LogInformation("Carrier {Num} ({Name}) NTSA licence expiring → Procurement + CFO", c.AgreementNumber, c.CarrierName); }
            if (c.GoodsInTransitInsuranceExpiry != null && c.InsuranceAlertSentAt == null && (c.GoodsInTransitInsuranceExpiry.Value - now).TotalDays <= 30)
            { c.InsuranceAlertSentAt = now; hit = true; logger.LogInformation("Carrier {Num} goods-in-transit insurance expiring → Procurement + CFO", c.AgreementNumber); }
            if (c.VehicleInspectionExpiry != null && c.InspectionAlertSentAt == null && (c.VehicleInspectionExpiry.Value - now).TotalDays <= 30)
            { c.InspectionAlertSentAt = now; hit = true; logger.LogInformation("Carrier {Num} vehicle inspection expiring → Procurement + CFO", c.AgreementNumber); }
            if (hit) { c.UpdatedAt = now; db.CarrierAgreements.Update(c); changed++; }
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
    }

    // C13 (P14, CRM-063) — payment/debtor alert cadences. Reads the Finance service (AR invoices,
    // supplier invoices, vouchers) for this tenant schema and logs deduplicated PaymentAlertLog rows.
    // Daily alerts dedup per day; per-invoice debtor escalations & unauthorised flags dedup once;
    // the CFO weekly summary dedups per ISO week and the MD dashboard per month.
    private async Task CheckPaymentDueAlertsAsync(
        CrmService.Infrastructure.Data.CrmDbContext db,
        Core.Interfaces.Services.IFinanceReadClient finance,
        string schema, CancellationToken ct)
    {
        if (!finance.IsConfigured) return;

        var now = DateTime.UtcNow;
        var day = now.ToString("yyyyMMdd");
        var week = $"{System.Globalization.ISOWeek.GetYear(now)}-W{System.Globalization.ISOWeek.GetWeekOfYear(now):D2}";
        var month = now.ToString("yyyyMM");

        var invoices = await finance.GetArInvoicesAsync(schema, ct);
        var suppliers = await finance.GetSupplierInvoicesAsync(schema, ct);
        var vouchers = await finance.GetVouchersAsync(schema, ct);

        var candidates = new List<PaymentAlertLog>();
        void Add(PaymentAlertLog a) => candidates.Add(a);

        // 1) Supplier invoices due within 7 days → daily FM alert.
        foreach (var s in suppliers.Where(s => s.Balance > 0 && s.DueDate >= now && (s.DueDate - now).TotalDays <= 7))
            Add(new PaymentAlertLog
            {
                AlertType = Core.Enums.PaymentAlertType.SupplierInvoiceDue, Severity = Core.Enums.PaymentAlertSeverity.Warning,
                TargetRole = "Finance Manager", ReferenceType = "SupplierInvoice", ReferenceId = s.Id, ReferenceNumber = s.SupplierInvoiceNo,
                Amount = s.Balance, DaysMetric = (int)Math.Ceiling((s.DueDate - now).TotalDays),
                Message = $"Supplier invoice {s.SupplierInvoiceNo} ({s.SupplierName}) of {s.Balance:N0} due in {Math.Ceiling((s.DueDate - now).TotalDays):0} day(s).",
                DedupKey = $"SUPINV_DUE|{s.Id}|{day}", CreatedBy = "system", UpdatedBy = "system",
            });

        // 2) Client invoices overdue → daily FM alert; 3) debtor escalation >45 (AO+LM) / >60 (MD), once.
        foreach (var i in invoices.Where(i => i.Balance > 0 && i.DueDate < now))
        {
            var daysOverdue = (int)Math.Floor((now - i.DueDate).TotalDays);
            Add(new PaymentAlertLog
            {
                AlertType = Core.Enums.PaymentAlertType.ClientInvoiceOverdue, Severity = Core.Enums.PaymentAlertSeverity.Warning,
                TargetRole = "Finance Manager", CustomerId = i.CustomerId, CustomerName = i.CustomerName,
                ReferenceType = "Invoice", ReferenceId = i.Id, ReferenceNumber = i.InvoiceNo, Amount = i.Balance, DaysMetric = daysOverdue,
                Message = $"Client invoice {i.InvoiceNo} ({i.CustomerName}) of {i.Balance:N0} is {daysOverdue} day(s) overdue.",
                DedupKey = $"CLIINV_OVR|{i.Id}|{day}", CreatedBy = "system", UpdatedBy = "system",
            });

            if (daysOverdue > 60)
                Add(new PaymentAlertLog
                {
                    AlertType = Core.Enums.PaymentAlertType.DebtorOver60, Severity = Core.Enums.PaymentAlertSeverity.Critical,
                    TargetRole = "Managing Director", CustomerId = i.CustomerId, CustomerName = i.CustomerName,
                    ReferenceType = "Invoice", ReferenceId = i.Id, ReferenceNumber = i.InvoiceNo, Amount = i.Balance, DaysMetric = daysOverdue,
                    Message = $"RED-005: {i.CustomerName} invoice {i.InvoiceNo} of {i.Balance:N0} is {daysOverdue} days overdue (>60) — escalate to MD.",
                    DedupKey = $"DEBTOR60|{i.Id}", CreatedBy = "system", UpdatedBy = "system",
                });
            else if (daysOverdue > 45)
                Add(new PaymentAlertLog
                {
                    AlertType = Core.Enums.PaymentAlertType.DebtorOver45, Severity = Core.Enums.PaymentAlertSeverity.Warning,
                    TargetRole = "Account Owner + Line Manager", CustomerId = i.CustomerId, CustomerName = i.CustomerName,
                    ReferenceType = "Invoice", ReferenceId = i.Id, ReferenceNumber = i.InvoiceNo, Amount = i.Balance, DaysMetric = daysOverdue,
                    Message = $"RED-005: {i.CustomerName} invoice {i.InvoiceNo} of {i.Balance:N0} is {daysOverdue} days overdue (>45) — chase debtor.",
                    DedupKey = $"DEBTOR45|{i.Id}", CreatedBy = "system", UpdatedBy = "system",
                });
        }

        // 4) Supplier vouchers awaiting authorisation for 3+ days (age via linked supplier invoice) → FM + CFO.
        var supById = suppliers.ToDictionary(s => s.Id, s => s);
        foreach (var v in vouchers.Where(Core.Services.PaymentAlertService.IsAwaitingAuthorisation))
        {
            var invDate = v.SupplierInvoiceId != null && supById.TryGetValue(v.SupplierInvoiceId, out var si) ? si.InvoiceDate : (DateTime?)null;
            if (invDate == null || (now - invDate.Value).TotalDays < 3) continue;
            Add(new PaymentAlertLog
            {
                AlertType = Core.Enums.PaymentAlertType.SupplierUnauthorised, Severity = Core.Enums.PaymentAlertSeverity.Critical,
                TargetRole = "Finance Manager + CFO", ReferenceType = "Voucher", ReferenceId = v.Id, ReferenceNumber = v.VoucherNo,
                Amount = v.Amount, DaysMetric = (int)Math.Floor((now - invDate.Value).TotalDays),
                Message = $"Payment voucher {v.VoucherNo} ({v.Payee}) of {v.Amount:N0} unauthorised for {Math.Floor((now - invDate.Value).TotalDays):0}+ days.",
                DedupKey = $"VCH_UNAUTH|{v.Id}", CreatedBy = "system", UpdatedBy = "system",
            });
        }

        // 5) Weekly CFO summary (once per ISO week).
        var overdueInv = invoices.Where(i => i.Balance > 0 && i.DueDate < now).ToList();
        var dueSup = suppliers.Where(s => s.Balance > 0 && s.DueDate >= now && (s.DueDate - now).TotalDays <= 7).ToList();
        Add(new PaymentAlertLog
        {
            AlertType = Core.Enums.PaymentAlertType.WeeklyCfoSummary, Severity = Core.Enums.PaymentAlertSeverity.Info,
            TargetRole = "CFO", ReferenceType = "Summary",
            Amount = overdueInv.Sum(i => i.Balance),
            Message = $"Weekly summary: {overdueInv.Count} overdue debtor invoice(s) totalling {overdueInv.Sum(i => i.Balance):N0}; {dueSup.Count} supplier invoice(s) due ≤7d totalling {dueSup.Sum(s => s.Balance):N0}.",
            DedupKey = $"CFO_WEEKLY|{week}", CreatedBy = "system", UpdatedBy = "system",
        });

        // 6) Monthly MD priority dashboard (once per month).
        Add(new PaymentAlertLog
        {
            AlertType = Core.Enums.PaymentAlertType.MonthlyMdDashboard, Severity = Core.Enums.PaymentAlertSeverity.Info,
            TargetRole = "Managing Director", ReferenceType = "Summary",
            Amount = overdueInv.Sum(i => i.Balance),
            Message = $"Monthly priority dashboard: total overdue receivables {overdueInv.Sum(i => i.Balance):N0} across {overdueInv.Count} invoice(s).",
            DedupKey = $"MD_MONTHLY|{month}", CreatedBy = "system", UpdatedBy = "system",
        });

        if (candidates.Count == 0) return;
        var keys = candidates.Select(c => c.DedupKey).ToList();
        var existing = await db.PaymentAlertLogs.Where(a => keys.Contains(a.DedupKey)).Select(a => a.DedupKey).ToListAsync(ct);
        var fresh = candidates.Where(c => !existing.Contains(c.DedupKey)).ToList();
        if (fresh.Count == 0) return;

        db.PaymentAlertLogs.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Payment-alert sweep for {Schema}: raised {Count} new alert(s)", schema, fresh.Count);
    }
}
