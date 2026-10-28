using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static class InstalledUpdaterInspection
{
    public static void Run(string dll)
    {
        using var stream = File.OpenRead(dll);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        Console.WriteLine("Assembly version: " + metadata.GetAssemblyDefinition().Version);
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => unchecked((ushort)op.Value));
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(type.Name) != "WindowsUpdatePlatform") continue;
            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                if (metadata.GetString(method.Name) != "IsInstalled") continue;
                var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
                for (int offset = 0; offset < il.Length;)
                {
                    ushort opcode = il[offset++];
                    if (opcode == 0xfe) opcode = (ushort)(0xfe00 | il[offset++]);
                    var operation = opcodes[opcode];
                    if (operation.OperandType == OperandType.InlineString)
                        Console.WriteLine("IsInstalled required marker: " + metadata.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il, offset) & 0xffffff)));
                    offset += operation.OperandType switch
                    {
                        OperandType.InlineNone => 0,
                        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                        OperandType.InlineVar => 2,
                        OperandType.InlineI8 or OperandType.InlineR => 8,
                        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                        _ => 4
                    };
                }
            }
        }
    }
}
