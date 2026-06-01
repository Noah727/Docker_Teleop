Shader "Custom/XRayTransparentHandle"
{
    Properties
    {
        _Color ("Color", Color) = (0.1, 0.72, 1.0, 0.88)
        _OccludedAlphaMultiplier ("Occluded Alpha Multiplier", Range(0.0, 1.0)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Occluded"
            ZTest Greater

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            fixed4 _Color;
            float _OccludedAlphaMultiplier;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = _Color;
                color.a *= _OccludedAlphaMultiplier;
                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "Visible"
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
            };

            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
