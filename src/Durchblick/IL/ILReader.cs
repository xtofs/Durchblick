namespace Durchblick.IL;

using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

/// <summary>
/// Pull-based reader over the IL byte stream of a method body, in the style of <see cref="System.Xml.XmlReader"/>:
/// <see cref="Read"/> advances to the next instruction, typed properties expose the operand of the
/// current instruction. Which property is valid is determined by <see cref="OperandType"/>.
/// </summary>
public sealed class ILReader
{
    private static readonly OpCode[] SingleByteOpCodes = BuildSingleByteTable();
    private static readonly Dictionary<ushort, OpCode> MultiByteOpCodes = BuildMultiByteTable();

    private readonly byte[] _il;
    private readonly Module _module;
    private readonly Type[]? _typeGenericArguments;
    private readonly Type[]? _methodGenericArguments;

    private int _position;          // start of the next instruction
    private int _operandPosition;   // start of the current instruction's operand
    private bool _hasInstruction;

    public ILReader(MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);

        _il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        _module = method.Module;
        _typeGenericArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;
        _methodGenericArguments = method is MethodInfo { IsGenericMethod: true }
            ? method.GetGenericArguments()
            : null;
    }

    /// <summary>IL offset of the current instruction.</summary>
    public int Offset { get; private set; }

    public OpCode OpCode { get; private set; }

    public OperandType OperandType => OpCode.OperandType;

    /// <summary>Advances to the next instruction. Returns false when the end of the body is reached.</summary>
    public bool Read()
    {
        if (_position >= _il.Length)
        {
            _hasInstruction = false;
            return false;
        }

        Offset = _position;
        var b = _il[_position++];
        OpCode = b != 0xFE
            ? SingleByteOpCodes[b]
            : MultiByteOpCodes[(ushort)(b << 8 | _il[_position++])];

        _operandPosition = _position;
        _position += OperandSize();
        _hasInstruction = true;
        return true;
    }

    /// <summary>Positions the reader so that the next <see cref="Read"/> decodes the instruction at <paramref name="ilOffset"/>.</summary>
    public void Seek(int ilOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ilOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ilOffset, _il.Length);

        _position = ilOffset;
        _hasInstruction = false;
    }

    /// <summary>Valid for <see cref="OperandType.InlineI"/> and <see cref="OperandType.ShortInlineI"/>.</summary>
    /// <remarks>ShortInlineI is sign-extended; the <c>unaligned.</c> prefix technically carries an unsigned byte, ignored here.</remarks>
    public int Int32Operand => RequiredOperandType(OperandType.InlineI, OperandType.ShortInlineI) == OperandType.InlineI
        ? ReadInt32(_operandPosition)
        : (sbyte)_il[_operandPosition];

    /// <summary>Valid for <see cref="OperandType.InlineI8"/>.</summary>
    public long Int64Operand
    {
        get
        {
            RequiredOperandType(OperandType.InlineI8);
            return BitConverter.ToInt64(_il, _operandPosition);
        }
    }

    /// <summary>Valid for <see cref="OperandType.ShortInlineR"/>.</summary>
    public float Float32Operand
    {
        get
        {
            RequiredOperandType(OperandType.ShortInlineR);
            return BitConverter.ToSingle(_il, _operandPosition);
        }
    }

    /// <summary>Valid for <see cref="OperandType.InlineR"/>.</summary>
    public double Float64Operand
    {
        get
        {
            RequiredOperandType(OperandType.InlineR);
            return BitConverter.ToDouble(_il, _operandPosition);
        }
    }

    /// <summary>Local or argument index (which one depends on the opcode). Valid for <see cref="OperandType.InlineVar"/> and <see cref="OperandType.ShortInlineVar"/>.</summary>
    public int VariableIndex => RequiredOperandType(OperandType.InlineVar, OperandType.ShortInlineVar) == OperandType.InlineVar
        ? BitConverter.ToUInt16(_il, _operandPosition)
        : _il[_operandPosition];

    /// <summary>Branch target as an absolute IL offset. Valid for <see cref="OperandType.InlineBrTarget"/> and <see cref="OperandType.ShortInlineBrTarget"/>.</summary>
    public int BranchTarget => RequiredOperandType(OperandType.InlineBrTarget, OperandType.ShortInlineBrTarget) == OperandType.InlineBrTarget
        ? _operandPosition + 4 + ReadInt32(_operandPosition)
        : _operandPosition + 1 + (sbyte)_il[_operandPosition];

    /// <summary>Switch targets as absolute IL offsets. Valid for <see cref="OperandType.InlineSwitch"/>.</summary>
    public int[] SwitchTargets
    {
        get
        {
            RequiredOperandType(OperandType.InlineSwitch);
            var count = ReadInt32(_operandPosition);
            var endOfInstruction = _operandPosition + 4 + (count * 4);
            return Enumerable.Range(0, count)
                .Select(i => endOfInstruction + ReadInt32(_operandPosition + 4 + (i * 4)))
                .ToArray();
        }
    }

    /// <summary>Valid for <see cref="OperandType.InlineString"/>.</summary>
    public string StringOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineString);
            return _module.ResolveString(ReadInt32(_operandPosition));
        }
    }

    /// <summary>Valid for <see cref="OperandType.InlineMethod"/>.</summary>
    public MethodBase MethodOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineMethod);
            return _module.ResolveMethod(ReadInt32(_operandPosition), _typeGenericArguments, _methodGenericArguments)
                ?? throw new InvalidOperationException($"Could not resolve method token at IL_{Offset:X4}.");
        }
    }

    /// <summary>Valid for <see cref="OperandType.InlineField"/>.</summary>
    public FieldInfo FieldOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineField);
            return _module.ResolveField(ReadInt32(_operandPosition), _typeGenericArguments, _methodGenericArguments)
                ?? throw new InvalidOperationException($"Could not resolve field token at IL_{Offset:X4}.");
        }
    }

    /// <summary>Valid for <see cref="OperandType.InlineType"/>.</summary>
    public Type TypeOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineType);
            return _module.ResolveType(ReadInt32(_operandPosition), _typeGenericArguments, _methodGenericArguments);
        }
    }

    /// <summary>Method, field or type. Valid for <see cref="OperandType.InlineTok"/>.</summary>
    public MemberInfo MemberOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineTok);
            return _module.ResolveMember(ReadInt32(_operandPosition), _typeGenericArguments, _methodGenericArguments)
                ?? throw new InvalidOperationException($"Could not resolve member token at IL_{Offset:X4}.");
        }
    }

    /// <summary>Signature blob. Valid for <see cref="OperandType.InlineSig"/>.</summary>
    public byte[] SignatureOperand
    {
        get
        {
            RequiredOperandType(OperandType.InlineSig);
            return _module.ResolveSignature(ReadInt32(_operandPosition));
        }
    }

    /// <summary>The operand of the current instruction as a reified <see cref="Operand"/> value.</summary>
    public Operand Operand => OperandType switch
    {
        OperandType.InlineNone => Operand.None,
        OperandType.InlineI or OperandType.ShortInlineI => Operand.ForInt32(Int32Operand, OperandType),
        OperandType.InlineI8 => new Operand(Int64Operand),
        OperandType.ShortInlineR => new Operand(Float32Operand),
        OperandType.InlineR => new Operand(Float64Operand),
        OperandType.InlineVar or OperandType.ShortInlineVar => Operand.ForVariableIndex(VariableIndex, OperandType),
        OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget => Operand.ForBranchTarget(BranchTarget, OperandType),
        OperandType.InlineSwitch => new Operand(SwitchTargets),
        OperandType.InlineString => new Operand(StringOperand),
        OperandType.InlineMethod => new Operand(MethodOperand),
        OperandType.InlineField => new Operand(FieldOperand),
        OperandType.InlineType => new Operand(TypeOperand),
        OperandType.InlineTok => Operand.ForToken(MemberOperand),
        OperandType.InlineSig => new Operand(SignatureOperand),
        _ => throw new NotSupportedException(OperandType.ToString()),
    };

    /// <summary>The current instruction as a reified <see cref="Instruction"/>.</summary>
    public Instruction Current => new(Offset, OpCode, Operand);

    /// <summary>Reads the remaining instructions as reified <see cref="Instruction"/> values.</summary>
    public IEnumerable<Instruction> ToInstructions()
    {
        while (Read())
        {
            yield return Current;
        }
    }

    /// <summary>Human-readable form of the current operand, or null for <see cref="OperandType.InlineNone"/>. For dumps and debugging.</summary>
    public string? OperandDisplay
    {
        get
        {
            var display = Operand.ToString();
            return display.Length == 0 ? null : display;
        }
    }

    private OperandType RequiredOperandType(OperandType expected, OperandType? alternative = null)
    {
        if (!_hasInstruction)
        {
            throw new InvalidOperationException("No current instruction. Call Read() first.");
        }

        var actual = OperandType;
        if (actual != expected && actual != alternative)
        {
            throw new InvalidOperationException($"Operand of {OpCode.Name} is {actual}, not {expected}{(alternative is null ? "" : $" or {alternative}")}.");
        }

        return actual;
    }

    private int ReadInt32(int position) => BitConverter.ToInt32(_il, position);

    private int OperandSize() => OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI or OperandType.InlineBrTarget or OperandType.ShortInlineR
            or OperandType.InlineString or OperandType.InlineSig or OperandType.InlineField
            or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok => 4,
        OperandType.InlineR or OperandType.InlineI8 => 8,
        OperandType.InlineSwitch => 4 + (ReadInt32(_operandPosition) * 4),
        _ => throw new NotSupportedException(OperandType.ToString()),
    };

    private static OpCode[] BuildSingleByteTable()
    {
        var table = new OpCode[256];
        foreach (var op in AllOpCodes().Where(op => op.Size == 1))
        {
            table[op.Value] = op;
        }

        return table;
    }

    private static Dictionary<ushort, OpCode> BuildMultiByteTable() =>
        AllOpCodes().Where(op => op.Size == 2).ToDictionary(op => (ushort)op.Value);

    private static IEnumerable<OpCode> AllOpCodes() =>
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(fi => (OpCode)fi.GetValue(null)!);
}
