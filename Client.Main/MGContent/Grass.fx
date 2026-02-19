#if OPENGL
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 World;
float4x4 View;
float4x4 Projection;

float Time;
float WindSpeed;
float WindStrength;
float AlphaCutoff;

texture GrassTexture;
sampler2D GrassSampler = sampler_state
{
    Texture = <GrassTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VS_IN
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 Tex      : TEXCOORD0;
    float4 Wind     : TEXCOORD1; // x=dirX, y=dirY, z=phase, w=amplitude
};

struct VS_OUT
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 Tex      : TEXCOORD0;
};

VS_OUT GrassVS(VS_IN input)
{
    VS_OUT output;

    float sway = sin(Time * WindSpeed + input.Wind.z) * input.Wind.w * WindStrength;
    float4 worldPos = input.Position;
    worldPos.xy += input.Wind.xy * sway;

    output.Position = mul(worldPos, World);
    output.Position = mul(output.Position, View);
    output.Position = mul(output.Position, Projection);
    output.Color = input.Color;
    output.Tex = input.Tex;
    return output;
}

float4 GrassPS(VS_OUT input) : SV_TARGET
{
    float4 tex = tex2D(GrassSampler, input.Tex);
    float alpha = tex.a * input.Color.a;
    clip(alpha - AlphaCutoff);
    return tex * input.Color;
}

technique Grass
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL GrassVS();
        PixelShader = compile PS_SHADERMODEL GrassPS();
    }
}
