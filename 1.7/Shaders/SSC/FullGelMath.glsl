// AI 辅助生成。标量函数同时用于 GLSL 和回归测试；不采样邻居、不扩大贴图轮廓。
// 测试直接编译此文件的相同函数体，避免用另一份 CPU 公式证明自身正确。
#if SSC_CSHARP
internal static class GelShaderMath
{
    private static float clamp(float value, float min, float max) => System.Math.Clamp(value, min, max);
    public static
#endif
// 保留原贴图的明暗顺序，将黑线抬为深紫色。暗部强度不与透明度绑定。
float sscGelChannel(float source, float tint)
{
    float detail = clamp(source, 0.0f, 1.0f);
    float softened = (0.34f + 0.66f * detail) * clamp(tint, 0.0f, 1.0f);
    // 仅在贴图已有亮部增加柔和光泽，不叠加噪声、假轮廓或位置无关的白边。
    float shine = clamp((detail - 0.65f) / 0.35f, 0.0f, 1.0f);
    shine = shine * shine * (3.0f - 2.0f * shine);
    return softened + (1.0f - softened) * shine * 0.12f;
}

// 保留原贴图抗锯齿与孔洞：透明像素始终透明，不因 RGB 暗部抬升而出现矩形背景。
#if SSC_CSHARP
    public static
#endif
float sscGelAlpha(float sourceAlpha, float tintAlpha, float vertexAlpha)
{
    return clamp(sourceAlpha, 0.0f, 1.0f) * clamp(tintAlpha, 0.0f, 1.0f) * clamp(vertexAlpha, 0.0f, 1.0f);
}
// 遮罩在柔化 RGB 之外保留覆盖率；只有真实皮肤区域参与绘制，不填补眼部孔洞。
#if SSC_CSHARP
    public static
#endif
float sscGelMaskedAlpha(float sourceAlpha, float maskAlpha, float tintAlpha, float vertexAlpha)
{
    return sscGelAlpha(sourceAlpha, tintAlpha, vertexAlpha) * clamp(maskAlpha, 0.0f, 1.0f);
}

// 深度皮肤遵循原 CutoutSkin 的 0.1 覆盖阈值；与凝胶不透明度独立。
#if SSC_CSHARP
    public static
#endif
bool sscGelWritesDepth(float coverage)
{
    return coverage >= 0.1f;
}
// 原版眼睛仍在原头图上采样。眼睛遮罩只选择原色/原Alpha，皮肤沿用既有凝胶函数。
#if SSC_CSHARP
    public static
#endif
float sscGelEyeBlend(float gel, float original, float eye)
{
    float weight = clamp(eye, 0.0f, 1.0f);
    return gel * (1.0f - weight) + original * weight;
}
// 原五官直接使用原色与本帧调制，凝胶参数独立；零通道/零Alpha也不需要做反除。
#if SSC_CSHARP
    public static
#endif
float sscGelOriginalChannel(float source, float sourceTint, float drawTint)
{
    return source * sourceTint * drawTint;
}
#if SSC_CSHARP
}
#endif
