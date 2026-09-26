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

// 受け手の部位の重み。x: 上半身の衣服, y: 下半身の衣服, z: 髪, w: 肌。靴は 1 から 4 つの和を引いた残りです。
//
// Projector は受け手の実際のマテリアルを読めないため、部位は位置から推定します。
// 頭の球の中は髪、ただし顔の向き側で頭頂でない所は肌、手の球の中は肌、足の球の中は靴、
// 腰の面より下は下半身の衣服、それ以外は上半身の衣服です。
// C# 側の LiquidCanvasSolver.RegionWeights と同じ定義です。
// 球の w は半径、hip の w は境界の幅で、0 ならその部位を使いません。
float4 SabaLiquidRegionWeights(float3 p, float4 head, float3 face, float3 up, float4 leftHand, float4 rightHand,
    float4 hip, float4 leftFoot, float4 rightFoot)
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
    float rest = 1.0 - hair - skin;

    float feet = 0.0;
    if (leftFoot.w > 0.0)
    {
        feet = max(feet, 1.0 - smoothstep(leftFoot.w * 0.9, leftFoot.w * 1.4, length(p - leftFoot.xyz)));
    }

    if (rightFoot.w > 0.0)
    {
        feet = max(feet, 1.0 - smoothstep(rightFoot.w * 0.9, rightFoot.w * 1.4, length(p - rightFoot.xyz)));
    }

    feet = min(feet, rest);
    rest -= feet;

    float lower = 0.0;
    if (hip.w > 0.0)
    {
        lower = rest * smoothstep(hip.w, -hip.w, dot(p - hip.xyz, up));
    }

    return float4(rest - lower, lower, hair, skin);
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

// 撥水面の水滴の 1 層。position は格子単位の座標、density は水滴が置かれる割合、
// gravity は面内の重力方向（単位ベクトル、0 なら伸ばさない）、steepness は面の傾き
// （垂直で 1、水平で 0）です。返り値は SabaLiquidBeads と同じ並びです。
float4 SabaLiquidBeadLayer(float2 position, float density, float amount, float2 gravity, float steepness, float seed)
{
    float2 cell = floor(position);
    float2 local = position - cell;
    // 水平な面では重力の向きが無く、引き伸ばしません。
    bool oriented = dot(gravity, gravity) > 0.5;
    float2 down = oriented ? gravity : float2(0.0, 1.0);
    float2 across = float2(-down.y, down.x);
    float best = 0.0;
    float2 slope = 0.0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbour = cell + float2(x, y) + seed;
            float2 random = SabaLiquidHash2(neighbour);
            if (random.y > density)
            {
                continue;
            }

            // 大きさは小さい方へ偏らせます。大粒はまれで、小粒が多いのが実際の水滴です。
            // さらにまれに、隣と合わさった大きな粒を置きます。
            float size = SabaLiquidHash(neighbour + 7.7);
            float merged = SabaLiquidHash(neighbour + 41.3);
            float radius = (0.06 + 0.3 * size * size + 0.28 * step(0.93, merged)) * sqrt(amount);
            float2 centre = float2(x, y) + 0.05 + 0.9 * SabaLiquidHash2(neighbour + 3.1);
            float2 d = local - centre;

            // 重力方向の下側を引き伸ばします。大きい水滴ほど重みで垂れた形になり、
            // 面が立っているほど強く、水平な面では伸びません。
            float along = dot(d, down);
            float weight = saturate(size + step(0.93, merged) * 0.5);
            float stretch = oriented ? 1.0 + (along > 0.0 ? 1.3 : 0.25) * weight * steepness : 1.0;
            float2 shaped = across * dot(d, across) + down * (along / stretch);
            float r2 = dot(shaped, shaped) / max(radius * radius, 1e-5);
            if (r2 < 1.0)
            {
                float height = sqrt(1.0 - r2) * (0.6 + 0.4 * size);
                if (height > best)
                {
                    best = height;
                    slope = shaped / max(radius, 1e-4);
                }
            }
        }
    }

    return float4(saturate(best * 5.0), best, slope);
}

