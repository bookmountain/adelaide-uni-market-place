using Application.Auth;
using Xunit;

namespace Application.UnitTests.Auth;

public sealed class RoleResolverTests
{
    [Fact]
    public void Admin_user_resolves_admin_role()
        => Assert.Equal("Admin", RoleResolver.Resolve(baseRole: "Student", isAdmin: true));

    [Fact]
    public void Non_admin_keeps_base_role()
        => Assert.Equal("Student", RoleResolver.Resolve(baseRole: "Student", isAdmin: false));
}
