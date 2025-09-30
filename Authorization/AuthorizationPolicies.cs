namespace SysJaky_N.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string AdminDashboardAccess = nameof(AdminDashboardAccess);
    public const string AdminOrInstructor = nameof(AdminOrInstructor);
    public const string EditorOnly = nameof(EditorOnly);
    public const string CompanyManagerOnly = nameof(CompanyManagerOnly);
    public const string StudentCustomer = nameof(StudentCustomer);
}
