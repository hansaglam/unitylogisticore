Shader "LogistiCore/Map/RegionalRelief"
{
    Properties
    {
        _MainTex ("Global Relief", 2D) = "white" {}
        _RegionalTex ("Regional Relief", 2D) = "black" {}
        _HaloTex ("Regional Halo", 2D) = "black" {}
        _RegionMin ("Region Min Lon Lat", Vector) = (25.269, 35.431, 0, 0)
        _RegionMax ("Region Max Lon Lat", Vector) = (45.217, 42.493, 0, 0)
        _HaloMin ("Halo Min Lon Lat", Vector) = (18.166666, 30.433333, 0, 0)
        _HaloMax ("Halo Max Lon Lat", Vector) = (52.3, 47.5, 0, 0)
        _Detail ("Detail", Range(0, 1)) = 1
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            TEXTURE2D(_RegionalTex);
            SAMPLER(sampler_RegionalTex);
            TEXTURE2D(_HaloTex);
            SAMPLER(sampler_HaloTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _RegionMin;
                float4 _RegionMax;
                float4 _HaloMin;
                float4 _HaloMax;
                float _Detail;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 global = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float lon = input.uv.x * 360.0 - 180.0;
                float lat = input.uv.y * 180.0 - 90.0;
                float2 span = max(_RegionMax.xy - _RegionMin.xy, float2(1e-4, 1e-4));
                float2 regionalUv = (float2(lon, lat) - _RegionMin.xy) / span;
                float inside = step(0.0, regionalUv.x) * step(regionalUv.x, 1.0) * step(0.0, regionalUv.y) * step(regionalUv.y, 1.0);
                half4 regional = SAMPLE_TEXTURE2D(_RegionalTex, sampler_RegionalTex, saturate(regionalUv));
                float2 haloSpan = max(_HaloMax.xy - _HaloMin.xy, float2(1e-4, 1e-4));
                float2 haloUv = (float2(lon, lat) - _HaloMin.xy) / haloSpan;
                float haloInside = step(0.0, haloUv.x) * step(haloUv.x, 1.0) * step(0.0, haloUv.y) * step(haloUv.y, 1.0);
                half4 halo = SAMPLE_TEXTURE2D(_HaloTex, sampler_HaloTex, saturate(haloUv));
                half copernicus = regional.a * (half)_Detail * (half)inside;
                half haloWeight = halo.a * (half)_Detail * (half)haloInside * (1.0h - regional.a);
                global.rgb = lerp(global.rgb, halo.rgb, haloWeight);
                global.rgb = lerp(global.rgb, regional.rgb, copernicus);
                return global;
            }
            ENDHLSL
        }
    }
}
