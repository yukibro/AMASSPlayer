Shader "Custom/ColorCoded_2"
{
    Properties
    {
        _Color ("Defualt Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always
            SetTexture[_MainTex] { combine primary }
        }
        LOD 200

        CGPROGRAM
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
//#pragma exclude_renderers d3d11 gles
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            half4 myColor;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        half4 _coords[700];
        half4 _colors[700];
        int _numPoints = 0;



        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v, out Input o) {

            //find nearest and second nearest point in the list
            int nearest_idx = -1;
            int second_nearest_idx = -1;
            half distNearest = 1000000;
            half distSecondNearest = 1000000;
            if (_numPoints < 2) {
                o.myColor = _Color;
				return;
			}
            half3 gpos = mul(unity_ObjectToWorld, v.vertex).xyz;
            for (int i = 0; i < _numPoints; i++) {
				half dist = distance(gpos, _coords[i]);
                if (dist < distNearest) {
					second_nearest_idx = nearest_idx;
					nearest_idx = i;
					distSecondNearest = distNearest;
					distNearest = dist;
                }
                else if (dist < distSecondNearest) {
					second_nearest_idx = i;
					distSecondNearest = dist;
				}
			}

            //find the projected length relative to line that connects nearest and second nearest
            half3 nearest = _coords[nearest_idx];
            half3 second_nearest = _coords[second_nearest_idx];
            half3 lline = second_nearest - nearest;
            half3 ppoint = gpos - nearest;
            half projectedLength = dot(ppoint, lline) / dot(lline, lline);

            projectedLength = clamp(projectedLength, 0, 1);
            //color code accordingly

            half4 color = _colors[nearest_idx] * (1 - projectedLength) + _colors[second_nearest_idx] * projectedLength;
           
            o.myColor = color;
		}

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = IN.myColor;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    //FallBack "Diffuse"
}
