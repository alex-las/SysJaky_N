using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SysJaky_N.Data;
using SysJaky_N.Extensions;
using SysJaky_N.Models;

namespace SysJaky_N.Services;

public class CartService
{
    private const string CartSessionKey = "Cart";
    private readonly ApplicationDbContext _context;

    public CartService(ApplicationDbContext context)
    {
        _context = context;
    }

    public IReadOnlyList<CartItem> GetItems(ISession session)
    {
        var items = session.GetObject<List<CartItem>>(CartSessionKey) ?? new List<CartItem>();
        return CloneItems(items);
    }

    public void SetItems(ISession session, IEnumerable<CartItem> items)
    {
        var list = CloneItems(items);
        if (list.Count == 0)
        {
            session.Remove(CartSessionKey);
        }
        else
        {
            session.SetObject(CartSessionKey, list);
        }
    }

    public void Clear(ISession session)
    {
        session.Remove(CartSessionKey);
    }

    public async Task<CartOperationResult> AddToCartAsync(
        ISession session,
        int courseId,
        int quantity = 1,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return CartOperationResult.Failed("Quantity must be greater than zero.", GetItems(session));
        }

        var cart = GetItems(session).ToList();
        var existing = cart.FirstOrDefault(item => item.CourseId == courseId);
        var requestedQuantity = (existing?.Quantity ?? 0) + quantity;

        var validationError = await ValidateCourseAsync(courseId, requestedQuantity, cancellationToken);
        if (validationError != null)
        {
            return CartOperationResult.Failed(validationError, cart);
        }

        if (existing == null)
        {
            cart.Add(new CartItem { CourseId = courseId, Quantity = requestedQuantity });
        }
        else
        {
            existing.Quantity = requestedQuantity;
        }

        SetItems(session, cart);
        return CartOperationResult.Successful(cart);
    }

    public CartOperationResult Remove(ISession session, int courseId)
    {
        var cart = GetItems(session).ToList();
        var existing = cart.FirstOrDefault(item => item.CourseId == courseId);
        if (existing == null)
        {
            return CartOperationResult.Successful(cart);
        }

        cart.Remove(existing);
        SetItems(session, cart);
        return CartOperationResult.Successful(cart);
    }

    public async Task<CartOperationResult> BuyNowAsync(
        ISession session,
        int courseId,
        int quantity = 1,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return CartOperationResult.Failed("Quantity must be greater than zero.", GetItems(session));
        }

        var validationError = await ValidateCourseAsync(courseId, quantity, cancellationToken);
        if (validationError != null)
        {
            return CartOperationResult.Failed(validationError, GetItems(session));
        }

        var cart = new List<CartItem> { new() { CourseId = courseId, Quantity = quantity } };
        SetItems(session, cart);
        return CartOperationResult.Successful(cart);
    }

    private async Task<string?> ValidateCourseAsync(int courseId, int requestedQuantity, CancellationToken cancellationToken)
    {
        if (requestedQuantity <= 0)
        {
            return "Quantity must be greater than zero.";
        }

        var courseExists = await _context.Courses
            .AsNoTracking()
            .AnyAsync(c => c.Id == courseId, cancellationToken);
        if (!courseExists)
        {
            return "Selected course is no longer available.";
        }

        var capacityRecords = await _context.CourseTerms
            .AsNoTracking()
            .Where(term => term.CourseId == courseId && term.IsActive)
            .Select(term => new { term.Capacity, term.SeatsTaken })
            .ToListAsync(cancellationToken);

        if (capacityRecords.Count == 0)
        {
            return null;
        }

        var availableSeats = capacityRecords.Sum(record => Math.Max(record.Capacity - record.SeatsTaken, 0));
        if (availableSeats < requestedQuantity)
        {
            return availableSeats > 0
                ? $"Only {availableSeats} seats remain for this course."
                : "This course is fully booked.";
        }

        return null;
    }

    private static List<CartItem> CloneItems(IEnumerable<CartItem> items)
    {
        return items
            .Select(item => new CartItem { CourseId = item.CourseId, Quantity = item.Quantity })
            .ToList();
    }

    public sealed record CartOperationResult(bool Success, string? ErrorMessage, IReadOnlyList<CartItem> Items)
    {
        public static CartOperationResult Successful(IEnumerable<CartItem> items) =>
            new(true, null, CloneItems(items));

        public static CartOperationResult Failed(string errorMessage, IEnumerable<CartItem> items) =>
            new(false, errorMessage, CloneItems(items));
    }
}
