using DouglasDwyer.CasCore.Tests.Shared;

namespace DouglasDwyer.CasCore.Tests.Csharp;

public static class TestSpans
{
    [TestSuccessful]
    public static int TestReadOnlySpanFromArray()
    {
        byte[] bytes = new byte[] { 1, 2, 3 };

        // The array -> span conversion, which is what deserializing a payload needs.
        ReadOnlySpan<byte> span = bytes;

        return span[2];
    }

    [TestSuccessful]
    public static int TestReadOnlySpanOfNonByteArray()
    {
        int[] values = new int[] { 4, 5, 6 };
        ReadOnlySpan<int> span = values;

        return span.Length + span.ToArray()[1];
    }
}
