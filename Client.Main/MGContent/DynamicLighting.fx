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
#define MAX_LIGHTS 16
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

struct PixelInput
{
    float4 Position     : SV_POSITION;
    float2 TexCoord     : TEXCOORD0;
    float3 WorldPos     : TEXCOORD1;
    float3 Normal       : TEXCOORD2;
    float4 Color        : COLOR0;
    float3 DynamicLight : TEXCOORD3; // Pre-computed dynamic lighting for terrain (vertex-based)
};

// Fast terrain lighting - optimized for vertex shader (max 8 lights, simplified math)
#define TERRAIN_MAX_LIGHTS 8
float3 CalculateTerrainLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);

    // Limit to 8 lights for terrain (sufficient for large polygons)
    int lightCount = min(min(ActiveLightCount, MaxLightsToProcess), TERRAIN_MAX_LIGHTS);

    for (int i = 0; i < lightCount; i++)
    {
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

        dynamicLight += lightColor * (lightIntensity * diffuse * attenuation);
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
    // Calculate dynamic lighting per-vertex for terrain (8 lights max, faster)
    output.DynamicLight = CalculateTerrainLighting(output.WorldPos, output.Normal);
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

float SampleShadow(float3 worldPos, float3 normal)
{
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

    // Check if outside shadow map bounds (branchless)
    float2 uvClamped = saturate(uv);
    float inBounds = step(abs(uv.x - uvClamped.x) + abs(uv.y - uvClamped.y), 0.0001);

    float ndotl = saturate(dot(normal, -SunDirection));
    float bias = ShadowBias + ShadowNormalBias * (1.0 - ndotl);

    // 2x2 PCF (4 samples) - use tex2Dlod for OpenGL compatibility (no gradient operations)
    float shadow = 0.0;
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2(-0.5, -0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2( 0.5, -0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2(-0.5,  0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow += step(depth - bias, tex2Dlod(ShadowSampler, float4(uv + float2( 0.5,  0.5) * ShadowMapTexelSize, 0, 0)).r);
    shadow *= 0.25;

    // Blend to no shadow (1.0) when outside bounds or shadows disabled
    return lerp(1.0, shadow, inBounds * ShadowsEnabled);
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

// Technique for TERRAIN (vertex color lighting + vertex dynamic lights)
technique DynamicLighting_Terrain
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_Terrain();
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
