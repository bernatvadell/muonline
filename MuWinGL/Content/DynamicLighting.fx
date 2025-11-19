// DynamicLighting.fx - Dynamic lighting shader for 3D objects

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

// Dynamic lights
#define MAX_LIGHTS 16
float3 LightPositions[MAX_LIGHTS];
float3 LightColors[MAX_LIGHTS];
float LightRadii[MAX_LIGHTS];
float LightIntensities[MAX_LIGHTS];
int ActiveLightCount = 0;
int MaxLightsToProcess = MAX_LIGHTS;
bool DebugLightingAreas = false;

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
    
    // Skip expensive calculations if ActiveLightCount is 0
    float3 finalLight = AmbientLight;
    if (ActiveLightCount > 0)
    {
        // Normalize only when needed
        float3 normal = normalize(input.Normal);
        
        // Calculate dynamic lighting with mathematical optimizations
        float3 dynamicLight = CalculateDynamicLighting(input.WorldPos, normal);
        
        // Debug mode: show lighting areas as black spots (branch predictor friendly)
        if (DebugLightingAreas && length(dynamicLight) > 0.1)
        {
            return float4(0, 0, 0, finalAlpha); // Black color for debug
        }
        
        // Combine ambient and dynamic lighting
        finalLight = AmbientLight + dynamicLight * 1.5;
    }
    
    // Apply lighting with single multiply
    float3 finalColor = texColor.rgb * finalLight;
    
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
        VertexShader = compile vs_3_0 VS_Main();
        PixelShader = compile ps_3_0 PS_Main();
    }
}