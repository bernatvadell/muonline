// DynamicLighting.fx - Dynamic lighting shader

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
float4x4 WorldViewProjection; // Includes World * View * Projection
#if !OPENGL
float4x4 BoneMatrices[256];
#endif

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

float DebugLightingAreas = 0.0;
float UseVertexColorLighting = 0.0;
float TerrainLightingPass = 0.0;
float TerrainDynamicIntensityScale = 1.5;
float GlobalLightMultiplier = 1.0; 

float3 TerrainLight = float3(1.0, 1.0, 1.0);

float2 TerrainUvScale = float2(0.0, 0.0);      
float UseProceduralTerrainUV = 0.0;            
float IsWaterTexture = 0.0;                    
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
    float3 DynamicLight : TEXCOORD3; 
};

// ============================================================================
// EXTREME OPTIMIZED LIGHTING FUNCTIONS
// ============================================================================

#define TERRAIN_MAX_LIGHTS MAX_LIGHTS
#define TERRAIN_LOW_MAX_LIGHTS 8

float3 CalculateTerrainLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);
    int lightCount = min(ActiveLightCount, MaxLightsToProcess);

    [loop]
    for (int i = 0; i < TERRAIN_MAX_LIGHTS; i++)
    {
        if (i >= lightCount) break;

        float3 lightDir = LightPositions[i] - worldPos;
        float distSq = dot(lightDir, lightDir);
        
        // Fast inverse radius (multiplication is faster than division)
        float invRad = 1.0 / (LightRadii[i] + 0.0001);
        float invRadSq = invRad * invRad;

        // Quadratic attenuation
        float attenuation = saturate(1.0 - (distSq * invRadSq));
        
        // Hemisphere check
        float vertical = saturate((LightPositions[i].z - worldPos.z) * invRad);
        attenuation *= vertical;

        // ALU TRICK: dot(normal, lightDir) * invDist saves 2 multiplications vs dot(normal, lightDir * invDist)
        float invDist = rsqrt(distSq + 0.0001);
        float diffuse = saturate(dot(normal, lightDir) * invDist);

        dynamicLight += LightColors[i] * (LightIntensities[i] * diffuse * attenuation);
    }
    return dynamicLight;
}

float3 CalculateTerrainLightingLow(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);
    int lightCount = min(min(ActiveLightCount, MaxLightsToProcess), TERRAIN_LOW_MAX_LIGHTS);

    // [unroll] is safe here because max iterations is strictly 8. Eliminates loop overhead completely.
    [unroll(TERRAIN_LOW_MAX_LIGHTS)]
    for (int i = 0; i < TERRAIN_LOW_MAX_LIGHTS; i++)
    {
        if (i >= lightCount) break;

        float3 lightDir = LightPositions[i] - worldPos;
        float distSq = dot(lightDir, lightDir);
        
        float invRad = 1.0 / (LightRadii[i] + 0.0001);
        float attenuation = saturate(1.0 - (distSq * (invRad * invRad)));
        
        float vertical = saturate((LightPositions[i].z - worldPos.z) * invRad);
        attenuation *= vertical;

        float invDist = rsqrt(distSq + 0.0001);
        float diffuse = saturate(dot(normal, lightDir) * invDist);

        dynamicLight += LightColors[i] * (LightIntensities[i] * diffuse * attenuation);
    }
    return dynamicLight;
}

float3 CalculateDynamicLighting(float3 worldPos, float3 normal)
{
    float3 dynamicLight = float3(0, 0, 0);
    int lightCount = min(min(ActiveLightCount, MaxLightsToProcess), MAX_LIGHTS);

    [loop]
    for (int i = 0; i < MAX_LIGHTS; i++)
    {
        if (i >= lightCount) break;

        float3 lightDir = LightPositions[i] - worldPos;
        float distSq = dot(lightDir, lightDir);
        
        float invRad = 1.0 / (LightRadii[i] + 0.0001);
        float attenuation = saturate(1.0 - (distSq * (invRad * invRad)));

        float invDist = rsqrt(distSq + 0.0001);
        float diffuse = saturate(dot(normal, lightDir) * invDist);

        dynamicLight += LightColors[i] * (LightIntensities[i] * diffuse * attenuation);
    }
    return dynamicLight;
}

