namespace Durchblick;

using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

/// <summary>A reified IL instruction: opcode plus decoded operand at a given offset.</summary>
public readonly record struct Instruction(int Offset, OpCode OpCode, Operand Operand)
{
    public override string ToString()
    {
        var operand = Operand.ToString();
        return operand.Length == 0
            ? $"IL_{Offset:X4}: {OpCode}"
            : $"IL_{Offset:X4}: {OpCode} {operand}";
    }
}

/// <summary>
/// Tagged union for an IL instruction operand: holds either an object reference or 64 bits of
/// value data, discriminated by <see cref="OperandType"/>. Typed access is guarded; reading a
/// kind that does not match the tag throws <see cref="InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// <c>default(Operand)</c> is NOT a valid "no operand" value because
/// <see cref="OperandType.InlineNone"/> is not the zero enum value. Use <see cref="None"/>.
/// </remarks>
public readonly struct Operand
{
    private readonly object? _object;
    private readonly ulong _bits;

    private Operand(OperandType operandType, object? obj = null, ulong bits = 0)
    {
        OperandType = operandType;
        _object = obj;
        _bits = bits;
    }

    public OperandType OperandType { get; }

    /// <summary>The operand of an instruction with <see cref="OperandType.InlineNone"/>.</summary>
    public static Operand None => new(OperandType.InlineNone);

    // --- Creation: constructors where the payload type determines the tag, factories otherwise ---

    public Operand(Type type) : this(OperandType.InlineType, type) => ArgumentNullException.ThrowIfNull(type);

    public Operand(MethodBase method) : this(OperandType.InlineMethod, method) => ArgumentNullException.ThrowIfNull(method);

    public Operand(FieldInfo field) : this(OperandType.InlineField, field) => ArgumentNullException.ThrowIfNull(field);

    public Operand(string str) : this(OperandType.InlineString, str) => ArgumentNullException.ThrowIfNull(str);

    /// <summary>Signature blob (<see cref="OperandType.InlineSig"/>).</summary>
    public Operand(byte[] signature) : this(OperandType.InlineSig, signature) => ArgumentNullException.ThrowIfNull(signature);

    /// <summary>Switch targets as absolute IL offsets (<see cref="OperandType.InlineSwitch"/>).</summary>
    public Operand(int[] switchTargets) : this(OperandType.InlineSwitch, switchTargets) => ArgumentNullException.ThrowIfNull(switchTargets);

    public Operand(long value) : this(OperandType.InlineI8, bits: unchecked((ulong)value)) { }

    public Operand(float value) : this(OperandType.ShortInlineR, bits: BitConverter.SingleToUInt32Bits(value)) { }

    public Operand(double value) : this(OperandType.InlineR, bits: BitConverter.DoubleToUInt64Bits(value)) { }

    /// <summary>Method, field or type token operand (<see cref="OperandType.InlineTok"/>, e.g. <c>ldtoken</c>).</summary>
    public static Operand ForToken(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new(OperandType.InlineTok, member);
    }

    public static Operand ForInt32(int value, OperandType operandType = OperandType.InlineI) =>
        new(RequireOneOf(operandType, OperandType.InlineI, OperandType.ShortInlineI), bits: unchecked((ulong)(long)value));

    /// <summary>Branch target as an absolute IL offset.</summary>
    public static Operand ForBranchTarget(int target, OperandType operandType) =>
        new(RequireOneOf(operandType, OperandType.InlineBrTarget, OperandType.ShortInlineBrTarget), bits: unchecked((ulong)(long)target));

    /// <summary>Local or argument index (which one depends on the opcode).</summary>
    public static Operand ForVariableIndex(int index, OperandType operandType) =>
        new(RequireOneOf(operandType, OperandType.InlineVar, OperandType.ShortInlineVar), bits: unchecked((ulong)(long)index));

    // --- Guarded typed access ---


    public int GetInt32()
    {
        Require(OperandType.InlineI, OperandType.ShortInlineI);
        return unchecked((int)_bits);
    }

    public long GetInt64()
    {
        Require(OperandType.InlineI8);
        return unchecked((long)_bits);
    }

    public float GetFloat32()
    {
        Require(OperandType.ShortInlineR);
        return BitConverter.UInt32BitsToSingle((uint)_bits);
    }

    public double GetFloat64()
    {
        Require(OperandType.InlineR);
        return BitConverter.UInt64BitsToDouble(_bits);
    }

    /// <summary>Local or argument index (which one depends on the opcode).</summary>
    public int GetVariableIndex()
    {
        Require(OperandType.InlineVar, OperandType.ShortInlineVar);
        return unchecked((int)_bits);
    }

    /// <summary>Branch target as an absolute IL offset.</summary>
    public int GetBranchTarget()
    {
        Require(OperandType.InlineBrTarget, OperandType.ShortInlineBrTarget);
        return unchecked((int)_bits);
    }

    /// <summary>Switch targets as absolute IL offsets.</summary>
    public int[] GetSwitchTargets()
    {
        Require(OperandType.InlineSwitch);
        return (int[])_object!;
    }

    public string GetString()
    {
        Require(OperandType.InlineString);
        return (string)_object!;
    }

    public MethodBase GetMethod()
    {
        Require(OperandType.InlineMethod);
        return (MethodBase)_object!;
    }

    public FieldInfo GetField()
    {
        Require(OperandType.InlineField);
        return (FieldInfo)_object!;
    }

    /// <summary>Named GetTypeOperand to avoid hiding <see cref="object.GetType"/>.</summary>
    public Type GetTypeOperand()
    {
        Require(OperandType.InlineType);
        return (Type)_object!;
    }

    /// <summary>Method, field or type of an <see cref="OperandType.InlineTok"/> operand.</summary>
    public MemberInfo GetMember()
    {
        Require(OperandType.InlineTok);
        return (MemberInfo)_object!;
    }

    /// <summary>Signature blob of an <see cref="OperandType.InlineSig"/> operand.</summary>
    public byte[] GetSignature()
    {
        Require(OperandType.InlineSig);
        return (byte[])_object!;
    }


    /// <summary>Human-readable form of the operand; empty for <see cref="OperandType.InlineNone"/>.</summary>
    public override string ToString() => OperandType switch
    {
        OperandType.InlineNone => "",
        OperandType.InlineI or OperandType.ShortInlineI => GetInt32().ToString(CultureInfo.InvariantCulture),
        OperandType.InlineI8 => GetInt64().ToString(CultureInfo.InvariantCulture),
        OperandType.ShortInlineR => GetFloat32().ToString(CultureInfo.InvariantCulture),
        OperandType.InlineR => GetFloat64().ToString(CultureInfo.InvariantCulture),
        OperandType.InlineVar or OperandType.ShortInlineVar => GetVariableIndex().ToString(CultureInfo.InvariantCulture),
        OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget => $"IL_{GetBranchTarget():X4}",
        OperandType.InlineSwitch => string.Join(", ", GetSwitchTargets().Select(t => $"IL_{t:X4}")),
        OperandType.InlineString => $"\"{GetString()}\"",
        OperandType.InlineMethod => GetMethod().ToString() ?? "",
        OperandType.InlineField => GetField().ToString() ?? "",
        OperandType.InlineType => GetTypeOperand().ToString(),
        OperandType.InlineTok => GetMember().ToString() ?? "",
        OperandType.InlineSig => Convert.ToHexString(GetSignature()),
        _ => throw new NotSupportedException(OperandType.ToString()),
    };

    private void Require(OperandType expected, OperandType? alternative = null, [CallerMemberName] string caller = "")
    {
        if (OperandType != expected && OperandType != alternative)
        {
            throw new InvalidOperationException($"{caller} called on an operand of type {OperandType}.");
        }
    }

    private static OperandType RequireOneOf(OperandType operandType, OperandType first, OperandType second) =>
        operandType == first || operandType == second
            ? operandType
            : throw new ArgumentOutOfRangeException(nameof(operandType), operandType, $"Expected {first} or {second}.");
}