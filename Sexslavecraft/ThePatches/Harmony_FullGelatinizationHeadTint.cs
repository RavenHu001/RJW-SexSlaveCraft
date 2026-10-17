// AI 辅助生成。仅扩展 1.7 开发源码；1.6 冻结 DLL 不重新编译。
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在原版/FA 选出本帧材质后染皮肤，并将目标角色的头部组移到光照后；不修改共享材质。</summary>
    public static class Harmony_FullGelatinizationHeadTint
    {
        internal const string MaskedShaderDefName = "SSC_FullGelHeadMasked17";
        internal const string DepthShaderDefName = "SSC_FullGelHeadDepth17";
        internal const string MaskedDepthShaderDefName = "SSC_FullGelHeadMaskedDepth17";
        // UnityStubs 的 EngineShader 是 internal；缓存真实 getter，读取已解析程序的深度状态，
        // 不根据名字猜 Cutout，也不把所有 Cover 层强制写深度。未知边界保留原材质。
        private static readonly PropertyInfo engineShaderProperty = typeof(Shader).GetProperty("EngineShader",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static Type faNodeType, faHeadType, faSkinType, faLidType;
        private static FieldInfo controllerField, layerField, dummyField;
        private static object basicLayer, coverLayer;
        private static bool warnedMissingShader;

        // 弱键随原材质释放。每种 Shader/用途复用一个副本，不持有 Pawn、不每帧 new Material。
        private static readonly ConditionalWeakTable<Material, Variant> variants = new ConditionalWeakTable<Material, Variant>();
        private static readonly ConditionalWeakTable<Material, object> generated = new ConditionalWeakTable<Material, object>();
        private static readonly object generatedMarker = new object();
        private sealed class Variant
        {
            // 染色副本与五官的仅移队列副本隔离，避免共享源材质的不同用途串色。
            public readonly System.Collections.Generic.Dictionary<string, Material> materials = new System.Collections.Generic.Dictionary<string, Material>();
        }

        /// <summary>仅给最终材质入口挂一次补丁；FA 的头部 worker 也调用这个基类方法。</summary>
        public static void Register(Harmony harmony)
        {
            InitializeFacialAnimation();
            MethodInfo original = AccessTools.Method(typeof(PawnRenderNodeWorker), "GetFinalizedMaterial",
                new[] { typeof(PawnRenderNode), typeof(PawnDrawParms) });
            if (original == null) throw new MissingMethodException("PawnRenderNodeWorker.GetFinalizedMaterial");
            harmony.Patch(original, postfix: new HarmonyMethod(typeof(Harmony_FullGelatinizationHeadTint), nameof(MaterialPostfix)));
            SSCLog.Important("[SSC FullGel] Head skin material hook registered; FA adapter: " + (faNodeType != null));
        }

        /// <summary>FA 为软依赖；只在启动时解析本机验证过的公开节点/控制器/枚举，不改其数据。</summary>
        internal static void InitializeFacialAnimation()
        {
            faNodeType = AccessTools.TypeByName("FacialAnimation.NLFacialAnimationPartNode");
            faHeadType = AccessTools.TypeByName("FacialAnimation.HeadControllerComp");
            faSkinType = AccessTools.TypeByName("FacialAnimation.SkinControllerComp");
            faLidType = AccessTools.TypeByName("FacialAnimation.LidControllerComp");
            controllerField = faNodeType == null ? null : AccessTools.Field(faNodeType, "controller");
            layerField = faNodeType == null ? null : AccessTools.Field(faNodeType, "layerType");
            Type poolType = AccessTools.TypeByName("FacialAnimation.GraphicPool");
            dummyField = poolType == null ? null : AccessTools.Field(poolType, "dummyGraphic");
            if (controllerField == null || layerField == null || !layerField.FieldType.IsEnum
                || faHeadType == null || faSkinType == null || faLidType == null || dummyField == null
                || !Enum.IsDefined(layerField.FieldType, "Basic") || !Enum.IsDefined(layerField.FieldType, "Cover"))
            {
                if (faNodeType != null) SSCLog.WarningImportant("[SSC FullGel] FA adapter shape differs; leaving FA materials unchanged.");
                faNodeType = null;
                return;
            }
            basicLayer = Enum.Parse(layerField.FieldType, "Basic");
            coverLayer = Enum.Parse(layerField.FieldType, "Cover");
        }

        /// <summary>白名单按控制器和层判断，不按纹理名、肤色相似度或 Head 父节点批量染五官。</summary>
        internal static bool IsSkinNode(PawnRenderNode node, out bool requiresMask)
        {
            requiresMask = false;
            if (node is PawnRenderNode_Head) return true;
            if (faNodeType == null || node == null || !faNodeType.IsInstanceOfType(node)) return false;
            object controller = controllerField.GetValue(node), layer = layerField.GetValue(node);
            if (controller == null) return false;
            if (faHeadType.IsInstanceOfType(controller))
                return Equals(layer, basicLayer) || Equals(layer, coverLayer);
            if (faSkinType.IsInstanceOfType(controller)) return Equals(layer, basicLayer);
            // Lid.Basic 是发色睫毛，Bottom 是眼部底层，均保留；只处理带皮肤遮罩的 Cover。
            requiresMask = faLidType.IsInstanceOfType(controller) && Equals(layer, coverLayer);
            return requiresMask;
        }

        /// <summary>按头部实际树归属处理五官、头饰及种族附加节点，不将全身所有节点一并移动。</summary>
        internal static bool IsHeadGroup(PawnRenderNode node)
        {
            // FA 节点及原版毛发可直接识别；其余附加部件从 parent 链确认属于头部。
            // 有界遍历防止其他模组构造异常循环树时卡住绘制线程。
            for (int depth = 0; node != null && depth < 64; depth++, node = node.parent)
                if (node is PawnRenderNode_Head || node is PawnRenderNode_Hair || node is PawnRenderNode_Beard
                    || (faNodeType != null && faNodeType.IsInstanceOfType(node))) return true;
            return false;
        }

        /// <summary>准入条件先于全部材质/队列操作；保留动画、遮罩、原深度及头部内部绘制关系。</summary>
        public static void MaterialPostfix(PawnRenderNode node, PawnDrawParms parms, ref Material __result)
        {
            Pawn pawn = parms.pawn;
            if (__result == null || pawn == null || parms.Statue || parms.rotDrawMode != RotDrawMode.Fresh
                || (parms.flags & PawnRenderFlags.Invisible) != 0 || pawn.IsPsychologicallyInvisible()
                || pawn.health?.hediffSet == null || !pawn.health.hediffSet.HasHead
                || !FullGelatinizationUtility.ShouldTintFullGelGraphic(pawn)
                || !IsHeadGroup(node)) return;

            // 其他 worker 若再次经过同一基类入口，不能把 0.85 再乘成 0.7225。
            if (generated.TryGetValue(__result, out _)) return;

            // FA 缺失/隐藏帧使用 dummy，头部 worker 会依赖该材质身份返回 null；必须原样保留。
            Graphic dummy = dummyField?.GetValue(null) as Graphic;
            if (dummy != null && ReferenceEquals(__result, dummy.MatSingle)) return;
            if (!IsSkinNode(node, out bool requiresMask))
            {
                // 只移动皮肤会让晚画的头皮盖住早画的透明嘴型/眼睑；五官等跟随整个头部组。
                // 此分支不染紫、不乘0.85，不替换原Shader或动画，只复制本帧属性与调整队列。
                MoveOriginalHeadMaterial(ref __result);
                return;
            }
            Material source = __result;
            if (source.mainTexture == null) return;
            bool hasMaskSlot = source.HasProperty("_MaskTex");
            Texture mask = hasMaskSlot ? source.GetTexture("_MaskTex") : null;
            // 已声明遮罩却没绑定纹理时也不降级，避免把整张头皮肤画到眼睛上。
            if ((requiresMask || hasMaskSlot) && mask == null) return;
            bool? depthWrite = OriginalDepthWrite(source.shader);
            if (!depthWrite.HasValue) return;
            string defName = depthWrite.Value
                ? (mask == null ? DepthShaderDefName : MaskedDepthShaderDefName)
                : (mask == null ? Harmony_FullGelatinizationBodyTint.NativeShaderDefName : MaskedShaderDefName);
            ApplyMaterial(ref __result, defName, true, mask);
        }

        /// <summary>真实程序的深度写入状态决定皮肤 Shader 变体；解析失败时不冒险改绘制行为。</summary>
        private static bool? OriginalDepthWrite(Shader shader)
        {
            if (shader == null || engineShaderProperty == null) return null;
            try { return (engineShaderProperty.GetValue(shader) as Waylith.Rendering.Shader)?.DepthWrite; }
            catch (TargetInvocationException) { return null; }
        }

        /// <summary>头部与 SA 共用材质副本缓存；此处只操作已选出的材质，不进入 GraphicDatabase。</summary>
        internal static void ApplyMaterial(ref Material result, string defName, bool headLayer, Texture mask = null)
        {
            if (result == null || result.mainTexture == null || generated.TryGetValue(result, out _)) return;
            Material source = result;
            if (!Harmony_FullGelatinizationBodyTint.TryGetNativeShader(defName, out Shader shader))
            {
                if (!warnedMissingShader)
                {
                    warnedMissingShader = true;
                    SSCLog.WarningImportant("[SSC FullGel] Material shader unavailable; preserving original material.");
                }
                return;
            }

            Variant entry = variants.GetValue(source, _ => new Variant());
            lock (entry)
            {
                string key = defName + (headLayer ? ":headPostLight" : ":bodyQueue");
                if (!entry.materials.TryGetValue(key, out Material tinted))
                {
                    tinted = new Material(source);
                    entry.materials.Add(key, tinted);
                    generated.Add(tinted, generatedMarker);
                }
                // 同一原材质可能动态切换眨眼/腮红贴图；每次复制当前属性，缓存只复用对象。
                tinted.CopyPropertiesFromMaterial(source);
                tinted.shader = shader;
                tinted.mainTexture = source.mainTexture;
                tinted.SetTexture("_MaskTex", mask);
                Color color = Harmony_FullGelatinizationBodyTint.FullGelColor;
                color.a *= source.color.a;
                tinted.color = color;
                // 原版/FA常规头部2900统一进入3150，交给原绘制请求次序和深度维持层级。
                // 已经在更晚队列的高光保持原队列；保留原Shader的深度写入，不恢复旧遮挡缺陷。
                tinted.renderQueue = headLayer ? PostLightQueue(source.renderQueue) : Harmony_FullGelatinizationBodyTint.PostLightRenderQueue;
                result = tinted;
            }
        }

        internal static int PostLightQueue(int originalQueue) => Math.Max(originalQueue, Harmony_FullGelatinizationBodyTint.PostLightRenderQueue);

        /// <summary>五官/毛发/头饰仅调整阶段。缓存按原材质弱引用，不持有Pawn、不请求Graphic。</summary>
        private static void MoveOriginalHeadMaterial(ref Material result)
        {
            Material source = result;
            if (source.mainTexture == null || source.renderQueue >= Harmony_FullGelatinizationBodyTint.PostLightRenderQueue) return;
            Variant entry = variants.GetValue(source, _ => new Variant());
            lock (entry)
            {
                const string key = "originalHeadPostLight";
                if (!entry.materials.TryGetValue(key, out Material moved))
                {
                    moved = new Material(source);
                    entry.materials.Add(key, moved);
                    generated.Add(moved, generatedMarker);
                }
                moved.CopyPropertiesFromMaterial(source);
                moved.shader = source.shader;
                moved.renderQueue = PostLightQueue(source.renderQueue);
                result = moved;
            }
        }
    }
}