// ============================================================================
// VERTEX SHADERS
// ============================================================================

float2 CalculateProceduralUV(float3 worldPos, float2 baseTexCoord)
{
    float2 procUv = worldPos.xy * TerrainUvScale;
    if (IsWaterTexture > 0.5)
    {
        float f = max(0.01, DistortionFrequency);
        
        float phase = frac(WaterTotal * f * 0.1591549) * 6.2831853;
        
        float2 offsets;
        offsets.x = sin(procUv.x * f + phase);
        offsets.y = cos(procUv.y * f + phase);
        
        return procUv + (WaterFlowDirection * WaterTotal) + (offsets * DistortionAmplitude);
    }
    return lerp(baseTexCoord, procUv, UseProceduralTerrainUV);
}

PixelInput VS_Terrain(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    // ALU TRICK: Use precalculated WorldViewProjection instead of multiplying View and Projection per vertex
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = CalculateProceduralUV(worldPos.xyz, input.TexCoord);
    output.Color = input.Color;
    output.DynamicLight = CalculateTerrainLighting(output.WorldPos, output.Normal);
    return output;
}

PixelInput VS_TerrainLow(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = CalculateProceduralUV(worldPos.xyz, input.TexCoord);
    output.Color = input.Color;
    output.DynamicLight = CalculateTerrainLightingLow(output.WorldPos, output.Normal);
    return output;
}

PixelInput VS_Objects(VertexInput input)
{
    PixelInput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.WorldPos = worldPos.xyz;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    output.DynamicLight = float3(0, 0, 0); 
    return output;
}

#if !OPENGL
PixelInput VS_ObjectsSkinned(VertexInputSkinned input)
{
    PixelInput output;
    int boneIndex = min(max((int)input.BoneIndex, 0), 255);
    float4 localPos = mul(float4(input.Position, 1.0), BoneMatrices[boneIndex]);
    
    output.WorldPos = mul(localPos, World).xyz;
    // ALU TRICK: localPos * WorldViewProjection is mathematically identical to worldPos * View * Projection
    // but saves 2 matrix multiplications per vertex!
    output.Position = mul(localPos, WorldViewProjection);
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
    
    output.WorldPos = worldPos.xyz;
    // ALU TRICK: Vector * Matrix is faster than Matrix * Matrix.
    // mul(mul(worldPos, View), Projection) takes 8 operations instead of 20!
    output.Position = mul(mul(worldPos, View), Projection);
    
    float3 localNormal = mul(input.Normal, (float3x3)BoneMatrices[boneIndex]);
    output.Normal = normalize(mul(localNormal, (float3x3)instanceWorld));
    output.TexCoord = input.TexCoord;
    output.Color = input.Color * input.InstanceColor;
    output.DynamicLight = float3(0, 0, 0);
    return output;
}
#endif

float SampleShadow(float3 worldPos, float3 normal)
{
    if (ShadowsEnabled < 0.5)
        return 1.0;

    float4 lightPos = mul(float4(worldPos, 1.0), LightViewProjection);
    float3 proj = lightPos.xyz / lightPos.w;

#if OPENGL
    float2 uv = float2(proj.x * 0.5 + 0.5, 0.5 - proj.y * 0.5); 
    float depth = proj.z * 0.5 + 0.5; 
#else
    float2 uv = float2(proj.x * 0.5 + 0.5, 0.5 - proj.y * 0.5); 
    float depth = proj.z; 
#endif

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        return 1.0;

    float ndotl = saturate(dot(normal, -SunDirection));
    float bias = ShadowBias + ShadowNormalBias * (1.0 - ndotl);

    float2 off = ShadowMapTexelSize * 0.5;
    
    // ALU TRICK: Pack 4 texture samples into a float4, do ONE step() operation, 
    // and use dot() to sum and multiply by 0.25 simultaneously.
    float4 s;
    s.x = tex2D(ShadowSampler, uv + float2(-off.x, -off.y)).r;
    s.y = tex2D(ShadowSampler, uv + float2( off.x, -off.y)).r;
    s.z = tex2D(ShadowSampler, uv + float2(-off.x,  off.y)).r;
    s.w = tex2D(ShadowSampler, uv + float2( off.x,  off.y)).r;
    
    float4 shadows = step(depth - bias, s);
    return dot(shadows, 0.25);
}

