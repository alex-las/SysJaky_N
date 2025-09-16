using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;
using SysJaky_N.Services;

namespace SysJaky_N.Controllers;

[ApiController]
[Route("api/waitlist")]
public class WaitlistController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WaitlistTokenService _tokenService;
    private readonly ILogger<WaitlistController> _logger;

    public WaitlistController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        WaitlistTokenService tokenService,
        ILogger<WaitlistController> logger)
    {
        _context = context;
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("{courseTermId:int}")]
    public async Task<IActionResult> AddAsync(int courseTermId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var courseTerm = await _context.CourseTerms.AsNoTracking().FirstOrDefaultAsync(t => t.Id == courseTermId && t.IsActive);
        if (courseTerm == null)
        {
            return NotFound();
        }

        var alreadyEnrolled = await _context.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseTermId == courseTermId);
        if (alreadyEnrolled)
        {
            return Conflict(new { message = "Uživatel je již zapsán na termínu." });
        }

        var existingEntry = await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.UserId == userId && w.CourseTermId == courseTermId);
        if (existingEntry != null)
        {
            return Conflict(new { message = "Uživatel je již v pořadníku." });
        }

        var entry = new WaitlistEntry
        {
            UserId = userId,
            CourseTermId = courseTermId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync();

        return Created($"/api/waitlist/{courseTermId}", new { entry.Id, entry.CourseTermId, entry.CreatedAtUtc });
    }

    [Authorize]
    [HttpDelete("{courseTermId:int}")]
    public async Task<IActionResult> RemoveAsync(int courseTermId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var entry = await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.UserId == userId && w.CourseTermId == courseTermId);
        if (entry == null)
        {
            return NotFound();
        }

        _context.WaitlistEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("claim")]
    public async Task<IActionResult> ClaimAsync([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { message = "Token je povinný." });
        }

        if (!_tokenService.TryValidateToken(token, out var entryId, out var reservationId))
        {
            return BadRequest(new { message = "Token je neplatný nebo expiroval." });
        }

        var entry = await _context.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == entryId);
        if (entry == null)
        {
            return NotFound();
        }

        if (entry.ReservationToken != reservationId || entry.ReservationExpiresAtUtc == null || entry.ReservationExpiresAtUtc <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Token již není platný." });
        }

        if (entry.ReservationConsumed)
        {
            return BadRequest(new { message = "Token již byl použit." });
        }

        entry.ReservationConsumed = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Waitlist entry {EntryId} confirmed via token.", entryId);

        return Ok(new { courseTermId = entry.CourseTermId });
    }
}
