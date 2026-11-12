using DouglasDwyer.CasCore.Tests.Shared;

namespace DouglasDwyer.CasCore.Tests.Csharp;

public static class TestArrays
{
    [TestSuccessful]
    public static int TestSingleDimensionalCreate()
    {
        var array = new int[3];
        array[1] = 5;
        return array[1];
    }

    [TestSuccessful]
    public static int TestJaggedCreate()
    {
        var array = new int[2][];
        array[0] = new int[3];
        array[0][1] = 7;
        return array[0][1];
    }

    [TestSuccessful]
    public static int TestMultiDimensionalCreate()
    {
        var array = new int[2, 3];
        return array.Length;
    }

    [TestSuccessful]
    public static int TestMultiDimensionalGetSet()
    {
        var array = new int[2, 3];
        array[1, 2] = 11;
        return array[1, 2];
    }

    [TestSuccessful]
    public static int TestThreeDimensionalCreate()
    {
        var array = new int[2, 3, 4];
        array[1, 2, 3] = 13;
        return array[1, 2, 3];
    }

    [TestSuccessful]
    public static int TestArrayCreateInstanceMultiDimensional()
    {
        var array = (int[,])Array.CreateInstance(typeof(int), 2, 3);
        array[0, 0] = 17;
        return array[0, 0];
    }
}
