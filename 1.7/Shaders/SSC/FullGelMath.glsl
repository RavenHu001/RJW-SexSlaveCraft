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
#if SSC_CSHARP
}
#endif
