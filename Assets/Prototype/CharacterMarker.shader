Shader "TurnBasedGame/CharacterMarker"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _InnerRadius ("Inner Radius", Range(0,1)) = 0
        _Softness ("Edge Softness", Range(0.001,0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _InnerRadius;
                float _Softness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                half mask = 1.0 - smoothstep(1.0 - _Softness, 1.0, radius);
                if (_InnerRadius > 0.0)
                    mask *= smoothstep(_InnerRadius, _InnerRadius + _Softness, radius);
                half alpha = _BaseColor.a * mask;
                clip(alpha - 0.0039);
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
