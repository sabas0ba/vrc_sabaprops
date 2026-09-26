// アバターへ Body Canvas の内容を重ねる Projector 用シェーダ。
//
// Projector の投影座標は使いません。受け手のワールド座標を Canvas 空間へ変換し、
// 法線の向きで 6 面アトラスから 3 枚を選んで混ぜます。Projector は
// 「どの Renderer をもう一度描くか」を選ぶためだけに置いています。
//
// 出力は premultiplied alpha です。
//   rgb: 顔料の色 + 液膜の反射
//   a  : 顔料の被覆と、液膜による下地の暗化
// Blend One OneMinusSrcAlpha で、下地は (1 - a) 倍に暗くなり、rgb が加わります。
//
// 照明について。Projector の描画パスにはライトの定数が設定されないため、
// ワールドの主光源は LiquidLighting がグローバルなシェーダ定数で渡します。
// 渡されていない場合は環境光だけで照らします。
//
// 受け手の素材について。同じ液でも、布は吸って暗くなり、革や樹脂ははじいて水滴になり、
// 肌には薄く広がります。受け手の実際のマテリアルは読めないため、部位（衣服、髪、肌）を
// 頭と手の位置から推定し、部位ごとの素材（LiquidSurfaceProfile）を混ぜて使います。
Shader "SabaProps/Liquid/Body Projector"
{
    Properties
    {
        [NoScaleOffset] _PigmentTex ("Pigment Canvas", 2D) = "black" {}
        [NoScaleOffset] _FilmTex ("Film Canvas", 2D) = "black" {}
        [NoScaleOffset] _DepthTex ("Depth Canvas", 2D) = "black" {}
        [NoScaleOffset] _GlowTex ("Glow Canvas", 2D) = "black" {}
        _FluorescentGain ("Fluorescent Emission", Range(0, 8)) = 2.5
        _LuminousGain ("Luminous Emission", Range(0, 4)) = 0.8
        _DepthTolerance ("Surface Depth Tolerance (m)", Range(0.01, 0.3)) = 0.12
        _WetDarken ("Wet Darkening", Range(0, 1)) = 0.5
        _WetReflection ("Wet Reflection", Range(0, 2)) = 1
        _WetSpecular ("Wet Highlight", Range(0, 4)) = 1.5
        _DryLighten ("Dry Pigment Lightening", Range(1, 1.5)) = 1.15
        _Relief ("Thickness Relief", Range(0, 8)) = 1.5
        _PigmentGrain ("Pigment Grain", Range(0, 0.5)) = 0.12
        _EdgeFade ("Canvas Edge Fade", Range(0.001, 0.3)) = 0.04
        _AmbientResponse ("Ambient Lighting Response", Range(0, 1)) = 1

        // 受け手の素材（LiquidSurfaceProfile）。A: 吸水性, 撥水性, 艶, 流れにくさ / B: にじみ, 毛束, 水滴の大きさ
        _SurfaceBodyA ("Body Surface A", Vector) = (0.8, 0.1, 0.1, 0.5)
        _SurfaceBodyB ("Body Surface B", Vector) = (0.5, 0, 0.012, 0)
        _SurfaceHairA ("Hair Surface A", Vector) = (0.35, 0.6, 0.5, 0.45)
        _SurfaceHairB ("Hair Surface B", Vector) = (0.2, 0.8, 0.01, 0)
        _SurfaceLowerA ("Lower Body Surface A", Vector) = (0.45, 0.5, 0.35, 0.5)
        _SurfaceLowerB ("Lower Body Surface B", Vector) = (0.25, 0, 0.012, 0)
        _SurfaceFeetA ("Feet Surface A", Vector) = (0.12, 0.8, 0.65, 0.35)
        _SurfaceFeetB ("Feet Surface B", Vector) = (0.08, 0, 0.014, 0)
        _SurfaceSkinA ("Skin Surface A", Vector) = (0.2, 0.15, 0.45, 0.8)
        _SurfaceSkinB ("Skin Surface B", Vector) = (0.12, 0, 0.012, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent+20" "IgnoreProjector"="True" }

        Pass
        {
            ZWrite Off
            ColorMask RGB
            Offset -1, -1
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SabaLiquidCanvas.cginc"

            sampler2D _PigmentTex;
            float4 _PigmentTex_TexelSize;
            sampler2D _FilmTex;
            sampler2D _DepthTex;
            sampler2D _GlowTex;
            float _DepthTolerance;
            float _FluorescentGain;
            float _LuminousGain;
            // 発光。x: 紫外線の強さ, y: 蓄光の蓄えた量
            float4 _Glow;
            // 明かりの範囲が与える光。_LocalLight.xyz: 光源へ向かう方向, w: 1 = 光源あり。
            // _LocalAmbient.a が 1 のとき、主光源と環境光をこれらで置き換えます。
            float4 _LocalLight;
            float4 _LocalLightColor;
            float4 _LocalAmbient;
            // 結露。x: 結露の量, y: 湿度
            float4 _Condensation;
            float4 _CanvasRowX;
            float4 _CanvasRowY;
            float4 _CanvasRowZ;
            float _CanvasInset;
            // 浸漬による付着。高さはすべて Canvas の正規化 y です。
            // x: 液膜の上端, y: 顔料の上端, z: 境界の幅, w: 1 = 有効
            float4 _ImmersionLevels;
            // x: 液膜の量, y: 顔料の被覆, z: 液膜の平滑度
            float4 _ImmersionAmounts;
            float4 _ImmersionColor;
            // 雪と雨。x: 積雪の深さ, y: 上を向いた面の水（雨と溶けた雪）, w: 1 = 有効
            float4 _Snow;
            float _WetDarken;
            float _WetReflection;
            float _WetSpecular;
            float _DryLighten;
            float _Relief;
            float _PigmentGrain;
            float _EdgeFade;
            float _AmbientResponse;

            // 部位の推定に使う頭と手（xyz: ワールド位置, w: 半径、0 で無効）。
            float4 _RegionHead;
            float4 _RegionHandL;
            float4 _RegionHandR;
            // 腰の面（xyz: 面上の点, w: 境界の幅）と足（xyz: 位置, w: 半径）。
            float4 _RegionHip;
            float4 _RegionFootL;
            float4 _RegionFootR;
            float4 _SurfaceLowerA;
            float4 _SurfaceLowerB;
            float4 _SurfaceFeetA;
            float4 _SurfaceFeetB;
            float4 _SurfaceBodyA;
            float4 _SurfaceBodyB;
            float4 _SurfaceHairA;
            float4 _SurfaceHairB;
            float4 _SurfaceSkinA;
            float4 _SurfaceSkinB;

            // LiquidLighting が設定する主光源。xyz: 光源へ向かう方向, w: 1 = 有効
            float4 _Udon_SabaLiquidLightDirection;
            float4 _Udon_SabaLiquidLightColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 法線が最も向いている面のタイル。起伏、にじみ、水滴、毛束の計算に使います。
            struct DominantTile
            {
                float2 uv;
                float2 metric;
                float3 uAxis;
                float3 vAxis;
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            float3 CanvasPoint(float3 worldPos)
            {
                float4 p = float4(worldPos, 1.0);
                return float3(dot(_CanvasRowX, p), dot(_CanvasRowY, p), dot(_CanvasRowZ, p));
            }

            // 行ベクトルは axis / halfExtent なので、方向は長さで割り戻して単位化します。
            float3 CanvasDirection(float3 worldDirection)
            {
                float3 d = float3(
                    dot(_CanvasRowX.xyz, worldDirection) / max(length(_CanvasRowX.xyz), 1e-4),
                    dot(_CanvasRowY.xyz, worldDirection) / max(length(_CanvasRowY.xyz), 1e-4),
                    dot(_CanvasRowZ.xyz, worldDirection) / max(length(_CanvasRowZ.xyz), 1e-4));
                return normalize(d);
            }

            // Canvas の軸のワールド方向（単位ベクトル）。
            float3 CanvasAxis(int axis)
            {
                float3 row = axis == 0 ? _CanvasRowX.xyz : (axis == 1 ? _CanvasRowY.xyz : _CanvasRowZ.xyz);
                return row / max(length(row), 1e-6);
            }

            float3 CanvasHalfExtents()
            {
                return float3(1.0 / max(length(_CanvasRowX.xyz), 1e-4),
                              1.0 / max(length(_CanvasRowY.xyz), 1e-4),
                              1.0 / max(length(_CanvasRowZ.xyz), 1e-4));
            }

            DominantTile Dominant(float3 q, float3 n)
            {
                float3 a = abs(n);
                int axis = a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
                float component = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);
                int face = SabaLiquidFace(axis, component);

                DominantTile tile;
                tile.uv = SabaLiquidAtlasUv(face, SabaLiquidTileUv(q, axis), _CanvasInset);
                float3 surface = q * CanvasHalfExtents();
                // タイルの (u, v) に対応する Canvas の軸。X 面は (z, y)、Y 面は (x, z)、Z 面は (x, y)。
                tile.metric = axis == 0 ? surface.zy : (axis == 1 ? surface.xz : surface.xy);
                tile.uAxis = CanvasAxis(axis == 0 ? 2 : 0);
                tile.vAxis = CanvasAxis(axis == 1 ? 2 : 1);
                return tile;
            }

            // 付着の厚み。顔料は被覆率、液膜は液量と粘性で決め、粘性の高い液ほど盛り上がります。
            float Thickness(float2 uv)
            {
                float4 pigment = tex2D(_PigmentTex, uv);
                float4 film = tex2D(_FilmTex, uv);
                return pigment.a * 0.6 + film.r * (0.25 + 0.75 * film.b);
            }

            void SampleCanvas(float3 q, float3 n, out float4 pigment, out float4 film, out float2 glow)
            {
                float3 weights = SabaLiquidAxisWeights(n);
                // 受け手の奥行きをメートルに戻して、付着した面の奥行きと比べます。
                float3 surface = q * CanvasHalfExtents();
                pigment = 0.0;
                film = 0.0;
                glow = 0.0;

                [unroll]
                for (int axis = 0; axis < 3; axis++)
                {
                    float component = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);
                    float weight = axis == 0 ? weights.x : (axis == 1 ? weights.y : weights.z);
                    int face = SabaLiquidFace(axis, component);
                    float2 uv = SabaLiquidAtlasUv(face, SabaLiquidTileUv(q, axis), _CanvasInset);

                    float2 depth = tex2D(_DepthTex, uv).rg;
                    float surfaceDepth = axis == 0 ? surface.x : (axis == 1 ? surface.y : surface.z);
                    weight *= SabaLiquidDepthMask(surfaceDepth, depth.r, depth.g, _DepthTolerance);

                    pigment += tex2D(_PigmentTex, uv) * weight;
                    film += tex2D(_FilmTex, uv) * weight;
                    glow += tex2D(_GlowTex, uv).rg * weight;
                }
            }

            // 繊維に沿ったにじみ。顔料を周りの 4 点と平均し、縁をぼかします。
            float4 BleedPigment(DominantTile tile, float bleed)
            {
                float2 offset = _PigmentTex_TexelSize.xy * (1.0 + 3.0 * bleed);
                float4 sum = tex2D(_PigmentTex, tile.uv);
                sum += tex2D(_PigmentTex, tile.uv + float2(offset.x, 0.0));
                sum += tex2D(_PigmentTex, tile.uv - float2(offset.x, 0.0));
                sum += tex2D(_PigmentTex, tile.uv + float2(0.0, offset.y));
                sum += tex2D(_PigmentTex, tile.uv - float2(0.0, offset.y));
                return sum * 0.2;
            }

            // 付着の厚みの勾配から法線を傾けます。垂れや塊が盛り上がって見え、ハイライトが縁に乗ります。
            float3 ReliefNormal(DominantTile tile, float3 worldNormal)
            {
                float2 step = _PigmentTex_TexelSize.xy;
                float du = Thickness(tile.uv + float2(step.x, 0.0)) - Thickness(tile.uv - float2(step.x, 0.0));
                float dv = Thickness(tile.uv + float2(0.0, step.y)) - Thickness(tile.uv - float2(0.0, step.y));
                return normalize(worldNormal - _Relief * (du * tile.uAxis + dv * tile.vAxis));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 q = CanvasPoint(input.worldPos);
                float boundary = 1.0 - max(abs(q.x), max(abs(q.y), abs(q.z)));
                clip(boundary);
                float fade = smoothstep(0.0, _EdgeFade, boundary);

                float3 worldNormal = normalize(input.worldNormal);
                float3 n = CanvasDirection(worldNormal);

                float4 pigment;
                float4 film;
                float2 glow;
                SampleCanvas(q, n, pigment, film, glow);

                // 発光する顔料の色。浸漬や雪を重ねる前の顔料から取ります。
                float3 glowHue = pigment.rgb / max(pigment.a, 1e-3);
                float glowAmount = glow.x * _Glow.x * _FluorescentGain + glow.y * _Glow.y * _LuminousGain;

                // 結露は体の表面全体に付く薄い水です。描き方は液膜と同じで、はじく素材では水滴になります。
                float condensation = saturate(_Condensation.x);
                if (condensation > 0.001)
                {
                    float dew = condensation * 0.7;
                    film.g = lerp(film.g, 0.95, saturate(dew - film.r));
                    film.r = max(film.r, dew);
                }

                // 浸漬は Canvas に焼かず、液面の高さから直接求めます。
                // 高さはテクセルからは復元できないが、ここでは受け手の位置が分かるため。
                if (_ImmersionLevels.w > 0.5)
                {
                    float edge = max(_ImmersionLevels.z, 1e-4);
                    float y = q.y + (SabaLiquidValueNoise(q.xz * 9.0) - 0.5) * edge;

                    float pigmentBelow = smoothstep(_ImmersionLevels.y + edge, _ImmersionLevels.y - edge, y);
                    float cover = saturate(pigmentBelow * _ImmersionAmounts.y);
                    pigment = pigment * (1.0 - cover) + float4(_ImmersionColor.rgb * cover, cover);

                    float filmBelow = smoothstep(_ImmersionLevels.x + edge, _ImmersionLevels.x - edge, y);
                    float immersionWet = filmBelow * _ImmersionAmounts.x;
                    film.g = lerp(film.g, _ImmersionAmounts.z, saturate(immersionWet - film.r));
                    film.r = max(film.r, immersionWet);
                }

                // 雪は上を向いた面に積もり、縁はむらになります。雨と溶けた雪の水は上を向いた面ほど多く残ります。
                float snow = 0.0;
                if (_Snow.w > 0.5)
                {
                    float upFacing = saturate((worldNormal.y - 0.15) / 0.55);
                    float patch = SabaLiquidValueNoise(input.worldPos.xz * 18.0 + input.worldPos.y * 7.0);
                    snow = smoothstep(0.25, 0.6, _Snow.x * upFacing * 1.3 + (patch - 0.5) * 0.35);
                    float melt = _Snow.y * saturate(upFacing * 1.5 + 0.2);
                    film.g = lerp(film.g, 0.9, saturate(melt - film.r));
                    film.r = max(film.r, melt);
                }

                if (pigment.a <= 0.001 && film.r <= 0.001 && snow <= 0.001 && glowAmount <= 0.001)
                {
                    return fixed4(0.0, 0.0, 0.0, 0.0);
                }

                // 受け手の素材。部位の重みで 5 つのプロファイルを混ぜます。
                float4 regions = SabaLiquidRegionWeights(input.worldPos, _RegionHead, CanvasAxis(2), CanvasAxis(1),
                    _RegionHandL, _RegionHandR, _RegionHip, _RegionFootL, _RegionFootR);
                float feetWeight = saturate(1.0 - regions.x - regions.y - regions.z - regions.w);
                float4 surfaceA = _SurfaceBodyA * regions.x + _SurfaceLowerA * regions.y + _SurfaceHairA * regions.z
                    + _SurfaceSkinA * regions.w + _SurfaceFeetA * feetWeight;
                float4 surfaceB = _SurfaceBodyB * regions.x + _SurfaceLowerB * regions.y + _SurfaceHairB * regions.z
                    + _SurfaceSkinB * regions.w + _SurfaceFeetB * feetWeight;
                float absorbency = saturate(surfaceA.x);
                float repellency = saturate(surfaceA.y);
                float sheen = saturate(surfaceA.z);
                float bleed = saturate(surfaceB.x);
                float strands = saturate(surfaceB.y);
                // 結露の水滴は、付着した液の水滴より細かくなります。
                float beadSize = max(surfaceB.z * lerp(1.0, 0.45, condensation), 0.002);

                DominantTile tile = Dominant(q, n);

                // にじみ：吸う素材ほど顔料の縁がぼやけます。
                if (bleed > 0.01 && pigment.a > 0.001)
                {
                    pigment = lerp(pigment, BleedPigment(tile, bleed), bleed);
                }

                // 雪の下の液は見えません。
                float wet = saturate(film.r) * (1.0 - snow);

                // 毛束：液が上下方向の筋にまとまります。
                if (strands > 0.01)
                {
                    float clump = 0.3 + 1.4 * SabaLiquidValueNoise(float2(tile.metric.x * 140.0, tile.metric.y * 5.0));
                    float s = lerp(1.0, clump, strands);
                    wet = saturate(wet * s);
                    pigment *= saturate(s);
                }

                // 雪は白くつや消しの顔料として重ねます。毛束の揺らぎは液だけにかけ、雪にはかけません。
                pigment = pigment * (1.0 - snow) + float4(float3(0.9, 0.92, 0.95) * snow, snow);

                // 水滴：はじく素材では、顔料の無い液膜が面ではなく水滴として見えます。
                // 結露は肌のように吸わない面でも水滴（汗や露）になります。
                float beadWeight = max(repellency, condensation * (1.0 - absorbency)) * (1.0 - saturate(pigment.a * 2.0));
                // 面内の重力方向。タイルの軸へ投影し、面が水平に近いときは伸ばしません。
                float2 gravity = float2(dot(float3(0.0, -1.0, 0.0), tile.uAxis), dot(float3(0.0, -1.0, 0.0), tile.vAxis));
                gravity = dot(gravity, gravity) > 0.04 ? normalize(gravity) : float2(0.0, 0.0);
                float4 beads = beadWeight > 0.01 ? SabaLiquidBeads(tile.metric, beadSize, wet, gravity) : 0.0;
                float sheet = wet * (1.0 - beadWeight);
                float beaded = beads.x * beadWeight * saturate(wet * 3.0);
                float wetLook = saturate(sheet + beaded);

                float3 shaded = ReliefNormal(tile, worldNormal);
                shaded = normalize(shaded + (beads.z * tile.uAxis + beads.w * tile.vAxis) * beaded * 1.5);
                float3 viewDir = normalize(UnityWorldSpaceViewDir(input.worldPos));

                // 吸う素材は艶が出にくく、はじく素材や肌は濡れると表面全体が艶を帯びます。水滴の上は最も滑らかです。
                float smoothness = max(saturate(film.g) * lerp(1.0, 0.3, absorbency), sheen);
                smoothness = lerp(smoothness, 1.0, saturate(beaded));

                // 投影の描画では受け手のプローブ定数が設定されない場合があるため、
                // 環境光は SH と固定の環境色の大きい方を使います。
                float3 ambient = max(unity_AmbientSky.rgb,
                    max(UNITY_LIGHTMODEL_AMBIENT.rgb, max(0.0, ShadeSH9(float4(shaded, 1.0)))));

                // 明かりの範囲の中では、その範囲の光と環境光で照らします。
                bool localLight = _LocalAmbient.a > 0.5;
                float4 mainLight = localLight ? _LocalLight : _Udon_SabaLiquidLightDirection;
                float3 mainColor = localLight ? _LocalLightColor.rgb : _Udon_SabaLiquidLightColor.rgb;
                ambient = localLight ? _LocalAmbient.rgb : ambient;

                float3 diffuse = lerp(float3(1.0, 1.0, 1.0), ambient, _AmbientResponse);
                float3 highlight = 0.0;

                if (mainLight.w > 0.5)
                {
                    float3 lightDir = normalize(mainLight.xyz);
                    float3 lightColor = mainColor;
                    diffuse += lightColor * saturate(dot(shaded, lightDir));

                    // 正規化 Blinn-Phong。平滑度から指数を決め、濡れているほど強く光ります。
                    float3 halfDir = normalize(lightDir + viewDir);
                    float exponent = exp2(10.0 * smoothness + 1.0);
                    float normalization = (exponent + 8.0) / (8.0 * UNITY_PI);
                    highlight = lightColor * pow(saturate(dot(shaded, halfDir)), exponent) * normalization
                        * saturate(dot(shaded, lightDir)) * wetLook * smoothness * _WetSpecular;
                }

                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(shaded, viewDir)), 5.0);
                float3 environment = localLight
                    ? ambient + mainColor * 0.2
                    : max(0.0, ShadeSH9(float4(reflect(-viewDir, shaded), 1.0)));
                // 水滴は丸い表面が周りを映すため、平らな液膜より反射が強く見えます。
                float3 reflection = environment * fresnel * (wetLook + beaded) * smoothness * _WetReflection;

                // 顔料の粒状のむら。広い面が一様な色で平板に見えるのを抑えます。
                float grain = 1.0 + _PigmentGrain * (SabaLiquidValueNoise(q.xy * 60.0 + q.z * 17.0) - 0.5) * 2.0;

                // 乾いた顔料は光を散らして明るく艶が無くなり、吸う素材に染みた顔料は深い色になります。
                float soak = lerp(1.0, 0.78, absorbency * wet);
                float3 pigmentColor = pigment.rgb * grain * diffuse * lerp(_DryLighten, 1.0, wet) * soak;

                // 下地の暗化は吸う素材ほど強く、はじく素材では水滴の所だけ少し暗くなります。
                float darkenStrength = _WetDarken * lerp(0.25, 1.2, absorbency);
                // 水滴の中は、光が屈折して下地の見え方が暗く沈みます。
                // 結露の細かい水滴は薄く、下地をほとんど暗くしません。艶と映り込みで見せます。
                float beadDarken = _WetDarken * 0.9 * (1.0 - 0.8 * condensation);
                float darken = saturate(sheet * darkenStrength + beaded * beadDarken) * (1.0 - pigment.a);
                float alpha = 1.0 - (1.0 - pigment.a) * (1.0 - darken);

                // 雪のきらめき。細かい結晶が主光源を拾って点状に光ります。
                float3 sparkle = 0.0;
                if (snow > 0.01 && mainLight.w > 0.5)
                {
                    float glint = step(0.992, SabaLiquidHash(floor(input.worldPos.xz * 300.0) + floor(input.worldPos.y * 300.0)));
                    sparkle = mainColor * glint * snow * saturate(dot(shaded, normalize(mainLight.xyz))) * 2.0;
                }

                // 発光。照明と関係なく顔料の色で光ります。雪の下は光りません。
                float3 emission = glowHue * glowAmount * (1.0 - snow);

                return fixed4((pigmentColor + reflection + highlight * fresnel * 2.5 + sparkle + emission) * fade,
                    saturate(alpha * fade));
            }
            ENDCG
        }
    }

    Fallback Off
}
