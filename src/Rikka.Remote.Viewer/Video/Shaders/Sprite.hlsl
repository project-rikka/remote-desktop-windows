cbuffer Sprite : register(b0)
{
    float4 Rectangle;
};

Texture2D<float4> Image : register(t0);
SamplerState Bilinear : register(s0);

struct Vertex
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

Vertex VertexMain(uint id : SV_VertexID)
{
    Vertex output;
    float2 corner = float2(id & 1, (id >> 1) & 1);
    output.Uv = corner;
    float2 position = lerp(Rectangle.xy, Rectangle.zw, corner);
    output.Position = float4(position, 0.0, 1.0);
    return output;
}

float4 PixelMain(Vertex input) : SV_Target
{
    return Image.SampleLevel(Bilinear, input.Uv, 0);
}
