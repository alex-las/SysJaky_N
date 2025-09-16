using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SysJaky_N.Data;
using System.Threading.Tasks;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("verify")]
public class VerifyController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VerifyController> _logger;

    public VerifyController(ApplicationDbContext context, ILogger<VerifyController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("{number}/{hash}")]
    public async Task<IActionResult> VerifyAsync(string number, string hash)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(hash))
        {
            return BadRequest(new { valid = false, message = "Number and hash are required." });
        }

        var normalizedNumber = number.Trim();
        var normalizedHash = hash.Trim();

        var certificate = await _context.Certificates
            .AsNoTracking()
            .Include(c => c.IssuedToEnrollment)
                .ThenInclude(e => e.User)
            .Include(c => c.IssuedToEnrollment)
                .ThenInclude(e => e.CourseTerm)
                    .ThenInclude(t => t.Course)
            .Include(c => c.IssuedToEnrollment)
                .ThenInclude(e => e.Attendance)
            .FirstOrDefaultAsync(c => c.Number == normalizedNumber && c.Hash == normalizedHash);

        if (certificate == null)
        {
            _logger.LogInformation("Certificate verification failed for {Number}.", normalizedNumber);
            return NotFound(new { valid = false });
        }

        var enrollment = certificate.IssuedToEnrollment;
        var result = new
        {
            valid = true,
            certificate = new
            {
                certificate.Number,
                certificate.VerifyUrl,
                certificate.PdfPath,
                issuedTo = enrollment?.User?.Email ?? enrollment?.UserId ?? string.Empty,
                completedAtUtc = enrollment?.Attendance?.CheckedInAtUtc,
                course = enrollment?.CourseTerm?.Course?.Title ?? $"Course {enrollment?.CourseTerm?.CourseId}",
                courseTermStartUtc = enrollment?.CourseTerm?.StartUtc,
                courseTermEndUtc = enrollment?.CourseTerm?.EndUtc
            }
        };

        return Ok(result);
    }
}
