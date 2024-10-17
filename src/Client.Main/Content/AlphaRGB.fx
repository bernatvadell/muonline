// AlphaRGB.fx

// Effect parameters
float4x4 WorldViewProjection;
Texture2D TextureSampler;
SamplerState SamplerState0;

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

    // Transform position in one step
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);

    // Pass through texture coordinates
    output.TexCoord = input.TexCoord;

    return output;
}

// Pixel Shader
float4 PS_Main(PixelInput input) : SV_Target
{
    // Sample color from texture
    float4 texColor = TextureSampler.Sample(SamplerState0, input.TexCoord);

    // Calculate brightness as the average of RGB values
    float brightness = (texColor.r + texColor.g + texColor.b) / 3.0;

    // Calculate alpha value
    float alpha = lerp(brightness / 0.25, 1.0, step(0.25, brightness));

    // Set color with the new alpha value
    return float4(texColor.rgb, alpha);
}

// Techniques
technique Technique1
{
    pass Pass1
    {
        // Compile Vertex Shader
        VertexShader = compile vs_2_0 VS_Main();

        // Compile Pixel Shader
        PixelShader = compile ps_2_0 PS_Main();
    }
}
