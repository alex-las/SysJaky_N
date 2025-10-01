using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Models;

namespace SysJaky_N.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class WishlistController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WishlistController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var courseIds = await _context.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => w.CourseId)
            .ToListAsync();

        return Ok(new { courseIds });
    }

    [HttpPost]
    public async Task<IActionResult> AddAsync([FromBody] WishlistRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        bool exists = await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.CourseId == request.CourseId);
        if (!exists)
        {
            _context.WishlistItems.Add(new WishlistItem
            {
                UserId = userId,
                CourseId = request.CourseId
            });
            await _context.SaveChangesAsync();
        }

        return Ok(new { isWishlisted = true });
    }

    [HttpDelete("{courseId:int}")]
    public async Task<IActionResult> RemoveAsync(int courseId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var entity = await _context.WishlistItems.FindAsync(userId, courseId);
        if (entity != null)
        {
            _context.WishlistItems.Remove(entity);
            await _context.SaveChangesAsync();
        }

        return Ok(new { isWishlisted = false });
    }

    public record WishlistRequest(int CourseId);
}
