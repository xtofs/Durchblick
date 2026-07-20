// Specimen inputs compiled directly into the test assembly. The tests reflect over these methods
// via [Specimen("specimen.Class1", "...")], so the suite decompiles its own IL with no external
// project dependency. Keep the bodies simple and the build Debug so the reconstructed IL shapes
// stay stable.
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
}
