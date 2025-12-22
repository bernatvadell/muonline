#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_5_0
    #define PS_SHADERMODEL ps_5_0
#endif

#if OPENGL
static const float GlowIntensityScale = 1.0;
#else
static const float GlowIntensityScale = 0.80;
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

texture ShadowMap;
sampler2D ShadowSampler = sampler_state
{
    Texture = <ShadowMap>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

int ItemOptions = 0;
float Time = 0;
float3 GlowColor = float3(0.6, 0.5, 0.0);
bool IsAncient = false;
bool IsExcellent = false;
float4x4 LightViewProjection;
float2 ShadowMapTexelSize = float2(1.0 / 2048.0, 1.0 / 2048.0);
float ShadowBias = 0.0015;
float ShadowNormalBias = 0.0025;
float ShadowsEnabled = 0.0; // OpenGL compatible: use 0.0/1.0 instead of bool
float ShadowStrength = 0.5;

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

// HSV to RGB conversion for smooth rainbow
float3 HSVtoRGB(float3 hsv)
{
    float h = hsv.x;
    float s = hsv.y;
    float v = hsv.z;
    
    float c = v * s;
    float x = c * (1.0 - abs(fmod(h * 6.0, 2.0) - 1.0));
    float m = v - c;
    
    float3 rgb;
    if (h < 1.0/6.0)
        rgb = float3(c, x, 0);
    else if (h < 2.0/6.0)
        rgb = float3(x, c, 0);
    else if (h < 3.0/6.0)
        rgb = float3(0, c, x);
    else if (h < 4.0/6.0)
        rgb = float3(0, x, c);
    else if (h < 5.0/6.0)
        rgb = float3(x, 0, c);
    else
        rgb = float3(c, 0, x);
    
    return rgb + m;
}

// Custom spectrum for Excellent items: Blue -> Orange -> Violet (NO GREEN)
// blueScale: controls blue intensity (0.30 for +7+, 1.0 for +0-+6)
float3 GetCustomSpectrum(float phase, float blueScale)
{
    phase = frac(phase) * 3.0;
    if (phase < 1.0)
        return lerp(float3(0.0, 0.3, 1.0) * float3(1.0, 1.0, blueScale), float3(1.0, 0.5, 0.0), frac(phase)); // Blue to Orange
    else if (phase < 2.0)
        return lerp(float3(1.0, 0.5, 0.0), float3(0.6, 0.0, 0.8), frac(phase)); // Orange to Violet
    else
        return lerp(float3(0.6, 0.0, 0.8), float3(0.0, 0.3, 1.0) * float3(1.0, 1.0, blueScale), frac(phase)); // Violet to Blue
}


float SampleShadow(float3 worldPos, float3 normal)
{
    // Branchless shadow sampling for OpenGL compatibility
    float4 lightPos = mul(float4(worldPos, 1.0), LightViewProjection);
    float3 proj = lightPos.xyz / lightPos.w;
    float2 uv = proj.xy * 0.5 + 0.5;
    float depth = proj.z * 0.5 + 0.5;

    // Branchless bounds check
    float2 uvClamped = saturate(uv);
    float inBounds = step(abs(uv.x - uvClamped.x) + abs(uv.y - uvClamped.y), 0.0001);

    float ndotl = saturate(dot(normal, -LightDirection));
    float bias = ShadowBias + ShadowNormalBias * (1.0 - ndotl);

    float shadow = 0.0;
    [unroll]
    for (int x = -1; x <= 1; x++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            float2 offset = float2(x, y) * ShadowMapTexelSize;
            float sampleDepth = tex2D(ShadowSampler, uv + offset).r;
            shadow += step(depth - bias, sampleDepth);
        }
    }

    // Return 1.0 (no shadow) if shadows disabled or out of bounds
    return lerp(1.0, shadow / 9.0, inBounds * ShadowsEnabled);
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
    
    float3 effectColor = GlowColor * GlowIntensityScale;
    float brightness = 1.0;
    float ghostIntensity = 0.0;
    
    if (itemLevel < 7)
    {    
        brightness = 1;
        ghostIntensity = 0;
    }
    else if (itemLevel < 9)
    {
        effectColor = GlowColor * GlowIntensityScale;
        brightness = 1.6 + (itemLevel -8) * 0.2;
        ghostIntensity = 0.30;
    }
    else if (itemLevel < 10)
    {
        effectColor = GlowColor * GlowIntensityScale;
        brightness = 1.8 + (itemLevel - 9) * 0.2;
        ghostIntensity = 0.8;
    }
    else
    {
        effectColor = GlowColor * GlowIntensityScale;
        brightness = 1.8 + (itemLevel -10 ) * 0.2;
        ghostIntensity = 0.7 + (itemLevel / 30 );
    }
    
    float subtlePulse = (1.0 + sin(Time * 0.8)) * 0.03 + 0.97;
    float shimmer = (1.0 + sin(Time * 8.0 + normal.x * 12.0)) * 0.15 + 0.85;
    
    // Main ghosting offsets
    float2 ghostOffset1 = float2(sin(Time * 0.8) * 0.035, cos(Time * 0.7) * 0.035) * ghostIntensity;
    float2 ghostOffset2 = float2(sin(Time * 1.0 + 2.1) * 0.025, cos(Time * 0.9 + 1.8) * 0.025) * ghostIntensity;
    float2 ghostOffset3 = float2(sin(Time * 1.2 + 4.2) * 0.02, cos(Time * 1.1 + 3.7) * 0.02) * ghostIntensity;
    float2 ghostOffset4 = float2(sin(Time * 0.6 + 1.1) * 0.015, cos(Time * 1.3 + 2.3) * 0.015) * ghostIntensity;
    
    // Ancient offsets
    float2 ancientOffset1 = float2(sin(Time * 0.5) * 0.02, cos(Time * 0.4) * 0.02);
    float2 ancientOffset2 = float2(sin(Time * 0.7 + 1.0) * 0.015, cos(Time * 0.6 + 1.5) * 0.015);
    
    // Excellent offsets - more layers for richer effect
    float2 excellentOffset1 = float2(sin(Time * 0.6) * 0.03, cos(Time * 0.5) * 0.03);
    float2 excellentOffset2 = float2(sin(Time * 0.8 + 1.2) * 0.025, cos(Time * 0.7 + 1.8) * 0.025);
    float2 excellentOffset3 = float2(sin(Time * 1.0 + 2.4) * 0.02, cos(Time * 0.9 + 2.6) * 0.02);
    float2 excellentOffset4 = float2(sin(Time * 0.5 + 3.6) * 0.015, cos(Time * 1.1 + 3.2) * 0.015);
    float2 excellentOffset5 = float2(sin(Time * 0.7 + 4.8) * 0.035, cos(Time * 0.6 + 4.4) * 0.035);
    float2 excellentOffset6 = float2(sin(Time * 0.9 + 6.0) * 0.028, cos(Time * 0.8 + 5.5) * 0.028);
    
    // Sample all textures
    float4 ghost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset1);
    float4 ghost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset2);
    float4 ghost3 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset3);
    float4 ghost4 = tex2D(DiffuseSampler, input.TextureCoordinate + ghostOffset4);
    float4 ancientGhost1 = tex2D(DiffuseSampler, input.TextureCoordinate + ancientOffset1);
    float4 ancientGhost2 = tex2D(DiffuseSampler, input.TextureCoordinate + ancientOffset2);
    float4 excellentGhost1 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset1);
    float4 excellentGhost2 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset2);
    float4 excellentGhost3 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset3);
    float4 excellentGhost4 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset4);
    float4 excellentGhost5 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset5);
    float4 excellentGhost6 = tex2D(DiffuseSampler, input.TextureCoordinate + excellentOffset6);
    
    float levelMask = step(7.0, itemLevel);
    
    // Apply effects based on level
    if (itemLevel >= 7)
    {
        float3 metallic = effectColor * 0.8;
        color.rgb = color.rgb * metallic * brightness * subtlePulse;
        color.rgb += ghost1.rgb * (0.8 * ghostIntensity) * shimmer * GlowIntensityScale;
        color.rgb += ghost2.rgb * (0.6 * ghostIntensity) * shimmer * GlowIntensityScale;
        color.rgb += ghost3.rgb * (0.5 * ghostIntensity) * shimmer * GlowIntensityScale;
        color.rgb += ghost4.rgb * (0.4 * ghostIntensity) * shimmer * GlowIntensityScale;
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
    
    // Ancient item effect - fast blue sweep with pause
    float ancientEnabled = IsAncient ? 1.0 : 0.0;
    float3 ancientColor = float3(0.3, 0.5, 1.0); // More blue color

    // Cycle with pause: sweep takes 12% of cycle, pause is 88%
    float cycleSpeed = 0.1; 
    float cycleDuration = 1.0 / cycleSpeed;
    float sweepPortion = 0.15; // Sweep happens in first 12% of cycle, very long pause after

    float cycleProgress = frac(Time * cycleSpeed); // 0 to 1 over cycle
    float sweepProgress = saturate(cycleProgress / sweepPortion); // 0 to 1 during sweep, then stays at 1

    // Fast sweep across mesh using texture coordinate
    float sweepPosition = sweepProgress;
    float meshPosition = input.TextureCoordinate.x;

    // Create sharp beam that sweeps across
    float beamWidth = 0.15;
    float distFromBeam = abs(meshPosition - sweepPosition);
    float beamIntensity = 1.0 - saturate(distFromBeam / beamWidth);
    beamIntensity = pow(beamIntensity, 2.0) * 3.0; // Sharp falloff, bright center

    // Fade out beam at the end of sweep (before pause)
    float fadeOut = 1.0 - smoothstep(0.85, 1.0, sweepProgress);
    beamIntensity *= fadeOut;

    // Add secondary vertical wave for depth
    float wave2 = sin(Time * 3.0 + input.TextureCoordinate.y * 6.0) * 0.3 + 0.7;
    float combinedWave = beamIntensity * wave2;

    float levelBoost = (itemLevel >= 9) ? 2.0 : 1.0;
    color.rgb += ancientGhost1.rgb * ancientColor * combinedWave * 1.5 * levelBoost * ancientEnabled;
    color.rgb += ancientGhost2.rgb * ancientColor * combinedWave * 1.1 * levelBoost * ancientEnabled;

    // Subtle base blue glow (always present)
    float baseGlow = sin(Time * 0.8) * 0.08 + 0.15;
    float baseGlowIntensity = (itemLevel >= 9) ? 0.5 : 0.25;
    color.rgb += color.rgb * ancientColor * baseGlow * baseGlowIntensity * ancientEnabled;
    
    // ==================== EXCELLENT SWEEP PULSE EFFECT ====================
    // Similar to Ancient sweep but with semi-transparent violet color (only for +7+)
    float excellentSweepEnabled = (IsExcellent && itemLevel >= 7) ? 1.0 : 0.0;
    float3 excellentSweepColor = float3(0.5, 0.3, 0.7); // Semi-transparent violet (less white, more violet)
    
    // Cycle with pause: sweep takes 15% of cycle, pause is 85%
    float exCycleSpeed = 0.12; 
    float exSweepPortion = 0.15;
    
    float exCycleProgress = frac(Time * exCycleSpeed);
    float exSweepProgress = saturate(exCycleProgress / exSweepPortion);
    
    // Fast sweep across mesh
    float exSweepPosition = exSweepProgress;
    float exMeshPosition = input.TextureCoordinate.x;
    
    // Create sharp beam
    float exBeamWidth = 0.18;
    float exDistFromBeam = abs(exMeshPosition - exSweepPosition);
    float exBeamIntensity = 1.0 - saturate(exDistFromBeam / exBeamWidth);
    exBeamIntensity = pow(exBeamIntensity, 2.0) * 2.0; // Reduced intensity for semi-transparency
    
    // Fade out beam at the end of sweep
    float exFadeOut = 1.0 - smoothstep(0.85, 1.0, exSweepProgress);
    exBeamIntensity *= exFadeOut;
    
    // Add secondary vertical wave for depth
    float exWave2 = sin(Time * 3.5 + input.TextureCoordinate.y * 6.0) * 0.3 + 0.7;
    float exCombinedWave = exBeamIntensity * exWave2;
    
    float exLevelBoost = (itemLevel >= 9) ? 1.5 : 1.0; // Reduced boost for subtlety
    color.rgb += excellentGhost1.rgb * excellentSweepColor * exCombinedWave * 1.2 * exLevelBoost * excellentSweepEnabled;
    color.rgb += excellentGhost2.rgb * excellentSweepColor * exCombinedWave * 0.9 * exLevelBoost * excellentSweepEnabled;
    
    // Subtle base violet glow (always present for excellent +7+)
    float exBaseGlow = sin(Time * 0.9) * 0.08 + 0.12; // Reduced base glow
    float exBaseGlowIntensity = (itemLevel >= 9) ? 0.4 : 0.2; // Reduced intensity
    color.rgb += color.rgb * excellentSweepColor * exBaseGlow * exBaseGlowIntensity * excellentSweepEnabled;
    
    // ==================== ENHANCED EXCELLENT EFFECT ====================
        float excellentEnabled = IsExcellent ? 1.0 : 0.0;
        
        // 1. Fresnel/Rim lighting effect - glowing edges
        float3 viewDir = normalize(input.ViewDirection);
        float fresnel = 1.0 - saturate(dot(viewDir, normal));
        fresnel = pow(fresnel, 2.5); // Sharper edge glow
        
        // 2. Custom spectrum color cycling (Blue -> Orange -> Violet, NO GREEN)
        float hueBase = frac(Time * 0.15); // Slow base rotation
        float hueSpatial = input.TextureCoordinate.x * 0.3 + input.TextureCoordinate.y * 0.2; // Spatial variation
        float hueNormal = (normal.x + normal.y) * 0.1; // Normal-based variation
        
        // Blue intensity: 0.30 for +7+, 1.0 for +0-+6
        float blueScale = (itemLevel >= 7) ? 0.01 : 1.0;
        
        // Excellent effect intensity scale
        float exScale = (itemLevel >= 7) ? 1.8 : 1.8;
        
        // Create multiple spectrum colors at different phases
        float3 rainbow1 = GetCustomSpectrum(hueBase + hueSpatial, blueScale);
        float3 rainbow2 = GetCustomSpectrum(hueBase + hueSpatial + 0.33, blueScale);
        float3 rainbow3 = GetCustomSpectrum(hueBase + hueSpatial + 0.66, blueScale);
        float3 rainbow4 = GetCustomSpectrum(hueBase + hueNormal + 0.5, blueScale);
        
        // 3. Sweeping shine effect - diagonal light beams
        float sweepSpeed1 = Time * 1.5;
        float sweepSpeed2 = Time * 1.2;
        float sweepSpeed3 = Time * 0.9;
        
        // Multiple diagonal sweeps at different angles
        float sweep1 = input.TextureCoordinate.x + input.TextureCoordinate.y * 0.5;
        float sweep2 = input.TextureCoordinate.x * 0.7 - input.TextureCoordinate.y * 0.3;
        float sweep3 = input.TextureCoordinate.y + input.TextureCoordinate.x * 0.3;
        
        float beam1 = pow(sin(sweep1 * 6.0 - sweepSpeed1) * 0.5 + 0.5, 8.0);
        float beam2 = pow(sin(sweep2 * 8.0 + sweepSpeed2) * 0.5 + 0.5, 10.0);
        float beam3 = pow(sin(sweep3 * 5.0 - sweepSpeed3) * 0.5 + 0.5, 6.0);
        
        float combinedBeams = beam1 * 0.7 + beam2 * 0.5 + beam3 * 0.4;
        
        // 4. Pulsating aura
        float pulse1 = sin(Time * 1.2) * 0.5 + 0.5;
        float pulse2 = sin(Time * 0.8 + 1.5) * 0.5 + 0.5;
        float pulse3 = sin(Time * 1.5 + 3.0) * 0.5 + 0.5;
        float combinedPulse = (pulse1 + pulse2 + pulse3) / 3.0;
        
        // 6. Color wave effect - colors flowing across the surface
        float colorWave1 = sin(Time * 0.6 + input.TextureCoordinate.x * 4.0) * 0.5 + 0.5;
        float colorWave2 = sin(Time * 0.5 + input.TextureCoordinate.y * 3.0 + 1.0) * 0.5 + 0.5;
        float colorWave3 = sin(Time * 0.7 + (input.TextureCoordinate.x + input.TextureCoordinate.y) * 2.5) * 0.5 + 0.5;
        
        // Blend rainbow colors based on waves
        float3 waveColor = rainbow1 * colorWave1 + rainbow2 * colorWave2 + rainbow3 * (1.0 - colorWave1 * colorWave2);
        waveColor = normalize(waveColor) * length(waveColor) * 0.4;
        
        // Apply ghost layers with rainbow colors
        float ghostBaseIntensity = 0.2 * combinedPulse + 0.1;
        
        color.rgb += excellentGhost1.rgb * rainbow1 * ghostBaseIntensity * (1.0 * exScale) * excellentEnabled;
        color.rgb += excellentGhost2.rgb * rainbow2 * ghostBaseIntensity * (0.9 * exScale) * excellentEnabled;
        color.rgb += excellentGhost3.rgb * rainbow3 * ghostBaseIntensity * (0.8 * exScale) * excellentEnabled;
        color.rgb += excellentGhost4.rgb * rainbow4 * ghostBaseIntensity * (0.7 * exScale) * excellentEnabled;
        color.rgb += excellentGhost5.rgb * waveColor * ghostBaseIntensity * (0.6 * exScale) * excellentEnabled;
        color.rgb += excellentGhost6.rgb * rainbow1 * ghostBaseIntensity * (0.5 * exScale) * excellentEnabled;
        
        // Apply sweeping beams with rainbow
        float3 beamColor = lerp(rainbow1, rainbow2, sin(Time * 0.4) * 0.5 + 0.5);
        color.rgb += beamColor * combinedBeams * 0.05 * excellentEnabled;
        
        // Apply rim/fresnel glow with shifting colors
        float3 rimColor = lerp(rainbow3, rainbow4, fresnel);
        color.rgb += rimColor * fresnel * 0.05 * excellentEnabled;
        
        // Subtle overall color enhancement
        float3 overlayColor = waveColor * 0.015;
        color.rgb += color.rgb * overlayColor * excellentEnabled;
        
        // Brightness boost for excellent items
        color.rgb *= lerp(1.0, 1.4, excellentEnabled);

        float shadowTerm = SampleShadow(input.WorldPosition, normal);
        float shadowMix = lerp(1.0 - ShadowStrength, 1.0, shadowTerm);
        color.rgb *= shadowMix;

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