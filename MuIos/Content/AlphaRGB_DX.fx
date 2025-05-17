// ─────────────────────────────────────────────────────────────
//  Alpha → RGB  (DirectX  –  vs_4_0_level_9_1 / ps_4_0_level_9_1)
// ─────────────────────────────────────────────────────────────
float4x4 WorldViewProjection;

Texture2D TextureSampler;
SamplerState PointClamp
{
    Filter   = Point;
    AddressU = Clamp;
    AddressV = Clamp;
};

struct VS_IN  { float3 Pos:POSITION0; float2 Tex:TEXCOORD0; };
struct VS_OUT { float4 Pos:SV_POSITION; float2 Tex:TEXCOORD0; };

VS_OUT VS_Main( VS_IN v )
{
    VS_OUT o;
    o.Pos = mul( float4(v.Pos,1), WorldViewProjection );
    o.Tex = v.Tex;
    return o;
}

float4 PS_Main( VS_OUT pin ) : SV_Target
{
    float4 col  = TextureSampler.Sample( PointClamp, pin.Tex );
    float  lum  = dot( col.rgb, 1.0/3.0 );
    float  a    = saturate( lum / 0.25 );
    return float4( col.rgb, a );
}

technique AlphaRGB
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Main();
        PixelShader  = compile ps_4_0_level_9_1 PS_Main();
    }
}
