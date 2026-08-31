using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Tickets;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Core.Services;

public class TicketService(
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    ITicketCommentRepository commentRepository,
    ITicketHistoryService historyService,
    ITicketEscalationRepository escalationRepository,
    ITicketWatcherRepository watcherRepository,
    IGenericRepository<TicketAssignment> assignmentRepository,
    ISLAService slaService,
    ICustomerService customerService,
    ITicketNotificationService deptNotificationService,
    INotificationService notificationService,
    IAlertService alertService,
    IPortalEmailService portalEmailService,
    ISmsSender smsSender,
    IComplaintWorkflowService complaintWorkflowService,
    Integrations.ICrmSync crmSync,
    Integrations.IEmployeeDirectory employeeDirectory,
    IWorkflowEngine workflowEngine,
    ITicketAssignmentClient ticketAssignmentClient,
    ITicketTransitionService transitionService,
    IHttpContextAccessor httpContextAccessor,
    IMapper mapper) : ITicketService
{
    public async Task<TicketReadDto> CreateAsync(CreateTicketDto dto, string createdByUserId)
    {
        var category = await categoryRepository.GetByIdAsync(dto.CategoryId)
            ?? throw new KeyNotFoundException($"Category {dto.CategoryId} not found.");

        var ticket = mapper.Map<Ticket>(dto);
        ticket.CreatedByUserId = createdByUserId;
        ticket.CreatedBy = createdByUserId;
        ticket.UpdatedBy = createdByUserId;
        ticket.Status = TicketStatus.New;
        ticket.RequiresEvidence = category.RequiresEvidence;

        // D8-1/D1-2/D1-3 — resolve the client: a CRM master pick wins (links to the org-wide customer);
        // then an explicit local customer id; otherwise a typed-in client name resolves-or-creates.
        if (!string.IsNullOrWhiteSpace(dto.CrmCustomerId))
            ticket.CustomerId = await customerService.ResolveOrCreateFromCrmAsync(dto.CrmCustomerId, createdByUserId);
        else if (!string.IsNullOrWhiteSpace(dto.CustomerId))
            ticket.CustomerId = dto.CustomerId;
        else if (!string.IsNullOrWhiteSpace(dto.ClientName))
            ticket.CustomerId = await customerService.ResolveOrCreateAsync(
                dto.ClientName, dto.ClientCompany, dto.ClientReference, createdByUserId);

        // D8-5 — for IT tickets, enrich the branch from the HR directory when it's available
        // (no-op until the HR adapter is registered; see INTEGRATIONS.md).
        if (category.Id.StartsWith("cat-it-") && !string.IsNullOrEmpty(ticket.EmployeeId) && employeeDirectory.IsEnabled)
        {
            try
            {
                var emp = await employeeDirectory.ResolveAsync(ticket.EmployeeId);
                if (emp != null && string.IsNullOrWhiteSpace(ticket.BranchId)) ticket.BranchId = emp.BranchId;
            }
            catch { /* directory failure must not break ticket creation */ }
        }

        // D4-3 — flag a 30-day repeat contact (same client + same category) before persisting.
        ticket.IsRepeat = await ticketRepository.HasRecentSimilarAsync(
            ticket.CustomerId, ticket.RequesterEmail, ticket.CategoryId, DateTime.UtcNow.AddDays(-30));

        var slaDeadlines = await slaService.CalculateSLADeadlinesAsync(dto.CategoryId, dto.Priority, ticket.CreatedAt);
        ticket.ResponseDueAt = slaDeadlines.ResponseDueAt;
        ticket.ResolutionDueAt = slaDeadlines.ResolutionDueAt;

        // (TenantId, TicketNumber) is unique-indexed (see TicketingDbContext); MAX+1 assignment is
        // racy under concurrent creates for the same tenant, so retry with a freshly computed
        // number if a concurrent create just took the one GetNextTicketNumberAsync counted.
        Ticket created = null!;
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            ticket.TicketNumber = await ticketRepository.GetNextTicketNumberAsync();  // D1-4
            try
            {
                created = await ticketRepository.CreateAsync(ticket);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly computed number
            }
        }

        await historyService.AppendAsync(created.Id, createdByUserId, "Created", null, TicketStatus.New.ToString());

        await workflowEngine.TriggerAsync(WorkflowTriggerEvent.TicketCreated, created, createdByUserId);

        // D4-1/D4-2/D4-3 — auto-acknowledge the requester + raise a repeat-contact alert.
        await SendCreationSignalsAsync(created, category);

        // D5-1 — complaint categories auto-initialise the 5-step handling workflow.
        if (category.IsComplaint)
            await complaintWorkflowService.InitializeAsync(created.Id);

        if (!string.IsNullOrEmpty(category.DefaultAssigneeId))
        {
            await AssignAsync(created.Id, new AssignTicketDto
            {
                AssignedToUserId = category.DefaultAssigneeId,
                Notes = "Auto-assigned from category default",
                IsPrimary = true
            }, createdByUserId);

            return await GetByIdAsync(created.Id) ?? mapper.Map<TicketReadDto>(created);
        }

        return mapper.Map<TicketReadDto>(created);
    }

    public async Task<IEnumerable<TicketAssignmentReadDto>> GetAssignmentsAsync(string ticketId)
    {
        var assignments = await assignmentRepository.GetAllAsync();
        return assignments
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new TicketAssignmentReadDto
            {
                Id               = a.Id,
                AssignedToUserId = a.AssignedToUserId,
                AssignedByUserId = a.AssignedByUserId,
                AssignedAt       = a.AssignedAt,
                HandoverNotes    = a.Notes,
                IsPrimary        = a.IsPrimary,
            });
    }

    public async Task<TicketReadDto> LinkChildAsync(string parentId, string childTicketId, string actor)
    {
        if (parentId == childTicketId)
            throw new InvalidOperationException("A ticket can't be linked to itself.");

        var parent = await ticketRepository.GetByIdAsync(parentId)
            ?? throw new KeyNotFoundException($"Ticket {parentId} not found.");
        var child = await ticketRepository.GetByIdAsync(childTicketId)
            ?? throw new KeyNotFoundException($"Ticket {childTicketId} not found.");

        // Keep the hierarchy one level deep and acyclic, and don't cross with dup-merge.
        if (!string.IsNullOrEmpty(parent.ParentTicketId))
            throw new InvalidOperationException("The chosen parent is itself a child ticket — nest only one level deep.");
        if ((await ticketRepository.GetChildrenAsync(childTicketId)).Any())
            throw new InvalidOperationException("That ticket already has its own children, so it can't become a child.");
        if (!string.IsNullOrEmpty(child.ParentTicketId) && child.ParentTicketId != parentId)
            throw new InvalidOperationException("That ticket is already linked to another parent.");
        if (child.MergedIntoTicketId != null || parent.MergedIntoTicketId != null)
            throw new InvalidOperationException("A merged ticket can't take part in parent/child linking.");

        child.ParentTicketId = parentId;
        child.UpdatedBy = actor;
        await ticketRepository.UpdateAsync(child);
        await historyService.AppendAsync(childTicketId, actor, "LinkedToParent", null, parentId, $"Linked as child of {parent.Reference}");
        await historyService.AppendAsync(parentId, actor, "ChildLinked", null, childTicketId, $"Child {child.Reference} linked");
        return mapper.Map<TicketReadDto>(child);
    }

    public async Task<TicketReadDto> UnlinkParentAsync(string childTicketId, string actor)
    {
        var child = await ticketRepository.GetByIdAsync(childTicketId)
            ?? throw new KeyNotFoundException($"Ticket {childTicketId} not found.");
        var oldParent = child.ParentTicketId;
        child.ParentTicketId = null;
        child.UpdatedBy = actor;
        await ticketRepository.UpdateAsync(child);
        await historyService.AppendAsync(childTicketId, actor, "UnlinkedFromParent", oldParent, null, "Unlinked from parent");
        return mapper.Map<TicketReadDto>(child);
    }

    public async Task<IEnumerable<TicketReadDto>> GetChildrenAsync(string parentId)
    {
        var children = await ticketRepository.GetChildrenAsync(parentId);
        return mapper.Map<IEnumerable<TicketReadDto>>(children);
    }

    // D3-1 — when a parent reaches a terminal state, carry its still-open children to the same state.
    // Forced (System) so the children skip the manual evidence/root-cause gates — those apply to the
    // ticket actually being worked, not to an inherited close.
    private async Task CascadeTerminalToChildrenAsync(Ticket parent, TicketStatus terminal, string actor)
    {
        var children = await ticketRepository.GetChildrenAsync(parent.Id);
        foreach (var child in children)
        {
            if (child.Status is TicketStatus.Resolved or TicketStatus.Closed) continue;
            if (terminal == TicketStatus.Closed) child.ClosureType = ClosureType.Completed;
            await transitionService.TransitionAsync(
                child, terminal, TransitionSource.System, actor,
                $"Auto-{terminal.ToString().ToLowerInvariant()} — parent ticket {parent.Reference} {terminal.ToString().ToLowerInvariant()}",
                terminal == TicketStatus.Closed ? "Closed" : "Resolved");
        }
    }

    // D4 — the tenant schema for alerts, from the JWT "schema" claim (staff) or the gateway-injected
    // X-Tenant-Schema header (anonymous portal). Empty when neither is present.
    private string CurrentSchema =>
        httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value
        ?? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault()
        ?? string.Empty;

    // D4-1/2/3 — post-creation side effects: auto-ack (email + optional SMS) and repeat-contact alert.
    // All best-effort; nothing here may break ticket creation.
    private async Task SendCreationSignalsAsync(Ticket ticket, TicketCategory category)
    {
        var customer = string.IsNullOrEmpty(ticket.CustomerId)
            ? null
            : await customerService.GetByIdAsync(ticket.CustomerId);

        // D4-1 — acknowledge the requester (ticket ref, category, SLA due) once.
        if (!string.IsNullOrWhiteSpace(ticket.RequesterEmail) && ticket.AckSentAt == null)
        {
            var toName = customer?.Name ?? "Customer";
            _ = portalEmailService.SendAcknowledgementAsync(
                toName, ticket.RequesterEmail!, ticket.Reference, category.Name, ticket.ResolutionDueAt);
            ticket.AckSentAt = DateTime.UtcNow;
            await ticketRepository.UpdateAsync(ticket);

            // D4-2 — SMS acknowledgement when the channel is enabled and we have a phone.
            if (smsSender.IsEnabled && !string.IsNullOrWhiteSpace(customer?.Phone))
                _ = smsSender.SendAsync(customer!.Phone!,
                    $"Ticket {ticket.Reference} received ({category.Name}). Track it with this reference.");
        }

        // D4-3 — repeat contact within 30 days → alert the Head of BD / managers.
        if (ticket.IsRepeat)
        {
            var who = customer?.Name ?? ticket.RequesterEmail ?? "A client";
            try
            {
                await alertService.CreateAsync(
                    CurrentSchema, source: "RepeatContact", severity: "Warning",
                    title: $"Repeat contact — {who} ({category.Name})",
                    message: $"{who} raised another {category.Name} ticket within 30 days ({ticket.Reference}). Review for a systemic/unresolved issue.",
                    ticketId: ticket.Id, ticketTitle: ticket.Title,
                    requiredPermission: "tickets.assign");
            }
            catch
            {
                // Non-fatal: a failed alert must not roll back ticket creation.
            }
        }
    }

    public async Task<TicketReadDto> UpdateAsync(string id, UpdateTicketDto dto, string updatedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        if (dto.Title != null) ticket.Title = dto.Title;
        if (dto.Description != null) ticket.Description = dto.Description;
        if (dto.DepartmentId != null) ticket.DepartmentId = dto.DepartmentId;
        if (dto.DueDate.HasValue) ticket.DueDate = dto.DueDate;
        // D3-2 — context refs (empty string clears; null leaves unchanged).
        // D8-4 — DEFERRED READ BOUNDARY (Projects/FSR Module 5): these are stored + displayed as plain
        // reference strings. Resolving them to live PROJECT / FIELD_SERVICE_REPORT records (title,
        // status, link) is intentionally NOT built — it needs Module 5's real read API. Wire it later
        // via an IProjectDirectory read seam behind an "Integrations:Projects" flag. See INTEGRATIONS.md.
        if (dto.ProjectId != null) ticket.ProjectId = dto.ProjectId.Length == 0 ? null : dto.ProjectId;
        if (dto.FsrId != null) ticket.FsrId = dto.FsrId.Length == 0 ? null : dto.FsrId;
        // D7-1 — IT-helpdesk context (empty clears; null leaves unchanged).
        if (dto.EmployeeId != null) ticket.EmployeeId = dto.EmployeeId.Length == 0 ? null : dto.EmployeeId;
        if (dto.BranchId != null) ticket.BranchId = dto.BranchId.Length == 0 ? null : dto.BranchId;
        if (dto.SystemAffected != null) ticket.SystemAffected = dto.SystemAffected.Length == 0 ? null : dto.SystemAffected;

        if (dto.Priority.HasValue && dto.Priority.Value != ticket.Priority)
        {
            var oldPriority = ticket.Priority.ToString();
            ticket.Priority = dto.Priority.Value;
            // #9: anchor the new SLA window at the change time, not CreatedAt — bumping an old ticket
            // to Critical must not compute a deadline already in the past (instant, no-fault breach).
            var slaDeadlines = await slaService.CalculateSLADeadlinesAsync(ticket.CategoryId, ticket.Priority, DateTime.UtcNow);
            if (ticket.FirstResponseAt == null) ticket.ResponseDueAt = slaDeadlines.ResponseDueAt;
            ticket.ResolutionDueAt = slaDeadlines.ResolutionDueAt;
            await historyService.AppendAsync(id, updatedByUserId, "PriorityChanged", oldPriority, ticket.Priority.ToString());
        }

        ticket.UpdatedBy = updatedByUserId;
        var updated = await ticketRepository.UpdateAsync(ticket);

        await workflowEngine.TriggerAsync(WorkflowTriggerEvent.TicketUpdated, updated, updatedByUserId);

        return mapper.Map<TicketReadDto>(updated);
    }

    public async Task<TicketReadDto> ChangeStatusAsync(string id, ChangeStatusDto dto, string changedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        var oldStatus = await transitionService.TransitionAsync(
            ticket, dto.NewStatus, TransitionSource.Manual, changedByUserId, dto.Notes, "StatusChanged");

        await notificationService.NotifyStatusChangedAsync(ticket, oldStatus.ToString(), dto.NewStatus.ToString(), changedByUserId);
        await notificationService.NotifyWatchersAsync(ticket, "StatusChanged",
            $"Ticket \"{ticket.Title}\" status changed to {dto.NewStatus}.", changedByUserId);
        await workflowEngine.TriggerAsync(WorkflowTriggerEvent.StatusChanged, ticket, changedByUserId);

        return mapper.Map<TicketReadDto>(ticket);
    }

    public async Task<TicketReadDto> AssignAsync(string id, AssignTicketDto dto, string assignedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        var oldAssignee = ticket.AssignedToUserId;
        var oldDeptId = ticket.DepartmentId;
        ticket.AssignedToUserId = dto.AssignedToUserId;
        if (!string.IsNullOrEmpty(dto.AssigneeName))
            ticket.AssigneeName = dto.AssigneeName;

        if (!string.IsNullOrEmpty(dto.DepartmentId))
            ticket.DepartmentId = dto.DepartmentId;

        if (ticket.Status == TicketStatus.New)
            ticket.Status = TicketStatus.Assigned;

        ticket.UpdatedBy = assignedByUserId;
        var updated = await ticketRepository.UpdateAsync(ticket);

        var assignment = new TicketAssignment
        {
            TicketId = id,
            AssignedToUserId = dto.AssignedToUserId,
            AssignedByUserId = assignedByUserId,
            AssignedAt = DateTime.UtcNow,
            Notes = dto.Notes,
            IsPrimary = dto.IsPrimary,
            CreatedBy = assignedByUserId
        };
        await assignmentRepository.CreateAsync(assignment);

        await historyService.AppendAsync(id, assignedByUserId, "Assigned", oldAssignee, dto.AssignedToUserId, dto.Notes);
        if (!string.IsNullOrEmpty(dto.DepartmentId) && dto.DepartmentId != oldDeptId)
            await historyService.AppendAsync(id, assignedByUserId, "DepartmentChanged", oldDeptId, dto.DepartmentId, null);

        await notificationService.NotifyAssignedAsync(ticket, dto.AssignedToUserId, assignedByUserId);

        // Create an Operations Assignment so the field work follows the assignment workflow
        if (string.IsNullOrEmpty(ticket.OperationsAssignmentId))
        {
            var bearerToken = httpContextAccessor.HttpContext?.Request.Headers.Authorization
                .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
            var opsAssignmentId = await ticketAssignmentClient.CreateAssignmentFromTicketAsync(
                ticket, assignedByUserId, bearerToken);
            if (opsAssignmentId is not null)
            {
                ticket.OperationsAssignmentId = opsAssignmentId;
                await ticketRepository.UpdateAsync(ticket);
                await historyService.AppendAsync(id, assignedByUserId, "OperationsAssignmentCreated",
                    null, opsAssignmentId, "Field assignment created in Operations module");
            }
        }

        return mapper.Map<TicketReadDto>(updated);
    }

    public async Task<TicketReadDto> ResolveAsync(string id, ResolveTicketDto dto, string resolvedByUserId)
    {
        var ticket = await ticketRepository.GetByIdWithDetailsAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        ticket.ResolutionNotes = dto.ResolutionNotes;
        ticket.RootCause = dto.RootCause;   // D3-3 — the gate rejects a manual resolve without one

        // Evidence gate + transition validation live in the gate now (enforceEvidence: true).
        await transitionService.TransitionAsync(
            ticket, TicketStatus.Resolved, TransitionSource.Manual, resolvedByUserId,
            dto.ResolutionNotes, "Resolved", enforceEvidence: true);

        await notificationService.NotifyResolvedAsync(ticket, resolvedByUserId);

        await CascadeTerminalToChildrenAsync(ticket, TicketStatus.Resolved, resolvedByUserId);  // D3-1

        return mapper.Map<TicketReadDto>(ticket);
    }

    public async Task<TicketReadDto> CloseAsync(string id, string closedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        // #5: manual close is only reachable from Resolved, so it's a completed closure.
        ticket.ClosureType = ClosureType.Completed;
        await transitionService.TransitionAsync(
            ticket, TicketStatus.Closed, TransitionSource.Manual, closedByUserId, null, "Closed");

        await CascadeTerminalToChildrenAsync(ticket, TicketStatus.Closed, closedByUserId);  // D3-1

        // D8-2 — log a customer interaction in CRM on close (no-op until the CRM adapter is wired).
        await RecordCustomerInteractionAsync(ticket, "TicketClosed",
            $"Ticket {ticket.Reference} closed.", closedByUserId);

        return mapper.Map<TicketReadDto>(ticket);
    }

    // D8-2 — best-effort CRM customer-interaction write. Guarded by ICrmSync.IsEnabled; never throws.
    private async Task RecordCustomerInteractionAsync(Ticket ticket, string type, string summary, string? actor)
    {
        if (!crmSync.IsEnabled || string.IsNullOrEmpty(ticket.CustomerId)) return;
        try
        {
            // D8-1 — prefer the linked CRM customer id so CRM matches by id, not name.
            var c = await customerService.GetByIdAsync(ticket.CustomerId);
            var crmId = string.IsNullOrWhiteSpace(c?.CrmCustomerId) ? ticket.CustomerId : c!.CrmCustomerId;
            await crmSync.RecordCustomerInteractionAsync(new Integrations.CustomerInteraction(
                CurrentSchema, crmId, ticket.Id, ticket.Reference, type, summary,
                DateTime.UtcNow, actor, c?.Name));
        }
        catch { /* integration failure must not break ticket handling */ }
    }

    public async Task<TicketReadDto> ReopenAsync(string id, string reopenedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        // #8: a reopened ticket needs a fresh resolution window — the original ResolutionDueAt is
        // long past, so without this every reopened ticket is instantly "breached". Recompute from now.
        // (FirstResponseAt is kept — the ticket was already responded to once.)
        var sla = await slaService.CalculateSLADeadlinesAsync(ticket.CategoryId, ticket.Priority, DateTime.UtcNow);
        ticket.ResolutionDueAt = sla.ResolutionDueAt;
        if (ticket.FirstResponseAt == null) ticket.ResponseDueAt = sla.ResponseDueAt;

        // Gate clears ResolvedAt/ClosedAt and persists the recomputed deadlines above.
        await transitionService.TransitionAsync(
            ticket, TicketStatus.Reopened, TransitionSource.Manual, reopenedByUserId, null, "Reopened");

        return mapper.Map<TicketReadDto>(ticket);
    }

    public async Task<TicketReadDto> EscalateAsync(string id, EscalateTicketDto dto, string escalatedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        // Escalation may force from any status (source != Manual); gate persists + logs "Escalated".
        ticket.IsEscalated = true;
        ticket.EscalationLevel = dto.EscalationLevel;
        // #10: escalation hands the ticket to the target — reassign so it's clearly owned by them.
        var previousAssignee = ticket.AssignedToUserId;
        if (!string.IsNullOrWhiteSpace(dto.EscalatedToUserId))
            ticket.AssignedToUserId = dto.EscalatedToUserId;
        await transitionService.TransitionAsync(
            ticket, TicketStatus.Escalated, TransitionSource.Escalation, escalatedByUserId, dto.Reason, "Escalated");
        if (!string.IsNullOrWhiteSpace(dto.EscalatedToUserId) && dto.EscalatedToUserId != previousAssignee)
        {
            await historyService.AppendAsync(id, escalatedByUserId, "Assigned", previousAssignee, dto.EscalatedToUserId, "Reassigned on escalation");
            // D2-5: record the reassignment in the ownership trail.
            await assignmentRepository.CreateAsync(new TicketAssignment
            {
                TicketId         = id,
                AssignedToUserId = dto.EscalatedToUserId,
                AssignedByUserId = escalatedByUserId,
                AssignedAt       = DateTime.UtcNow,
                Notes            = $"Reassigned on escalation to {dto.EscalationLevel}" + (string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" — {dto.Reason}"),
                IsPrimary        = true,
                CreatedBy        = escalatedByUserId,
            });
        }

        var escalation = new TicketEscalation
        {
            TicketId = id,
            EscalationLevel = dto.EscalationLevel,
            EscalatedToUserId = dto.EscalatedToUserId,
            EscalatedByUserId = escalatedByUserId,
            Reason = dto.Reason,
            EscalatedAt = DateTime.UtcNow,
            CreatedBy = escalatedByUserId
        };
        await escalationRepository.CreateAsync(escalation);

        await notificationService.NotifyEscalatedAsync(ticket, escalatedByUserId);
        await workflowEngine.TriggerAsync(WorkflowTriggerEvent.TicketEscalated, ticket, escalatedByUserId);

        return mapper.Map<TicketReadDto>(ticket);
    }

    public async Task<TicketReadDto?> GetByIdAsync(string id)
    {
        var ticket = await ticketRepository.GetByIdWithDetailsAsync(id);
        if (ticket == null) return null;

        var dto = mapper.Map<TicketReadDto>(ticket);
        dto.IsResponseBreached = slaService.IsResponseBreached(ticket);
        dto.IsResolutionBreached = slaService.IsResolutionBreached(ticket);
        return dto;
    }

    public async Task<PaginatedResult<TicketReadDto>> GetPagedAsync(TicketFilterParameters parameters, string? currentUserId = null)
    {
        var result = await ticketRepository.GetPagedAsync(parameters, currentUserId);
        return new PaginatedResult<TicketReadDto>
        {
            Items = mapper.Map<IEnumerable<TicketReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<CommentReadDto> AddCommentAsync(string ticketId, CreateCommentDto dto, string authorUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        var comment = new TicketComment
        {
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Content = dto.Content,
            IsInternal = dto.IsInternal,
            CreatedBy = authorUserId
        };

        var created = await commentRepository.CreateAsync(comment);

        await historyService.AppendAsync(ticketId, authorUserId, "Commented", null, null,
            dto.IsInternal ? "Internal note added" : "Comment added");

        // #3: the first PUBLIC reply by someone other than the requester is the first response.
        // Awaited before the fire-and-forget notifications so the write completes in-scope.
        if (!dto.IsInternal && ticket.FirstResponseAt == null && authorUserId != ticket.CreatedByUserId)
        {
            ticket.FirstResponseAt = DateTime.UtcNow;
            ticket.UpdatedBy = authorUserId;
            await ticketRepository.UpdateAsync(ticket);
            await historyService.AppendAsync(ticketId, authorUserId, "FirstResponse", null, null, "First staff response recorded");
        }

        if (!dto.IsInternal)
        {
            await notificationService.NotifyCommentAddedAsync(ticket, authorUserId, dto.Content);
            await notificationService.NotifyWatchersAsync(ticket, "CommentAdded",
                $"New comment on \"{ticket.Title}\": {(dto.Content.Length > 100 ? dto.Content[..100] + "…" : dto.Content)}", authorUserId);
        }

        await workflowEngine.TriggerAsync(WorkflowTriggerEvent.CommentAdded, ticket, authorUserId);

        return mapper.Map<CommentReadDto>(created);
    }

    public async Task<IEnumerable<CommentReadDto>> GetCommentsAsync(string ticketId)
    {
        var comments = await commentRepository.GetByTicketIdAsync(ticketId);
        return mapper.Map<IEnumerable<CommentReadDto>>(comments);
    }

    public async Task<IEnumerable<CommentReadDto>> GetPublicConversationByReferenceAsync(string reference)
    {
        var ticket = await ResolveByReferenceAsync(reference);
        if (ticket == null) return Enumerable.Empty<CommentReadDto>();
        var comments = await commentRepository.GetByTicketIdAsync(ticket.Id);
        // Public only — internal notes never leave the building.
        return mapper.Map<IEnumerable<CommentReadDto>>(comments.Where(c => !c.IsInternal));
    }

    public async Task<(bool Found, CommentReadDto? Comment)> AddPublicReplyAsync(string reference, string message, string? authorName)
    {
        var ticket = await ResolveByReferenceAsync(reference);
        if (ticket == null) return (false, null);

        var content = string.IsNullOrWhiteSpace(authorName) ? message.Trim() : $"{authorName.Trim()}: {message.Trim()}";
        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorUserId = "portal-customer",   // external requester, not a staff first response (#3 skipped)
            Content = content,
            IsInternal = false,
            CreatedBy = "portal-customer",
        };
        var created = await commentRepository.CreateAsync(comment);
        await historyService.AppendAsync(ticket.Id, "portal-customer", "CustomerReplied", null, null, "Customer replied via the portal");

        // A reply means the customer is no longer the blocker — un-pend so the no-response
        // auto-close stops and the SLA clock resumes (gate handles the resume + deadline extension).
        if (ticket.Status == TicketStatus.Pending)
            await transitionService.TransitionAsync(
                ticket, TicketStatus.InProgress, TransitionSource.System, "portal-customer",
                "Customer replied via the portal", "CustomerReplied");

        // Awaited (portal request scope) to avoid concurrent use of the scoped DbContext.
        await notificationService.NotifyCommentAddedAsync(ticket, "portal-customer", message);
        await notificationService.NotifyWatchersAsync(ticket, "CustomerReplied",
            $"Customer replied on \"{ticket.Title}\".", "portal-customer");

        return (true, mapper.Map<CommentReadDto>(created));
    }

    public async Task<IEnumerable<TicketReadDto>> FindDuplicatesAsync(string id)
    {
        var ticket = await ticketRepository.GetByIdAsync(id);
        if (ticket == null) return Enumerable.Empty<TicketReadDto>();

        var all = await ticketRepository.GetAllAsync();
        // Likely duplicates: another still-open ticket from the same requester (email preferred,
        // else the same creator), not itself and not already merged.
        var candidates = all.Where(t =>
            t.Id != ticket.Id
            && t.MergedIntoTicketId == null
            && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved
            && (
                (!string.IsNullOrWhiteSpace(ticket.RequesterEmail)
                    && string.Equals(t.RequesterEmail, ticket.RequesterEmail, StringComparison.OrdinalIgnoreCase))
                || (string.IsNullOrWhiteSpace(ticket.RequesterEmail)
                    && !string.IsNullOrWhiteSpace(ticket.CreatedByUserId)
                    && t.CreatedByUserId == ticket.CreatedByUserId)
            ));
        return mapper.Map<IEnumerable<TicketReadDto>>(candidates);
    }

    public async Task<TicketReadDto> MergeAsync(string sourceId, string targetId, string mergedByUserId)
    {
        if (sourceId == targetId)
            throw new InvalidOperationException("A ticket can't be merged into itself.");

        var source = await ticketRepository.GetByIdAsync(sourceId)
            ?? throw new KeyNotFoundException($"Ticket {sourceId} not found.");
        var target = await ticketRepository.GetByIdAsync(targetId)
            ?? throw new KeyNotFoundException($"Ticket {targetId} not found.");

        if (source.MergedIntoTicketId != null)
            throw new InvalidOperationException("This ticket has already been merged.");
        if (target.MergedIntoTicketId != null)
            throw new InvalidOperationException("Can't merge into a ticket that is itself a merged duplicate.");

        var sourceRef = source.Reference;
        var targetRef = target.Reference;

        // Link + close the source as a duplicate (via the gate, so stamping/history are consistent).
        source.MergedIntoTicketId = target.Id;
        source.ClosureType = ClosureType.Cancelled;
        source.ClosureReason = $"Merged into {targetRef}";
        await transitionService.TransitionAsync(
            source, TicketStatus.Closed, TransitionSource.System, mergedByUserId,
            $"Merged into ticket {targetRef}", "Merged");

        // Note the merge on the surviving ticket so its handler sees the history.
        await commentRepository.CreateAsync(new TicketComment
        {
            TicketId = target.Id,
            AuthorUserId = mergedByUserId,
            Content = $"Duplicate ticket {sourceRef} — \"{source.Title}\" — was merged into this one.",
            IsInternal = true,
            CreatedBy = mergedByUserId,
        });
        await historyService.AppendAsync(target.Id, mergedByUserId, "MergedIn", sourceRef, targetRef, $"Ticket {sourceRef} merged in");

        return mapper.Map<TicketReadDto>(source);
    }

    private Task<Ticket?> ResolveByReferenceAsync(string reference)
        => ticketRepository.GetByReferenceAsync(reference);  // D1-4 — handles TKT-000123 + legacy

    public async Task AddWatcherAsync(string ticketId, string userId, string addedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException($"Ticket {ticketId} not found.");

        var existing = await watcherRepository.GetAsync(ticketId, userId);
        if (existing == null)
        {
            await watcherRepository.CreateAsync(new TicketWatcher
            {
                TicketId  = ticketId,
                UserId    = userId,
                AddedAt   = DateTime.UtcNow,
                CreatedBy = addedByUserId,
            });
        }

        await historyService.AppendAsync(ticketId, addedByUserId, "WatcherAdded", null, userId);
    }

    public async Task RemoveWatcherAsync(string ticketId, string userId)
    {
        var watcher = await watcherRepository.GetAsync(ticketId, userId);
        if (watcher != null)
            await watcherRepository.DeleteAsync(watcher.Id);
    }

    public async Task<IEnumerable<string>> GetWatcherIdsAsync(string ticketId)
    {
        var watchers = await watcherRepository.GetByTicketIdAsync(ticketId);
        return watchers.Select(w => w.UserId);
    }

    public async Task<IEnumerable<TicketReadDto>> GetMyTicketsAsync(string userId)
    {
        var tickets = await ticketRepository.GetTicketsByUserAsync(userId);
        return mapper.Map<IEnumerable<TicketReadDto>>(tickets);
    }

    public async Task<IEnumerable<TicketReadDto>> GetCreatedByMeAsync(string userId)
    {
        var tickets = await ticketRepository.GetTicketsCreatedByUserAsync(userId);
        return mapper.Map<IEnumerable<TicketReadDto>>(tickets);
    }

    public async Task<TicketReadDto> AssignToDepartmentAsync(string id, AssignToDepartmentDto dto, string assignedByUserId)
    {
        var ticket = await ticketRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        var oldDepartment = ticket.DepartmentId;
        ticket.DepartmentId = dto.DepartmentId;
        ticket.UpdatedBy = assignedByUserId;

        var updated = await ticketRepository.UpdateAsync(ticket);

        await historyService.AppendAsync(id, assignedByUserId, "DepartmentAssigned", oldDepartment, dto.DepartmentId, dto.Notes);

        _ = deptNotificationService.SendDepartmentAssignmentNotificationsAsync(
            id, ticket.Title, dto.DepartmentId, assignedByUserId, dto.Notes);

        return mapper.Map<TicketReadDto>(updated);
    }

    public async Task<TicketReadDto?> GetByReferenceAsync(string reference)
    {
        var ticket = await ticketRepository.GetByReferenceAsync(reference);  // D1-4
        return ticket == null ? null : mapper.Map<TicketReadDto>(ticket);
    }

    public async Task<object> GetDashboardSummaryAsync(string? departmentId = null)
    {
        var all = (await ticketRepository.GetAllAsync()).ToList();
        var tickets = departmentId != null
            ? all.Where(t => t.DepartmentId == departmentId).ToList()
            : all;
        return new
        {
            Total = tickets.Count,
            ByStatus = Enum.GetValues<TicketStatus>()
                .ToDictionary(s => s.ToString(), s => tickets.Count(t => t.Status == s)),
            ByPriority = Enum.GetValues<TicketPriority>()
                .ToDictionary(p => p.ToString(), p => tickets.Count(t => t.Priority == p)),
            Open = tickets.Count(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved),
        };
    }

    public async Task<object> GetMySummaryAsync(string userId)
    {
        var assigned = (await ticketRepository.GetTicketsByUserAsync(userId)).ToList();
        var created  = (await ticketRepository.GetTicketsCreatedByUserAsync(userId)).ToList();
        return new
        {
            AssignedToMe     = assigned.Count,
            CreatedByMe      = created.Count,
            MyOpenTickets    = assigned.Count(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved),
            MyOverdueTickets = assigned.Count(t =>
                t.ResolutionDueAt.HasValue &&
                t.ResolutionDueAt.Value < DateTime.UtcNow &&
                t.Status != TicketStatus.Resolved &&
                t.Status != TicketStatus.Closed),
            ByStatus = Enum.GetValues<TicketStatus>()
                .ToDictionary(s => s.ToString(), s => assigned.Count(t => t.Status == s)),
        };
    }

    public async Task<(bool Found, string NewStatus)> ApplyWorkUpdateAsync(
        string ticketId, WorkUpdateType updateType, string source, string? notes)
    {
        // Load with attachments so the evidence gate (below) can see them.
        var ticket = await ticketRepository.GetByIdWithDetailsAsync(ticketId);
        if (ticket == null) return (false, string.Empty);

        // #2: field work is the most evidence-critical, yet the callback used to resolve directly and
        // skip the evidence check that blocks manual resolve. If evidence is required but none is
        // attached, hold the ticket in Pending (awaiting evidence) instead of resolving it.
        var evidenceHeld = updateType == WorkUpdateType.WorkCompleted
            && ticket.RequiresEvidence && !ticket.Attachments.Any();

        // Map the external work event to a target status (WorkStarted only advances from Assigned/New).
        var target = updateType switch
        {
            WorkUpdateType.WorkStarted   => ticket.Status is TicketStatus.Assigned or TicketStatus.New
                                            ? TicketStatus.InProgress : ticket.Status,
            WorkUpdateType.WorkSubmitted => TicketStatus.Pending,
            WorkUpdateType.WorkCompleted => evidenceHeld ? TicketStatus.Pending : TicketStatus.Resolved,
            WorkUpdateType.WorkCancelled => TicketStatus.Closed,
            _                            => ticket.Status
        };

        var noteText = evidenceHeld
            ? $"{source} reported work complete, but this ticket requires evidence and none is attached — held in Pending until evidence is added."
            : notes ?? $"{source} reported: {updateType}";

        // #5: cancelled field work is a distinct closure (not "done") — capture why, and mark the
        // closure type so it doesn't count as a resolution in reports (no ResolvedAt is set).
        if (updateType == WorkUpdateType.WorkCancelled)
        {
            ticket.ClosureType = ClosureType.Cancelled;
            ticket.ClosureReason = string.IsNullOrWhiteSpace(notes) ? "Work cancelled in the field." : notes;
        }

        // Forced (source = WorkUpdate); gate stamps ResolvedAt/ClosedAt and logs the work event.
        var previousStatus = (await transitionService.TransitionAsync(
            ticket, target, TransitionSource.WorkUpdate, source, noteText, updateType.ToString())).ToString();

        // These side-effects are awaited (not fire-and-forget): the alert is a DB write that must
        // commit before the request scope disposes its DbContext, and awaiting keeps every operation
        // sequential on the shared scoped DbContext (concurrent use throws).
        await notificationService.NotifyStatusChangedAsync(ticket, previousStatus, ticket.Status.ToString(), source);

        if (evidenceHeld)
        {
            var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value
                ?? httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault()
                ?? "public";
            await alertService.CreateAsync(
                schema, source: "Evidence", severity: "Warning",
                title: $"Evidence required — {ticket.Title}",
                message: $"Field work on \"{ticket.Title}\" was reported complete but no evidence is attached. " +
                         "The ticket is held in Pending until evidence is added, then it can be resolved.",
                ticketId: ticket.Id, ticketTitle: ticket.Title,
                assignedToUserId: ticket.AssignedToUserId, requiredPermission: "tickets.resolve");
        }
        else if (updateType == WorkUpdateType.WorkCompleted)
        {
            await notificationService.NotifyResolvedAsync(ticket, source);
        }

        return (true, ticket.Status.ToString());
    }
}
