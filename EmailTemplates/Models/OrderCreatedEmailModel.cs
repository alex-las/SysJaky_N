using System;

namespace SysJaky_N.EmailTemplates.Models;

public record class OrderCreatedEmailModel(
    int OrderId,
    decimal Total,
    DateTime CreatedAtUtc,
    string? RecipientName);