// 面を伝って垂れる水滴。position は水滴の格子単位の座標、down は面内の重力方向（単位ベクトル）、
// steepness は面の傾き（垂直で 1、水平で 0）、time は秒です。返り値は SabaLiquidBeads と同じ並びです。
//
// 重力方向に沿った列ごとに、まれに垂れる水滴を置きます。水滴は列に沿って落ち、通った跡に
// 細い筋を残します。落ちる速さは面が立っているほど速く、水平な面では垂れません。
float4 SabaLiquidRunnels(float2 position, float2 down, float steepness, float amount, float time)
{
    const float width = 2.5;
    const float period = 7.0;
    float2 across = float2(-down.y, down.x);
    float a = dot(position, down);
    float c = dot(position, across);

    float column = floor(c / width);
    float2 random = SabaLiquidHash2(float2(column, 91.7));
    float density = saturate((amount - 0.3) * 2.0) * steepness * steepness * 0.55;
    if (random.x > density)
    {
        return 0.0;
    }

    // 列の中心は少し揺らし、落ちる途中でわずかに蛇行させます。
    float centre = (column + 0.5) * width + (random.y - 0.5) * width * 0.4
        + sin(a * 0.9 + random.y * 6.283) * 0.18;
    float dx = c - centre;

    // 大きい滴ほど速く落ちます。列ごとに周期をずらし、そろって落ちないようにします。
    float big = SabaLiquidHash(float2(column, 13.1));
    float speed = lerp(0.8, 2.6, big) * steepness;
    float phase = SabaLiquidHash(float2(column, 57.3));
    float span = period * (1.0 + phase);
    float local = frac((a + phase * span) / span) * span;
    float head = frac(time * speed / span + phase) * span;

    float radius = 0.28 + 0.22 * big;
    float4 result = 0.0;

    // 先頭の滴。下側に重みが寄った楕円です。
    float along = local - head;
    float2 shaped = float2(dx, along / (along > 0.0 ? 1.45 : 1.1));
    float r2 = dot(shaped, shaped) / (radius * radius);
    if (r2 < 1.0)
    {
        float height = sqrt(1.0 - r2) * 0.95;
        float2 slope = (across * shaped.x + down * shaped.y) / radius;
        result = float4(saturate(height * 5.0), height, slope);
    }

    // 通った跡。先頭に近いほど太く、上へ行くほど細くなって途切れます。
    float trail = min(head, 3.2 * (0.6 + big));
    float behind = head - local;
    if (behind > 0.0 && behind < trail)
    {
        float halfWidth = radius * 0.32 * (1.0 - behind / trail);
        float t = abs(dx) / max(halfWidth, 1e-4);
        if (t < 1.0)
        {
            float height = (1.0 - t * t) * 0.3 * (1.0 - behind / trail);
            if (height > result.y)
            {
                result = float4(saturate(height * 6.0), height, across * (dx / max(halfWidth, 1e-4)) * 0.5);
            }
        }
    }

    return result;
}

// 撥水面の水滴。metric は面内の座標（m）、size は水滴の間隔（m）、wet は液量、
// gravity は面内の重力方向（単位ベクトル、水平な面では 0）、steepness は面の傾き
// （垂直で 1、水平で 0）、time は秒です。
// x: 水滴の中か（縁を滑らかにした被覆）, y: 水滴の高さ（球の断面）, zw: 水滴の中心から外向きの傾き。
//
// 格子に 1 つずつ置くだけでは並びと大きさがそろって見えるため、細かい層と粗い層を重ね、
// 低い周波数のむらで水滴の多い所と少ない所を作ります。液量が少ないほど小さく疎らになります。
// 立った面で液量が多いと、一部の水滴が重みで垂れ、筋を引いて落ちていきます。
float4 SabaLiquidBeads(float2 metric, float size, float wet, float2 gravity, float steepness, float time)
{
    float amount = saturate(wet * 1.5);
    float2 position = metric / max(size, 1e-4);
    float cluster = 0.35 + 1.1 * SabaLiquidValueNoise(position * 0.18 + 5.3);
    float density = saturate((amount + 0.15) * cluster);

    float4 fine = SabaLiquidBeadLayer(position, density, amount, gravity, steepness, 0.0);

    // 粗い層は格子を 35 度回し、細かい層と並びがそろわないようにします。
    float2x2 turn = float2x2(0.819, -0.574, 0.574, 0.819);
    float4 coarse = SabaLiquidBeadLayer(mul(turn, position) * 0.42, density * 0.45, amount,
        mul(turn, gravity), steepness, 17.0);
    float4 beads = coarse.y > fine.y ? coarse : fine;

    if (dot(gravity, gravity) > 0.5 && steepness > 0.2)
    {
        float4 runnel = SabaLiquidRunnels(position * 0.5, gravity, steepness, amount, time);
        runnel.zw *= 0.5;
        beads = runnel.y > beads.y ? runnel : beads;
    }

    return beads;
}

#endif
