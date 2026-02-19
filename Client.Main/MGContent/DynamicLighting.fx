// DynamicLighting.fx - Dynamic lighting shader for 3D objects

#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_5_0
    #define PS_SHADERMODEL ps_5_0
#endif

// Transformation matrices
float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldViewProjection;
#if !OPENGL
float4x4 BoneMatrices[256];
#endif

// Camera position for specular highlights
float3 EyePosition;

// Texture
texture DiffuseTexture;
sampler2D SamplerState0 = sampler_state
{
    Texture = <DiffuseTexture>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
};

// Lighting parameters  
float3 AmbientLight = float3(0.8, 0.8, 0.8);
float Alpha = 1.0;
float3 SunDirection = float3(1.0, 0.0, -0.6);
float3 SunColor = float3(1.0, 0.95, 0.85);
float SunStrength = 0.8;
float ShadowStrength = 0.5;
float4x4 LightViewProjection;
float2 ShadowMapTexelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
float ShadowBias = 0.0015;
float ShadowNormalBias = 0.0025;
// Use float for both DX and OpenGL for C# compatibility (SetValue works with float on both)
float ShadowsEnabled = 0.0;

texture ShadowMap;
sampler2D ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
};

// Dynamic lights
#define MAX_LIGHTS 32
float3 LightPositions[MAX_LIGHTS];
float3 LightColors[MAX_LIGHTS];
float LightRadii[MAX_LIGHTS];
float LightIntensities[MAX_LIGHTS];
int ActiveLightCount = 0;
int MaxLightsToProcess = MAX_LIGHTS;
// Use float for both DX and OpenGL for C# compatibility (SetValue works with float on both)
float DebugLightingAreas = 0.0;
float UseVertexColorLighting = 0.0;
float TerrainLightingPass = 0.0;
float TerrainDynamicIntensityScale = 1.5;
float GlobalLightMultiplier = 1.0; // Day-night cycle multiplier for vertex color lighting

// Terrain static lighting
float3 TerrainLight = float3(1.0, 1.0, 1.0);

// Terrain UV generation (CPU-friendly: compute UV from world position in shader)
float2 TerrainUvScale = float2(0.0, 0.0);      // UV per world unit (already includes 1/TERRAIN_SCALE)
float UseProceduralTerrainUV = 0.0;            // 0 = use vertex TexCoord (objects), 1 = procedural (terrain)
float IsWaterTexture = 0.0;                    // 1 = apply water flow/distortion in VS
float2 WaterFlowDirection = float2(1.0, 0.0);
float WaterTotal = 0.0;
float DistortionAmplitude = 0.0;
float DistortionFrequency = 0.0;

// Input structures
struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float4 Color    : COLOR0;
};

#if !OPENGL
struct VertexInputSkinned
{
    float3 Position  : POSITION0;
    float3 Normal    : NORMAL0;
    float2 TexCoord  : TEXCOORD0;
    float4 Color     : COLOR0;
    float  BoneIndex : TEXCOORD1;
};

struct VertexInputSkinnedInstanced
{
    float3 Position      : POSITION0;
    float3 Normal        : NORMAL0;
    float2 TexCoord      : TEXCOORD0;
    float4 Color         : COLOR0;
    float  BoneIndex     : TEXCOORD1;
    float4 InstWorld0    : TEXCOORD2;
    float4 InstWorld1    : TEXCOORD3;
    float4 InstWorld2    : TEXCOORD4;
    float4 InstWorld3    : TEXCOORD5;
    float4 InstanceColor : COLOR1;
};
#endif

struct PixelInput
{
    float4 Position     : SV_POSITION;
    float2 TexCoord     : TEXCOORD0;
    float3 WorldPos     : TEXCOORD1;
    float3 Normal       : TEXCOORD2;
    float4 Color        : COLOR0;
    float3 DynamicLight : TEXCOORD3; // Pre-computed dynamic lighting for terrain (vertex-based)
};

