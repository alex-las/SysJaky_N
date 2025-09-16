using System;
using SysJaky_N.Models;

namespace SysJaky_N.EmailTemplates.Models;

public record class CourseReminderEmailModel(
    string CourseTitle,
    DateTime CourseDate,
    CourseType CourseType,
    string? CustomMessage);
