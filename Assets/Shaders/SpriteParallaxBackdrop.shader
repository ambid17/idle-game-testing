// Alpha-blended, unlit sprite shader for Atmosphere.ParallaxBackdrop's depth planes. Two things
// Sprites-Default can't do:
//  - Mine clip: a plane sits far behind the mine (z > 0), so what it covers on screen shifts with
//    the camera. Each fragment casts the camera ray back to the mine's own z = 0 plane and is
//    discarded unless it lands inside the mine (_MineRect: x min, x max, surface y) - so the
//    backdrop only ever shows through the mine, never above the surface or past its sides.
//  - Biome crossfade: alpha ramps to 0 over _FadeLength world units at the plane's _FadeTop /
//    _FadeBottom edges, where the neighbouring biome's plane overlaps it.
// Built on Unity's sprite include so SpriteRenderer colour (_RendererColor) and Tiled draw mode
// work exactly like Sprites-Default.
Shader "Custom/SpriteParallaxBackdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _MineRect ("Mine Rect (xMin, xMax, surfaceY, mineZ)", Vector) = (0, 30, 1, 0)
        [PerRendererData] _FadeTop ("Fade Top Y", Float) = 100000
        [PerRendererData] _FadeBottom ("Fade Bottom Y", Float) = -100000
        [PerRendererData] _FadeLength ("Fade Length", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float4 _MineRect;
            float _FadeTop;
            float _FadeBottom;
            float _FadeLength;

            struct v2fBackdrop
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2fBackdrop vert(appdata_t IN)
            {
                v2f s = SpriteVert(IN);
                v2fBackdrop OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = s.vertex;
                OUT.color = s.color;
                OUT.texcoord = s.texcoord;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                return OUT;
            }

            fixed4 frag(v2fBackdrop IN) : SV_Target
            {
                // Where this fragment's camera ray crosses the mine plane.
                float3 cam = _WorldSpaceCameraPos;
                float t = (_MineRect.w - cam.z) / (IN.worldPos.z - cam.z);
                float2 onMine = cam.xy + (IN.worldPos.xy - cam.xy) * t;
                clip(min(min(onMine.x - _MineRect.x, _MineRect.y - onMine.x), _MineRect.z - onMine.y));

                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                float y = IN.worldPos.y;
                c.a *= saturate((_FadeTop - y) / _FadeLength) * saturate((y - _FadeBottom) / _FadeLength);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
