// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "Hidden/RATS-Node"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _XStretch ("X Stretch", Float) = 5.0
        _YStretch ("Y Stretch", Float) = 1.0
        _Roundness ("Roundness", Float) = 0.05
        _CullArea ("Cull Area", Vector) = (0,1,0,1)
        _SrcBlend ("SrcBlend", Int) = 5.0 // SrcAlpha
        _DstBlend ("DstBlend", Int) = 10.0 // OneMinusSrcAlpha
        _ZWrite ("ZWrite", Int) = 1.0 // On
        _ZTest ("ZTest", Int) = 4.0 // LEqual
        _Cull ("Cull", Int) = 0.0 // Off
        _ZBias ("ZBias", Float) = 0.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            Offset [_ZBias], [_ZBias]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            float sdRoundedBox( float2 p, float2 b, float4 r )
            {
                r.xy = (p.x>0.0)?r.xy : r.zw;
                r.x  = (p.y>0.0)?r.x  : r.y;
                float2 q = abs(p)-b+r.x;
                return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
            }
            float sdRoundedBoxSimple( float2 p, float2 b, float r )
            {
                return sdRoundedBox(p, b, float4(r,r,r,r));
            }

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f {
                fixed4 color : COLOR;
                float4 vertex : SV_POSITION;
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float4 _Color;
            float _XStretch;
            float _YStretch;
            float _Roundness;
            float4 _CullArea;
            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float2 uv = 0;
                uv = v.color.a == 0.1 ? float2(0, 0) : uv;
                uv = v.color.a == 0.2 ? float2(0, 1) : uv;
                uv = v.color.a == 0.3 ? float2(1, 1) : uv;
                uv = v.color.a == 0.4 ? float2(1, 0) : uv;
                uv.x *= _XStretch;
                uv.y *= _YStretch;
                o.uv.xy = uv;
                o.uv.zw = uv;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // if(i.vertex.x < _CullArea.x || i.vertex.x > _CullArea.y || i.vertex.y < _CullArea.z || i.vertex.y > _CullArea.w)
                    // discard;
                float sdf = sdRoundedBoxSimple(i.uv - float2(_XStretch/2, _YStretch/2), float2(_XStretch/2, _YStretch/2), _Roundness);
                // return float4(i.uv.x, i.uv.y, 0, 1);
                // return i.uv;
                if(any(i.color.rgb))
                    return sdf < 0 ? float4(i.color.rgb, 1) : 0;
                else
                {
                    // if(sdf < 0)
                        // return float4(i.color.rgb, 1);
                    // else
                        return lerp(float4(i.color.rgb, 1), float4(i.color.rgb, 0), smoothstep(-0.03, 0, sdf));
                }
                // return i.color;
            }
            ENDCG
        }
    }
}