// ============================================================================
// PIXEL SHADERS 
// ============================================================================

float3 PrepareNormal(float3 rawNormal)
{
    return normalize(rawNormal + float3(0.0, 0.0, 0.00001));
}

float4 PS_Terrain(PixelInput input) : SV_Target
{
    float4 texColor = tex2D(SamplerState0, input.TexCoord);
    float finalAlpha = texColor.a * Alpha * input.Color.a;
    
    // EXTREME OPTIMIZATION: Early clip! 
    // If pixel is transparent, GPU aborts here and skips ALL lighting math below!
    clip(finalAlpha - 0.01);

    float3 normal = PrepareNormal(input.Normal);
    float3 baseLight = input.Color.rgb * GlobalLightMultiplier;

    float hasActiveLights = step(0.5, float(ActiveLightCount));
    float3 finalLight = baseLight + input.DynamicLight * TerrainDynamicIntensityScale * hasActiveLights;

    float isDebugPixel = DebugLightingAreas * step(0.01, dot(input.DynamicLight, input.DynamicLight)) * hasActiveLights;

    float shadowTerm = SampleShadow(input.WorldPos, normal);
    float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
    finalLight *= lerp(1.0, shadowMix, ShadowsEnabled);

    float3 finalColor = lerp(texColor.rgb * finalLight, float3(0, 0, 0), isDebugPixel);

    return float4(finalColor, finalAlpha);
}

float4 PS_Objects(PixelInput input) : SV_Target
{
    float4 texColor = tex2D(SamplerState0, input.TexCoord);
    float finalAlpha = texColor.a * Alpha * input.Color.a;
    
    // EXTREME OPTIMIZATION: Early clip!
    clip(finalAlpha - 0.01);

    float3 normal = PrepareNormal(input.Normal);

    float3 sunDir = normalize(SunDirection);
    float ndotlRaw = dot(normal, -sunDir);
    float ndotl = saturate(ndotlRaw) + saturate(-ndotlRaw) * 0.35; 
    
    float shadowFactor = saturate(lerp(1.0 - ShadowStrength, 1.0, ndotl));
    float3 sunLight = SunColor * ndotl * SunStrength;
    float3 baseLight = AmbientLight * shadowFactor + sunLight;

    float3 dynamicLight = CalculateDynamicLighting(input.WorldPos, normal);
    float hasActiveLights = step(0.5, float(ActiveLightCount));
    float3 finalLight = baseLight + dynamicLight * TerrainDynamicIntensityScale * hasActiveLights;

    float isDebugPixel = DebugLightingAreas * step(0.01, dot(dynamicLight, dynamicLight)) * hasActiveLights;

    float shadowTerm = SampleShadow(input.WorldPos, normal);
    float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
    finalLight *= lerp(1.0, shadowMix, ShadowsEnabled);

    float3 finalColor = lerp(texColor.rgb * finalLight, float3(0, 0, 0), isDebugPixel);

    return float4(finalColor, finalAlpha);
}

// ============================================================================
// TECHNIQUES
// ============================================================================

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

technique DynamicLighting_Terrain
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL VS_Terrain();
        PixelShader = compile PS_SHADERMODEL PS_Terrain();
    }
}

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
    float2 Depth    : TEXCOORD1; 
};

ShadowVertexOutput ShadowVS(VertexInput input)
{
    ShadowVertexOutput output;
    float4 worldPos = mul(float4(input.Position, 1.0), World);
    output.Position = mul(worldPos, LightViewProjection);
    output.TexCoord = CalculateProceduralUV(worldPos.xyz, input.TexCoord);
    output.Depth = output.Position.zw; 
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
    output.TexCoord = CalculateProceduralUV(worldPos.xyz, input.TexCoord);
    output.Depth = output.Position.zw;
    return output;
}
#endif

float4 ShadowPS(ShadowVertexOutput input) : SV_TARGET
{
    float alphaMask = tex2D(SamplerState0, input.TexCoord).a;
    clip(alphaMask - 0.01);

    float depth = input.Depth.x / input.Depth.y;
#if OPENGL
    float linearDepth = depth * 0.5 + 0.5; 
#else
    float linearDepth = depth; 
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
