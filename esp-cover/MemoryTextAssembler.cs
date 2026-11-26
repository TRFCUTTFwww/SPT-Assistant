using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace LocalSpt5Esp;

internal static class MemoryTextAssembler
{
    sealed record Form(MethodInfo Method, Type[] Operands);
    readonly record struct Immediate(ulong Magnitude, bool Negative);
    readonly record struct SizedMemory(AssemblerMemoryOperand Value, int Size);

    static readonly Lazy<IReadOnlyDictionary<string, Form[]>> instructions = new(() =>
        typeof(Assembler).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType == typeof(void) && !method.IsSpecialName &&
                method.Name[0] is >= 'a' and <= 'z' && Enum.TryParse<Mnemonic>(method.Name, true, out _))
            .GroupBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key,
                group => group.Select(method => new Form(method, method.GetParameters().Select(p => p.ParameterType).ToArray())).ToArray(),
                StringComparer.OrdinalIgnoreCase));

    static readonly IReadOnlyDictionary<string, object> registers =
        typeof(AssemblerRegisters).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType.Name.StartsWith("AssemblerRegister", StringComparison.Ordinal))
            .ToDictionary(field => field.Name, field => field.GetValue(null)!, StringComparer.OrdinalIgnoreCase);

    internal static bool TryEmit(Assembler asm, string mnemonic, string[] text, out string? error)
    {
        error = null;
        if (!instructions.Value.TryGetValue(mnemonic, out var forms)) return false;
        error = $"Iced 无法编码这组操作数：{mnemonic} {string.Join(", ", text)}";
        if (!forms.Any(form => form.Operands.Length == text.Length)) return false;
        var operands = new object[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            if (!TryParseOperand(text[i], out var operand)) return false;
            operands[i] = operand!;
        }

        var arguments = new object?[operands.Length];
        foreach (var form in forms)
        {
            if (form.Operands.Length != operands.Length) continue;
            int index = 0;
            while (index < operands.Length && TryBind(operands[index], form.Operands[index], out arguments[index]))
                index++;
            if (index != operands.Length) continue;
            try
            {
                form.Method.Invoke(asm, arguments);
                int actualSize = asm.Instructions[^1].MemorySize.GetSize();
                if (actualSize != 0 && operands.Any(operand => operand is SizedMemory memory &&
                    memory.Size != 0 && memory.Size != actualSize))
                    return false;
                error = null;
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException or InvalidOperationException)
            {
                // Some Iced overloads have additional constraints on memory size or registers.
            }
        }
        return false;
    }

    static bool TryBind(object operand, Type type, out object? value)
    {
        value = operand;
        if (type.IsInstanceOfType(operand)) return true;
        if (operand is SizedMemory memory && type == typeof(AssemblerMemoryOperand))
        {
            value = memory.Value;
            return true;
        }
        if (operand is not Immediate immediate) return false;
        ulong magnitude = immediate.Magnitude;
        long signed = immediate.Negative ? unchecked(-(long)magnitude) : unchecked((long)magnitude);
        bool InRange(ulong positive, ulong negative) => magnitude <= (immediate.Negative ? negative : positive);
        value = Type.GetTypeCode(type) switch
        {
            TypeCode.SByte when InRange((ulong)sbyte.MaxValue, 128) => (object)(sbyte)signed,
            TypeCode.Int16 when InRange((ulong)short.MaxValue, 32768) => (object)(short)signed,
            TypeCode.Int32 when InRange(int.MaxValue, 2147483648) => (object)(int)signed,
            TypeCode.Int64 when InRange(long.MaxValue, 1UL << 63) => (object)signed,
            TypeCode.Byte when !immediate.Negative && magnitude <= byte.MaxValue => (object)(byte)magnitude,
            TypeCode.UInt16 when !immediate.Negative && magnitude <= ushort.MaxValue => (object)(ushort)magnitude,
            TypeCode.UInt32 when !immediate.Negative && magnitude <= uint.MaxValue => (object)(uint)magnitude,
            TypeCode.UInt64 when !immediate.Negative => (object)magnitude,
            _ => null
        };
        return value != null;
    }

    static bool TryParseOperand(string text, out object? operand)
    {
        operand = null;
        int first = text.IndexOf('{');
        string core = (first < 0 ? text : text[..first]).Trim();
        string modifiers = first < 0 ? "" : text[first..].Trim();
        bool broadcast = false;
        int memorySize = 0;
        var properties = new List<string>();
        while (modifiers.Length != 0)
        {
            int end = modifiers.IndexOf('}');
            if (modifiers[0] != '{' || end <= 1) return false;
            string token = modifiers[1..end].Trim().ToLowerInvariant();
            if (token is "1to2" or "1to4" or "1to8" or "1to16" or "1to32" or "1to64") broadcast = true;
            else if (token is "k1" or "k2" or "k3" or "k4" or "k5" or "k6" or "k7" or "z" or "sae" or
                "rn-sae" or "rd-sae" or "ru-sae" or "rz-sae")
                properties.Add(token.Replace('-', '_'));
            else return false;
            modifiers = modifiers[(end + 1)..].TrimStart();
        }

        if (core.Contains('['))
        {
            if (!TryParseMemory(core, broadcast, out var memory, out memorySize)) return false;
            operand = memory;
        }
        else if (broadcast) return false;
        else if (TryRegister(core, out var register)) operand = register;
        else if (properties.Count == 0 && TryParseImmediate(core, out var immediate)) operand = immediate;
        else return false;

        foreach (string property in properties)
        {
            operand = operand!.GetType().GetProperty(property)?.GetValue(operand);
            if (operand == null) return false;
        }
        if (operand is AssemblerMemoryOperand parsedMemory) operand = new SizedMemory(parsedMemory, memorySize);
        return true;
    }

    static bool TryRegister(string text, out object? register)
    {
        string name = text.Trim();
        if (name.Length == 5 && name.StartsWith("st(", StringComparison.OrdinalIgnoreCase) && name[^1] == ')')
            name = "st" + name[3];
        return registers.TryGetValue(name, out register);
    }

    static bool TryParseMemory(string text, bool broadcast, out AssemblerMemoryOperand operand, out int size)
    {
        operand = default;
        size = 0;
        int open = text.IndexOf('[');
        if (open < 0 || !text.EndsWith(']') || !TryParseAddress(text[(open + 1)..^1].Trim(), out var address))
            return false;
        string prefix = text[..open].Trim().ToLowerInvariant();
        string segment = "";
        if (prefix.EndsWith(':'))
        {
            int split = prefix.LastIndexOf(' ');
            segment = prefix[(split + 1)..^1];
            prefix = split < 0 ? "" : prefix[..split].TrimEnd();
        }
        if (prefix.EndsWith(" ptr", StringComparison.Ordinal)) prefix = prefix[..^4].TrimEnd();
        if (prefix.EndsWith(" bcst", StringComparison.Ordinal))
        {
            broadcast = true;
            prefix = prefix[..^5].TrimEnd();
        }
        (AssemblerMemoryOperandFactory? Sized, int Bytes) syntax = (prefix, broadcast) switch
        {
            ("" or "ptr", false) => (__, 0),
            ("byte", false) => (__byte_ptr, 1),
            ("word", false) => (__word_ptr, 2),
            ("dword", false) => (__dword_ptr, 4),
            ("qword", false) => (__qword_ptr, 8),
            ("fword", false) => (__fword_ptr, 6),
            ("mmword", false) => (__mmword_ptr, 8),
            ("tbyte" or "tword", false) => (__tbyte_ptr, 10),
            ("oword" or "xmmword", false) => (__xmmword_ptr, 16),
            ("ymmword", false) => (__ymmword_ptr, 32),
            ("zmmword", false) => (__zmmword_ptr, 64),
            ("", true) => (__bcst, 0),
            ("word", true) => (__word_bcst, 2),
            ("dword", true) => (__dword_bcst, 4),
            ("qword", true) => (__qword_bcst, 8),
            _ => (null, 0)
        };
        if (syntax.Sized is not {} factory) return false;
        AssemblerMemoryOperandFactory? segmented = segment switch
        {
            "" => factory, "es" => factory.es, "cs" => factory.cs,
            "ss" => factory.ss, "ds" => factory.ds, "fs" => factory.fs, "gs" => factory.gs,
            _ => null
        };
        if (segmented is not {} result) return false;
        operand = result[address];
        size = syntax.Bytes;
        return true;
    }

    static bool TryParseAddress(string text, out AssemblerMemoryOperand address)
    {
        address = default;
        if (text.Length == 0) return false;
        object? baseReg = null, indexReg = null;
        long displacement = 0;
        int scale = 1;
        int start = 0;
        while (start < text.Length)
        {
            bool subtract = text[start] == '-';
            if (subtract || text[start] == '+') start++;
            int end = start;
            while (end < text.Length && text[end] != '+' && text[end] != '-') end++;
            string term = text[start..end].Trim();
            start = end;
            int star = term.IndexOf('*');
            string registerText = star < 0 ? term : term[..star];
            if (TryRegister(registerText, out var reg))
            {
                if (subtract || reg is not (AssemblerRegister32 or AssemblerRegister64 or
                    AssemblerRegisterXMM or AssemblerRegisterYMM or AssemblerRegisterZMM)) return false;
                if (star < 0 && baseReg == null && reg is AssemblerRegister32 or AssemblerRegister64)
                {
                    baseReg = reg;
                    continue;
                }
                if (indexReg != null || (star >= 0 &&
                    (!int.TryParse(term.AsSpan(star + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out scale) ||
                     scale is not (1 or 2 or 4 or 8)))) return false;
                indexReg = reg;
            }
            else
            {
                if (!TryParseImmediate(term, out var immediate) || immediate.Negative ||
                    (subtract && immediate.Magnitude > (1UL << 63))) return false;
                displacement = unchecked(displacement + (subtract ? -(long)immediate.Magnitude : (long)immediate.Magnitude));
            }
        }

        if (indexReg == null)
        {
            address = baseReg switch
            {
                AssemblerRegister32 r => r + displacement,
                AssemblerRegister64 r => r + displacement,
                _ => __[displacement]
            };
            return true;
        }

        var indexed = indexReg switch
        {
            AssemblerRegister32 r => r * scale,
            AssemblerRegister64 r => r * scale,
            AssemblerRegisterXMM r => r * scale,
            AssemblerRegisterYMM r => r * scale,
            AssemblerRegisterZMM r => r * scale,
            _ => default
        };
        address = (baseReg switch
        {
            AssemblerRegister32 r => r + indexed,
            AssemblerRegister64 r => r + indexed,
            _ => indexed
        }) + displacement;
        return true;
    }

    static bool TryParseImmediate(string text, out Immediate immediate)
    {
        immediate = default;
        ReadOnlySpan<char> value = text.AsSpan().Trim();
        bool negative = value.Length > 0 && value[0] == '-';
        if (negative || (value.Length > 0 && value[0] == '+')) value = value[1..].TrimStart();
        NumberStyles style = NumberStyles.None;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            style = NumberStyles.AllowHexSpecifier;
            value = value[2..];
        }
        else if (value.EndsWith("h", StringComparison.OrdinalIgnoreCase))
        {
            style = NumberStyles.AllowHexSpecifier;
            value = value[..^1];
        }
        if (!ulong.TryParse(value, style, CultureInfo.InvariantCulture, out ulong magnitude)) return false;
        immediate = new(magnitude, negative);
        return true;
    }
}
