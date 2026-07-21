// Specimen inputs compiled into the decompile demo. The demo reflects over the `specimen`
// namespace of its own assembly, reconstructs each method body, and prints it via CodeFormatter.
namespace specimen;

public class Class1
{
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
        //     foreach (var x in Enumerable.Range(0, a))
        //     {
        //         sum += x;
        //     }
        return sum;
    }
}
