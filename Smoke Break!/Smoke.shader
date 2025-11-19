Shader "Custom/Smoke"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
    }
    SubShader
    {
        Name "Smoke Shader"
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 200

        ZWrite On
        ZTest LEqual

        CGPROGRAM
        #ifndef SMOKE_SHADER
        #define SMOKE_SHADER
        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"

        #pragma surface surf Custom fullforwardshadows addshadow

        // lighting quality
        #pragma target 3.0

        #define MAX_NUM_BALLS 40

        // Uniforms
        int _NumSmokeBalls;
        float _BallInnerRadii[MAX_NUM_BALLS];
        float _BallOuterRadii[MAX_NUM_BALLS];
        float4 _BallPositions[MAX_NUM_BALLS];

        fixed4 _Color;
        float4 _CenterPos;

		sampler2D _MainTex;
        sampler2D _NoiseTex;
        float2 _NoiseOffset;
        float _NoiseImpact;

        // Structures
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos : POSITION0;
        };

        struct SurfaceOutputCustom
        {
	        fixed3 Albedo;
	        fixed3 Normal;
	        fixed3 Emission;
	        half Specular;
	        fixed Alpha;
            float3 worldPos;
            float3 worldNormalDir;
        };

        struct ray
        {
            float3 origin;
            float3 direction;
        };

        struct rayData
        {
            float3 hitPoint;
            float3 normalDir;
			float2 uv;
        };

        const float pi = 3.14159265;

        float calcCurve(float input, float innerRadius, float outerRadius)
        {
            // Move into range 0-1
            float rangedInput = (input - innerRadius) / (outerRadius - innerRadius);
            // Clamp
            rangedInput = saturate(rangedInput);
            // Flip from 0-1 to 1-0
            float flip = 1.0f - rangedInput;
            // Square it for a sharp drop off
            return flip; //* flip;
        }

        half4 LightingCustom (SurfaceOutputCustom s, UnityGI gi)
        {
			// Technically this value is incorrect, I just can't figure out how to get the correct world space value
            half3 lightDir = gi.light.dir; 

            // Lambert
            half diffuse = max(dot (s.worldNormalDir, lightDir), 0.0f);
            //diffuse += 1.0f;
            //diffuse *= 0.5f;

            half4 c;
            c.rgb = s.Albedo * gi.light.color * diffuse;
            c.rgb += s.Albedo * gi.indirect.diffuse;
            c.a = s.Alpha;
            return c;
        }

        void LightingCustom_GI (SurfaceOutputCustom s, UnityGIInput data, inout UnityGI gi)
        {
            data.worldPos = s.worldPos;
			
            gi = UnityGlobalIllumination(data, 1.0, s.Normal);
        }

        void surf (Input IN, inout SurfaceOutputCustom o)
        {
            ray r;
            r.origin = IN.worldPos;
            r.direction = normalize(IN.worldPos - _WorldSpaceCameraPos.xyz);

            rayData rData;

            // Have to inline my raycast function so it knows it can only run in the fragment shader
            bool raycastResult = false;
            {
				#ifdef SHADOWS_DEPTH
                const float dt = 0.25f;
				const float totalDist = 3.0f;
				#else
				const float dt = 0.01f;
				const float totalDist = 1.0f;
				#endif

                for (float time = 0.0f; time < totalDist; time += dt) // upper cap on number of checks
                {
                    float3 pos = r.origin + r.direction * time;

                    // Apply noise
                    float2 noiseUV = float2((pos.xz - _CenterPos.xz) / 2.0f);
                    noiseUV += _NoiseOffset;
                    float4 expandedUV = float4(noiseUV, 0.0f, 1.0f);
                    float scalar = (tex2Dlod (_NoiseTex, expandedUV)).r;

                    // Center around 1
                    scalar *= _NoiseImpact;
                    scalar += 1.0f - (_NoiseImpact / 2.0f);

                    float sum = 0.0f;
                    for (int j = 0; j < _NumSmokeBalls; j++)
                    {
                        float3 ballToPos = pos - _BallPositions[j];
                        float dist = length(ballToPos);
                        dist *= scalar;
                        dist = calcCurve(dist, _BallInnerRadii[j], _BallOuterRadii[j]); // move dist into range 0-1, where 0 is farthest and 1 is closest
                        sum += dist;
                    }

                    if (sum >= 1.0f)
                    {
                        // Do the loop one final time for our final point, filling in the actual normal data
                        float3 normal = float3(0.0f, 0.0f, 0.0f);
                        for (int j = 0; j < _NumSmokeBalls; j++)
                        {
                            float3 ballToPos = pos - _BallPositions[j];
                            float dist = length(ballToPos);
                            float ratio = calcCurve(dist, _BallInnerRadii[j], _BallOuterRadii[j]); // move dist into range 0-1, where 0 is farthest and 1 is closest
                        
                            normal += normalize(ballToPos) * ratio;
                        }

                        rData.hitPoint = pos;
                        rData.normalDir = normal;
						rData.uv = noiseUV;
                        raycastResult = true;
                        break;
                    }
                }
            }

            if (!raycastResult)
            {
                discard;
            }

            // Update the position and normal of the surface
            // Bypass unity's transformations of the normal by passing through our own
            o.Normal = fixed3(0.0f, 0.0f, 1.0f);
            o.worldNormalDir = rData.normalDir;
            o.worldPos = rData.hitPoint;

            fixed4 c = _Color * tex2D(_MainTex, rData.uv);
            o.Albedo = c.rgb;
            
            // Simple fade by distance to center: 
            o.Alpha = 1.0f;//lerp(0.5f, 1.0f, smoothstep(8.0f, 0.0f, distance(_CenterPos.xyz, rData.hitPoint)));
        }

        #endif
        ENDCG
    }
}
