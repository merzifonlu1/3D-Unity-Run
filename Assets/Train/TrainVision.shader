// Tren surulurken gorulmeyen yerleri karartan tam ekran gecis.
// URP renderer'daki "Train Vision" Full Screen Pass Renderer Feature bu shader'i kullanir.
// Degerleri TrainVision.cs global olarak verir.
Shader "Train/Vision Overlay"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TrainVision"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _TrainVisionOn;
            float4x4 _TrainVisionWorldToLocal;
            float4 _TrainVisionHull;      // xy: ic alanin yari genisligi / yari uzunlugu
            float4 _TrainVisionOrigin;    // xy: surucunun yerel x/z'si, z: gorus mesafesi, w: aci sayisi
            float4 _TrainVisionSectors[4]; // x: merkez acisi (radyan), y: yari genislik (radyan)

            static const float EdgeSoftness = 0.06; // radyan
            static const float HullMargin = 0.05;   // metre

            float Visibility(float3 worldPos, bool isSky)
            {
                float3 local = mul(_TrainVisionWorldToLocal, float4(worldPos, 1.0)).xyz;

                // Trenin kendisi her zaman gorunur.
                if (!isSky && abs(local.x) <= _TrainVisionHull.x + HullMargin && abs(local.z) <= _TrainVisionHull.y + HullMargin)
                    return 1.0;

                int count = (int)_TrainVisionOrigin.w;
                if (isSky || count == 0)
                    return 0.0;

                float2 offset = local.xz - _TrainVisionOrigin.xy;
                float angle = atan2(offset.x, offset.y); // 0 = on, +pi/2 = sag
                float visible = 0.0;
                for (int i = 0; i < 4; i++)
                {
                    if (i >= count)
                        break;
                    float diff = abs(angle - _TrainVisionSectors[i].x);
                    diff = min(diff, 2.0 * PI - diff);
                    float halfWidth = _TrainVisionSectors[i].y;
                    visible = max(visible, 1.0 - smoothstep(halfWidth - EdgeSoftness, halfWidth, diff));
                }

                float range = _TrainVisionOrigin.z;
                visible *= 1.0 - smoothstep(range * 0.8, range, length(offset));
                return visible;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_TrainVisionOn <= 0.0)
                    return half4(0, 0, 0, 0);

                float2 uv = input.texcoord;
                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool isSky = depth <= 0.0;
                #else
                    bool isSky = depth >= 1.0;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                float darkness = (1.0 - Visibility(worldPos, isSky)) * _TrainVisionOn;
                return half4(0.02, 0.02, 0.03, darkness);
            }
            ENDHLSL
        }
    }
}
