using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class SalesStat
{
    [Key]
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    [DataType(DataType.Currency)]
    public decimal Revenue { get; set; }

    public int OrderCount { get; set; }

    [DataType(DataType.Currency)]
    public decimal AverageOrderValue { get; set; }
}
