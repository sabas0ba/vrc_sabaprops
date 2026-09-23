#ifndef SABA_LIQUID_CANVAS_INCLUDED
#define SABA_LIQUID_CANVAS_INCLUDED

// Canvas の座標系とアトラス配置。
//
// C# 側の LiquidCanvasSolver.cs と同じ定義を持ちます。片方を変えたら
// もう片方も変え、.github/verify/offline/OfflineLiquidTests.cs を通してください。
//
// Canvas 空間は体に固定した直交座標系で、原点は箱の中心、各軸の範囲は
// 半径 (half extents) で割った [-1, 1] に正規化します。
//
// アトラスは 3 列 x 2 行に 6 面を並べます。
//   face 0: +X   face 1: -X   face 2: +Y      (row 0)
//   face 3: -Y   face 4: +Z   face 5: -Z      (row 1)
// 面内の (u, v) 軸は X 面が (z, y)、Y 面が (x, z)、Z 面が (x, y) です。
// 同じ軸の正負 2 面は同じ (u, v) 対応を持ち、別々のタイルに書かれます。

#define SABA_LIQUID_ATLAS_COLUMNS 3.0
#define SABA_LIQUID_ATLAS_ROWS 2.0

int SabaLiquidFace(int axis, float sign)
{
    return axis * 2 + (sign < 0.0 ? 1 : 0);
}

int SabaLiquidFaceAxis(int face)
{
    return face / 2;
}

float2 SabaLiquidTileUv(float3 q, int axis)
{
    float2 pair = axis == 0 ? q.zy : (axis == 1 ? q.xz : q.xy);
    return pair * 0.5 + 0.5;
}

float2 SabaLiquidAtlasUv(int face, float2 tileUv, float inset)
{
    float column = (float)(face % 3);
    float row = (float)(face / 3);
    float2 local = inset + saturate(tileUv) * (1.0 - 2.0 * inset);
    return float2((column + local.x) / SABA_LIQUID_ATLAS_COLUMNS,
                  (row + local.y) / SABA_LIQUID_ATLAS_ROWS);
}

int SabaLiquidFaceAtAtlasUv(float2 uv)
{
    int column = (int)clamp(floor(uv.x * SABA_LIQUID_ATLAS_COLUMNS), 0.0, 2.0);
    int row = (int)clamp(floor(uv.y * SABA_LIQUID_ATLAS_ROWS), 0.0, 1.0);
    return row * 3 + column;
}

float2 SabaLiquidTileUvAtAtlasUv(float2 uv, float inset)
{
    float2 local = frac(uv * float2(SABA_LIQUID_ATLAS_COLUMNS, SABA_LIQUID_ATLAS_ROWS));
    return (local - inset) / (1.0 - 2.0 * inset);
}

// 法線の向きから、各軸で表側にある面の重みを返します。3 軸の和は 1 です。
// 4 乗で鋭くしているのは、斜めの面で 2 枚のタイルが同程度に混ざり、
// 付着の輪郭がぼけるのを抑えるためです。
float3 SabaLiquidAxisWeights(float3 normalCanvas)
{
    float3 n2 = normalCanvas * normalCanvas;
    float3 n4 = n2 * n2;
    return n4 / max(n4.x + n4.y + n4.z, 1e-6);
}

float SabaLiquidFaceWeight(float3 normalCanvas, int face)
{
    int axis = SabaLiquidFaceAxis(face);
    float sign = (face % 2) == 0 ? 1.0 : -1.0;
    float component = axis == 0 ? normalCanvas.x : (axis == 1 ? normalCanvas.y : normalCanvas.z);
    if (component * sign <= 0.0)
    {
        return 0.0;
    }

    float3 weights = SabaLiquidAxisWeights(normalCanvas);
    return axis == 0 ? weights.x : (axis == 1 ? weights.y : weights.z);
}

float SabaLiquidStampFalloff(float distance, float radius)
{
    float t = saturate(1.0 - (distance * distance) / max(radius * radius, 1e-8));
    return t * t;
}

float SabaLiquidHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SabaLiquidValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float2 s = f * f * (3.0 - 2.0 * f);
    float a = SabaLiquidHash(cell);
    float b = SabaLiquidHash(cell + float2(1.0, 0.0));
    float c = SabaLiquidHash(cell + float2(0.0, 1.0));
    float d = SabaLiquidHash(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, s.x), lerp(c, d, s.x), s.y);
}

#endif