// Terrain lighting: process ALL uploaded lights so no selection boundary
// exists (which caused visible flickering when lights swapped in/out of
// the top-N set between snapshots).  Must match DynamicLightArrayCapacity
// on the CPU side (32).  DX vs_5_0 has no instruction limit; GL vs_3_0
// has 512 slots, but the branchless active-mask approach keeps ALU low.
#define TERRAIN_MAX_LIGHTS MAX_LIGHTS
#define TERRAIN_LOW_MAX_LIGHTS 8
float3 CalculateTerrainLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);

    // Static loop bound so MojoShader (OpenGL vs_3_0) can unroll without
    // "relative address needs replicate swizzle" errors from dynamic indexing.
    // Inactive slots are zeroed out branchlessly via the 'active' mask.
    float fLightCount = float(min(ActiveLightCount, MaxLightsToProcess));

    for (int i = 0; i < TERRAIN_MAX_LIGHTS; i++)
    {
        // Branchless mask: 1.0 if i < lightCount, 0.0 otherwise
        float active = step(float(i) + 0.5, fLightCount);

        float3 lightPos = LightPositions[i];
        float3 lightColor = LightColors[i];
        float lightRadius = LightRadii[i];
        float lightIntensity = LightIntensities[i];

        // Vector to light
        float3 lightDir = lightPos - worldPos;
        float distanceSquared = dot(lightDir, lightDir);
        float radiusSquared = lightRadius * lightRadius;

        // Early skip if outside radius (branchless)
        float inRange = step(distanceSquared, radiusSquared);

        // Smooth quadratic falloff (faster than linear, looks good)
        float normalizedDist = distanceSquared / radiusSquared;
        float attenuation = saturate(1.0 - normalizedDist) * inRange;

        // Hemisphere check for terrain (light from above)
        float vertical = saturate((lightPos.z - worldPos.z) * (1.0 / lightRadius));
        attenuation *= vertical;

        // Fast normalize using rsqrt
        float invDistance = rsqrt(distanceSquared + 0.001);
        lightDir *= invDistance;

        // Simple diffuse
        float diffuse = saturate(dot(normal, lightDir));

        dynamicLight += lightColor * (lightIntensity * diffuse * attenuation * active);
    }

    return dynamicLight;
}

// Reduced-cost terrain variant for weaker GPUs (fewer lights, same model).
float3 CalculateTerrainLightingLow(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);
    float fLightCount = float(min(min(ActiveLightCount, MaxLightsToProcess), TERRAIN_LOW_MAX_LIGHTS));

    for (int i = 0; i < TERRAIN_LOW_MAX_LIGHTS; i++)
    {
        float active = step(float(i) + 0.5, fLightCount);

        float3 lightPos = LightPositions[i];
        float3 lightColor = LightColors[i];
        float lightRadius = LightRadii[i];
        float lightIntensity = LightIntensities[i];

        float3 lightDir = lightPos - worldPos;
        float distanceSquared = dot(lightDir, lightDir);
        float radiusSquared = lightRadius * lightRadius;

        float inRange = step(distanceSquared, radiusSquared);
        float normalizedDist = distanceSquared / radiusSquared;
        float attenuation = saturate(1.0 - normalizedDist) * inRange;
        float vertical = saturate((lightPos.z - worldPos.z) * (1.0 / lightRadius));
        attenuation *= vertical;

        float invDistance = rsqrt(distanceSquared + 0.001);
        lightDir *= invDistance;

        float diffuse = saturate(dot(normal, lightDir));
        dynamicLight += lightColor * (lightIntensity * diffuse * attenuation * active);
    }

    return dynamicLight;
}

// Full quality dynamic lighting for objects (per-pixel)
float3 CalculateDynamicLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);

    // Process all available lights for objects
    int lightCount = min(min(ActiveLightCount, MaxLightsToProcess), MAX_LIGHTS);

    for (int i = 0; i < lightCount; i++)
    {
        float3 lightPos = LightPositions[i];
        float3 lightColor = LightColors[i];
        float lightRadius = LightRadii[i];
        float lightIntensity = LightIntensities[i];

        // Single vector subtraction
        float3 lightDir = lightPos - worldPos;
        float distanceSquared = dot(lightDir, lightDir);

        // Precompute radius squared once
        float radiusSquared = lightRadius * lightRadius;

        // Use rsqrt for fast inverse square root (GPU optimized)
        float invDistance = rsqrt(distanceSquared + 0.001);
        float distance = 1.0 / invDistance;

        // Fast attenuation using inverse distance
        float attenuation = 1.0 - (distance * (1.0 / lightRadius));
        attenuation = saturate(attenuation);

        // Skip light if outside radius using multiplication instead of branches
        float inRange = step(distanceSquared, radiusSquared);
        attenuation *= inRange;

        // Normalize light direction using precomputed inverse distance
        lightDir *= invDistance;

        // Simple diffuse with saturate (clamp to 0-1)
        float diffuse = saturate(dot(normal, lightDir));

        // Single multiply-add operation with all optimizations
        dynamicLight += lightColor * (lightIntensity * diffuse * attenuation);
    }

    return dynamicLight;
}

