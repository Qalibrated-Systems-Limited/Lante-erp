namespace UserService.Core.DTOs.Auth;

public class SwitchBranchDto
{
    /// <summary>Branch to switch to. Null = switch to company admin view (no branch filter).</summary>
    public string? BranchId { get; set; }
}
