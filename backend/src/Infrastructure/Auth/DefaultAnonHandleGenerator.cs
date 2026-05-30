using System.Security.Cryptography;
using Application.Common.Interfaces;

namespace Infrastructure.Auth;

public sealed class DefaultAnonHandleGenerator : IAnonHandleGenerator
{
    private static readonly string[] Adjectives =
    {
        "quiet", "brave", "happy", "clever", "gentle", "swift", "calm", "bright",
        "lucky", "mellow", "nimble", "witty", "sunny", "bold", "cosy", "keen"
    };

    private static readonly string[] Nouns =
    {
        "koala", "emu", "quokka", "wombat", "magpie", "possum", "dingo", "echidna",
        "galah", "numbat", "bilby", "kelpie", "lorikeet", "platypus", "wallaby", "kookaburra"
    };

    public string Generate()
    {
        var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var suffix = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
        return $"{adjective}-{noun}-{suffix}";
    }
}
