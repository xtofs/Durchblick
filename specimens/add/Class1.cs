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
}
