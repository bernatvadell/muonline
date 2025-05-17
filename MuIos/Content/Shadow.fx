#if OPENGL
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix WorldViewProj;        
float4 ShadowColor;         
float ShadowOpacity;        
float HeightAboveTerrain;   

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 WorldPos : TEXCOORD1;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, WorldViewProj);
    output.TexCoord = input.TexCoord;
    output.WorldPos = output.Position;
    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float2 center = float2(0.5, 0.5);
    float dist = length(input.TexCoord - center);

    float shadow = dist < 0.5 ? 1.0 : 0.0;

    float heightFactor = saturate(1.0 - HeightAboveTerrain / 100.0);

    float finalOpacity = 1.0;;

    return float4(0, 0, 0, finalOpacity);
}

technique Shadow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}