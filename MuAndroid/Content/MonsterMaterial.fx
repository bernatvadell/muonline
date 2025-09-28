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

// Monster-specific parameters
float3 GlowColor = float3(1.0, 0.8, 0.0); // Default gold glow
float GlowIntensity = 1.0;
float Time = 0;
bool EnableGlow = false;
bool SimpleColorMode = false; // Simple color tinting without ghosting
float Alpha = 1.0;

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
    
    float3 normal = normalize(input.Normal);
    float lightIntensity = max(0.1, dot(normal, -LightDirection));
    color.rgb *= lightIntensity;

    // Store original lit color for proper blending
    float3 originalColor = color.rgb;

    float simpleMask = SimpleColorMode ? 1.0 : 0.0;
    float glowMask = (!SimpleColorMode && EnableGlow && GlowIntensity > 0.0) ? 1.0 : 0.0;

    float3 simpleColor = originalColor * GlowColor * GlowIntensity;

    // Pre-calculate ghosting regardless of mask to avoid divergent texture fetches
    float subtlePulse = (1.0 + sin(Time * 1.5)) * 0.03 + 0.97;
    float shimmer = (1.0 + sin(Time * 20.0 + normal.x * 12.0)) * 0.15 + 0.85;
    float3 metallic = GlowColor * 2.0;
    float intensityMultiplier = GlowIntensity;
    float extraGlow = max(0.0, GlowIntensity - 2.0) * 0.5;
    float glowEffect = (1.0 + sin(Time * 4.0)) * 0.1 + 0.8;

    float2 ghostOffset1 = float2(sin(Time * 4.0) * 0.035, cos(Time * 3.5) * 0.035);
    float2 ghostOffset2 = float2(sin(Time * 5.5 + 2.1) * 0.025, cos(Time * 4.8 + 1.8) * 0.025);
    float2 ghostOffset3 = float2(sin(Time * 6.2 + 4.2) * 0.02, cos(Time * 5.9 + 3.7) * 0.02);
    float2 ghostOffset4 = float2(sin(Time * 3.3 + 1.1) * 0.015, cos(Time * 6.7 + 2.3) * 0.015);

    float4 ghost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset1);
    float4 ghost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset2);
    float4 ghost3 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset3);
    float4 ghost4 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset4);

    float3 glowColor = originalColor * metallic * (2.2 * intensityMultiplier) * subtlePulse;
    glowColor += ghost1.rgb * (0.5 * intensityMultiplier) * shimmer;
    glowColor += ghost2.rgb * (0.4 * intensityMultiplier) * shimmer;
    glowColor += ghost3.rgb * (0.3 * intensityMultiplier) * shimmer;
    glowColor += ghost4.rgb * (0.2 * intensityMultiplier) * shimmer;
    glowColor += GlowColor * glowEffect * extraGlow;

    float3 baseColor = originalColor;
    baseColor = lerp(baseColor, simpleColor, simpleMask);
    color.rgb = lerp(baseColor, glowColor, glowMask);

    color.a *= Alpha;
    
    return color;
}

technique MonsterMaterialDrawing
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
