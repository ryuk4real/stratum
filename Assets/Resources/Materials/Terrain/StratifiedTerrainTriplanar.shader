Shader "Stratum/ProceduralTerrainTriplanar"
{
    Properties
    {
        [Header(Rock Base Layer)]
        _InteriorTex ("Rock Albedo (GroundPatchCracked01)", 2D) = "white" {}
        [HDR] _InteriorColor ("Rock Tint", Color) = (1, 1, 1, 1)
        _InteriorNormal ("Rock Normal Map", 2D) = "bump" {}
        _InteriorNormalScale ("Rock Normal Scale", Range(0, 2)) = 1.0
        _InteriorTiling ("Rock Tiling", Float) = 0.25
        _InteriorSmoothness ("Rock Smoothness", Range(0, 1)) = 0.15
        _InteriorMetallic ("Rock Metallic", Range(0, 1)) = 0.0

        [Header(Strata and Sediments Simulation)]
        _StrataFrequency ("Strata Frequency (Lower = Thicker Bands)", Float) = 0.08
        _StrataWarpFrequency ("Strata Warp Frequency", Float) = 0.08
        _StrataWarpStrength ("Strata Warp Strength", Float) = 0.8
        _StrataHardness ("Strata Layer Edge Sharpness", Range(0.01, 0.99)) = 0.55
        _StrataIntensity ("Strata Intensity (Interior)", Range(0, 2)) = 1.15
        _MicroStrataStrength ("Micro-Sediment Detail", Range(0, 1)) = 0.05

        [Header(Sediment Palette)]
        _StrataColor1 ("Sediment 1: Subsurface Soil / Silt", Color) = (0.74, 0.62, 0.48, 1)
        _StrataColor2 ("Sediment 2: Red Sandstone / Clay", Color) = (0.72, 0.44, 0.32, 1)
        _StrataColor3 ("Sediment 3: Dark Shale / Basalt", Color) = (0.38, 0.35, 0.34, 1)
        _StrataColor4 ("Sediment 4: Pale Limestone / Chalk", Color) = (0.80, 0.76, 0.65, 1)

        [Header(Terrain Height Sync)]
        _SurfaceBaseHeight ("Surface Base Height (Y)", Float) = 8.0
        _NoiseFrequency ("Noise Frequency", Float) = 0.03
        _TerrainHeightVariation ("Terrain Height Variation", Float) = 4.0
        _DepthGradientDistance ("Deep Gradient Distance (Y)", Float) = 25.0
        _DepthDarkening ("Deep Underground Darkening", Range(0, 1)) = 0.35
        _DeepUndergroundColor ("Deep Bedrock Tint", Color) = (0.45, 0.43, 0.40, 1)
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 4.0

        [Header(Rendering Settings)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode (0=Off, 1=Front, 2=Back)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        // Forward Lit Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_InteriorTex);        SAMPLER(sampler_InteriorTex);
            TEXTURE2D(_InteriorNormal);     SAMPLER(sampler_InteriorNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _InteriorTex_ST;
                half4  _InteriorColor;
                half4  _StrataColor1;
                half4  _StrataColor2;
                half4  _StrataColor3;
                half4  _StrataColor4;
                half4  _DeepUndergroundColor;

                float  _InteriorTiling;
                float  _InteriorNormalScale;
                half   _InteriorSmoothness;
                half   _InteriorMetallic;

                float  _StrataFrequency;
                float  _StrataWarpFrequency;
                float  _StrataWarpStrength;
                float  _StrataHardness;
                float  _StrataIntensity;
                float  _MicroStrataStrength;

                float  _SurfaceBaseHeight;
                float  _NoiseFrequency;
                float  _TerrainHeightVariation;
                float  _DepthGradientDistance;
                float  _DepthDarkening;
                float  _TriplanarSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                half   fogFactor    : TEXCOORD2;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord  : TEXCOORD3;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return output;
            }

            // Fast procedural noise matching MarchingCubes3D.compute
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash12(i);
                float b = hash12(i + float2(1.0, 0.0));
                float c = hash12(i + float2(0.0, 1.0));
                float d = hash12(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm2D(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise2D(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            // Triplanar Albedo sampling
            half4 SampleTriplanarAlbedo(Texture2D tex, SamplerState samp, float3 worldPos, float3 blendWeights, float tiling)
            {
                float2 uvX = worldPos.zy * tiling;
                float2 uvY = worldPos.xz * tiling;
                float2 uvZ = worldPos.xy * tiling;

                half4 colX = SAMPLE_TEXTURE2D(tex, samp, uvX);
                half4 colY = SAMPLE_TEXTURE2D(tex, samp, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;
            }

            // Triplanar Normal sampling
            half3 SampleTriplanarNormal(Texture2D normTex, SamplerState samp, float3 worldPos, float3 geomNormal, float3 blendWeights, float tiling, float scale)
            {
                float2 uvX = worldPos.zy * tiling;
                float2 uvY = worldPos.xz * tiling;
                float2 uvZ = worldPos.xy * tiling;

                half3 tNormX = UnpackNormalScale(SAMPLE_TEXTURE2D(normTex, samp, uvX), scale);
                half3 tNormY = UnpackNormalScale(SAMPLE_TEXTURE2D(normTex, samp, uvY), scale);
                half3 tNormZ = UnpackNormalScale(SAMPLE_TEXTURE2D(normTex, samp, uvZ), scale);

                half3 worldNormX = half3(tNormX.z * sign(geomNormal.x), tNormX.y, tNormX.x * sign(geomNormal.x));
                half3 worldNormY = half3(tNormY.x, tNormY.z * sign(geomNormal.y), tNormY.y * sign(geomNormal.y));
                half3 worldNormZ = half3(tNormZ.x, tNormZ.y, tNormZ.z * sign(geomNormal.z));

                return normalize(worldNormX * blendWeights.x + worldNormY * blendWeights.y + worldNormZ * blendWeights.z);
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 worldPos = input.positionWS;
                float3 geomNormal = normalize(input.normalWS);

                // Calculate local terrain surface height and depth below the surface
                float2 noiseCoord = worldPos.xz * _NoiseFrequency;
                float surfaceOffset = (fbm2D(noiseCoord) - 0.5) * 2.0 * _TerrainHeightVariation;
                float localSurfaceHeight = _SurfaceBaseHeight + surfaceOffset;
                float depthBelowSurface = localSurfaceHeight - worldPos.y;

                // Triplanar blending weights
                float3 blendWeights = pow(abs(geomNormal), max(_TriplanarSharpness, 1.0));
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 1e-5);

                // Sample Rock Albedo and Normal
                half4 intTexCol = SampleTriplanarAlbedo(_InteriorTex, sampler_InteriorTex, worldPos, blendWeights, _InteriorTiling);
                half4 intAlbedo = intTexCol * _InteriorColor;

                half3 intNormalWS = SampleTriplanarNormal(_InteriorNormal, sampler_InteriorNormal, worldPos, geomNormal, blendWeights, _InteriorTiling, _InteriorNormalScale);

                // Sediments (distributed according to depth below reference ceiling)
                float rockDepth = max(0.0, depthBelowSurface);

                // Natural organic wave distortion
                float warp = (sin(worldPos.x * _StrataWarpFrequency + worldPos.z * _StrataWarpFrequency * 0.73) * 0.5
                            + cos(worldPos.x * _StrataWarpFrequency * 0.47 - worldPos.z * _StrataWarpFrequency * 1.19) * 0.5) * _StrataWarpStrength;

                float strataVal = frac((rockDepth + warp) * _StrataFrequency);
                if (strataVal < 0.0) strataVal += 1.0;

                // 4 distinct sediment bands
                float bandFloat = strataVal * 4.0;
                int band = (int)floor(bandFloat);
                float bandT = frac(bandFloat);

                float h = clamp(_StrataHardness, 0.01, 0.98);
                float tSmooth = smoothstep(0.5 - h * 0.5, 0.5 + h * 0.5, bandT);

                half4 c1, c2;
                if (band == 0)      { c1 = _StrataColor1; c2 = _StrataColor2; }
                else if (band == 1) { c1 = _StrataColor2; c2 = _StrataColor3; }
                else if (band == 2) { c1 = _StrataColor3; c2 = _StrataColor4; }
                else                { c1 = _StrataColor4; c2 = _StrataColor1; }

                half3 strataRGB = lerp(c1.rgb, c2.rgb, tSmooth);

                // Micro-sediment fine detail
                float microWave = sin((rockDepth + warp) * _StrataFrequency * 12.0) * 0.5 + 0.5;
                strataRGB *= lerp(1.0, 0.85 + 0.3 * microWave, _MicroStrataStrength);

                // Bedrock depth darkening
                float depthGradient = saturate(rockDepth / max(1.0, _DepthGradientDistance));
                strataRGB = lerp(strataRGB, strataRGB * _DeepUndergroundColor.rgb, depthGradient * _DepthDarkening);

                // Modulate Rock with the sediment colors
                half3 strataTint = strataRGB * 2.0;
                half3 finalAlbedo = intAlbedo.rgb * lerp(half3(1.0, 1.0, 1.0), strataTint, _StrataIntensity);
                half3 finalNormalWS = intNormalWS;
                half  finalSmoothness = _InteriorSmoothness;
                half  finalMetallic = _InteriorMetallic;

                // Setup URP InputData
                InputData inputData = (InputData)0;
                inputData.positionWS = worldPos;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(worldPos);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(worldPos);
                #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                inputData.fogCoord = InitializeInputDataFog(float4(worldPos, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(finalNormalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Setup URP SurfaceData
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = finalMetallic;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = half3(0, 0, 0);
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);

                return finalColor;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth Only Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth Normals Pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_TARGET
            {
                float3 normalWS = normalize(input.normalWS);
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
