namespace SysJaky_N.Models;

using System.ComponentModel.DataAnnotations;

public class CourseBlock
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Název je povinný.")]
    [StringLength(100, ErrorMessage = "Název může mít nejvýše 100 znaků.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Popis může mít nejvýše 1000 znaků.")]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Cena musí být nezáporná.")]
    public decimal Price { get; set; }

    public ICollection<Course> Modules { get; set; } = new List<Course>();
}

