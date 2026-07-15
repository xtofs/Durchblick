namespace specimen;

public class Class1
{
    public int Calculate(int a, int b)
    {
        return a + b;
    }

    public int Calculate2(int a, int b)
    {
        var c = a + b;
        return c * 2;
    }

    //  .method public hidebysig instance int32
    //     Calculate2(
    //       int32 a,
    //       int32 b
    //     ) cil managed
    //   {
    //     .maxstack 2
    //     .locals init (
    //       [0] int32 c,
    //       [1] int32 V_1
    //     )

    //     // [11 5 - 11 6]
    //     IL_0000: nop

    //     // [12 9 - 12 23]
    //     IL_0001: ldarg.1      // a
    //     IL_0002: ldarg.2      // b
    //     IL_0003: add
    //     IL_0004: stloc.0      // c

    //     // [13 9 - 13 22]
    //     IL_0005: ldloc.0      // c
    //     IL_0006: ldc.i4.2
    //     IL_0007: mul
    //     IL_0008: stloc.1      // V_1
    //     IL_0009: br.s         IL_000b

    //     // [14 5 - 14 6]
    //     IL_000b: ldloc.1      // V_1
    //     IL_000c: ret

    //   } // end of method Class1::Calculate2

    public int Calculate3(int a, int b)
    {

        if (a > 3)
        {
            return (a + b) * 2;
        }
        return a + b;
    }
}
