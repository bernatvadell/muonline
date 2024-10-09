// Matrices del mundo, vista y proyección
float4x4 World;
float4x4 View;
float4x4 Projection;

// Textura y sampler
Texture2D Texture : register(t0);
sampler TextureSampler = sampler_state
{
    Texture = <Texture>;
    MipFilter = Linear;
    MinFilter = Linear;
    MagFilter = Linear;
};

// Estructuras para el shader
struct VertexShaderInput
{
    float3 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;

    // Transformación de posición
    float4 worldPosition = mul(float4(input.Position, 1.0), World);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);

    // Pasar coordenadas de textura
    output.TexCoord = input.TexCoord;

    return output;
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TexCoord);

// Calcular el brillo del píxel (promedio de RGB)
float brightness = (texColor.r + texColor.g + texColor.b) / 3.0;

if (brightness < 0.25)
{
    // Ajustar el alfa para que sea proporcional al brillo
    texColor.a = brightness / 0.25;
}
else
{
    // Píxeles más brillantes son completamente opacos
    texColor.a = 1.0;
}

return texColor;
}

technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 MainVS();
        PixelShader = compile ps_2_0 MainPS();
    }
}
