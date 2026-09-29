// Additive, unlit sprite/particle shader for Atmosphere glows (ore veins, critters, the Critter
// Shop lantern) and ambient particles. Built on Unity's own sprite include so SpriteRenderer's
// colour (passed via _RendererColor, not vertex colour, in Unity 6) and flipping work exactly like
// Sprites-Default; particles get their colour through vertex colour and use the material's own
// _MainTex. No LightMode tag, so URP draws it in its default unlit pass.
Shader "Custom/SpriteAdditiveGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        // Premultiplied additive: colour * alpha is added, so alpha still fades the glow.
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
