// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "CaronteFX/Standard VA (double sided)" {
	Properties {
    _Color("Color", Color) = (1,1,1,1)
    [NoScaleOffset] _MainTex("Albedo (RGB)", 2D) = "white" {}
	[NoScaleOffset] _SpecGlossMap("SpecSmooth", 2D) = "black" {}
	_Glossiness("Smoothness Scale", Range(0,2)) = 1.0
	[NoScaleOffset] _OcclusionMap("Occlusion", 2D) = "white" {}
	_OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
	_BumpScale("Scale", Float) = 1.0
    [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
    _Cutoff("Alpha Cutoff", Range(0.01,1)) = 0.5
	[HideInInspector] _PositionsTex("Positions", 2D) = "white" {}
	[HideInInspector] _NormalsTex("Normals", 2D) = "white" {}
	}

	SubShader {
      Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
      Blend SrcAlpha OneMinusSrcAlpha
      LOD 200
      ZWrite Off
      Cull Off
  
      Pass{
          ColorMask 0
          ZWrite On

          CGPROGRAM
          #pragma vertex vert
          #pragma fragment frag 

          // Use shader model 4.0 target for SV_VertexId
          #pragma target 4.0
          
          #include "UnityCG.cginc"
          
          struct v2f {
            float4 vertex : SV_POSITION;
            float2 texcoord : TEXCOORD0;
            
          };
          
          sampler2D _MainTex;
          fixed _Cutoff;

          sampler2D _PositionsTex;
          float4    _PositionsTex_TexelSize;
          float _useSampler = 0.0;

          float3 getFloat3FromTexture(uint vid, sampler2D texSampler, float4 texSizeInfo) 
          {
            float offU = texSizeInfo.x * 0.5;
            float offV = texSizeInfo.y * 0.5;

            float s = fmod(vid, texSizeInfo.z) * texSizeInfo.x + offU;
            float t = floor(vid / texSizeInfo.z) * texSizeInfo.y + offV;

            float4 texelId = float4(s, t, 0, 0);
            return tex2Dlod(texSampler, texelId);
          }
          
          v2f vert(appdata_img v, uint vid : SV_VertexId)
          {
            if (_useSampler)
            {
              v.vertex.xyz = getFloat3FromTexture(vid, _PositionsTex, _PositionsTex_TexelSize);
            }
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.texcoord = v.texcoord;
            return o;
          }
          
          fixed4 frag(v2f i) : SV_Target
          {
            fixed4 col = tex2D(_MainTex, i.texcoord);
            clip(col.a - _Cutoff);
            return 0;
          }
          ENDCG
      }

      Pass
      {
        Tags{ "LightMode" = "ShadowCaster" }
        ZWrite On
        Cull Off

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_shadowcaster

        // Use shader model 4.0 target for SV_VertexId
        #pragma target 4.0

        #include "UnityCG.cginc"
        
        struct v2f {
          V2F_SHADOW_CASTER;
          float2 texcoord : TEXCOORD1;
        };

        float3 getFloat3FromTexture(uint vid, sampler2D texSampler, float4 texSizeInfo)
        {
          float offU = texSizeInfo.x * 0.5;
          float offV = texSizeInfo.y * 0.5;

          float s = fmod(vid, texSizeInfo.z) * texSizeInfo.x + offU;
          float t = floor(vid / texSizeInfo.z) * texSizeInfo.y + offV;

          float4 texelId = float4(s, t, 0, 0);
          return tex2Dlod(texSampler, texelId);
        }

        sampler2D _PositionsTex;
        float4    _PositionsTex_TexelSize;
        float _useSampler = 0.0;
        
        v2f vert(appdata_base v, uint vid : SV_VertexId)
        {
          if (_useSampler)
          {
            v.vertex.xyz = getFloat3FromTexture(vid, _PositionsTex, _PositionsTex_TexelSize), 1.0;
          }

          v2f o;
          TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
          o.texcoord = v.texcoord;
          return o;
        }
        
        sampler2D _MainTex;
        fixed _Cutoff;
        
        float4 frag(v2f i) : SV_Target
        {
          fixed4 col = tex2D(_MainTex, i.texcoord);
          clip(col.a - _Cutoff);
          SHADOW_CASTER_FRAGMENT(i)
        }
        ENDCG
      }
	
		  CGPROGRAM
      #pragma surface surf StandardSpecular fullforwardshadows alpha:fade nolightmap vertex:vert
		  
      // Use shader model 4.0 target for SV_VertexId
		  #pragma target 4.0

      struct vertex_input {
        float4 vertex : POSITION;
        float4 tangent : TANGENT;
        float3 normal : NORMAL;
        float4 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
        float4 texcoord2 : TEXCOORD2;
        uint vid : SV_VertexId;
      };

      struct Input {
        float2 uv_MainTex;
        fixed facing : VFACE;
      };

      sampler2D _MainTex;
      sampler2D _SpecGlossMap;
      sampler2D _BumpMap;

	  half _Glossiness;
	  fixed4 _Color;
	  half _BumpScale;
	  half _OcclusionStrength;
	  sampler2D _OcclusionMap;
      fixed _Cutoff;

      sampler2D _PositionsTex;
      sampler2D _NormalsTex;
      float _useSampler = 0.0;

      float4    _PositionsTex_TexelSize;
      float4    _NormalsTex_TexelSize;

      float3 getFloat3FromTexture(uint vid, sampler2D texSampler, float4 texSizeInfo) 
      {
        float offU = texSizeInfo.x * 0.5;
        float offV = texSizeInfo.y * 0.5;

        float s = fmod(vid, texSizeInfo.z) * texSizeInfo.x + offU;
        float t = floor(vid / texSizeInfo.z) * texSizeInfo.y + offV;

        float4 texelId = float4(s, t, 0, 0);
        return tex2Dlod(texSampler, texelId);
      }

      void vert(inout vertex_input v) 
      { 
        if (_useSampler)
        {
          v.vertex.xyz = getFloat3FromTexture(v.vid, _PositionsTex, _PositionsTex_TexelSize);
          v.normal = getFloat3FromTexture(v.vid, _NormalsTex, _NormalsTex_TexelSize);
        }
      }

		  void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        o.Albedo = c.rgb;
        o.Alpha = saturate(c.a / _Cutoff);
		float4 sg = tex2D(_SpecGlossMap, IN.uv_MainTex);
		o.Specular = sg.rgb;
		o.Smoothness = saturate(sg.a * _Glossiness);
		o.Occlusion = LerpOneTo(tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);
        o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);

        if (IN.facing < 0.5)
          o.Normal *= -1.0;
		  }
		  ENDCG
	}
	FallBack "Diffuse"
}
