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
    vec4 _EyeGelBaseColor; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    float _UseSkinMask; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _EyeMask;
layout(set = 2, binding = 3) uniform sampler2D _MaskTex;
void main()
{
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
    float eye = texture(_EyeMask, uv).r;
    vec3 gel = vec3(sscGelChannel(source.r, tint.r), sscGelChannel(source.g, tint.g), sscGelChannel(source.b, tint.b));
    vec4 baseColor = uMaterial._EyeGelBaseColor;
    vec4 modulation = vec4(sscGelEyeModulation(tint.r, baseColor.r), sscGelEyeModulation(tint.g, baseColor.g), sscGelEyeModulation(tint.b, baseColor.b), sscGelEyeModulation(tint.a, baseColor.a));
    vec3 original = source.rgb * uMaterial._EyeSourceColor.rgb * modulation.rgb;
    vec3 rgb = vec3(sscGelEyeBlend(gel.r, original.r, eye), sscGelEyeBlend(gel.g, original.g, eye), sscGelEyeBlend(gel.b, original.b, eye));
    float alpha = sscGelEyeBlend(sscGelAlpha(coverage, tint.a, vColor.a), sscGelAlpha(coverage, uMaterial._EyeSourceColor.a * modulation.a, vColor.a), eye);
    if (alpha <= 0.0) discard;
    outColor = vec4(rgb * vColor.rgb, alpha);
}
