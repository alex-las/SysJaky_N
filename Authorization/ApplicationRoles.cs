using System.Collections.Generic;

namespace SysJaky_N.Authorization;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Instructor = "Instructor";
    public const string CompanyManager = "CompanyManager";
    public const string StudentCustomer = "Student\\Customer";

    public static IReadOnlyCollection<string> AdminDashboardRoles { get; } = new[]
    {
        Admin,
        Editor
    };

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        Admin,
        Editor,
        Instructor,
        CompanyManager,
        StudentCustomer
    };
}
