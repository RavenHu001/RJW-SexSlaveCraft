#version 450
// AI 辅助生成。保留一张原版头图/一次绘制；保护眼睛原色和Alpha，皮肤沿用已验收配置。
#include "SSC/FullGelMath.glsl"
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec4 vInstColor;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _EyeSourceColor; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // 无个体覆盖时使用H272.5/S90/V68；皮肤Alpha仍为0.85，独立于游戏绘制调制。
    vec4 _GelColor; // @color @default = vec4(0.3995, 0.068, 0.68, 0.85)
    float _UseSkinMask; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _EyeMask;
layout(set = 2, binding = 3) uniform sampler2D _MaskTex;
void main()
{
// 两种绘制路径读取同一语义的本帧调制，凝胶色保留为材质独立uniform。
#ifdef INSTANCED
    vec4 tint = vInstColor;
#else
    vec4 tint = uMaterial._Color;
#endif
    vec2 uv = vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy;
    vec4 source = texture(_MainTex, uv);
    float skinMask = uMaterial._UseSkinMask > 0.5 ? texture(_MaskTex, uv).a : 1.0;
    float coverage = source.a * skinMask;
    // 此变体保留原材质不写深度的行为。
    // 眼部遮罩来自当前源头图；保持同一UV和一次绘制，不新增图层或改变头型。
    float eye = texture(_EyeMask, uv).r;
    vec3 gel = vec3(sscGelChannel(source.r, tint.r * uMaterial._GelColor.r), sscGelChannel(source.g, tint.g * uMaterial._GelColor.g), sscGelChannel(source.b, tint.b * uMaterial._GelColor.b));
    // 原眼睛直接响应游戏绘制调制；不除以凝胶颜色，允许纯色、黑色与零不透明度。
    vec3 original = vec3(sscGelOriginalChannel(source.r, uMaterial._EyeSourceColor.r, tint.r), sscGelOriginalChannel(source.g, uMaterial._EyeSourceColor.g, tint.g), sscGelOriginalChannel(source.b, uMaterial._EyeSourceColor.b, tint.b));
    vec3 rgb = vec3(sscGelEyeBlend(gel.r, original.r, eye), sscGelEyeBlend(gel.g, original.g, eye), sscGelEyeBlend(gel.b, original.b, eye));
    // 只对皮肤分支乘凝胶不透明度；眼睛沿用源Alpha及游戏淡出。
    float alpha = sscGelEyeBlend(sscGelAlpha(coverage, tint.a * uMaterial._GelColor.a, vColor.a), sscGelAlpha(coverage, tint.a, vColor.a), eye);
    // 完全透明片元丢弃，写深度变体也不会留下不可见皮肤的遮挡。
    if (alpha <= 0.0) discard;
    outColor = vec4(rgb * vColor.rgb, alpha);
}
