// Specimen inputs compiled directly into the test assembly. The tests reflect over these methods
// via [Specimen("specimen.Class1", "...")], so the suite decompiles its own IL with no external
// project dependency. Keep the bodies simple and the build Debug so the reconstructed IL shapes
// stay stable.
namespace specimen;

public class Class1
{
    private readonly int _field = 42;
    private static readonly int StaticField = 7;
    private int _mutableField;

    public static int Calculate1(int a, int b)
    {
        return a + b;
    }

    public int Calculate2(int a, int b)
    {
        var c = a + b;
        return c * 2;
    }

    public int Calculate3(int a, int b)
    {

        if (a > 3)
        {
            return (a + b) * 2;
        }
        return a + b;
    }

    public int Calculate4(int a, int b)
    {
        var accu = 0;
        for (var i = 0; i < b; i++)
        {
            accu += a;
        }
        return accu;
    }

    public int Calculate5(int a)
    {
        switch (a)
        {
            case 0: return 10;
            case 1: return 11;
            case 2: return 12;
            case 3: return 13;
            default: return -1;
        }
    }

    public int Calculate6(int a)
    {
        var sum = 0;
        var enumerator = Enumerable.Range(0, a).GetEnumerator();
        while (enumerator.MoveNext())
        {
            var x = enumerator.Current;
            sum += x;
        }
        return sum;
    }

    public int Calculate7(int a, int b)
    {
        if (a == b)
        {
            return 10;
        }

        return 20;
    }

    public string Calculate8()
    {
        return "hello";
    }

    public int Calculate9()
    {
        return _field;
    }

    public object Calculate10()
    {
        return new object();
    }

    public void Calculate11(System.Text.StringBuilder builder)
    {
        builder.Append("hello");
    }

    public int Calculate12()
    {
        return StaticField;
    }

    public void Calculate13(int value)
    {
        _mutableField = value;
    }

    public string? Calculate14(object value)
    {
        return value as string;
    }

    public void Calculate15()
    {
        throw new InvalidOperationException();
    }
}
