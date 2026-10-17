// AI 辅助生成。只加载编译器并输出隔离证据，不启动游戏、不创建设备、不写入游戏目录。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Waylith.Rendering;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Arguments: repositoryRoot gameRoot outputDirectory");
        string root = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
        Directory.CreateDirectory(output);
        // 在第一次 JIT Waylith 调用前设置解析器；依赖始终从显式目标游戏读取，避免 NuGet 替代品。
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(game, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        // 保留 Brutal 自己的 DllImportResolver；仅在常规解析未命中时补充显式游戏路径。
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += (_, name) =>
        {
            string path = Path.Combine(game, name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll");
            return File.Exists(path) && NativeLibrary.TryLoad(path, out var handle) ? handle : IntPtr.Zero;
        };
        // NativeLibrary 的嵌套依赖也从本次进程 PATH 找到，不修改系统 PATH 或游戏文件。
        Environment.SetEnvironmentVariable("PATH", game + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
        return Verify(root, game, output);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Verify(string root, string game, string output)
    {
        var def = XDocument.Load(Path.Combine(root, "1.7/Defs/ShaderDefs/SSC_FullGelBody17.xml")).Root!.Element("ShaderDef")!;
        var xml = def.Element("shaderProgram")!;
        // 直接由目标类解析字段及枚举，错误的 XML 字段/枚举会失败，不能被模型替身放过。
        var program = new ShaderProgramDefinition();
        foreach (var element in xml.Elements())
        {
            var field = typeof(ShaderProgramDefinition).GetField(element.Name.LocalName) ?? throw new Exception("Unknown shader field: " + element.Name);
            if (field.FieldType == typeof(string)) field.SetValue(program, element.Value);
            else if (field.FieldType == typeof(bool)) field.SetValue(program, bool.Parse(element.Value));
            else if (field.FieldType.IsEnum) field.SetValue(program, Enum.Parse(field.FieldType, element.Value));
            // renderQueue 是引擎自定义值类型，不在此模拟 XML 的通用自定义读取器。
            else if (element.Name != "renderQueue") throw new Exception("Unhandled shader field: " + element.Name);
        }
        if (program.ConfigErrors().Any()) throw new Exception(string.Join("; ", program.ConfigErrors()));
        _ = program.ToPipelineState();
        ShaderSourceLoader.RegisterShaderSourceDirectory(Path.Combine(game, "Data/Core/Shaders"));
        ShaderSourceLoader.SetOverrideShaderDirectories(new[] { Path.Combine(root, "1.7/Shaders") });
        if (ShaderSourceLoader.HasStageInterfaceErrors(program.vertexShaderFileName, program.fragmentShaderFileName))
            throw new Exception("Vertex/fragment interfaces do not match.");
        _ = ShaderSourceLoader.CreateMaterialLayout(program.fragmentShaderFileName);

        var results = new List<object>();
        foreach (bool instanced in new[] { false, true })
            foreach (var pair in new[] { (program.vertexShaderFileName, RuntimeShaderStage.Vertex), (program.fragmentShaderFileName, RuntimeShaderStage.Fragment) })
            {
                string path = ShaderSourceLoader.ResolveShaderPath(pair.Item1);
                string source = ShaderSourceLoader.ReadShaderSource(pair.Item1);
                if (instanced) source = source.Replace("#version 450", "#version 450\n#define INSTANCED 1", StringComparison.Ordinal);
                byte[] spirv = RuntimeShaderCompiler.Compile(source, path, "main", RuntimeShaderLanguage.Glsl, pair.Item2);
                if (spirv.Length < 20 || BitConverter.ToUInt32(spirv) != 0x07230203) throw new Exception("Invalid SPIR-V output.");
                string name = pair.Item2 + (instanced ? "-instanced" : "-normal") + ".spv";
                File.WriteAllBytes(Path.Combine(output, name), spirv);
                results.Add(new { stage = pair.Item2.ToString(), instanced, source = path, bytes = spirv.Length, sha256 = Convert.ToHexString(SHA256.HashData(spirv)).ToLowerInvariant() });
                Console.WriteLine($"PASS {name}: {spirv.Length} bytes");
            }
        // 故意提供语法错误，验证当前编译器真的编译源码，而非只返回一个固定成功对象。
        bool rejected = false;
        try { RuntimeShaderCompiler.Compile("#version 450\nvoid main() { definitely invalid; }", Path.Combine(output, "invalid.frag.glsl"), "main", RuntimeShaderLanguage.Glsl, RuntimeShaderStage.Fragment); }
        catch (InvalidOperationException e) when (e.Message.Contains("Failed to compile shader", StringComparison.Ordinal)) { rejected = true; }
        if (!rejected) throw new Exception("Compiler accepted deliberately invalid GLSL.");
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(output, "shader-compile.json"), JsonSerializer.Serialize(new
        {
            gameBuild = File.ReadAllText(Path.Combine(game, "Version.txt")).Trim(),
            gameAssemblySha256 = Hash(Path.Combine(game, "Assembly-CSharp.dll")),
            waylithMvid = typeof(RuntimeShaderCompiler).Assembly.ManifestModule.ModuleVersionId,
            stageInterfacesValid = true, materialLayoutParsed = true, invalidSourceRejected = rejected,
            variants = results, gameRuntimeVerified = false,
            resources = Directory.GetFiles(Path.Combine(root, "1.7/Shaders/SSC")).Append(Path.Combine(root, "1.7/Defs/ShaderDefs/SSC_FullGelBody17.xml"))
                .Select(path => new { path = Path.GetRelativePath(root, path), sha256 = Hash(path) }).ToArray()
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS native compiler negative control; 4/4 shader stages compiled. No game rendering was performed.");
        return 0;
    }
}