// ============================================================================
// VERTEX SHADERS - Separate versions for terrain and objects
// ============================================================================

// Vertex Shader for TERRAIN - calculates dynamic lighting per-vertex
PixelInput VS_Terrain(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(worldPos, mul(View, Projection));
    output.Normal = normalize(mul(input.Normal, (float3x3)World));

    // Default to vertex-provided UVs (objects), but allow terrain to use procedural UVs.
    float2 procUv = worldPos.xy * TerrainUvScale;
    if (IsWaterTexture > 0.5)
    {
        float2 uv = procUv + WaterFlowDirection * WaterTotal;
        float f = max(0.01, DistortionFrequency);
        float wrapPeriod = 6.2831853 / f; // 2*pi/f
        float phase = WaterTotal - floor(WaterTotal / wrapPeriod) * wrapPeriod;
        uv.x += sin((procUv.x + phase) * f) * DistortionAmplitude;
        uv.y += cos((procUv.y + phase) * f) * DistortionAmplitude;
        procUv = uv;
    }
    output.TexCoord = lerp(input.TexCoord, procUv, UseProceduralTerrainUV);

    output.Color = input.Color;
    // Calculate dynamic lighting per-vertex for terrain.
    output.DynamicLight = CalculateTerrainLighting(output.WorldPos, output.Normal);
    return output;
}

// Vertex Shader for TERRAIN (low) - fewer dynamic lights for integrated GPUs
PixelInput VS_TerrainLow(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(worldPos, mul(View, Projection));
    output.Normal = normalize(mul(input.Normal, (float3x3)World));

    float2 procUv = worldPos.xy * TerrainUvScale;
    if (IsWaterTexture > 0.5)
    {
        float2 uv = procUv + WaterFlowDirection * WaterTotal;
        float f = max(0.01, DistortionFrequency);
        float wrapPeriod = 6.2831853 / f;
        float phase = WaterTotal - floor(WaterTotal / wrapPeriod) * wrapPeriod;
        uv.x += sin((procUv.x + phase) * f) * DistortionAmplitude;
        uv.y += cos((procUv.y + phase) * f) * DistortionAmplitude;
        procUv = uv;
    }
    output.TexCoord = lerp(input.TexCoord, procUv, UseProceduralTerrainUV);

    output.Color = input.Color;
    output.DynamicLight = CalculateTerrainLightingLow(output.WorldPos, output.Normal);
    return output;
}

// Vertex Shader for OBJECTS - no dynamic light calculation (done in PS)
PixelInput VS_Objects(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(worldPos, mul(View, Projection));
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.DynamicLight = float3(0, 0, 0); // Not used, PS calculates per-pixel
    return output;
}

