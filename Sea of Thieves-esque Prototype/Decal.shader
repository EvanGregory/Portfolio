Shader "Unlit/Decal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest"}
        LOD 100

        Pass
        {
			Tags { "LightMode"="ForwardBase" }
			ZWrite Off
			ZTest Off
			Cull Off
			AlphaToMask On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap

            #include "UnityCG.cginc"
			#include "Lighting.cginc"
			
			#include "AutoLight.cginc"

            struct appdata
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
				float4 worldPos : POSITION1;
				float4 screenPos : TEXCOORD0;
				half4 ambient : COLOR0;
				half4 diffuse : COLOR1;
				LIGHTING_COORDS(1,2)
            };

			sampler2D _CameraDepthTexture; // Unity sets this

            sampler2D _MainTex;
            float4 _MainTex_ST;

			#define AABB_MIN float3(-0.5f, -0.5f, -0.5f)
			#define AABB_MAX float3(0.5f, 0.5f, 0.5f)

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.position.xyz);
				o.screenPos = ComputeScreenPos(o.pos);
				o.worldPos = mul(unity_ObjectToWorld, float4(v.position.xyz, 1.0f));

				// Per vertex lighting
				float3 worldNormal = UnityObjectToWorldNormal(float3(0, 1, 0));

				// Ambient + spherical harmonics
				o.ambient = half4(ShadeSH9(half4(worldNormal, 1.0f)), 1.0) / 1.2f;

				// Phong for primary light (we dont have accurate normals, so... flat shade?)
				o.diffuse = half4(_LightColor0.rgb, 1.0);
				
				TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float3 cameraToPos = i.worldPos.xyz - _WorldSpaceCameraPos.xyz;
				float3 cameraDir = normalize(cameraToPos);

				float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float terrainZValue = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV));
                float terrainDepth = terrainZValue / dot(cameraDir, unity_WorldToCamera._m20_m21_m22);

				float4 finalWorldPos = float4(_WorldSpaceCameraPos.xyz + cameraDir * terrainDepth, 1.0f);

				float4 modelSpacePos = mul(unity_WorldToObject, finalWorldPos);

				// Clip if modelSpacePos is outside the AABB
				if (modelSpacePos.x > AABB_MAX.x
					|| modelSpacePos.x < AABB_MIN.x
					|| modelSpacePos.y > AABB_MAX.y
					|| modelSpacePos.y < AABB_MIN.y
					|| modelSpacePos.z > AABB_MAX.z
					|| modelSpacePos.z < AABB_MIN.z)
				{
					discard;
				}

				float2 uv = modelSpacePos.xz;
				uv += 0.5f; // shift from -.5 - .5 to 0 - 1

                fixed4 texColor = tex2D(_MainTex, uv);
				float attenuation = LIGHT_ATTENUATION(i);

				fixed3 color = texColor.xyz;
				fixed3 lightColor = i.ambient + i.diffuse * attenuation;

				color *= lightColor;
                return fixed4(color, texColor.a);
            }
            ENDCG
        }

		Pass
		{
			Tags { "LightMode"="ForwardAdd" }
			ZWrite Off
			ZTest Off
			Cull Off
			AlphaToMask On

			CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_fwdadd nolightmap nodirlightmap nodynlightmap

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			
			#include "AutoLight.cginc"

            struct appdata
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
				float4 worldPos : POSITION1;
				float4 screenPos : TEXCOORD0;
				half4 ambient : COLOR0;
				half4 diffuse : COLOR1;
				LIGHTING_COORDS(1,2)
            };

			sampler2D _CameraDepthTexture; // Unity sets this

            sampler2D _MainTex;
            float4 _MainTex_ST;

			#define AABB_MIN float3(-0.5f, -0.5f, -0.5f)
			#define AABB_MAX float3(0.5f, 0.5f, 0.5f)

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.position.xyz);
				o.screenPos = ComputeScreenPos(o.pos);
				o.worldPos = mul(unity_ObjectToWorld, float4(v.position.xyz, 1.0f));

				// Per vertex lighting
				float3 worldNormal = UnityObjectToWorldNormal(float3(0, 1, 0));

				// Ambient + spherical harmonics
				o.ambient = half4(ShadeSH9(half4(worldNormal, 1.0f)), 1.0) / 1.2f;

				// Phong for primary light (we dont have accurate normals, so... flat shade?)
				o.diffuse = half4(1.0f, 1.0f, 1.0f, 1.0f);
				
				//TRANSFER_VERTEX_TO_FRAGMENT(o); // prob dont need this, lighting coords seem correct?
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				float3 cameraToPos = i.worldPos.xyz - _WorldSpaceCameraPos.xyz;
				float3 cameraDir = normalize(cameraToPos);

				float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float terrainZValue = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV));
                float terrainDepth = terrainZValue / dot(cameraDir, unity_WorldToCamera._m20_m21_m22);

				float4 finalWorldPos = float4(_WorldSpaceCameraPos.xyz + cameraDir * terrainDepth, 1.0f);

				float4 modelSpacePos = mul(unity_WorldToObject, finalWorldPos);

				// Clip if modelSpacePos is outside the AABB
				if (modelSpacePos.x > AABB_MAX.x
					|| modelSpacePos.x < AABB_MIN.x
					|| modelSpacePos.y > AABB_MAX.y
					|| modelSpacePos.y < AABB_MIN.y
					|| modelSpacePos.z > AABB_MAX.z
					|| modelSpacePos.z < AABB_MIN.z)
				{
					discard;
				}

				float2 uv = modelSpacePos.xz;
				uv += 0.5f; // shift from -.5 - .5 to 0 - 1

                fixed4 texColor = tex2D(_MainTex, uv);
				float attenuation = LIGHT_ATTENUATION(i);

				fixed3 color = texColor.xyz;
				fixed3 lightColor = i.ambient + i.diffuse * attenuation;

				color *= lightColor;
                return fixed4(color, texColor.a);
            }

			ENDCG
		}
    }
}
