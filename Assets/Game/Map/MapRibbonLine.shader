Shader "LogistiCore/Map/RibbonLine"
{
    Properties
    {
        _SoftnessPixels ("Softness Pixels", Float) = 0.65
        _SmallRingWorld ("Small Ring World", Float) = 0.35
        _DetailOrtho ("Detail Ortho", Float) = 40
        _OrthoSize ("Ortho Size", Float) = 0
        _BorderNear ("Border Fade Near", Float) = 28
        _BorderFar ("Border Fade Far", Float) = 110
        _BorderFloor ("Border Fade Floor", Float) = 0.16
        _TurkeyMul ("Turkey Mul", Vector) = (0.82, 0.74, 0.76, 0.70)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _SoftnessPixels;
                float _SmallRingWorld;
                float _DetailOrtho;
                float _OrthoSize;
                float _BorderNear;
                float _BorderFar;
                float _BorderFloor;
                float4 _TurkeyMul;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 sideWidth : TEXCOORD0;
                float2 prevOS : TEXCOORD1;
                float2 nextOS : TEXCOORD2;
                float2 extentRole : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float side : TEXCOORD0;
                float width : TEXCOORD1;
                float extent : TEXCOORD2;
                float role : TEXCOORD3;
            };

            float2 ToPixel(float2 local)
            {
                float4 clipPos = TransformObjectToHClip(float3(local, 0));
                float w = abs(clipPos.w) < 1e-6 ? 1 : clipPos.w;
                float2 ndc = clipPos.xy / w;
                return (ndc * 0.5 + 0.5) * _ScreenParams.xy;
            }

            float2 Unit(float2 delta)
            {
                float len = length(delta);
                return len > 0.25 ? delta / len : float2(0, 0);
            }

            Varyings Vert(Attributes input)
            {
                float2 pos = input.positionOS.xy;
                float2 pixelPos = ToPixel(pos);
                float2 dirIn = Unit(pixelPos - ToPixel(input.prevOS));
                float2 dirOut = Unit(ToPixel(input.nextOS) - pixelPos);
                float2 normalIn = float2(-dirIn.y, dirIn.x);
                float2 normalOut = float2(-dirOut.y, dirOut.x);
                float hasIn = step(0.5, length(dirIn));
                float hasOut = step(0.5, length(dirOut));

                float2 miter = normalOut;
                float scale = 1;
                if (hasIn > 0.5 && hasOut < 0.5)
                    miter = normalIn;
                else if (hasIn > 0.5 && hasOut > 0.5)
                {
                    miter = normalIn + normalOut;
                    float miterLength = length(miter);
                    miter = miterLength > 1e-4 ? miter / miterLength : normalOut;
                    scale = 1.0 / max(dot(miter, normalOut), 0.35);
                }
                else if (hasIn < 0.5 && hasOut < 0.5)
                {
                    miter = float2(0, 1);
                    scale = 0;
                }

                float width = max(input.sideWidth.y, 0.5);
                float reach = width * 0.5 + max(_SoftnessPixels, 0.05);
                float2 pixelOffset = miter * input.sideWidth.x * reach * scale;
                float2 ndcOffset = float2(pixelOffset.x / _ScreenParams.x, pixelOffset.y / _ScreenParams.y) * 2.0;

                Varyings output;
                output.positionCS = TransformObjectToHClip(float3(pos, 0));
                output.positionCS.xy += ndcOffset * output.positionCS.w;
                output.color = input.color;
                output.side = input.sideWidth.x;
                output.width = width;
                output.extent = input.extentRole.x;
                output.role = input.extentRole.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float dist = abs(input.side);
                float halfWidth = input.width * 0.5;
                float outer = halfWidth + max(_SoftnessPixels, 0.05);
                float core = halfWidth / max(outer, 1e-3);
                float feather = smoothstep(core, 1.0, dist);
                float fade = 1;
                if (input.extent > 0 && input.extent < _SmallRingWorld)
                    fade = saturate((2.0 * _DetailOrtho - _OrthoSize) / max(_DetailOrtho, 0.001));

                // Role 1 is an international border. Coasts and the Türkiye outline stay.
                if (input.role > 0.5 && input.role < 1.5)
                {
                    float span = max(_BorderFar - _BorderNear, 0.001);
                    float t = saturate((_OrthoSize - _BorderNear) / span);
                    fade *= lerp(1.0, _BorderFloor, t);
                }

                float3 gamma = input.color.rgb;
                float alphaScale = 1;
                // Role 2 is the Türkiye outline. The multiplier softens the baked cyan.
                if (input.role > 1.5)
                {
                    gamma *= max(_TurkeyMul.rgb, float3(0, 0, 0));
                    alphaScale = saturate(_TurkeyMul.a);
                }

                float3 rgb = pow(saturate(gamma), 2.2);
                float alpha = input.color.a * alphaScale * (1 - feather) * fade;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
