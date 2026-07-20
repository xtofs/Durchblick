using System.Reflection.Emit;

public static class StackBehaviourExtensions
{
    public static int NumberOfPops(this StackBehaviour behaviour)
    {
        switch (behaviour)
        {
            case StackBehaviour.Pop0:
                return 0;

            case StackBehaviour.Pop1:
            case StackBehaviour.Popi:
            case StackBehaviour.Popref:
                return 1;

            case StackBehaviour.Pop1_pop1:
            case StackBehaviour.Popi_pop1:
            case StackBehaviour.Popi_popi:
            case StackBehaviour.Popi_popi8:
            case StackBehaviour.Popref_pop1:
            case StackBehaviour.Popref_popi:
                return 2;

            case StackBehaviour.Popi_popi_popi:
            case StackBehaviour.Popref_popi_popi:
            case StackBehaviour.Popref_popi_popi8:
            case StackBehaviour.Popref_popi_popr4:
            case StackBehaviour.Popref_popi_popr8:
                return 3;

            default:
                // number of pops can only be called with pop behaviours
                throw new NotSupportedException($"Unsupported stack behaviour: {behaviour}");
        }
    }
}