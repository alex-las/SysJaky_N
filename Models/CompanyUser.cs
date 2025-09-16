namespace SysJaky_N.Models;

public class CompanyUser
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public CompanyRole Role { get; set; } = CompanyRole.Viewer;
}

public enum CompanyRole
{
    Owner,
    Manager,
    Viewer
}
