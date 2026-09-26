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
Shader "SabaProps/Liquid/Body Projector"
{
    Properties
    {
        [NoScaleOffset] _PigmentTex ("Pigment Canvas", 2D) = "black" {}
        [NoScaleOffset] _FilmTex ("Film Canvas", 2D) = "black" {}
        [NoScaleOffset] _DepthTex ("Depth Canvas", 2D) = "black" {}
        _DepthTolerance ("Surface Depth Tolerance (m)", Range(0.01, 0.3)) = 0.12
        _WetDarken ("Wet Darkening", Range(0, 1)) = 0.5
        _WetReflection ("Wet Reflection", Range(0, 2)) = 1
        _WetSpecular ("Wet Highlight", Range(0, 4)) = 1.5
        _DryLighten ("Dry Pigment Lightening", Range(1, 1.5)) = 1.15
        _Relief ("Thickness Relief", Range(0, 8)) = 2.5
        _PigmentGrain ("Pigment Grain", Range(0, 0.5)) = 0.12
        _EdgeFade ("Canvas Edge Fade", Range(0.001, 0.3)) = 0.04
        _AmbientResponse ("Ambient Lighting Response", Range(0, 1)) = 1
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
            float _DepthTolerance;
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
            float _WetDarken;
            float _WetReflection;
            float _WetSpecular;
            float _DryLighten;
            float _Relief;
            float _PigmentGrain;
            float _EdgeFade;
            float _AmbientResponse;

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

            // 付着の厚み。顔料は被覆率、液膜は液量と粘性で決め、粘性の高い液ほど盛り上がります。
            float Thickness(float2 uv)
            {
                float4 pigment = tex2D(_PigmentTex, uv);
                float4 film = tex2D(_FilmTex, uv);
                return pigment.a * 0.6 + film.r * (0.25 + 0.75 * film.b);
            }

            void SampleCanvas(float3 q, float3 n, out float4 pigment, out float4 film)
            {
                float3 weights = SabaLiquidAxisWeights(n);
                // 半径は行ベクトルの長さの逆数です。受け手の奥行きをメートルに戻すのに使います。
                float3 halfExtents = float3(1.0 / max(length(_CanvasRowX.xyz), 1e-4),
                                            1.0 / max(length(_CanvasRowY.xyz), 1e-4),
                                            1.0 / max(length(_CanvasRowZ.xyz), 1e-4));
                float3 surface = q * halfExtents;
                pigment = 0.0;
                film = 0.0;

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
                }
            }

            // 付着の厚みの勾配から法線を傾けます。垂れや塊が盛り上がって見え、ハイライトが縁に乗ります。
            // 計算は法線が最も向いている面のタイルだけで行います。
            float3 ReliefNormal(float3 q, float3 n, float3 worldNormal)
            {
                float3 a = abs(n);
                int axis = a.x >= a.y && a.x >= a.z ? 0 : (a.y >= a.z ? 1 : 2);
                float component = axis == 0 ? n.x : (axis == 1 ? n.y : n.z);
                int face = SabaLiquidFace(axis, component);
                float2 uv = SabaLiquidAtlasUv(face, SabaLiquidTileUv(q, axis), _CanvasInset);

                float2 step = _PigmentTex_TexelSize.xy;
                float du = Thickness(uv + float2(step.x, 0.0)) - Thickness(uv - float2(step.x, 0.0));
                float dv = Thickness(uv + float2(0.0, step.y)) - Thickness(uv - float2(0.0, step.y));

                // タイルの (u, v) に対応する Canvas の軸。X 面は (z, y)、Y 面は (x, z)、Z 面は (x, y)。
                float3 uAxis = CanvasAxis(axis == 0 ? 2 : 0);
                float3 vAxis = CanvasAxis(axis == 1 ? 2 : 1);
                return normalize(worldNormal - _Relief * (du * uAxis + dv * vAxis));
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
                SampleCanvas(q, n, pigment, film);

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

                float wet = saturate(film.r);
                float smoothness = saturate(film.g);
                if (pigment.a <= 0.001 && wet <= 0.001)
                {
                    return fixed4(0.0, 0.0, 0.0, 0.0);
                }

                float3 shaded = ReliefNormal(q, n, worldNormal);
                float3 viewDir = normalize(UnityWorldSpaceViewDir(input.worldPos));

                // 投影の描画では受け手のプローブ定数が設定されない場合があるため、
                // 環境光は SH と固定の環境色の大きい方を使います。
                float3 ambient = max(unity_AmbientSky.rgb,
                    max(UNITY_LIGHTMODEL_AMBIENT.rgb, max(0.0, ShadeSH9(float4(shaded, 1.0)))));
                float3 diffuse = lerp(float3(1.0, 1.0, 1.0), ambient, _AmbientResponse);
                float3 highlight = 0.0;

                if (_Udon_SabaLiquidLightDirection.w > 0.5)
                {
                    float3 lightDir = normalize(_Udon_SabaLiquidLightDirection.xyz);
                    float3 lightColor = _Udon_SabaLiquidLightColor.rgb;
                    diffuse += lightColor * saturate(dot(shaded, lightDir));

                    // 正規化 Blinn-Phong。平滑度から指数を決め、濡れているほど強く光ります。
                    float3 halfDir = normalize(lightDir + viewDir);
                    float exponent = exp2(10.0 * smoothness + 1.0);
                    float normalization = (exponent + 8.0) / (8.0 * UNITY_PI);
                    highlight = lightColor * pow(saturate(dot(shaded, halfDir)), exponent) * normalization
                        * saturate(dot(shaded, lightDir)) * wet * smoothness * _WetSpecular;
                }

                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(shaded, viewDir)), 5.0);
                float3 environment = max(0.0, ShadeSH9(float4(reflect(-viewDir, shaded), 1.0)));
                float3 reflection = environment * fresnel * wet * smoothness * _WetReflection;

                // 顔料の粒状のむら。広い面が一様な色で平板に見えるのを抑えます。
                float grain = 1.0 + _PigmentGrain * (SabaLiquidValueNoise(q.xy * 60.0 + q.z * 17.0) - 0.5) * 2.0;

                // 乾いた顔料は光を散らして明るく、艶が無くなります。
                float3 pigmentColor = pigment.rgb * grain * diffuse * lerp(_DryLighten, 1.0, wet);
                float darken = wet * _WetDarken * (1.0 - pigment.a);
                float alpha = 1.0 - (1.0 - pigment.a) * (1.0 - darken);

                return fixed4((pigmentColor + reflection + highlight * fresnel * 4.0) * fade, saturate(alpha * fade));
            }
            ENDCG
        }
    }

    Fallback Off
}
