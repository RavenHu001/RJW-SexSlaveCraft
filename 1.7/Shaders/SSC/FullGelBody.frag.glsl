#version 450
// AI 辅助生成。1.7 原生 GLSL；身体、基础头部皮肤和 SA 附加节点共用相同变换。
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

void main()
{
    // 与引擎 Mote 顶点接口一致：实例绘制从实例颜色读取，普通绘制从材质读取。
// 两种绘制路径读取同一语义的本帧调制，凝胶色保留为材质独立uniform。
#ifdef INSTANCED
    vec4 tint = vInstColor;
#else
    vec4 tint = uMaterial._Color;
#endif
    vec4 source = texture(_MainTex, vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy);
    float alpha = sscGelAlpha(source.a, tint.a * uMaterial._GelColor.a, vColor.a);
    if (alpha <= 0.0)
        discard;

    // 先处理贴图 RGB，再乘顶点光照；保持原画细节，不把环境阴影当成黑描边提亮。
    vec3 gel = vec3(sscGelChannel(source.r, tint.r * uMaterial._GelColor.r),
                    sscGelChannel(source.g, tint.g * uMaterial._GelColor.g),
                    sscGelChannel(source.b, tint.b * uMaterial._GelColor.b));
    outColor = vec4(gel * vColor.rgb, alpha);
}
