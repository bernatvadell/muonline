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
float3 GlowColor = float3(0.6, 0.5, 0.0); // Dimmer gold to reduce brightness
bool IsAncient = false;
bool IsExcellent = false;

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
    
    float waveBase = frac(Time * 0.001) * 10000.0 * 0.0001;
    float3 view = normalize(input.ViewDirection) + normal + float3(10000.5, 10000.5, 10000.5);
    
    // Determine glow parameters based on item level
    float3 effectColor = GlowColor;
    float brightness = 1.0;
    float ghostIntensity = 0.0;
    
    if (itemLevel < 7)
    {
        // Level 0-6: no effects
        brightness = 1.2;
        ghostIntensity = 0.0;
    }
    else if (itemLevel < 9)
    {
        // Level 7-8: low ghosting
        effectColor = GlowColor;
        brightness = 1.6;
        ghostIntensity = 0.3;
    }
    else if (itemLevel < 10)
    {
        // Level 9: medium ghosting
        effectColor = GlowColor;
        brightness = 1.8;
        ghostIntensity = 0.6;
    }
    else
    {
        // Level 10+: full ghosting with increasing brightness per level
        effectColor = GlowColor;
        brightness = 1.8 + (itemLevel - 10) * 0.2;
        ghostIntensity = 0.8;
    }
    
    // Pre-calculate all values to avoid flow control issues
    float subtlePulse = (1.0 + sin(Time * 0.8)) * 0.03 + 0.97;
    float shimmer = (1.0 + sin(Time * 8.0 + normal.x * 12.0)) * 0.15 + 0.85;
    
    // Main ghosting offsets
    float2 ghostOffset1 = float2(sin(Time * 0.8) * 0.035, cos(Time * 0.7) * 0.035) * ghostIntensity;
    float2 ghostOffset2 = float2(sin(Time * 1.0 + 2.1) * 0.025, cos(Time * 0.9 + 1.8) * 0.025) * ghostIntensity;
    float2 ghostOffset3 = float2(sin(Time * 1.2 + 4.2) * 0.02, cos(Time * 1.1 + 3.7) * 0.02) * ghostIntensity;
    float2 ghostOffset4 = float2(sin(Time * 0.6 + 1.1) * 0.015, cos(Time * 1.3 + 2.3) * 0.015) * ghostIntensity;
    
    // Ancient/Excellent offsets
    float2 ancientOffset1 = float2(sin(Time * 0.5) * 0.02, cos(Time * 0.4) * 0.02);
    float2 ancientOffset2 = float2(sin(Time * 0.7 + 1.0) * 0.015, cos(Time * 0.6 + 1.5) * 0.015);
    float2 excellentOffset1 = float2(sin(Time * 0.6) * 0.025, cos(Time * 0.5) * 0.025);
    float2 excellentOffset2 = float2(sin(Time * 0.8 + 1.2) * 0.02, cos(Time * 0.7 + 1.8) * 0.02);
    
    // Sample all textures at once
    float4 ghost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset1);
    float4 ghost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset2);
    float4 ghost3 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset3);
    float4 ghost4 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset4);
    float4 ancientGhost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ancientOffset1);
    float4 ancientGhost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ancientOffset2);
    float4 excellentGhost1 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset1);
    float4 excellentGhost2 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset2);
    
    // Level 7+ mask
    float levelMask = step(7.0, itemLevel);
    
    // Apply effects based on level
    if (itemLevel >= 7)
    {
        // Apply metallic effect for +7 and higher  
        float3 metallic = effectColor * 0.8;
        color.rgb = color.rgb * metallic * brightness * subtlePulse;
        color.rgb += ghost1.rgb * (0.8 * ghostIntensity) * shimmer;
        color.rgb += ghost2.rgb * (0.6 * ghostIntensity) * shimmer;
        color.rgb += ghost3.rgb * (0.5 * ghostIntensity) * shimmer;
        color.rgb += ghost4.rgb * (0.4 * ghostIntensity) * shimmer;
    }
    else
    {
        color.rgb = color.rgb * brightness;
    }
    
    // Additional brightness boost for higher levels
    float level10Mask = step(10.0, itemLevel);
    float extraGlow = (itemLevel - 9.0) * 0.1;
    float glowEffect = (1.0 + sin(Time * 1.0)) * 0.03 + 0.2;
    color.rgb += effectColor * glowEffect * extraGlow * level10Mask;
    
    // Ancient item effect - sweeping white-blue light wave
    float ancientEnabled = IsAncient ? 1.0 : 0.0;
    float3 ancientColor = float3(0.8, 0.9, 1.0); // Brighter white-blue
    
    // Create sweeping wave effect across texture
    float waveSpeed = Time * 1.2; // Faster sweep
    float wavePhase = waveSpeed + input.TextureCoordinate.x * 8.0; // Wave travels across X axis
    float wave = sin(wavePhase) * 0.5 + 0.5; // 0-1 range
    
    // Create narrow focused beam effect
    float beamWidth = 0.3; // Narrower beam
    float beamIntensity = pow(wave, 6.0) * 2.0; // Sharp, focused beam
    
    // Additional wave for more complex pattern
    float wave2 = sin(waveSpeed * 0.7 + input.TextureCoordinate.y * 4.0) * 0.3 + 0.7;
    float combinedWave = beamIntensity * wave2;
    
    // Apply the sweeping light effect with higher intensity for +9 and higher items
    float levelBoost = (itemLevel >= 9) ? 2.0 : 1.0; // Double intensity for +9 and higher
    color.rgb += ancientGhost1.rgb * ancientColor * combinedWave * 1.2 * levelBoost * ancientEnabled;
    color.rgb += ancientGhost2.rgb * ancientColor * combinedWave * 0.9 * levelBoost * ancientEnabled;
    
    // Add stronger base glow for higher level items
    float baseGlow = sin(Time * 0.5) * 0.1 + 0.2;
    float baseGlowIntensity = (itemLevel >= 9) ? 0.6 : 0.3; // Stronger for +9 and higher
    color.rgb += color.rgb * ancientColor * baseGlow * baseGlowIntensity * ancientEnabled;
    
    // Excellent item effect - smooth rainbow gradient always visible
    float excellentPulse = sin(Time * 0.4) * 0.15 + 0.25;
    float excellentEnabled = IsExcellent ? 1.0 : 0.0;
    
    // Smooth rainbow gradient with slower transitions and reduced intensity
    float timePhase = Time * 0.8; // Slower color cycling
    float3 rainbowColor1 = float3(
        sin(timePhase) * 0.3 + 0.4,
        sin(timePhase + 2.1) * 0.3 + 0.4,
        sin(timePhase + 4.2) * 0.3 + 0.4
    );
    float3 rainbowColor2 = float3(
        sin(timePhase + 1.5) * 0.3 + 0.4,
        sin(timePhase + 3.6) * 0.3 + 0.4,
        sin(timePhase + 5.7) * 0.3 + 0.4
    );
    
    // Create gradient effect by blending two rainbow colors
    float gradientFactor = sin(Time * 0.6) * 0.5 + 0.5;
    float3 rainbowGradient = lerp(rainbowColor1, rainbowColor2, gradientFactor);
    
    float excellentIntensity = excellentPulse * 0.8;
    
    color.rgb += excellentGhost1.rgb * rainbowGradient * excellentIntensity * excellentEnabled;
    color.rgb += excellentGhost2.rgb * rainbowGradient * excellentIntensity * 0.8 * excellentEnabled;
    
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