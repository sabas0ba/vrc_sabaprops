// Canvas の RenderTexture を 1 周期ぶん進める Blit 用シェーダ。
//
// Pass 0: 顔料（premultiplied RGB と被覆率 A）
// Pass 1: 液膜（R: 液量, G: 平滑度, B: 粘性, A: 蒸発率）
// Pass 2: 消去
// Pass 3: 奥行き（R: 付着した面の奥行き（m）, G: 記録の確かさ）
//
// 1 回の Blit で、重力方向への流下、DrawOp の適用、蒸発をまとめて行います。
// 流下と蒸発は液膜の状態で決まり、顔料は液膜に運ばれるだけです。
Shader "Hidden/SabaProps/Liquid/Canvas Update"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _FilmTex ("Film", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "SabaLiquidCanvas.cginc"

    #define SABA_LIQUID_MAX_STAMPS 16

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _FilmTex;

    float _StampCount;
    // xyz: Canvas ローカルの中心（m）, w: 半径（m）
    float4 _StampPos[SABA_LIQUID_MAX_STAMPS];
    // xyz: Canvas ローカルの法線, w: 顔料の量
    float4 _StampNormal[SABA_LIQUID_MAX_STAMPS];
    // rgb: 顔料の色, a: 液膜の量
    float4 _StampColor[SABA_LIQUID_MAX_STAMPS];
    // x: 洗浄量, y: 平滑度, z: 粘性, w: 蒸発率（符号化済み）
    float4 _StampFilm[SABA_LIQUID_MAX_STAMPS];
    // x: 乱数シード, y: 輪郭の不規則さ
    float4 _StampShape[SABA_LIQUID_MAX_STAMPS];

    float4 _HalfExtents;
    float4 _GravityCanvas;
    float _Inset;
    float _DeltaTime;
    float _FlowSpeed;
    float _MaxEvaporationRate;
    // x: 洗う範囲の上端（Canvas 正規化 y）, y: 洗う量, z: 境界の幅
    float4 _Wash;

    struct CanvasTexel
    {
        int face;
        int axis;
        float2 tileUv;
        float2 metric;
        float2 tileMin;
        float2 tileMax;
    };

    float2 SabaLiquidPair(float3 v, int axis)
    {
        return axis == 0 ? v.zy : (axis == 1 ? v.xz : v.xy);
    }

    CanvasTexel SabaLiquidTexel(float2 uv)
    {
        CanvasTexel t;
        t.face = SabaLiquidFaceAtAtlasUv(uv);
        t.axis = SabaLiquidFaceAxis(t.face);
        t.tileUv = SabaLiquidTileUvAtAtlasUv(uv, _Inset);
        t.metric = (t.tileUv * 2.0 - 1.0) * SabaLiquidPair(_HalfExtents.xyz, t.axis);
        t.tileMin = SabaLiquidAtlasUv(t.face, float2(0.0, 0.0), _Inset) + _MainTex_TexelSize.xy * 0.5;
        t.tileMax = SabaLiquidAtlasUv(t.face, float2(1.0, 1.0), _Inset) - _MainTex_TexelSize.xy * 0.5;
        return t;
    }

    // 重力方向に一周期で進む距離だけ上流の uv。タイルの外には出しません。
    float2 SabaLiquidUpstreamUv(float2 uv, CanvasTexel t)
    {
        float2 gravity = SabaLiquidPair(_GravityCanvas.xyz, t.axis);
        float2 tileScale = (1.0 - 2.0 * _Inset)
            / (2.0 * SabaLiquidPair(_HalfExtents.xyz, t.axis))
            / float2(SABA_LIQUID_ATLAS_COLUMNS, SABA_LIQUID_ATLAS_ROWS);
        float2 upstream = uv - gravity * tileScale * (_FlowSpeed * _DeltaTime);
        return clamp(upstream, t.tileMin, t.tileMax);
    }

    // 流れやすさ。液量が多く粘性が低いほど大きく、筋状の揺らぎを持ちます。
    float SabaLiquidMobility(float4 film, CanvasTexel t)
    {
        float2 gravity = SabaLiquidPair(_GravityCanvas.xyz, t.axis);
        float2 across = float2(-gravity.y, gravity.x);
        float streak = lerp(0.35, 1.0,
            SabaLiquidValueNoise(float2(dot(t.metric, across) * 40.0, (float)t.face * 7.0)));
        return saturate(film.r * (1.0 - film.b) * streak);
    }

    // テクセルの高さ（Canvas 正規化 y）。
    // X 面と Z 面はタイルの v がそのまま y です。Y 面のテクセルは高さを持たないため、
    // 上面は頭や肩の高さ、下面は足裏の高さと見なします。
    float SabaLiquidTexelHeight(CanvasTexel t)
    {
        if (t.axis == 1)
        {
            return t.face == 2 ? 0.8 : -1.0;
        }

        return t.tileUv.y * 2.0 - 1.0;
    }

    float SabaLiquidStampAt(int i, CanvasTexel t)
    {
        float face = SabaLiquidFaceWeight(_StampNormal[i].xyz, t.face);
        if (face <= 0.0)
        {
            return 0.0;
        }

        float radius = max(_StampPos[i].w, 1e-4);
        float2 center = SabaLiquidPair(_StampPos[i].xyz, t.axis);
        float2 offset = t.metric - center;
        float noise = SabaLiquidValueNoise(t.metric * (3.0 / radius) + _StampShape[i].x);
        float shaped = radius * (1.0 + _StampShape[i].y * (noise - 0.5));
        return face * SabaLiquidStampFalloff(length(offset), shaped);
    }

    struct appdata_blit
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f_blit
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f_blit vert(appdata_blit v)
    {
        v2f_blit o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    float4 fragPigment(v2f_blit input) : SV_Target
    {
        CanvasTexel t = SabaLiquidTexel(input.uv);
        float2 upstreamUv = SabaLiquidUpstreamUv(input.uv, t);

        float4 self = tex2D(_MainTex, input.uv);
        float4 upstream = tex2D(_MainTex, upstreamUv);
        float leave = SabaLiquidMobility(tex2D(_FilmTex, input.uv), t);
        float arrive = SabaLiquidMobility(tex2D(_FilmTex, upstreamUv), t);
        float4 pigment = self * (1.0 - leave) + upstream * arrive;

        float edge = max(_Wash.z, 1e-4);
        float washed = smoothstep(_Wash.x + edge, _Wash.x - edge, SabaLiquidTexelHeight(t));
        pigment *= 1.0 - saturate(washed * _Wash.y);

        int count = (int)_StampCount;
        for (int i = 0; i < SABA_LIQUID_MAX_STAMPS; i++)
        {
            if (i >= count)
            {
                break;
            }

            float k = SabaLiquidStampAt(i, t);
            float wash = saturate(k * _StampFilm[i].x);
            pigment *= 1.0 - wash;

            float cover = saturate(k * _StampNormal[i].w);
            pigment = pigment * (1.0 - cover) + float4(_StampColor[i].rgb * cover, cover);
        }

        return saturate(pigment);
    }

    float4 fragFilm(v2f_blit input) : SV_Target
    {
        CanvasTexel t = SabaLiquidTexel(input.uv);
        float2 upstreamUv = SabaLiquidUpstreamUv(input.uv, t);

        float4 self = tex2D(_MainTex, input.uv);
        float4 upstream = tex2D(_MainTex, upstreamUv);
        float leave = SabaLiquidMobility(self, t);
        float arrive = SabaLiquidMobility(upstream, t);

        // 液量で重み付けして性質を運ぶ。液の無いところの性質が混ざらないようにするため。
        float amount = self.r * (1.0 - leave) + upstream.r * arrive;
        float3 carried = self.gba * self.r * (1.0 - leave) + upstream.gba * upstream.r * arrive;
        float3 properties = amount > 1e-4 ? carried / amount : self.gba;

        int count = (int)_StampCount;
        for (int i = 0; i < SABA_LIQUID_MAX_STAMPS; i++)
        {
            if (i >= count)
            {
                break;
            }

            float added = saturate(SabaLiquidStampAt(i, t) * _StampColor[i].a);
            float total = saturate(amount + added);
            float share = total > 1e-4 ? added / total : 0.0;
            properties = lerp(properties, _StampFilm[i].yzw, share);
            amount = total;
        }

        amount = max(0.0, amount - _DeltaTime * properties.z * _MaxEvaporationRate);
        return float4(amount, properties);
    }

    // 付着した面の奥行きを記録します。SabaLiquidDepthMask の説明を参照してください。
    // 流下で運ばれた液は上流の奥行きを引き継ぎ、新しい付着はその面の奥行きで上書きします。
    // 洗浄だけの DrawOp は奥行きを変えません。
    float4 fragDepth(v2f_blit input) : SV_Target
    {
        CanvasTexel t = SabaLiquidTexel(input.uv);
        float2 upstreamUv = SabaLiquidUpstreamUv(input.uv, t);

        float2 depth = tex2D(_MainTex, input.uv).rg;
        float2 upstream = tex2D(_MainTex, upstreamUv).rg;
        float arrive = SabaLiquidMobility(tex2D(_FilmTex, upstreamUv), t) * upstream.g;
        if (arrive > 0.0)
        {
            depth.r = lerp(depth.r, upstream.r, depth.g > 0.0 ? arrive * 0.5 : 1.0);
            depth.g = max(depth.g, arrive);
        }

        int count = (int)_StampCount;
        for (int i = 0; i < SABA_LIQUID_MAX_STAMPS; i++)
        {
            if (i >= count)
            {
                break;
            }

            if (_StampNormal[i].w + _StampColor[i].a <= 0.0)
            {
                continue;
            }

            float share = saturate(SabaLiquidStampAt(i, t) * 4.0);
            if (share <= 0.0)
            {
                continue;
            }

            float3 centre = _StampPos[i].xyz;
            float stampDepth = t.axis == 0 ? centre.x : (t.axis == 1 ? centre.y : centre.z);
            depth.r = lerp(depth.r, stampDepth, depth.g > 0.0 ? share : 1.0);
            depth.g = max(depth.g, share);
        }

        return float4(depth, 0.0, 0.0);
    }

    float4 fragClear(v2f_blit input) : SV_Target
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }
    ENDCG

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Pigment"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragPigment
            ENDCG
        }

        Pass
        {
            Name "Film"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragFilm
            ENDCG
        }

        Pass
        {
            Name "Clear"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragClear
            ENDCG
        }

        Pass
        {
            Name "Depth"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragDepth
            ENDCG
        }
    }

    Fallback Off
}
