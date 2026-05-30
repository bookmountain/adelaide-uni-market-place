namespace Application.Common.Interfaces;

public interface IAnonHandleGenerator
{
    /// <summary>Returns a candidate handle of the form "adjective-noun-NNNN". Uniqueness is enforced by the caller.</summary>
    string Generate();
}
