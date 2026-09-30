// Additive diagonal shine line for Atmosphere.OreGlowLayer's vein glints. Every hidden cell of a
// vein draws a plain square sprite with this shader; brightness comes from the fragment's world
// position relative to a line (x + y = _Sweep, running top-left to bottom-right) that the
// layer slides from the vein's bottom-left to its top-right. The line is worked out in world
// space, so it runs unbroken across neighbouring cells while staying clipped to the vein's
// own cells. The sprite texture is ignored.
Shader "Custom/OreShine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _Sweep ("Sweep (world x + y of the line)", Float) = 0
        [PerRendererData] _LineWidth ("Line half-width (world units)", Float) = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            float _Sweep;
            float _LineWidth;

            struct shine_v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            shine_v2f vert(appdata_t IN)
            {
                shine_v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 position = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(position);
                OUT.worldPos = mul(unity_ObjectToWorld, position).xy;
                OUT.color = IN.color * _Color * _RendererColor;
                return OUT;
            }

            fixed4 frag(shine_v2f IN) : SV_Target
            {
                // Perpendicular distance to the line, in world units.
                float d = (IN.worldPos.x + IN.worldPos.y - _Sweep) * 0.70710678;
                float core = exp(-(d * d) / (_LineWidth * _LineWidth));
                float halo = 0.35 * exp(-(d * d) / (9.0 * _LineWidth * _LineWidth));
                float a = saturate(core + halo) * IN.color.a;
                return fixed4(IN.color.rgb * a, a);
            }
            ENDCG
        }
    }
}
