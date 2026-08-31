namespace FleetService.Core.Entities;

public class Material : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual ICollection<MaterialVariant> Variants { get; set; } = new List<MaterialVariant>();
    public virtual ICollection<MaterialPhoto> Photos { get; set; } = new List<MaterialPhoto>();
    public virtual ICollection<MaterialCost> Costs { get; set; } = new List<MaterialCost>();
}

public class MaterialVariant : BaseEntity
{
    public string MaterialId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual Material? Material { get; set; }
    public virtual ICollection<MaterialVariantPhoto> Photos { get; set; } = new List<MaterialVariantPhoto>();
}

public class MaterialPhoto : BaseEntity
{
    public string MaterialId { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }

    public virtual Material? Material { get; set; }
}

public class MaterialVariantPhoto : BaseEntity
{
    public string MaterialVariantId { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }

    public virtual MaterialVariant? MaterialVariant { get; set; }
}

public class MaterialCost : BaseEntity
{
    public string MaterialId { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? Location { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool Synced { get; set; } = false;

    public virtual Material? Material { get; set; }
}
