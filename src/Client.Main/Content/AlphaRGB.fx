// AlphaRGB.fx

// Effect parameters
float4x4 WorldViewProjection;

// Declaration of texture and sampler
Texture2D TextureSampler;
SamplerState SamplerState0
{
    AddressU = Clamp;
    AddressV = Clamp;
};

// Input and output structures
struct VertexInput
{
    float3 Position : POSITION0;
    float3 Normal   : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
};

// Vertex Shader
PixelInput VS_Main(VertexInput input)
{
    PixelInput output;

    // Position transformation
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);

    // Passing texture coordinates
    output.TexCoord = input.TexCoord;

    return output;
}

// Pixel Shader
float4 PS_Main(PixelInput input) : SV_Target
{
    // Sampling color from texture
    float4 texColor = TextureSampler.Sample(SamplerState0, input.TexCoord);

    // Calculating brightness as the average of RGB values
    float brightness = (texColor.r + texColor.g + texColor.b) / 3.0;

    // Calculating alpha value
    float alpha = lerp(brightness / 0.25, 1.0, step(0.25, brightness));

    // Setting color with new alpha value
    return float4(texColor.rgb, alpha);
}

// Techniques
technique Technique1
{
    pass Pass1
    {
        // Compiling Vertex Shader
        VertexShader = compile vs_3_0 VS_Main();

        // Compiling Pixel Shader
        PixelShader = compile ps_3_0 PS_Main();
    }
}