#if !OPENGL
PixelInput VS_ObjectsSkinned(VertexInputSkinned input)
{
    PixelInput output;
    int boneIndex = min(max((int)input.BoneIndex, 0), 255);
    float4 localPos = mul(float4(input.Position, 1.0), BoneMatrices[boneIndex]);
    float4 worldPos = mul(localPos, World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(worldPos, mul(View, Projection));
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.DynamicLight = float3(0, 0, 0);
    return output;
}

PixelInput VS_ObjectsSkinnedInstanced(VertexInputSkinnedInstanced input)
{
    PixelInput output;
    int boneIndex = min(max((int)input.BoneIndex, 0), 255);
    float4x4 instanceWorld = float4x4(input.InstWorld0, input.InstWorld1, input.InstWorld2, input.InstWorld3);
    float4 localPos = mul(float4(input.Position, 1.0), BoneMatrices[boneIndex]);
    float4 worldPos = mul(localPos, instanceWorld);
    float3 localNormal = mul(input.Normal, (float3x3)BoneMatrices[boneIndex]);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(worldPos, mul(View, Projection));
    output.Normal = normalize(mul(localNormal, (float3x3)instanceWorld));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color * input.InstanceColor;
    output.DynamicLight = float3(0, 0, 0);
    return output;
}
#endif

float SampleShadow(float3 worldPos, float3 normal)
{
    // Uniform branch: skip shadow-map fetches entirely when shadows are disabled.
    if (ShadowsEnabled < 0.5)
        return 1.0;

    float4 lightPos = mul(float4(worldPos, 1.0), LightViewProjection);
    float3 proj = lightPos.xyz / lightPos.w;

    // Map from NDC to UV coordinates
    // Both DirectX and OpenGL need Y flip due to texture coordinate systems
#if OPENGL
    float2 uv = float2(proj.x * 0.5 + 0.5, 0.5 - proj.y * 0.5); // Flip Y for OpenGL render target
    float depth = proj.z * 0.5 + 0.5; // OpenGL depth is [-1, 1], map to [0, 1]
#else
    float2 uv = float2(proj.x * 0.5 + 0.5, 0.5 - proj.y * 0.5); // Flip Y for DirectX
    float depth = proj.z; // DirectX depth is already [0, 1]
#endif

    // Skip fetches if outside the shadow-map bounds.
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 1.0;

    float ndotl = saturate(dot(normal, -SunDirection));
    float bias = ShadowBias + ShadowNormalBias * (1.0 - ndotl);

    // 2x2 PCF (4 samples) - use tex2Dlod for OpenGL compatibility (no gradient operations)
    float shadow = 0.0;
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2(-0.5, -0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2( 0.5, -0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2(-0.5,  0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2( 0.5,  0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow *= 0.25;

    return shadow;
}

// ============================================================================
// PIXEL SHADERS - Separate versions for terrain and objects (NO BRANCHES!)
// ============================================================================

// Helper: Prepare normal (branchless for both DX and GL)
float3 PrepareNormal(float3 rawNormal)
{
    float normalLenSq = dot(rawNormal, rawNormal);
    float useFallback = step(normalLenSq, 0.0001);
    return normalize(rawNormal * (1.0 - useFallback) + float3(0.0, 0.0, 1.0) * useFallback);
}

// Pixel Shader for TERRAIN - uses vertex color lighting + vertex dynamic light
float4 PS_Terrain(PixelInput input) : SV_Target
{
    float4 texColor = tex2D(SamplerState0, input.TexCoord);
    float finalAlpha = texColor.a * Alpha * input.Color.a;
    clip(finalAlpha - 0.01);

    float3 normal = PrepareNormal(input.Normal);

    // Terrain uses vertex color lighting (pre-baked)
    float3 baseLight = input.Color.rgb * GlobalLightMultiplier;

    // Use pre-computed dynamic lighting from vertex shader
    float3 dynamicLight = input.DynamicLight;
    float hasActiveLights = step(1.0, float(ActiveLightCount));
    float3 finalLight = baseLight + dynamicLight * TerrainDynamicIntensityScale * hasActiveLights;

    // Debug mode
    float isDebugPixel = DebugLightingAreas * step(0.1, length(dynamicLight)) * hasActiveLights;

    // Shadows
    float shadowTerm = SampleShadow(input.WorldPos, normal);
    float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
    finalLight *= lerp(1.0, shadowMix, ShadowsEnabled);

    float3 finalColor = texColor.rgb * finalLight;
    finalColor = lerp(finalColor, float3(0, 0, 0), isDebugPixel);

    return float4(finalColor, finalAlpha);
}

// Pixel Shader for OBJECTS - uses sun lighting + per-pixel dynamic light
float4 PS_Objects(PixelInput input) : SV_Target
{
    float4 texColor = tex2D(SamplerState0, input.TexCoord);
    float finalAlpha = texColor.a * Alpha * input.Color.a;
    clip(finalAlpha - 0.01);

    float3 normal = PrepareNormal(input.Normal);

    // Objects use sun-based lighting
    float3 sunDir = normalize(SunDirection);
    float ndotlRaw = dot(normal, -sunDir);
    float ndotl = saturate(ndotlRaw);
    float backfill = saturate(-ndotlRaw) * 0.35;
    ndotl += backfill;
    float shadowFactor = saturate(lerp(1.0 - ShadowStrength, 1.0, ndotl));
    float3 sunLight = SunColor * ndotl * SunStrength;
    float3 baseLight = AmbientLight * shadowFactor + sunLight;

    // Calculate per-pixel dynamic lighting (higher quality for objects)
    float3 dynamicLight = CalculateDynamicLighting(input.WorldPos, normal);
    float hasActiveLights = step(1.0, float(ActiveLightCount));
    float3 finalLight = baseLight + dynamicLight * TerrainDynamicIntensityScale * hasActiveLights;

    // Debug mode
    float isDebugPixel = DebugLightingAreas * step(0.1, length(dynamicLight)) * hasActiveLights;

    // Shadows
    float shadowTerm = SampleShadow(input.WorldPos, normal);
    float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
    finalLight *= lerp(1.0, shadowMix, ShadowsEnabled);

    float3 finalColor = texColor.rgb * finalLight;
    finalColor = lerp(finalColor, float3(0, 0, 0), isDebugPixel);

    return float4(finalColor, finalAlpha);
}

// ============================================================================
// TECHNIQUES - Switch in C# code instead of using uniform branches
// ============================================================================

// Default technique for OBJECTS (sun lighting + per-pixel dynamic lights)
technique DynamicLighting
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_Objects();
        PixelShader = compile PS_SHADERMODEL PS_Objects();
    }
}

#if !OPENGL
technique DynamicLighting_Skinned
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_ObjectsSkinned();
        PixelShader = compile PS_SHADERMODEL PS_Objects();
    }
}

technique DynamicLighting_SkinnedInstanced
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_ObjectsSkinnedInstanced();
        PixelShader = compile PS_SHADERMODEL PS_Objects();
    }
}
#endif

