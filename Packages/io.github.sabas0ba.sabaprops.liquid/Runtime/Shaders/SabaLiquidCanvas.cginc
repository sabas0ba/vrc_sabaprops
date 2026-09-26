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

// 付着した面の奥行きによる表示の重み。
//
// 6 面アトラスの 1 枚のタイルは、その面の向きを向いた表面のうち (u, v) が同じものを
// すべて同じテクセルで表します。胴の側面と、その外側にある腕の側面は区別できません。
// そこで付着を書くときに、当たった面の奥行き（面の軸方向の Canvas ローカル座標、m）を
// 記録しておき、描くときに受け手の奥行きと比べて、離れていれば描きません。
//
// coverage はその奥行きの記録の確かさで、記録の無いテクセル（coverage 0）は制限しません。
float SabaLiquidDepthMask(float surfaceDepth, float storedDepth, float coverage, float tolerance)
{
    float mask = 1.0 - smoothstep(tolerance, tolerance * 2.0, abs(surfaceDepth - storedDepth));
    return lerp(1.0, mask, saturate(coverage));
}

// 受け手の部位の重み。x: 衣服（体）, y: 髪, z: 肌。和は 1 です。
//
// Projector は受け手の実際のマテリアルを読めないため、部位は位置から推定します。
// 頭の球の中は髪、ただし顔の向き側で頭頂でない所は肌、手の球の中は肌、それ以外は衣服です。
// C# 側の LiquidCanvasSolver.RegionWeights と同じ定義です。
// head.w と hand の w は半径で、0 なら その部位を使いません。
float3 SabaLiquidRegionWeights(float3 p, float4 head, float3 face, float3 up, float4 leftHand, float4 rightHand)
{
    float hair = 0.0;
    float skin = 0.0;

    if (head.w > 0.0)
    {
        float3 offset = p - head.xyz;
        float distance = length(offset);
        float inside = 1.0 - smoothstep(head.w * 0.95, head.w * 1.25, distance);
        float3 direction = offset / max(distance, 1e-5);
        float front = smoothstep(0.1, 0.5, dot(direction, face));
        float crown = smoothstep(0.35, 0.7, dot(direction, up));
        float faceSkin = front * (1.0 - crown);
        hair = inside * (1.0 - faceSkin);
        skin = inside * faceSkin;
    }

    if (leftHand.w > 0.0)
    {
        skin = max(skin, 1.0 - smoothstep(leftHand.w * 0.9, leftHand.w * 1.4, length(p - leftHand.xyz)));
    }

    if (rightHand.w > 0.0)
    {
        skin = max(skin, 1.0 - smoothstep(rightHand.w * 0.9, rightHand.w * 1.4, length(p - rightHand.xyz)));
    }

    hair = min(hair, 1.0 - skin);
    return float3(1.0 - hair - skin, hair, skin);
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

float2 SabaLiquidHash2(float2 p)
{
    return float2(SabaLiquidHash(p), SabaLiquidHash(p + 19.19));
}

// 撥水面の水滴。metric は面内の座標（m）、size は水滴の間隔（m）、wet は液量です。
// x: 水滴の中か（縁を滑らかにした被覆）, y: 水滴の高さ（球の断面）, zw: 水滴の中心から外向きの傾き。
// 液量が少ないほど水滴は小さく疎らになります。
float4 SabaLiquidBeads(float2 metric, float size, float wet)
{
    float2 position = metric / max(size, 1e-4);
    float2 cell = floor(position);
    float2 local = position - cell;
    float amount = saturate(wet * 1.5);
    float best = 0.0;
    float2 slope = 0.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbour = cell + float2(x, y);
            float2 random = SabaLiquidHash2(neighbour);
            if (random.y > amount + 0.15)
            {
                continue;
            }

            float2 centre = float2(x, y) + 0.2 + 0.6 * random;
            float radius = (0.18 + 0.3 * SabaLiquidHash(neighbour + 7.7)) * sqrt(amount);
            float2 d = local - centre;
            float r2 = dot(d, d) / max(radius * radius, 1e-5);
            if (r2 < 1.0)
            {
                float height = sqrt(1.0 - r2);
                if (height > best)
                {
                    best = height;
                    slope = d / max(radius, 1e-4);
                }
            }
        }
    }

    return float4(saturate(best * 5.0), best, slope);
}

#endif
