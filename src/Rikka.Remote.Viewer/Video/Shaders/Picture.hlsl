cbuffer Picture : register(b0)
{
    float4 RowR;
    float4 RowG;
    float4 RowB;
    float2 LumaScale;
    float2 ChromaOffset;
    float2 LumaSize;
    float2 Origin;
    uint Mode;
    uint Transfer;
    uint Gamut;
    uint Padding;
    float4 GamutR;
    float4 GamutG;
    float4 GamutB;
};

Texture2DArray<float> Luma : register(t0);
Texture2DArray<float2> Chroma : register(t1);
Texture2DArray<float> ChromaU : register(t1);
Texture2DArray<float> ChromaV : register(t2);
SamplerState Bilinear : register(s0);

static const uint ModeExact = 0;
static const uint TransferPq = 1;
static const uint TransferHlg = 2;
static const float SdrWhiteNits = 203.0;
static const float PeakNits = 1000.0;

struct Vertex
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

Vertex VertexMain(uint id : SV_VertexID)
{
    Vertex output;
    float2 corner = float2((id << 1) & 2, id & 2);
    output.Uv = corner;
    output.Position = float4(corner * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}

float SampleCubic(Texture2DArray<float> plane, float2 uv, float2 size)
{
    float2 position = uv * size;
    float2 center = floor(position - 0.5) + 0.5;
    float2 f = position - center;
    float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    float2 w3 = f * f * (-0.5 + 0.5 * f);
    float2 w12 = w1 + w2;
    float2 p0 = (center - 1.0) / size;
    float2 p3 = (center + 2.0) / size;
    float2 p12 = (center + w2 / w12) / size;
    float result = 0.0;
    result += plane.SampleLevel(Bilinear, float3(p0.x, p0.y, 0), 0) * w0.x * w0.y;
    result += plane.SampleLevel(Bilinear, float3(p12.x, p0.y, 0), 0) * w12.x * w0.y;
    result += plane.SampleLevel(Bilinear, float3(p3.x, p0.y, 0), 0) * w3.x * w0.y;
    result += plane.SampleLevel(Bilinear, float3(p0.x, p12.y, 0), 0) * w0.x * w12.y;
    result += plane.SampleLevel(Bilinear, float3(p12.x, p12.y, 0), 0) * w12.x * w12.y;
    result += plane.SampleLevel(Bilinear, float3(p3.x, p12.y, 0), 0) * w3.x * w12.y;
    result += plane.SampleLevel(Bilinear, float3(p0.x, p3.y, 0), 0) * w0.x * w3.y;
    result += plane.SampleLevel(Bilinear, float3(p12.x, p3.y, 0), 0) * w12.x * w3.y;
    result += plane.SampleLevel(Bilinear, float3(p3.x, p3.y, 0), 0) * w3.x * w3.y;
    return result;
}

float SampleLuma(Vertex input, float2 uv)
{
    float luma;
    [branch]
    if (Mode == ModeExact)
    {
        int2 texel = int2(input.Position.xy - Origin);
        luma = Luma.Load(int4(texel, 0, 0));
    }
    else
    {
        luma = SampleCubic(Luma, uv, LumaSize);
    }

    return luma;
}

float3 PqToNits(float3 value)
{
    const float m1 = 0.1593017578125;
    const float m2 = 78.84375;
    const float c1 = 0.8359375;
    const float c2 = 18.8515625;
    const float c3 = 18.6875;
    float3 power = pow(saturate(value), 1.0 / m2);
    return pow(max(power - c1, 0.0) / (c2 - c3 * power), 1.0 / m1) * 10000.0;
}

float3 HlgToNits(float3 value)
{
    const float a = 0.17883277;
    const float b = 0.28466892;
    const float c = 0.55991073;
    float3 v = saturate(value);
    float3 scene = v <= 0.5 ? v * v / 3.0 : (exp((v - c) / a) + b) / 12.0;
    float luminance = dot(scene, float3(0.2627, 0.6780, 0.0593));
    return PeakNits * pow(max(luminance, 1e-6), 0.2) * scene;
}

float3 ToneMap(float3 linearWhite)
{
    float peak = PeakNits / SdrWhiteNits;
    float luminance = dot(linearWhite, float3(0.2126, 0.7152, 0.0722));
    float mapped = luminance * (1.0 + luminance / (peak * peak)) / (1.0 + luminance);
    return luminance > 0.0 ? linearWhite * (mapped / luminance) : 0.0;
}

float4 Finish(float3 yuv)
{
    float3 rgb = float3(dot(RowR.xyz, yuv) + RowR.w, dot(RowG.xyz, yuv) + RowG.w, dot(RowB.xyz, yuv) + RowB.w);
    if (Transfer == 0 && Gamut == 0)
    {
        return float4(saturate(rgb), 1.0);
    }

    float3 light = Transfer == TransferPq ? PqToNits(rgb) / SdrWhiteNits
        : Transfer == TransferHlg ? HlgToNits(rgb) / SdrWhiteNits
        : pow(saturate(rgb), 2.4);
    if (Gamut != 0)
    {
        light = float3(dot(GamutR.xyz, light), dot(GamutG.xyz, light), dot(GamutB.xyz, light));
    }

    if (Transfer != 0)
    {
        light = ToneMap(light);
    }

    return float4(pow(saturate(light), 1.0 / 2.4), 1.0);
}

float4 TwoPlaneMain(Vertex input) : SV_Target
{
    float2 uv = input.Uv * LumaScale;
    float y = SampleLuma(input, uv);
    float2 uv2 = Chroma.SampleLevel(Bilinear, float3(uv + ChromaOffset, 0), 0);
    return Finish(float3(y, uv2));
}

float4 ThreePlaneMain(Vertex input) : SV_Target
{
    float2 uv = input.Uv * LumaScale;
    float y = SampleLuma(input, uv);
    float u = ChromaU.SampleLevel(Bilinear, float3(uv + ChromaOffset, 0), 0);
    float v = ChromaV.SampleLevel(Bilinear, float3(uv + ChromaOffset, 0), 0);
    return Finish(float3(y, u, v));
}
