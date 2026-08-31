using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Org;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H1 — job positions (HR-DEC-3) and the org chart (P32).
/// <para>The chart is assembled from <see cref="Employee.ReportsToId"/>, which is the single source of truth;
/// <see cref="OrgChartNode"/> supplies sibling ordering and is kept in step with it. Employees with no manager
/// surface as roots and are also reported in <see cref="OrgChartDto.MissingReportingLine"/>, so a broken
/// hierarchy is visible instead of quietly disappearing from the tree.</para>
/// </summary>
public class OrgService(
    IGenericRepository<Position> positions,
    IGenericRepository<Employee> employees,
    IGenericRepository<OrgChartNode> nodes,
    IGenericRepository<HrAuditLog> audit,
    IUserDirectoryGateway directory) : IOrgService
{
    // ── Positions ──
    public async Task<List<PositionDto>> ListPositionsAsync(bool includeInactive)
    {
        var q = positions.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(p => p.IsActive);
        var list = await q.OrderBy(p => p.Title).ToListAsync();

        var counts = await employees.Query().AsNoTracking()
            .Where(e => e.PositionId != null
                     && e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
            .GroupBy(e => e.PositionId!)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToListAsync();

        return list.Select(p =>
        {
            var filled = counts.FirstOrDefault(c => c.PositionId == p.Id)?.Count ?? 0;
            return new PositionDto
            {
                Id = p.Id, Title = p.Title, Code = p.Code, JobGrade = p.JobGrade,
                DepartmentId = p.DepartmentId, DepartmentName = p.DepartmentName, Description = p.Description,
                ApprovedHeadcount = p.ApprovedHeadcount, IsActive = p.IsActive,
                FilledCount = filled,
                Vacancies = p.ApprovedHeadcount is null ? null : Math.Max(0, p.ApprovedHeadcount.Value - filled),
            };
        }).ToList();
    }

    public async Task<PositionDto?> GetPositionAsync(string id)
        => (await ListPositionsAsync(true)).FirstOrDefault(p => p.Id == id);

    public async Task<OrgActionResult> CreatePositionAsync(CreatePositionDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Err("The position title is required.");
        var title = dto.Title.Trim();
        if (await positions.Query().AnyAsync(p => p.Title.ToLower() == title.ToLower()))
            return Err($"A position titled '{title}' already exists.");

        var departmentName = await ResolveDepartmentNameAsync(dto.DepartmentId);
        var created = await positions.CreateAsync(new Position
        {
            Title = title, Code = dto.Code, JobGrade = dto.JobGrade, DepartmentId = dto.DepartmentId,
            DepartmentName = departmentName, Description = dto.Description,
            ApprovedHeadcount = dto.ApprovedHeadcount, IsActive = true,
            CreatedBy = userId, UpdatedBy = userId,
        });
        await LogAsync("Position", created.Id, HrAuditAction.PositionCreated, $"Position '{title}' created.", userId);
        return new OrgActionResult("Created", $"Position '{title}' created.");
    }

    public async Task<OrgActionResult> UpdatePositionAsync(string id, UpdatePositionDto dto, string userId)
    {
        var p = await positions.GetByIdAsync(id);
        if (p is null) return Err("Position not found.");

        if (!string.IsNullOrWhiteSpace(dto.Title))
        {
            var title = dto.Title.Trim();
            if (await positions.Query().AnyAsync(x => x.Id != id && x.Title.ToLower() == title.ToLower()))
                return Err($"Another position is already titled '{title}'.");
            p.Title = title;
        }
        if (dto.Code is not null) p.Code = dto.Code;
        if (dto.JobGrade is not null) p.JobGrade = dto.JobGrade;
        if (dto.Description is not null) p.Description = dto.Description;
        if (dto.ApprovedHeadcount is not null) p.ApprovedHeadcount = dto.ApprovedHeadcount;
        if (dto.DepartmentId is not null && dto.DepartmentId != p.DepartmentId)
        {
            p.DepartmentId = dto.DepartmentId;
            p.DepartmentName = await ResolveDepartmentNameAsync(dto.DepartmentId);
        }
        if (dto.IsActive is not null)
        {
            // Deactivating a position people still hold would hide them from position reporting.
            if (dto.IsActive == false)
            {
                var holders = await employees.Query().AsNoTracking().CountAsync(e => e.PositionId == id
                    && e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated);
                if (holders > 0) return Err($"{holders} serving employee(s) still hold this position — reassign them first.");
            }
            p.IsActive = dto.IsActive.Value;
        }

        Touch(p, userId);
        await positions.UpdateAsync(p);
        return new OrgActionResult("Ok", "Position updated.");
    }

    // ── Org chart ──
    public async Task<OrgChartDto> GetChartAsync(bool includeInactive)
    {
        var staff = await employees.Query().AsNoTracking()
            .Select(e => new
            {
                e.Id, e.EmployeeNumber, e.FirstName, e.OtherNames, e.LastName,
                e.JobTitle, e.DepartmentId, e.DepartmentName, e.ReportsToId, e.Status,
            }).ToListAsync();

        var live = includeInactive
            ? staff
            : staff.Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated).ToList();

        var nodeList = await nodes.Query().AsNoTracking().ToListAsync();
        var order = nodeList.ToDictionary(n => n.EmployeeId, n => n.DisplayOrder);

        var dtos = live.ToDictionary(e => e.Id, e => new OrgChartNodeDto
        {
            Id = nodeList.FirstOrDefault(n => n.EmployeeId == e.Id)?.Id ?? string.Empty,
            EmployeeId = e.Id,
            EmployeeNumber = e.EmployeeNumber,
            FullName = string.Join(' ', new[] { e.FirstName, e.OtherNames, e.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))),
            JobTitle = e.JobTitle,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.DepartmentName,
            ParentEmployeeId = e.ReportsToId,
            DisplayOrder = order.TryGetValue(e.Id, out var o) ? o : 0,
            Status = e.Status.ToString(),
            IsActive = e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated,
        });

        var roots = new List<OrgChartNodeDto>();
        foreach (var dto in dtos.Values)
        {
            // A manager who is not in the live set (e.g. a leaver) leaves their reports at the top rather
            // than orphaning them out of the chart entirely.
            if (dto.ParentEmployeeId is not null && dtos.TryGetValue(dto.ParentEmployeeId, out var parent))
                parent.Reports.Add(dto);
            else
                roots.Add(dto);
        }

        void Sort(List<OrgChartNodeDto> level)
        {
            level.Sort((a, b) => a.DisplayOrder != b.DisplayOrder
                ? a.DisplayOrder.CompareTo(b.DisplayOrder)
                : string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));
            foreach (var n in level) Sort(n.Reports);
        }
        Sort(roots);

        int Depth(OrgChartNodeDto n, int d = 1) => n.Reports.Count == 0 ? d : n.Reports.Max(r => Depth(r, d + 1));
        void Stamp(OrgChartNodeDto n, int level) { n.Level = level; foreach (var r in n.Reports) Stamp(r, level + 1); }
        foreach (var r in roots) Stamp(r, 0);

        return new OrgChartDto
        {
            Roots = roots,
            NodeCount = dtos.Count,
            MaxDepth = roots.Count == 0 ? 0 : roots.Max(r => Depth(r)),
            UnplacedEmployees = live.Count(e => nodeList.All(n => n.EmployeeId != e.Id)),
            // A single root is the legitimate apex (the MD), so reporting it as "missing" would be noise.
            // More than one root means the extras genuinely have no line — that is the signal worth raising.
            MissingReportingLine = roots.Count <= 1
                ? []
                : roots.Select(r => r.FullName).ToList(),
        };
    }

    public async Task<OrgActionResult> SetReportingLineAsync(string employeeId, SetReportingLineDto dto, string userId)
    {
        var e = await employees.GetByIdAsync(employeeId);
        if (e is null) return Err("Employee not found.");
        if (dto.ReportsToId == employeeId) return Err("An employee cannot report to themselves.");

        if (!string.IsNullOrWhiteSpace(dto.ReportsToId))
        {
            if (!await employees.Query().AnyAsync(x => x.Id == dto.ReportsToId))
                return Err("The specified line manager is not an employee.");
            if (await WouldCycleAsync(employeeId, dto.ReportsToId))
                return Err("That reporting line would create a cycle in the org chart.");
        }

        var previous = e.ReportsToId;
        e.ReportsToId = string.IsNullOrWhiteSpace(dto.ReportsToId) ? null : dto.ReportsToId;
        Touch(e, userId);
        await employees.UpdateAsync(e);

        var node = await nodes.Query().FirstOrDefaultAsync(n => n.EmployeeId == employeeId);
        if (node is not null)
        {
            node.ParentEmployeeId = e.ReportsToId;
            if (dto.DisplayOrder is not null) node.DisplayOrder = dto.DisplayOrder.Value;
            node.Level = await DepthOfAsync(e.ReportsToId);
            Touch(node, userId);
            await nodes.UpdateAsync(node);
        }

        await LogAsync("OrgChart", employeeId, HrAuditAction.ReportingLineChanged,
            $"{e.FullName}: reporting line changed from {previous ?? "(apex)"} to {e.ReportsToId ?? "(apex)"}.", userId);
        return new OrgActionResult("Ok", "Reporting line updated.");
    }

    public async Task<OrgActionResult> RebuildAsync(string userId)
    {
        var staff = await employees.Query().AsNoTracking().ToListAsync();
        var existing = await nodes.Query().ToListAsync();
        int created = 0, updated = 0;

        // Deterministic sibling ordering on a rebuild: by employee number within each manager.
        var grouped = staff.GroupBy(e => e.ReportsToId ?? string.Empty);
        foreach (var group in grouped)
        {
            var ordered = group.OrderBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var e = ordered[i];
                var active = e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated;
                var level = await DepthOfAsync(e.ReportsToId);
                var node = existing.FirstOrDefault(n => n.EmployeeId == e.Id);
                if (node is null)
                {
                    await nodes.CreateAsync(new OrgChartNode
                    {
                        EmployeeId = e.Id, ParentEmployeeId = e.ReportsToId, Level = level,
                        DepartmentId = e.DepartmentId, DepartmentName = e.DepartmentName,
                        DisplayOrder = i, IsActive = active, CreatedBy = userId, UpdatedBy = userId,
                    });
                    created++;
                }
                else
                {
                    node.ParentEmployeeId = e.ReportsToId;
                    node.Level = level;
                    node.DepartmentId = e.DepartmentId;
                    node.DepartmentName = e.DepartmentName;
                    node.DisplayOrder = i;
                    node.IsActive = active;
                    Touch(node, userId);
                    await nodes.UpdateAsync(node);
                    updated++;
                }
            }
        }

        await LogAsync("OrgChart", "all", HrAuditAction.OrgChartRebuilt,
            $"Org chart rebuilt from employee reporting lines: {created} node(s) created, {updated} refreshed.", userId);
        return new OrgActionResult("Ok", $"Org chart rebuilt — {created} created, {updated} refreshed.");
    }

    // ── Directory (user-service owns these) ──
    public Task<List<OrgUnitDto>> ListDepartmentsAsync() => directory.ListDepartmentsAsync();
    public Task<List<OrgUnitDto>> ListBranchesAsync() => directory.ListBranchesAsync();

    // ── Helpers ──
    private async Task<string?> ResolveDepartmentNameAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var units = await directory.ListDepartmentsAsync();
        return units.FirstOrDefault(u => u.Id == id)?.Name;
    }

    private async Task<int> DepthOfAsync(string? managerId)
    {
        var depth = 0;
        var seen = new HashSet<string>();
        var current = managerId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current) && depth < 20)
        {
            current = await employees.Query().AsNoTracking()
                .Where(x => x.Id == current).Select(x => x.ReportsToId).FirstOrDefaultAsync();
            depth++;
        }
        return depth;
    }

    private async Task<bool> WouldCycleAsync(string employeeId, string? managerId)
    {
        var seen = new HashSet<string>();
        var current = managerId;
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            if (current == employeeId) return true;
            current = await employees.Query().AsNoTracking()
                .Where(x => x.Id == current).Select(x => x.ReportsToId).FirstOrDefaultAsync();
        }
        return false;
    }

    private static OrgActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, OccurredAt = DateTime.UtcNow, CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
