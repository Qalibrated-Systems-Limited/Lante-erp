namespace ProcurementService.Core.DTOs.Suppliers;

/// <summary>DEC-B — options for importing the pre-ASR supplier masters.</summary>
public class SeedSuppliersDto
{
    /// <summary>Off by default, and deliberately so: imported suppliers land <c>Pending</c> and must pass the
    /// PROC-001 conflict check and approval before they can be used on an LPO. Turning this on grants ASR
    /// approval purely because the supplier was already being traded with, which is recorded as such in the
    /// audit trail. Existing suppliers already in trade are the reason it exists.</summary>
    public bool AutoApprove { get; set; }
    /// <summary>Inactive rows in Finance/Stores are skipped unless this is set.</summary>
    public bool IncludeInactive { get; set; }
}

/// <summary>What the seed did (or, in preview, would do) to one candidate.</summary>
public class SupplierSeedEntryDto
{
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    /// <summary>"finance", "stores", or "finance+stores" when the same supplier was found in both.</summary>
    public string Sources { get; set; } = string.Empty;
    /// <summary>Import | Link | Skip</summary>
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    /// <summary>The ASR supplier matched or created (absent in preview for new imports).</summary>
    public string? SupplierId { get; set; }
    public string? SupplierNumber { get; set; }
    /// <summary>How the candidate was matched to an existing ASR row: KraPin | Name | BackReference.</summary>
    public string? MatchedOn { get; set; }
}

public class SupplierSeedResultDto
{
    public bool Preview { get; set; }
    /// <summary>Per-source outcome, including sources that were disabled or unreachable.</summary>
    public List<string> SourceMessages { get; set; } = new();
    public int FinanceRead { get; set; }
    public int StoresRead { get; set; }
    public int Candidates { get; set; }
    public int Imported { get; set; }
    /// <summary>Already in the ASR; only the missing Finance/Stores back-reference was filled in.</summary>
    public int Linked { get; set; }
    public int Skipped { get; set; }
    public bool AutoApproved { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SupplierSeedEntryDto> Entries { get; set; } = new();
}
