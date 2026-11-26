using System;

namespace LocalSpt.Shared;

internal static class Nw11Contact
{
    internal const string Id = "6aba11000000000000000011";
    internal const string Name = "Nw11";
    internal const int AccountId = 1900000011;

    internal static bool IsContact(string? id) => string.Equals(id, Id, StringComparison.Ordinal);
}
