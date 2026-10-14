using System;

namespace HomuraHimeAP.GameTests;

/// <summary>Surity has no assertions of its own: a test fails by throwing.</summary>
internal static class Assert
{
    internal static void IsTrue(bool condition, string message = "expected true")
    {
        if (!condition) throw new Exception(message);
    }

    internal static void IsFalse(bool condition, string message = "expected false") => IsTrue(!condition, message);

    internal static void Equal<T>(T expected, T actual, string what = "value")
    {
        if (!Equals(expected, actual)) throw new Exception($"{what}: expected {expected}, got {actual}");
    }

    internal static void NotNull(object value, string what = "value")
    {
        if (value == null) throw new Exception(what + " is null");
    }
}
