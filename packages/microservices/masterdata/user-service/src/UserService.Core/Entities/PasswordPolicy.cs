namespace UserService.Core.Entities;

public class PasswordPolicy : BaseEntity
{
    public int MinimumLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialCharacter { get; set; } = true;
    public int MaxAgeDays { get; set; } = 90;
}
