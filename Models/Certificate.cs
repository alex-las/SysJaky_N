using System.ComponentModel.DataAnnotations;

namespace SysJaky_N.Models;

public class Certificate
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string Number { get; set; } = string.Empty;

    [MaxLength(512)]
    public string PdfPath { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Hash { get; set; } = string.Empty;

    [MaxLength(512)]
    public string VerifyUrl { get; set; } = string.Empty;

    public int IssuedToEnrollmentId { get; set; }
    public Enrollment? IssuedToEnrollment { get; set; }
}
