#if OPENGL
    // DesktopGL → SM 3.0
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    // DX
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif


float4x4 World;
float4x4 ViewProjection;
float4   ShadowTint;
texture  ShadowTexture;

sampler2D ShadowSampler = sampler_state
{
    Texture   = <ShadowTexture>;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
};

//---------------- VS -------------------------
struct VS_IN  { float4 Position : POSITION0; float2 Tex : TEXCOORD0; };
struct VS_OUT { float4 Position : SV_POSITION; float2 Tex : TEXCOORD0; };

VS_OUT ShadowVS(VS_IN vin)
{
    VS_OUT vout;
    vout.Position = mul(vin.Position, World);
    vout.Position = mul(vout.Position, ViewProjection);
    vout.Tex      = vin.Tex;
    return vout;
}

//---------------- PS -------------------------
float4 ShadowPS(VS_OUT pin) : SV_TARGET
{
    float alphaMask = tex2D(ShadowSampler, pin.Tex).a;
    return float4(0,0,0, alphaMask * ShadowTint.a);
}

technique Shadow
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL ShadowVS();
        PixelShader  = compile PS_SHADERMODEL ShadowPS();
    }
}
