#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldViewProjection;

float3 EyePosition;
float3 LightDirection = float3(0.707, -0.707, 0);
float4 AmbientColor = float4(0.3, 0.3, 0.3, 1.0);
float4 DiffuseColor = float4(1.0, 1.0, 1.0, 1.0);

texture DiffuseTexture;
sampler2D DiffuseSampler = sampler_state
{
    Texture = <DiffuseTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
    AddressU = Wrap;
    AddressV = Wrap;
};

int ItemOptions = 0;
float Time = 0;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TextureCoordinate : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float3 WorldPosition : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float2 TextureCoordinate : TEXCOORD2;
    float3 ViewDirection : TEXCOORD3;
};

float GetRandom(float2 coords)
{
    return frac(sin(dot(coords, float2(12.9898, 78.233))) * 43758.5453);
}

float Noise2(float2 coords)
{
    float2 texSize = float2(1.0, 1.0);
    float2 pc = coords * texSize;
    float2 base = floor(pc);
    float s1 = GetRandom((base + float2(0.0, 0.0)) / texSize);
    float s2 = GetRandom((base + float2(1.0, 0.0)) / texSize);
    float s3 = GetRandom((base + float2(0.0, 1.0)) / texSize);
    float s4 = GetRandom((base + float2(1.0, 1.0)) / texSize);
    float2 f = smoothstep(0.0, 1.0, frac(pc));
    float px1 = lerp(s1, s2, f.x);
    float px2 = lerp(s3, s4, f.x);
    float result = lerp(px1, px2, f.y);
    return result;
}

VertexShaderOutput MainVS(in VertexShaderInput input)
{
    VertexShaderOutput output = (VertexShaderOutput)0;

    float4 worldPosition = mul(input.Position, World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
    
    output.WorldPosition = worldPosition.xyz;
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.TextureCoordinate = input.TextureCoordinate;
    output.ViewDirection = normalize(EyePosition - worldPosition.xyz);

    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float4 color = tex2D(DiffuseSampler, input.TextureCoordinate);
    
    if (color.a < 0.1)
        discard;
    
    int itemLevel = ItemOptions % 16;
    bool isExcellent = (ItemOptions / 16) > 0;
    
    float3 normal = normalize(input.Normal);
    float lightIntensity = max(0.1, dot(normal, -LightDirection));
    color.rgb *= lightIntensity;
    
    float wave = frac(Time * 0.001) * 10000.0 * 0.0001;
    float3 view = normalize(input.ViewDirection) + normal + float3(10000.5, 10000.5, 10000.5);
    float mixAmount = (1.0 + sin(Time * 4.0)) / 2.0;
    
    if (itemLevel < 2)
    {
        // Level 0-1: no special effect
    }
    else if (itemLevel < 5)
    {
        // Level 2-4: red tint
        float3 minColor = float3(0.4, 0.3, 0.3);
        float3 maxColor = float3(0.7, 0.5, 0.5);
        float3 partColor = lerp(minColor, maxColor, mixAmount);
        color.rgb *= partColor;
    }
    else if (itemLevel < 7)
    {
        // Level 5-6: blue tint
        float3 minColor = float3(0.3, 0.4, 0.4);
        float3 maxColor = float3(0.5, 0.6, 0.6);
        float3 partColor = lerp(minColor, maxColor, mixAmount);
        color.rgb *= partColor;
    }
    else if (itemLevel < 9)
    {
        // Level 7-8: gold effect with texture distortion
        float nSecond = normal.z * 0.5 + wave;
        float nFirst = normal.y * 0.5 + wave * 2.0;
        
        float2 uvOffset = float2(nFirst, nSecond);
        float4 texColor = tex2D(DiffuseSampler, input.TextureCoordinate + uvOffset);
        
        color.rgb = color.rgb * 0.8 + texColor.rgb * color.rgb * 0.9;
    }
    else if (itemLevel < 10)
    {
        // Level 9: enhanced gold effect
        float nSecond = normal.z * 0.5 + wave;
        float nFirst = normal.y * 0.5 + wave * 2.0;
        
        float2 uvOffset = float2(nFirst, nSecond);
        float4 texColor = tex2D(DiffuseSampler, input.TextureCoordinate + uvOffset);
        
        color.rgb = color.rgb * 0.8 + color.rgb * float3(1.0, 0.9, 0.0) + texColor.rgb * float3(0.7, 0.6, 0.5) * 0.3;
    }
    else
    {
        // Level 10+: ultra bright metallic effect with dynamic ghosting
        float subtlePulse = (1.0 + sin(Time * 1.5)) * 0.03 + 0.97; // Even more constant brightness
        float shimmer = (1.0 + sin(Time * 20.0 + normal.x * 12.0)) * 0.15 + 0.85; // More shimmer
        
        // Dynamic ghosting effect - larger movement and more samples
        float2 ghostOffset1 = float2(sin(Time * 4.0) * 0.035, cos(Time * 3.5) * 0.035);
        float2 ghostOffset2 = float2(sin(Time * 5.5 + 2.1) * 0.025, cos(Time * 4.8 + 1.8) * 0.025);
        float2 ghostOffset3 = float2(sin(Time * 6.2 + 4.2) * 0.02, cos(Time * 5.9 + 3.7) * 0.02);
        float2 ghostOffset4 = float2(sin(Time * 3.3 + 1.1) * 0.015, cos(Time * 6.7 + 2.3) * 0.015);
        
        float4 ghost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset1);
        float4 ghost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset2);
        float4 ghost3 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset3);
        float4 ghost4 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset4);
        
        // Ultra bright metallic base
        float3 metallic = float3(2.5, 2.2, 1.8);
        
        // Strong glow based on level
        float levelBoost = min(itemLevel / 7.0, 3.0);
        
        // Combine original with enhanced ghosting effects
        color.rgb = color.rgb * metallic * (2.2 + levelBoost) * subtlePulse;
        color.rgb += ghost1.rgb * 0.5 * shimmer;
        color.rgb += ghost2.rgb * 0.4 * shimmer;
        color.rgb += ghost3.rgb * 0.3 * shimmer;
        color.rgb += ghost4.rgb * 0.2 * shimmer;
    }
    
    // Excellent item enhancement
    if (isExcellent)
    {
        float glowEffect = (1.0 + sin(Time * 4.0)) * 0.1 + 0.8; // Mostly constant glow
        color.rgb += float3(glowEffect * 0.5, glowEffect * 0.4, glowEffect * 0.1);
        color.rgb *= 1.3; // Strong brightness boost for excellent items
    }
    
    return color;
}

technique BasicColorDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}