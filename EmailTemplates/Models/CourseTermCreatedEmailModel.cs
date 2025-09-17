using System;

namespace SysJaky_N.EmailTemplates.Models;

public record class CourseTermCreatedEmailModel(
    string CourseTitle,
    DateTime StartUtc,
    DateTime EndUtc,
    string DetailUrl);