// Technique for TERRAIN (vertex color lighting + vertex dynamic lights)
technique DynamicLighting_Terrain
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_Terrain();
        PixelShader = compile PS_SHADERMODEL PS_Terrain();
    }
}

// Reduced terrain dynamic-light cost for integrated GPUs.
technique DynamicLighting_Terrain_Low
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_TerrainLow();
        PixelShader = compile PS_SHADERMODEL PS_Terrain();
    }
}

struct ShadowVertexOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float2 Depth    : TEXCOORD1; // x = z, y = w (pass depth from VS since PS can't access SV_POSITION.zw in ps_3_0)
};

// Use same input struct as main technique to ensure vertex layout compatibility
ShadowVertexOutput ShadowVS(VertexInput input)
{
    ShadowVertexOutput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.Position = mul(worldPos, LightViewProjection);
    float2 procUv = worldPos.xy * TerrainUvScale;
    output.TexCoord = lerp(input.TexCoord, procUv, UseProceduralTerrainUV);
    output.Depth = output.Position.zw; // Pass z and w for depth calculation in PS
    return output;
}

#if !OPENGL
ShadowVertexOutput ShadowVS_Skinned(VertexInputSkinned input)
{
    ShadowVertexOutput output;
    int boneIndex = min(max((int)input.BoneIndex, 0), 255);
    float4 localPos = mul(float4(input.Position, 1.0), BoneMatrices[boneIndex]);
    float4 worldPos = mul(localPos, World);
    output.Position = mul(worldPos, LightViewProjection);
    float2 procUv = worldPos.xy * TerrainUvScale;
    output.TexCoord = lerp(input.TexCoord, procUv, UseProceduralTerrainUV);
    output.Depth = output.Position.zw;
    return output;
}
#endif

float4 ShadowPS(ShadowVertexOutput input) : SV_TARGET
{
    float alphaMask = tex2D(SamplerState0, input.TexCoord).a;
    clip(alphaMask - 0.01);

    // Use interpolated depth values (passed from VS since ps_3_0 can't access SV_POSITION.zw)
    float depth = input.Depth.x / input.Depth.y;
#if OPENGL
    float linearDepth = depth * 0.5 + 0.5; // Map from [-1, 1] to [0, 1]
#else
    float linearDepth = depth; // DirectX already in [0, 1]
#endif
    return float4(linearDepth, linearDepth, linearDepth, 1.0);
}

technique ShadowCaster
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL ShadowVS();
        PixelShader  = compile PS_SHADERMODEL ShadowPS();
    }
}

#if !OPENGL
technique ShadowCaster_Skinned
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL ShadowVS_Skinned();
        PixelShader  = compile PS_SHADERMODEL ShadowPS();
    }
}
#endif
