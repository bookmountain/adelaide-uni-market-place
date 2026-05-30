namespace Application.Auth;

public static class RoleResolver
{
    public const string AdminRole = "Admin";

    public static string Resolve(string baseRole, bool isAdmin) => isAdmin ? AdminRole : baseRole;
}
