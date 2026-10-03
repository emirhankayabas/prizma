// The whole backdrop in one opaque pass. The same layers Backdrop used to stack as separate
// full-screen images — the deep fade, the three light pools, the grid, the vignette and the
// grain — composited here in the same order with the same "source over" blend the UI used,
// sampling the same generated textures. One full-screen write instead of seven blended ones:
// on a weak phone GPU the stack cost more fill rate than the whole rest of the interface.
//
// Colours arrive from Material.SetColor, which converts them to linear the way the canvas
// converts vertex colours, and the blend happens in linear like the framebuffer's did.
Shader "PRIZMA/Backdrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Fade ("Fade", 2D) = "white" {}
        _Pool ("Pool", 2D) = "white" {}
        _Grid ("Grid", 2D) = "white" {}
        _Vignette ("Vignette", 2D) = "white" {}
        _Grain ("Grain", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _Fade, _Pool, _Grid, _Vignette, _Grain;

            float4 _Base;                       // the camera's clear colour, under everything
            float4 _Deep, _Glow, _Aura, _Floor; // layer colours, alpha included
            float4 _GridColor, _VignetteColor, _GrainColor;
            float4 _GlowRect, _AuraRect, _FloorRect; // xy: bottom-left corner, zw: size — canvas units
            float4 _Size;                       // xy: the backdrop's size in canvas units
            float4 _GrainTile;                  // xy: one grain tile, in canvas units

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // "Source over", as the UI blends: texture times colour, laid on by its alpha.
            float3 Over(float3 below, float4 layer)
            {
                return lerp(below, layer.rgb, layer.a);
            }

            float3 Pool(float3 below, float4 colour, float4 rect, float2 p)
            {
                float2 uv = (p - rect.xy) / rect.zw;
                // Outside its rectangle a pool draws nothing, as the image did.
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                float4 layer = tex2D(_Pool, saturate(uv)) * colour;
                layer.a *= inside;
                return Over(below, layer);
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * _Size.xy;
                float3 c = _Base.rgb;
                c = Over(c, tex2D(_Fade, i.uv) * _Deep);
                c = Pool(c, _Glow, _GlowRect, p);
                c = Pool(c, _Aura, _AuraRect, p);
                c = Pool(c, _Floor, _FloorRect, p);
                c = Over(c, tex2D(_Grid, i.uv) * _GridColor);
                c = Over(c, tex2D(_Vignette, i.uv) * _VignetteColor);
                c = Over(c, tex2D(_Grain, p / _GrainTile.xy) * _GrainColor);
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
}
