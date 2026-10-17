// AI 辅助生成。仅扩展 1.7 开发源码；1.6 冻结 DLL 不重新编译。
using System;
using System.Reflection;
using System.Linq.Expressions;
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
        private static Type faNodeType, faHeadType, faSkinType, faLidType;
        // 仅在启动时反射并生成访问器；绘制时直接调用，不装箱 FA 的 layer 枚举。
        private static Func<PawnRenderNode, object> readController;
        private static Func<PawnRenderNode, int> readLayer;
        private static Func<Graphic> readDummy;
        private static int basicLayer, coverLayer;
        private static bool warnedMissingShader;

        // 弱键随原材质释放。每种 Shader/用途复用一个副本，不持有 Pawn、不每帧 new Material。
        private static readonly ConditionalWeakTable<Material, Variant> variants = new ConditionalWeakTable<Material, Variant>();
        private static readonly ConditionalWeakTable<Material, object> generated = new ConditionalWeakTable<Material, object>();
        private static readonly object generatedMarker = new object();
        private sealed class Variant
        {
            // 染色副本与五官的仅移队列副本隔离，避免共享源材质的不同用途串色。
            // 值元组不会像拼接字符串那样在每次缓存命中时分配对象；null名称代表仅移队列。
            public readonly System.Collections.Generic.Dictionary<(string shader, bool head), CachedMaterial> materials =
                new System.Collections.Generic.Dictionary<(string shader, bool head), CachedMaterial>();
        }
        private sealed class CachedMaterial
        {
            public Material material;
            public Waylith.Rendering.Material sourceEngine, outputEngine;
            public int sourceVersion, outputVersion, queue;
            public Shader sourceShader, targetShader;
            public Texture mainTexture, mask;
            public bool initialized;
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
            readController = null; readLayer = null; readDummy = null;
            FieldInfo controllerField = faNodeType == null ? null : AccessTools.Field(faNodeType, "controller");
            FieldInfo layerField = faNodeType == null ? null : AccessTools.Field(faNodeType, "layerType");
            Type poolType = AccessTools.TypeByName("FacialAnimation.GraphicPool");
            FieldInfo dummyField = poolType == null ? null : AccessTools.Field(poolType, "dummyGraphic");
            if (controllerField == null || layerField == null || !layerField.FieldType.IsEnum
                || faHeadType == null || faSkinType == null || faLidType == null || dummyField == null
                || controllerField.IsStatic || layerField.IsStatic || !dummyField.IsStatic
                || !typeof(PawnRenderNode).IsAssignableFrom(faNodeType)
                || !Enum.IsDefined(layerField.FieldType, "Basic") || !Enum.IsDefined(layerField.FieldType, "Cover"))
            {
                if (faNodeType != null) SSCLog.WarningImportant("[SSC FullGel] FA adapter shape differs; leaving FA materials unchanged.");
                faNodeType = null;
                return;
            }
            try
            {
                var node = Expression.Parameter(typeof(PawnRenderNode), "node");
                var converted = Expression.Convert(node, faNodeType);
                readController = Expression.Lambda<Func<PawnRenderNode, object>>(
                    Expression.Convert(Expression.Field(converted, controllerField), typeof(object)), node).Compile();
                readLayer = Expression.Lambda<Func<PawnRenderNode, int>>(
                    Expression.Convert(Expression.Field(converted, layerField), typeof(int)), node).Compile();
                readDummy = Expression.Lambda<Func<Graphic>>(
                    Expression.Convert(Expression.Field(null, dummyField), typeof(Graphic))).Compile();
                basicLayer = Convert.ToInt32(Enum.Parse(layerField.FieldType, "Basic"));
                coverLayer = Convert.ToInt32(Enum.Parse(layerField.FieldType, "Cover"));
            }
            catch (Exception ex)
            {
                // 未知 FA 形状只禁用适配，不把初始化异常带入每帧绘制。
                faNodeType = null; readController = null; readLayer = null; readDummy = null;
                SSCLog.WarningImportant("[SSC FullGel] FA accessor unavailable; preserving FA materials: " + ex.GetType().Name);
            }
        }

        /// <summary>白名单按控制器和层判断，不按纹理名、肤色相似度或 Head 父节点批量染五官。</summary>
        internal static bool IsSkinNode(PawnRenderNode node, out bool requiresMask)
        {
            requiresMask = false;
            if (node is PawnRenderNode_Head) return true;
            if (faNodeType == null || node == null || !faNodeType.IsInstanceOfType(node)) return false;
            object controller = readController(node);
            int layer = readLayer(node);
            if (controller == null) return false;
            if (faHeadType.IsInstanceOfType(controller))
                return layer == basicLayer || layer == coverLayer;
            if (faSkinType.IsInstanceOfType(controller)) return layer == basicLayer;
            // Lid.Basic 是发色睫毛，Bottom 是眼部底层，均保留；只处理带皮肤遮罩的 Cover。
            requiresMask = faLidType.IsInstanceOfType(controller) && layer == coverLayer;
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
            Graphic dummy = readDummy?.Invoke();
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
            // 1.7公开的原生材质入口包含真实Shader状态，不再通过internal属性反射读取。
            bool? depthWrite = source.shader == null ? null : source.engineMaterial.Shader?.DepthWrite;
            if (!depthWrite.HasValue) return;
            string defName = depthWrite.Value
                ? (mask == null ? DepthShaderDefName : MaskedDepthShaderDefName)
                : (mask == null ? Harmony_FullGelatinizationBodyTint.NativeShaderDefName : MaskedShaderDefName);
            ApplyMaterial(ref __result, defName, true, mask);
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

            int queue = headLayer ? PostLightQueue(source.renderQueue) : Harmony_FullGelatinizationBodyTint.PostLightRenderQueue;
            UpdateVariant(ref result, (defName, headLayer), shader, queue, mask, true);
        }

        internal static int PostLightQueue(int originalQueue) => Math.Max(originalQueue, Harmony_FullGelatinizationBodyTint.PostLightRenderQueue);

        /// <summary>五官/毛发/头饰仅调整阶段。缓存按原材质弱引用，不持有Pawn、不请求Graphic。</summary>
        private static void MoveOriginalHeadMaterial(ref Material result)
        {
            Material source = result;
            if (source.mainTexture == null || source.renderQueue >= Harmony_FullGelatinizationBodyTint.PostLightRenderQueue) return;
            UpdateVariant(ref result, (null, true), source.shader, PostLightQueue(source.renderQueue), null, false);
        }

        /// <summary>原生版本跟踪贴图、UV、颜色、关键词和Shader变化；静态帧不重复复制和写属性。</summary>
        private static void UpdateVariant(ref Material result, (string shader, bool head) key,
            Shader shader, int queue, Texture mask, bool tint)
        {
            Material source = result;
            Variant entry = variants.GetValue(source, _ => new Variant());
            lock (entry)
            {
                var sourceEngine = source.engineMaterial;
                int version = sourceEngine.Version;
                Texture mainTexture = source.mainTexture;
                if (!entry.materials.TryGetValue(key, out CachedMaterial state))
                {
                    state = new CachedMaterial();
                    entry.materials.Add(key, state);
                }
                Material output = state.material;
                // 同时核对原生对象身份、目标绑定和输出版本，保护延迟材质重绑定及其他模组的改写。
                if (state.initialized && output != null && ReferenceEquals(sourceEngine, state.sourceEngine)
                    && version == state.sourceVersion && ReferenceEquals(source.shader, state.sourceShader)
                    && ReferenceEquals(shader, state.targetShader) && ReferenceEquals(mainTexture, state.mainTexture)
                    && ReferenceEquals(mask, state.mask) && queue == state.queue
                    && ReferenceEquals(output.engineMaterial, state.outputEngine)
                    && output.engineMaterial.Version == state.outputVersion)
                {
                    result = output;
                    return;
                }
                if (output == null)
                {
                    output = new Material(source);
                    state.material = output;
                    generated.Add(output, generatedMarker);
                }
                else output.CopyPropertiesFromMaterial(source);
                // CopyPropertiesFromMaterial只添加关键词；源关闭关键词时也需同步删除旧值。
                // 该数组仅在源变化时获取，稳定帧不会走这里。
                output.shaderKeywords = source.shaderKeywords;
                output.shader = shader;
                if (tint)
                {
                    output.mainTexture = mainTexture;
                    output.SetTexture("_MaskTex", mask);
                    Color color = Harmony_FullGelatinizationBodyTint.FullGelColor;
                    color.a *= source.color.a;
                    output.color = color;
                }
                output.renderQueue = queue;
                // 记录复制前读到的源版本；源在复制过程中变化时，下一次调用会再次刷新，不吞更新。
                state.sourceEngine = sourceEngine; state.sourceVersion = version;
                state.sourceShader = source.shader; state.targetShader = shader;
                state.mainTexture = mainTexture; state.mask = mask; state.queue = queue;
                state.outputEngine = output.engineMaterial; state.outputVersion = state.outputEngine.Version;
                state.initialized = true;
                result = output;
            }
        }
    }
}
