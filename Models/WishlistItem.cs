namespace SysJaky_N.Models;

public class WishlistItem
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
}
