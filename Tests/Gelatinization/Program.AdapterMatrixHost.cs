// AI 辅助生成。隔离编译的四种可选类型组合；只执行生产补丁，不启动游戏。
#if SSC_ADAPTER_MATRIX
using System;
using HarmonyLib;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class AdapterMatrixHost
{
    private static int Main()
    {
        try
        {
            bool hasFa = AccessTools.TypeByName("FacialAnimation.NLFacialAnimationPartNode") != null;
            bool hasSa = AccessTools.TypeByName("SizedApparel.SizedApparelRenderNodeWorker") != null;
#if SSC_WITHOUT_FA
            Ensure(!hasFa, "FA类型必须真正不存在");
#else
            Ensure(hasFa, "FA类型必须存在");
#endif
#if SSC_WITHOUT_SA
            Ensure(!hasSa, "SA类型必须真正不存在");
#else
            Ensure(hasSa, "SA类型必须存在");
#endif
            SSCMod.settings = new SSCSettings { enableFullGelatinizedBodyTint = true };
            var hooks = new Harmony();
            Harmony_FullGelatinizationHeadTint.Register(hooks);
            Harmony_FullGelatinizationBodyTint.RegisterSizedApparel(hooks);
            Ensure(hooks.Patched.Count == (hasSa ? 2 : 1), "统一头部入口加可选SA入口");

            var pixels = new Color32[64 * 64];
            for (int y = 8; y < 56; y++) for (int x = 8; x < 56; x++)
                pixels[y*64+x] = x < 10 || x > 53 || y < 10 || y > 53 ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
            foreach (int ox in new[] { 22, 38 }) for (int y = 24; y <= 28; y++) for (int x = ox; x < ox + 4; x++)
                pixels[y*64+x] = new Color32(0,0,0,255);
            var texture = new Texture2D(64,64,TextureFormat.RGBA32,false,true); texture.SetPixels32(pixels);
            var pawn = new Pawn { FullGel = true };
            var parms = new PawnDrawParms { pawn = pawn, facing = Rot4.South };
            var source = new Material(new Shader("Vanilla")) { mainTexture = texture, color = new Color(.8f,.7f,.6f,1) };
            source.shader.EngineShader.DepthWrite = true;
            var node = new PawnRenderNode_Head(); var result = source;
            Harmony_FullGelatinizationHeadTint.MaterialPostfix(node,parms,ref result);
            Ensure(result != source && result.shader == ShaderDef.VanillaDepth && result.GetTexture("_EyeMask") != null && result.color.a == .85f, "无论FA/SA组合都保护原版眼睛");
            Graphic body = new Graphic { path = "VanillaBody" };
            Harmony_FullGelatinizationBodyTint.Postfix(pawn,ref body);
            Ensure(body.shader == ShaderDef.Native && body.color.a == .85f, "无SA仍支持原版身体");
#if !SSC_WITHOUT_FA
            var fa = new FacialAnimation.NLFacialAnimationPartNode { controller = new FacialAnimation.EyeballControllerComp() };
            result = source; Harmony_FullGelatinizationHeadTint.MaterialPostfix(fa,parms,ref result);
            Ensure(result.shader == source.shader && result.color.a == 1 && result.GetTexture("_EyeMask") == null, "FA眼睛保留原色");
#endif
#if !SSC_WITHOUT_SA
            result = source;
            Harmony_FullGelatinizationBodyTint.SizedApparelMaterialPostfix(new SizedApparel.SizedApparelRenderNodeWorker(),new PawnRenderNode { tree = new PawnRenderTree { pawn = pawn } },parms,ref result);
            Ensure(result.shader == ShaderDef.Native && result.color.a == .85f, "SA入口独立染色");
#endif
            SSCMod.settings.enableFullGelatinizedBodyTint = false;
            result = source; Harmony_FullGelatinizationHeadTint.MaterialPostfix(node,parms,ref result);
            Ensure(result == source, "关闭开关恢复原材质");
            Console.WriteLine($"PASS isolated FA={hasFa} SA={hasSa}");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }
    private static void Ensure(bool value, string message) { if (!value) throw new Exception(message); }
}
#endif
