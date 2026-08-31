using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Tickets;

public class UpdateTicketDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TicketPriority? Priority { get; set; }
    public string? DepartmentId { get; set; }
    public DateTime? DueDate { get; set; }
    /// D3-2 — context references into Projects (Module 5) and field service reports. Pass empty
    /// string to clear a reference; null leaves it unchanged.
    public string? ProjectId { get; set; }
    public string? FsrId { get; set; }
    /// D7-1 — internal IT-helpdesk context. Empty string clears; null leaves unchanged.
    public string? EmployeeId { get; set; }
    public string? BranchId { get; set; }
    public string? SystemAffected { get; set; }
}
