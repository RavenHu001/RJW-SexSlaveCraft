// AI 辅助生成。只读真实 FA 元数据，避免加载/运行其游戏控制器或依赖旧 Unity 运行时。
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

internal static class FaContract
{
    public static object Check(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();
        var checks = new List<string>();
        TypeDefinition Type(string name)
        {
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                if (reader.GetString(type.Namespace) == "FacialAnimation" && reader.GetString(type.Name) == name) return type;
            }
            throw new Exception("FA type missing: " + name);
        }
        string TypeName(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeReference)
            {
                var type = reader.GetTypeReference((TypeReferenceHandle)handle);
                return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
            }
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                var type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
            }
            throw new Exception("Unexpected FA type token: " + handle.Kind);
        }
        void Field(string owner, string name, string expectedType, bool isStatic = false)
        {
            var field = Type(owner).GetFields().Select(reader.GetFieldDefinition).Single(f => reader.GetString(f.Name) == name);
            if (((field.Attributes & FieldAttributes.Static) != 0) != isStatic) throw new Exception("FA field static mismatch: " + name);
            var blob = reader.GetBlobReader(field.Signature);
            if (blob.ReadSignatureHeader().Kind != SignatureKind.Field) throw new Exception("Not a field signature.");
            var code = blob.ReadSignatureTypeCode();
            if (code != SignatureTypeCode.TypeHandle || TypeName(blob.ReadTypeHandle()) != expectedType)
                throw new Exception("FA field type changed: " + owner + "." + name);
            checks.Add(owner + "." + name + ": " + expectedType);
        }
        if (TypeName(Type("NLFacialAnimationPartNode").BaseType) != "Verse.PawnRenderNode") throw new Exception("FA node base changed.");
        Field("NLFacialAnimationPartNode", "controller", "FacialAnimation.IFacialAnimationController");
        Field("NLFacialAnimationPartNode", "layerType", "FacialAnimation.NLFacialAnimationLayerType");
        Field("GraphicPool", "dummyGraphic", "Verse.Graphic", true);
        foreach (string name in new[] { "HeadControllerComp", "SkinControllerComp", "LidControllerComp", "EyeballControllerComp", "BrowControllerComp", "MouthControllerComp" })
        {
            _ = Type(name); checks.Add("Controller exists: " + name);
        }
        string[] layers = Type("NLFacialAnimationLayerType").GetFields().Select(reader.GetFieldDefinition).Select(f => reader.GetString(f.Name)).ToArray();
        foreach (string name in new[] { "Basic", "Cover", "Highlight", "Bottom" })
        {
            if (!layers.Contains(name)) throw new Exception("FA layer missing: " + name);
            checks.Add("Layer exists: " + name);
        }
        var method = Type("NLFacialAnimationHeadNodeWorker").GetMethods().Select(reader.GetMethodDefinition)
            .Single(m => reader.GetString(m.Name) == "GetFinalizedMaterial");
        if ((method.Attributes & MethodAttributes.Static) != 0) throw new Exception("FA head material method changed.");
        checks.Add("FA head final material override exists");
        Console.WriteLine($"PASS actual FA contract: {checks.Count} metadata checks");
        return new { assembly = path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
            mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid), checks };
    }
}
