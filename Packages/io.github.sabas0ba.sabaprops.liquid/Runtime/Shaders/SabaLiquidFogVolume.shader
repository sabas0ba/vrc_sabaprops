// 箱の中だけに掛かる霧。サウナや浴室の湯気の立ちこめ方に使います。
//
// 単位立方体（-0.5〜0.5）のメッシュに貼り、裏面を描きます。視線が箱を通る区間の長さを解析的に求め、
// 濃さ _Density（1/m）で指数的に減衰させた分を霧の色で覆います。箱の中にカメラがあっても描けるよう、
// 裏面を ZTest Always で描き、区間の終わりはシーンの深度（_CameraDepthTexture）で打ち切ります。
//
// _CameraDepthTexture は、リアルタイムの影を落とすディレクショナルライトがある場合などに
// カメラが生成します。無い場合は箱の奥の面までを区間とします。
Shader "SabaProps/Liquid/Fog Volume"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.85, 0.87, 0.9, 1)
        _Density ("Density (1/m)", Range(0, 4)) = 0.3
        _HeightFalloff ("Height Falloff", Range(0, 2)) = 0.4
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Density;
            float _HeightFalloff;
            float _MaxOpacity;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screen : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            // 光線と単位立方体（オブジェクト空間）の交差区間。
            float2 BoxInterval(float3 origin, float3 direction)
            {
                float3 inverse = 1.0 / (abs(direction) > 1e-6 ? direction : 1e-6);
                float3 t0 = (-0.5 - origin) * inverse;
                float3 t1 = (0.5 - origin) * inverse;
                float3 near = min(t0, t1);
                float3 far = max(t0, t1);
                return float2(max(max(near.x, near.y), near.z), min(min(far.x, far.y), far.z));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 camera = _WorldSpaceCameraPos;
                float3 toSurface = input.worldPos - camera;
                float surfaceDistance = length(toSurface);
                float3 rayWorld = toSurface / max(surfaceDistance, 1e-5);

                // オブジェクト空間での区間。方向は長さを保ったまま変換し、t をワールドの距離で扱います。
                float3 originObject = mul(unity_WorldToObject, float4(camera, 1.0)).xyz;
                float3 directionObject = mul((float3x3)unity_WorldToObject, rayWorld);
                float2 interval = BoxInterval(originObject, directionObject);
                float enter = max(interval.x, 0.0);
                float exit = interval.y;

                // シーンの深度で打ち切ります。深度は視線方向の距離なので、光線上の距離に直します。
                float2 uv = input.screen.xy / input.screen.w;
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float eyeDepth = LinearEyeDepth(rawDepth);
                float3 forward = -UNITY_MATRIX_V[2].xyz;
                float sceneDistance = eyeDepth / max(dot(rayWorld, forward), 1e-4);
                exit = min(exit, sceneDistance);

                float span = max(exit - enter, 0.0);

                // 上ほど濃くします。湯気は上に溜まるためです。
                float3 middle = camera + rayWorld * (enter + exit) * 0.5;
                float3 middleObject = mul(unity_WorldToObject, float4(middle, 1.0)).xyz;
                float height = saturate(middleObject.y + 0.5);
                float density = _Density * lerp(1.0 - _HeightFalloff * 0.5, 1.0 + _HeightFalloff * 0.5, height);

                float opacity = (1.0 - exp(-density * span)) * _MaxOpacity;
                float3 lit = _Color.rgb * max(ShadeSH9(float4(0.0, 1.0, 0.0, 1.0)), UNITY_LIGHTMODEL_AMBIENT.rgb * 2.0);
                return fixed4(lit, opacity);
            }
            ENDCG
        }
    }

    Fallback Off
}
