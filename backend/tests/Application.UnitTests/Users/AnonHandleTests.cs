using System.Text.RegularExpressions;
using Infrastructure.Auth;
using Xunit;

namespace Application.UnitTests.Users;

public sealed class AnonHandleTests
{
    [Fact]
    public void Generate_matches_adjective_noun_number_pattern()
    {
        var generator = new DefaultAnonHandleGenerator();

        var handle = generator.Generate();

        Assert.Matches(new Regex("^[a-z]+-[a-z]+-[0-9]{4}$"), handle);
    }

    [Fact]
    public void Generate_produces_varied_values()
    {
        var generator = new DefaultAnonHandleGenerator();

        var handles = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToHashSet();

        // Not a strict uniqueness guarantee, but 50 draws should not all collide.
        Assert.True(handles.Count > 1);
    }
}
