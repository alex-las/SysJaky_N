using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Instructor")]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(ApplicationDbContext context, ILogger<AttendanceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public record CheckInRequest(string? Code);

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckInAsync([FromBody] CheckInRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Code is required." });
        }

        var code = request.Code.Trim();
        var (enrollment, error) = await FindEnrollmentAsync(code, cancellationToken);
        if (enrollment == null)
        {
            var message = error ?? "Enrollment was not found for the provided code.";
            _logger.LogWarning("Attendance check-in failed. Code: {Code}. Reason: {Reason}", code, message);
            return NotFound(new { message });
        }

        var attendance = await _context.Attendances
            .FirstOrDefaultAsync(a => a.EnrollmentId == enrollment.Id, cancellationToken);

        var created = false;
        if (attendance == null)
        {
            attendance = new Attendance
            {
                EnrollmentId = enrollment.Id,
                CheckedInAtUtc = DateTime.UtcNow
            };
            _context.Attendances.Add(attendance);
            created = true;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var response = new
        {
            status = created ? "checked-in" : "already-checked-in",
            checkedInAtUtc = attendance.CheckedInAtUtc,
            enrollment = new
            {
                enrollment.Id,
                participant = enrollment.User?.Email ?? enrollment.UserId,
                course = enrollment.CourseTerm?.Course?.Title ?? $"Course {enrollment.CourseTerm?.CourseId}",
                courseTermStartUtc = enrollment.CourseTerm?.StartUtc,
                courseTermEndUtc = enrollment.CourseTerm?.EndUtc
            }
        };

        return Ok(response);
    }

    private async Task<(Enrollment? enrollment, string? error)> FindEnrollmentAsync(string code, CancellationToken cancellationToken)
    {
        if (TryParseEnrollmentId(code, out var enrollmentId))
        {
            var enrollment = await LoadEnrollmentAsync(enrollmentId, cancellationToken);
            if (enrollment != null)
            {
                return (enrollment, null);
            }

            return (null, "Enrollment was not found.");
        }

        var normalized = code.Trim().ToUpperInvariant();
        var seatToken = await _context.SeatTokens
            .Include(t => t.OrderItem)
            .FirstOrDefaultAsync(t => t.Token == normalized, cancellationToken);

        if (seatToken == null)
        {
            return (null, "Enrollment was not found for the provided code.");
        }

        if (seatToken.RedeemedByUserId == null)
        {
            return (null, "Seat token has not been redeemed yet.");
        }

        var enrollments = await _context.Enrollments
            .Include(e => e.User)
            .Include(e => e.CourseTerm)
                .ThenInclude(t => t.Course)
            .Include(e => e.Attendance)
            .Where(e => e.UserId == seatToken.RedeemedByUserId)
            .ToListAsync(cancellationToken);

        if (seatToken.OrderItem != null)
        {
            enrollments = enrollments
                .Where(e => e.CourseTerm?.CourseId == seatToken.OrderItem.CourseId)
                .ToList();
        }

        var enrollment = enrollments
            .OrderBy(e => e.Attendance == null ? 0 : 1)
            .ThenBy(e => e.CourseTerm?.StartUtc)
            .FirstOrDefault();

        return enrollment == null
            ? (null, "Enrollment was not found for the provided code.")
            : (enrollment, null);
    }

    private async Task<Enrollment?> LoadEnrollmentAsync(int enrollmentId, CancellationToken cancellationToken)
    {
        return await _context.Enrollments
            .Include(e => e.User)
            .Include(e => e.CourseTerm)
                .ThenInclude(t => t.Course)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken);
    }

    private static bool TryParseEnrollmentId(string code, out int enrollmentId)
    {
        var trimmed = code.Trim();
        if (trimmed.Length > 0 && trimmed.Length <= 10 && trimmed.All(char.IsDigit))
        {
            return int.TryParse(trimmed, out enrollmentId);
        }

        var match = Regex.Match(trimmed, @"(?:^|\b)(?:enrollment|enr)(?:ment)?(?:id)?[\s:=#\-/]*(?<id>\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["id"].Value, out enrollmentId))
        {
            return true;
        }

        match = Regex.Match(trimmed, @"enrollments?/(?<id>\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["id"].Value, out enrollmentId))
        {
            return true;
        }

        match = Regex.Match(trimmed, @"enrollment(?:id)?=(?<id>\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["id"].Value, out enrollmentId))
        {
            return true;
        }

        enrollmentId = 0;
        return false;
    }
}
