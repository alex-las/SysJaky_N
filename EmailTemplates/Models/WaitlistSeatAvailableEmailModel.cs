using System;

namespace SysJaky_N.EmailTemplates.Models;

public record class WaitlistSeatAvailableEmailModel(
    string CourseTitle,
    string ClaimUrl,
    DateTime ClaimExpiresAtUtc,
    int ValidHours);
