// ─────────────────────────────────────────────────────────────
//  FXAA 3.11  –  DirectX  (vs_4_0_level_9_1 / ps_4_0_level_9_1)
// ─────────────────────────────────────────────────────────────
float2   Resolution;
Texture2D SceneTexture;

SamplerState LinearClamp
{
    Filter   = Linear;
    AddressU = Clamp;
    AddressV = Clamp;
};

#define SAMPLE(uv) SceneTexture.Sample( LinearClamp , uv )

struct VS_IN  { float3 Pos:POSITION0; float2 Tex:TEXCOORD0; };
struct VS_OUT { float4 Pos:SV_POSITION; float2 Tex:TEXCOORD0; };

VS_OUT VS_Pass( VS_IN v )
{
    VS_OUT o;  o.Pos = float4(v.Pos,1);  o.Tex = v.Tex;  return o;
}

float4 PS_FXAA( VS_OUT IN ) : SV_Target
{
    const float FXAA_SPAN_MAX   = 8.0;
    const float FXAA_REDUCE_MUL = 1.0/8.0;
    const float FXAA_REDUCE_MIN = 1.0/128.0;

    float2 invRes = 1.0 / Resolution;
    float2 uv     = IN.Tex;

    float3 rgbNW = SAMPLE( uv + float2(-1,-1) * invRes ).rgb;
    float3 rgbNE = SAMPLE( uv + float2( 1,-1) * invRes ).rgb;
    float3 rgbSW = SAMPLE( uv + float2(-1, 1) * invRes ).rgb;
    float3 rgbSE = SAMPLE( uv + float2( 1, 1) * invRes ).rgb;
    float3 rgbM  = SAMPLE( uv ).rgb;

    float3 lumaC = float3(0.299,0.587,0.114);
    float  lumaNW = dot(rgbNW,lumaC), lumaNE = dot(rgbNE,lumaC);
    float  lumaSW = dot(rgbSW,lumaC), lumaSE = dot(rgbSE,lumaC);
    float  lumaM  = dot(rgbM ,lumaC);

    float lumaMin = min(lumaM, min(min(lumaNW,lumaNE), min(lumaSW,lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW,lumaNE), max(lumaSW,lumaSE)));

    float2 dir;
    dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

    float dirReduce = max( (lumaNW+lumaNE+lumaSW+lumaSE)
                           * (0.25*FXAA_REDUCE_MUL), FXAA_REDUCE_MIN );

    float rcpDirMin = 1.0 / (min(abs(dir.x),abs(dir.y)) + dirReduce);
    dir = clamp(dir*rcpDirMin, -FXAA_SPAN_MAX, FXAA_SPAN_MAX) * invRes;

    float3 rgbA = 0.5 * ( SAMPLE(uv + dir*(1.0/3.0 - 0.5)).rgb +
                          SAMPLE(uv + dir*(2.0/3.0 - 0.5)).rgb );

    float3 rgbB = rgbA*0.5 + 0.25 * ( SAMPLE(uv + dir*-0.5).rgb +
                                      SAMPLE(uv + dir* 0.5).rgb );

    float lumaB = dot(rgbB,lumaC);

    return float4( (lumaB<lumaMin || lumaB>lumaMax)? rgbA : rgbB , 1 );
}

technique FXAA
{
    pass P0
    {
        VertexShader = compile vs_4_0_level_9_1 VS_Pass();
        PixelShader  = compile ps_4_0_level_9_1 PS_FXAA();
    }
}
