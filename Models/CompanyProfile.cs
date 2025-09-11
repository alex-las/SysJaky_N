namespace SysJaky_N.Models;

public class CompanyProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReferenceCode { get; set; } = string.Empty;
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }
    public List<ApplicationUser> Users { get; set; } = new();
}
