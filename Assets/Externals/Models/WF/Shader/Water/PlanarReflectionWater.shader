
Shader "Custom/URP/PlanarReflectionWater"
{
    Properties
    {
        _WaterTint ("Water Tint", Color) = (0.1, 0.3, 0.4, 0.7)
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.8

        _NormalMap ("Water Normal", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 0.05
        _WaveSpeed ("Wave Speed", Vector) = (0.02, 0.03, -0.01, 0.02)

        _ReflectionUVOffset ("Reflection UV Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PlanarReflectionTexture);
            SAMPLER(sampler_PlanarReflectionTexture);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterTint;
                float _ReflectionStrength;

                float _NormalScale;
                float4 _WaveSpeed;
                float4 _NormalMap_ST;

                float4 _ReflectionUVOffset;
                float4x4 _ReflectionVP;
                float _ReflectionYFlip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.uv =
                    TRANSFORM_TEX(input.uv, _NormalMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Reflection projection
                float4 reflectionClip =
                    mul(
                        _ReflectionVP,
                        float4(input.positionWS, 1)
                    );

                float2 reflectionUV =
                    reflectionClip.xy / reflectionClip.w * 0.5 + 0.5;

                if (_ReflectionYFlip > 0.5)
                {
                    reflectionUV.y = 1.0 - reflectionUV.y;
                }

                // Animated normal distortion
                float2 uv1 =
                    input.uv + _Time.y * _WaveSpeed.xy;

                float2 uv2 =
                    input.uv + _Time.y * _WaveSpeed.zw;

                half3 normal1 =
                    UnpackNormal(
                        SAMPLE_TEXTURE2D(
                            _NormalMap,
                            sampler_NormalMap,
                            uv1
                        )
                    );

                half3 normal2 =
                    UnpackNormal(
                        SAMPLE_TEXTURE2D(
                            _NormalMap,
                            sampler_NormalMap,
                            uv2
                        )
                    );

                float2 distortion =
                    (normal1.xy + normal2.xy) * _NormalScale;

                // Normal distortion + manual UV offset
                reflectionUV += distortion;
                reflectionUV += _ReflectionUVOffset.xy;

                // Reflection sampling
                half3 reflection =
                    SAMPLE_TEXTURE2D(
                        _PlanarReflectionTexture,
                        sampler_PlanarReflectionTexture,
                        reflectionUV
                    ).rgb;

                // Water color blending
                half3 waterColor =
                    lerp(
                        _WaterTint.rgb,
                        reflection,
                        _ReflectionStrength
                    );

                return half4(waterColor, _WaterTint.a);
            }

            ENDHLSL
        }
    }
}
