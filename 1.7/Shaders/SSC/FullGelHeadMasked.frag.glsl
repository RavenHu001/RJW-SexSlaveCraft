#version 450
// AI 辅助生成。复用身体颜色变换，但保留 FA 眼睑/皮肤遮罩的 RGBA 覆盖关系。
#include "SSC/FullGelMath.glsl"
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec4 vInstColor;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    // 无个体覆盖时使用H272.5/S90/V68；皮肤Alpha仍为0.85，独立于游戏绘制调制。
    vec4 _GelColor; // @color @default = vec4(0.3995, 0.068, 0.68, 0.85)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _MaskTex;
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
    vec4 mask = texture(_MaskTex, uv);
    float alpha = sscGelMaskedAlpha(source.a, mask.a, tint.a * uMaterial._GelColor.a, vColor.a);
    if (alpha <= 0.0) discard;
    vec3 gel = vec3(sscGelChannel(source.r, tint.r * uMaterial._GelColor.r),
                    sscGelChannel(source.g, tint.g * uMaterial._GelColor.g),
                    sscGelChannel(source.b, tint.b * uMaterial._GelColor.b));
    // 遮罩 RGB 仍作调制，不把黑色遮罩误抬升成紫色覆盖；Alpha 为零的地方完全不画。
    outColor = vec4(gel * mask.rgb * vColor.rgb, alpha);
}
