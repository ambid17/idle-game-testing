// Alpha-blended, unlit sprite shader for Atmosphere.ParallaxBackdrop's depth planes. Two things
// Sprites-Default can't do:
//  - Mine clip: a plane sits far behind the mine (z > 0), so what it covers on screen shifts with
//    the camera. Each fragment casts the camera ray back to the mine's own z = 0 plane and is
//    discarded unless it lands inside the mine (_MineRect: x min, x max, surface y) - so the
//    backdrop only ever shows through the mine, never above the surface or past its sides.
//    Sky planes (_SkyClip = 1) flip that: they only show above the surface, at any x.
//  - Edges (_EdgeMode):
//    0 = crossfade (sky bands): alpha ramps to 0 over _FadeLength world units at the plane's
//        _FadeTop / _FadeBottom edges, where the neighbouring band's plane overlaps it.
//    1 = seam (biomes): a hard cut at _FadeTop / _FadeBottom, offset by a pixel-stepped noise of
//        world x (_EdgeJag), seeded by the edge's own y. The planes on either side of a biome
//        boundary share that edge value, so their cuts interlock exactly - a jagged rock contact
//        rather than a double exposure. _EdgeShade darkens the plane toward the cut, a crevice
//        along the contact.
//  - World tiling (_TileRect.z > 0): the texture repeats from a world-space origin instead of
//    from the renderer's own tiling, so ParallaxBackdrop knows exactly where tile (and band)
//    rows land and can put biome cuts on the empty rows between formations. _TileClampY = 1
//    repeats only sideways: past the strip's top or bottom the edge row stretches on, for the
//    surface strips (soil wall, horizon hills) that must always reach the surface line.
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
        [PerRendererData] _SkyClip ("Sky Clip (1 = above the surface only)", Float) = 0
        [PerRendererData] _EdgeMode ("Edge Mode (0 = crossfade, 1 = seam)", Float) = 0
        [PerRendererData] _EdgeJag ("Seam Jag Amplitude", Float) = 0
        [PerRendererData] _EdgePixel ("Seam Pixel Size", Float) = 0.1
        [PerRendererData] _EdgeShade ("Seam Shade", Float) = 1
        [PerRendererData] _EdgeShadeLength ("Seam Shade Length", Float) = 1
        [PerRendererData] _TileRect ("World Tile (origin xy, size zw; z = 0 uses sprite UVs)", Vector) = (0, 0, 0, 0)
        [PerRendererData] _TileClampY ("Clamp World Tile Vertically", Float) = 0
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
            float _SkyClip;
            float _EdgeMode;
            float _EdgeJag;
            float _EdgePixel;
            float _EdgeShade;
            float _EdgeShadeLength;
            float4 _TileRect;
            float _TileClampY;

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float ValueNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), f) * 2.0 - 1.0;
            }

            // Where a seam edge at edgeY actually runs at world x: stepped to _EdgePixel so it
            // reads as pixel art. Seeded by edgeY so every boundary has its own profile.
            float SeamY(float edgeY, float x)
            {
                float qx = floor(x / _EdgePixel) * _EdgePixel;
                float seed = frac(edgeY * 0.0137) * 97.0;
                float n = 0.6 * ValueNoise(qx / 7.0 + seed) + 0.3 * ValueNoise(qx / 2.3 + seed * 1.7) + 0.1 * ValueNoise(qx / 0.8 + seed * 2.3);
                return edgeY + floor(n * _EdgeJag / _EdgePixel) * _EdgePixel;
            }

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
                float belowSurface = _MineRect.z - onMine.y;
                float insideMine = min(min(onMine.x - _MineRect.x, _MineRect.y - onMine.x), belowSurface);
                clip(_SkyClip > 0.5 ? -belowSurface : insideMine);

                float2 uv = IN.texcoord;
                if (_TileRect.z > 0)
                {
                    uv = (IN.worldPos.xy - _TileRect.xy) / _TileRect.zw;
                    // Clamp inside the edge texel rows (point filtering, repeat wrap).
                    uv = float2(frac(uv.x), _TileClampY > 0.5 ? clamp(uv.y, 0.0005, 0.9995) : frac(uv.y));
                }
                fixed4 c = SampleSpriteTexture(uv) * IN.color;
                float y = IN.worldPos.y;
                if (_EdgeMode > 0.5)
                {
                    float inside = min(SeamY(_FadeTop, IN.worldPos.x) - y, y - SeamY(_FadeBottom, IN.worldPos.x));
                    clip(inside);
                    c.rgb *= lerp(_EdgeShade, 1.0, saturate(inside / _EdgeShadeLength));
                }
                else
                {
                    c.a *= saturate((_FadeTop - y) / _FadeLength) * saturate((y - _FadeBottom) / _FadeLength);
                }
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
