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
bool ShadowsEnabled = false;

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
bool DebugLightingAreas = false;
bool UseVertexColorLighting = false;
bool TerrainLightingPass = false;
float TerrainDynamicIntensityScale = 1.5;
float GlobalLightMultiplier = 1.0; // Day-night cycle multiplier for vertex color lighting

// Terrain static lighting
float3 TerrainLight = float3(1.0, 1.0, 1.0);

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
};

// Vertex Shader
PixelInput VS_Main(VertexInput input)
{
    PixelInput output;
    
    // Transform position to world space
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    
    // Transform position to screen space
    output.Position = mul(worldPos, mul(View, Projection));
    
    // Transform normal to world space
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    
    // Pass through texture coordinates and vertex color
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    
    return output;
}

// Maximum performance dynamic lighting optimized for all GPUs
float3 CalculateDynamicLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);
    
    // Process only essential lights with aggressive culling
    int lightCount = min(min(ActiveLightCount, MaxLightsToProcess), MAX_LIGHTS);
    
    // Remove unroll directive to let GPU decide optimal approach
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
        float invDistance = rsqrt(distanceSquared + 0.001); // Add small epsilon to avoid division by zero
        float distance = 1.0 / invDistance;
        
        // Fast attenuation using inverse distance (avoid division)
        float attenuation = 1.0 - (distance * (1.0 / lightRadius));
        attenuation = saturate(attenuation); // Clamp to 0-1 range
        if (TerrainLightingPass)
        {
            // Only light points that are below the light (hemisphere)
            float vertical = saturate((lightPos.z - worldPos.z) * (1.0 / lightRadius));
            attenuation *= vertical;
        }
        
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
    // Convert bool to float branchlessly: bool evaluates to 1.0 when true, 0.0 when false in HLSL
    float shadowEnabled = float(ShadowsEnabled);
    return lerp(1.0, shadow, inBounds * shadowEnabled);
}

// Pixel Shader - Optimized for minimum GPU overhead
float4 PS_Main(PixelInput input) : SV_Target
{
    // Sample texture once
    float4 texColor = tex2D(SamplerState0, input.TexCoord);
    
    // Calculate final alpha first 
    float finalAlpha = texColor.a * Alpha * input.Color.a;
    
    // Simplified alpha test with smaller threshold for performance
    if (finalAlpha < 0.01)
        discard;
    
    float3 rawNormal = input.Normal;
    float normalLenSq = dot(rawNormal, rawNormal);
    float3 normal = normalize(rawNormal);
    if (normalLenSq < 0.0001)
    {
        // Fallback normal for meshes/billboards without valid normals (e.g., leaves, curtains)
        normal = float3(0.0, 0.0, 1.0);
    }
    float3 baseLight;
    if (UseVertexColorLighting)
    {
        baseLight = input.Color.rgb * GlobalLightMultiplier;
    }
    else
    {
        float3 sunDir = normalize(SunDirection);

        float ndotlRaw = dot(normal, -sunDir);
        float ndotl = saturate(ndotlRaw); // front face
        float backfill = saturate(-ndotlRaw) * 0.35; // give a bit of light to flipped normals/backfaces (thin planes, leaves)
        ndotl += backfill;
        float shadowFactor = saturate(lerp(1.0 - ShadowStrength, 1.0, ndotl));
        float3 sunLight = SunColor * ndotl * SunStrength;

        baseLight = AmbientLight * shadowFactor + sunLight;
    }

    float3 finalLight = baseLight;

    // Calculate dynamic lighting (branchless - always calculate, multiply by condition)
    float3 dynamicLight = CalculateDynamicLighting(input.WorldPos, normal);
    float hasActiveLights = step(1.0, float(ActiveLightCount));
    finalLight += dynamicLight * TerrainDynamicIntensityScale * hasActiveLights;

    // Debug mode: show lighting areas as black spots (branchless)
    float isDebugPixel = float(DebugLightingAreas) * step(0.1, length(dynamicLight)) * hasActiveLights;

    // Apply shadow (branchless - always calculate, apply based on condition)
    float shadowTerm = SampleShadow(input.WorldPos, normal);
    float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
    float shadowWeight = float(ShadowsEnabled);
    finalLight *= lerp(1.0, shadowMix, shadowWeight);
    
    // Apply lighting with single multiply
    float3 finalColor = texColor.rgb * finalLight;

    // Apply debug mode (branchless) - black color for debug pixels
    finalColor = lerp(finalColor, float3(0, 0, 0), isDebugPixel);

    return float4(finalColor, finalAlpha);
    
    // Apply lighting: Don't multiply by vertex color if it's too bright
    // Use vertex color for alpha but not for RGB to avoid over-brightening
    //float3 finalColor = texColor.rgb * finalLight;
    
    //return float4(finalColor, texColor.a * Alpha * input.Color.a);
    
    // Debug tests (all passed):
    // Test VERTEX COLOR: Check if vertex color works (PASSED)
    // return float4(texColor.rgb * input.Color.rgb, texColor.a * Alpha * input.Color.a);
    
    // Test TEXTURE: Check if texture sampling works (PASSED)
    // return float4(texColor.rgb, 1.0); // Use texture color with full alpha
    
    // Test BASIC: Return constant color to test if shader works at all (PASSED)
    // return float4(1.0, 0.0, 1.0, 1.0); // Magenta color
}

// Technique
technique DynamicLighting
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_Main();
        PixelShader = compile PS_SHADERMODEL PS_Main();
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
    output.TexCoord = input.TexCoord;
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
