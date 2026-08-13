var asmPath = @"E:\Program Files\steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll";
var asm = System.Reflection.Assembly.Load(System.IO.File.ReadAllBytes(asmPath));
var type = asm.GetType("RimWorld.PawnRenderNode_Equipment");
if (type != null) {
    Console.WriteLine("=== PawnRenderNode_Equipment Fields ===");
    foreach (var f in type.GetFields()) Console.WriteLine($"{f.Name} : {f.FieldType}");
    Console.WriteLine("=== PawnRenderNode_Equipment Properties ===");
    foreach (var p in type.GetProperties()) Console.WriteLine($"{p.Name} : {p.PropertyType}");
    Console.WriteLine($"=== Base type: {type.BaseType} ===");
    Console.WriteLine("=== Base fields ===");
    foreach (var f in type.BaseType.GetFields()) Console.WriteLine($"{f.Name} : {f.FieldType}");
    Console.WriteLine("=== Base Properties ===");
    foreach (var p in type.BaseType.GetProperties()) Console.WriteLine($"{p.Name} : {p.PropertyType}");
} else {
    Console.WriteLine("PawnRenderNode_Equipment NOT FOUND");
    foreach (var t in asm.GetExportedTypes()) {
        if (t.Name.Contains("Equipment") && t.Name.Contains("RenderNode"))
            Console.WriteLine($"Found: {t}");
    }
}
// Also check what PawnRenderNodeWorker types exist for equipment
var workerType = asm.GetType("RimWorld.PawnRenderNodeWorker_Equipment");
Console.WriteLine($"\nPawnRenderNodeWorker_Equipment exists: {workerType != null}");
// Check how CanDrawNow signature looks
var canDrawNow = asm.GetType("Verse.PawnRenderNodeWorker").GetMethod("CanDrawNow");
if (canDrawNow != null) {
    Console.WriteLine($"\nCanDrawNow signature:");
    var parms = canDrawNow.GetParameters();
    foreach (var p in parms) Console.WriteLine($"  {p.ParameterType} {p.Name}");
}
