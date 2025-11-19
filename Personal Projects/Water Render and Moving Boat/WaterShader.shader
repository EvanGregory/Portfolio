
Shader "Custom/WaterShader"
{
    Properties
    {
        _TopColor ("Wave Top Color", Color) = (1,1,1,1)
        _BottomColor ("Wave Bottom Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Specular ("Specular", Range(0, 1)) = 1.0
        _Thickness ("Liquid Thickness", Float) = 10.0
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.5
        _Cube ("Cube Map", CUBE) = "" {}
    }
    SubShader
    {
        Pass
        {
            Tags {"Queue"="Transparent" "LightMode"="ForwardBase"}
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM

            //#pragma surface surf Custom vertex:vert
            #pragma vertex vert
            #pragma fragment frag //fullforwardshadows addshadow (I think these terms are only for surface shaders)
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"

            #define eConst 2.7182818284f

            struct vertIn
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct vertToFrag
            {
                float2 uv : TEXCOORD0; // texture coordinate
                float4 pos : SV_POSITION; // clip space position
                float4 screenPos : TEXCOORD1;
                float4 worldPos : POSITION1;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_SHADOW_COORDS(2)
            };

            sampler2D _CameraDepthTexture; // Unity sets this
            
            sampler2D _MainTex;
            float _Specular;
            float _MinAlpha;
            float4 _TopColor;
            float4 _BottomColor;
            float _Thickness;
            samplerCUBE _Cube;

            // Wave uniforms
            #define MAX_NUM_WAVES 32
            int _NumWaves;
            float2 _WaveDirs[MAX_NUM_WAVES];
            float _WaveSpeeds[MAX_NUM_WAVES];
            float _WavePeriod;
            float _WaveAmplitude;
            float _WaveDeltaPeriod;
            float _WaveDeltaAmplitude;

			// was 0.36787f
            #define MAGIC_SHIFT 0.42f
            
            vertToFrag vert (vertIn v) 
            {
                float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0f)).xyz;

                float waveValue = 0.0f;
                float2 prevDeriv = float2(0.0f, 0.0f);
                float currentPeriod = _WavePeriod;
                float currentAmplitude = _WaveAmplitude;
                for (int i = 0; i < MAX_NUM_WAVES; i++)
                {
                    if (i == _NumWaves)
                    {
                        break;
                    }

                    float temp = dot(worldPos.xz + saturate(prevDeriv / 5.0f), _WaveDirs[i]) * currentPeriod + _Time.y * _WaveSpeeds[i];
                    float sineVal = sin(temp) * currentAmplitude;
                    float ePower = pow(eConst, sineVal - 1.0f);
                    waveValue += ePower;

                    float cosVal = cos(temp) * currentAmplitude;
                    float slope = ePower * cosVal;
                    prevDeriv = _WaveDirs[i] * slope;

                    currentAmplitude = currentAmplitude * _WaveDeltaAmplitude;
                    currentPeriod = currentPeriod * _WaveDeltaPeriod;
                }

                float4 posOut = v.vertex;
                posOut.y += (waveValue - MAGIC_SHIFT * _NumWaves);
                
                vertToFrag o;

                o.worldPos = float4(worldPos, 1.0f);
                o.pos = UnityObjectToClipPos(posOut);
                o.screenPos = ComputeScreenPos(o.pos);
                o.uv = v.uv;
                o.color = lerp(_BottomColor, _TopColor, saturate(waveValue));
                return o;
            }

            fixed4 frag (vertToFrag IN) : SV_Target
            {
                float2 sumDeriv = float2(0.0f, 0.0f);
                float2 prevDeriv = float2(0.0f, 0.0f);
                float currentPeriod = _WavePeriod;
                float currentAmplitude = _WaveAmplitude;
                for (int i = 0; i < MAX_NUM_WAVES; i++)
                {
                    if (i == _NumWaves)
                    {
                        break;
                    }
                
                    float temp = dot(IN.worldPos.xz + saturate(prevDeriv / 5.0f), _WaveDirs[i]) * currentPeriod + _Time.y * _WaveSpeeds[i]; 
                    float sineVal = sin(temp) * currentAmplitude;
                    float ePower = pow(eConst, sineVal - 1.0f);
                
                    float cosVal = cos(temp) * currentAmplitude;
                    float slope = ePower * cosVal;
                    prevDeriv = _WaveDirs[i] * slope;
                
                    sumDeriv += prevDeriv;
                
                    currentAmplitude *= _WaveDeltaAmplitude;
                    currentPeriod *= _WaveDeltaPeriod;
                }
                
                float3 tangent = float3(1.0f, sumDeriv.x, 0.0f);
                float3 biTangent = float3(0.0f, sumDeriv.y, 1.0f);
                float3 normal = normalize(cross(biTangent, tangent));

                // -- Lighting --
                float3 posToCamera = _WorldSpaceCameraPos.xyz - IN.worldPos.xyz;
                float3 viewDir = normalize(posToCamera);
                float3 lightDir = normalize(UnityWorldSpaceLightDir(IN.worldPos.xyz));
                float3 reflectedDir = 2.0f * normal * dot(viewDir, normal) - viewDir;
                float3 halfwayDir = normalize(viewDir + lightDir);

                float fresnel = pow(1.0f - max(dot(viewDir, normal), 0.0f), 5.0f);
                
                fixed4 skyColor = texCUBE(_Cube, reflectedDir);
                
                // Water depth
                // Depth ranges from 0-1 where 0 is thin water and 1 is when water is >= _Thickness thick 
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float terrainZValue = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV));
                float terrainDepth = terrainZValue / dot(normalize(-posToCamera), unity_WorldToCamera._m20_m21_m22);
                float waterCameraDepth = length(posToCamera);
                float waterDepth = terrainDepth - waterCameraDepth;
                float depth = saturate(waterDepth / _Thickness);

                // Diffuse
                float diffuse = (dot(lightDir, normal) + 1.0f) * 0.5f;
                fixed4 diffuseColor = lerp(_TopColor, _BottomColor, depth) * _LightColor0 * diffuse;

                // Specular
                float halfDot = max(dot(normal, halfwayDir), 0.0f);
                float specular = pow(halfDot, 80.0f);
                fixed4 specularColor = _LightColor0 * _Specular * specular;

                // Finalize color
                fixed4 c = fixed4(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w, 1.0f);
                c += diffuseColor;
                c += specularColor;
                c += skyColor * (fresnel * 0.6f);

                // Recieve Shadows (does not work)
                float attenuation = LIGHT_ATTENUATION(IN);
                c *= attenuation;

                c.a = (depth * (1.0f - _MinAlpha)) + _MinAlpha;

                return c;
            }
            
            ENDCG
        }
    }
} 
