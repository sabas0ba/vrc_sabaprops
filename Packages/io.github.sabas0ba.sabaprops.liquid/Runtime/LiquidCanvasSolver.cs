using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Body Canvas の幾何計算。
    /// <para>
    /// LiquidBodyCanvas の部分クラスですが、基底型も VRChat の参照も持たないため、
    /// このファイルだけで単独コンパイルできます。.github/verify/offline/OfflineLiquidTests.cs が
    /// Unity も VRChat も無い環境でここを実行します。
    /// </para>
    /// <para>
    /// 成立条件はひとつだけです。ここのメソッドは LiquidBodyCanvas のフィールドを参照せず、
    /// 必要な値はすべて引数で受け取ります。
    /// </para>
    /// <para>
    /// アトラスの配置と面の重みは Shaders/SabaLiquidCanvas.cginc と同じ定義です。
    /// 片方を変えたらもう片方も変えます。
    /// </para>
    /// </summary>
    public partial class LiquidBodyCanvas
    {
        /// <summary>アトラスの面の数。±X, ±Y, ±Z。</summary>
        public const int FaceCount = 6;

        /// <summary>アトラスの列数。</summary>
        public const int AtlasColumns = 3;

        /// <summary>アトラスの行数。</summary>
        public const int AtlasRows = 2;

        /// <summary>一度の更新で適用する DrawOp の上限。シェーダの配列長と一致させます。</summary>
        public const int MaxStampsPerUpdate = 16;

        /// <summary>
        /// ボーンが無いときに GetBonePosition が返す値かどうか。
        /// <para>
        /// VRChat は存在しないボーンに対して Vector3.zero を返します。ワールド原点に
        /// 本当にボーンがある場合と区別できませんが、腰と脚と胸が同時に原点に来ることは
        /// 無いので、軸が縮退した場合の代替と合わせて実害はありません。
        /// </para>
        /// </summary>
        private bool IsMissingBone(Vector3 position)
        {
            return position.sqrMagnitude < 1e-12f;
        }

        /// <summary>
        /// 体の上方向。腰から胸へ向かう方向で、寝転んだ姿勢にも追従します。
        /// 胸が無い、または腰と重なる場合は fallbackUp を使います。
        /// </summary>
        private Vector3 SolveFrameUp(Vector3 hips, Vector3 chest, Vector3 fallbackUp)
        {
            if (!IsMissingBone(hips) && !IsMissingBone(chest))
            {
                Vector3 spine = chest - hips;
                if (spine.sqrMagnitude > 1e-6f)
                {
                    return spine.normalized;
                }
            }

            Vector3 up = fallbackUp.normalized;
            return up.sqrMagnitude > 0.5f ? up : Vector3.up;
        }

        /// <summary>
        /// 体の右方向。左右の太腿の付け根を結ぶ方向を、上方向と直交させたものです。
        /// <para>
        /// ボーンの回転を使わないのは、Humanoid のボーンのローカル軸がモデルの書き出し元に
        /// よって異なるためです。位置だけから組めば、リグに依存しません。
        /// </para>
        /// 脚が無い場合は fallbackForward（プレイヤーの向き）から求めます。
        /// </summary>
        private Vector3 SolveFrameRight(Vector3 up, Vector3 leftUpperLeg, Vector3 rightUpperLeg, Vector3 fallbackForward)
        {
            if (!IsMissingBone(leftUpperLeg) && !IsMissingBone(rightUpperLeg))
            {
                Vector3 lateral = rightUpperLeg - leftUpperLeg;
                Vector3 right = lateral - up * Vector3.Dot(lateral, up);
                if (right.sqrMagnitude > 1e-6f)
                {
                    return right.normalized;
                }
            }

            Vector3 fromForward = Vector3.Cross(up, fallbackForward);
            if (fromForward.sqrMagnitude > 1e-6f)
            {
                return fromForward.normalized;
            }

            // プレイヤーの向きが体の上方向と平行な場合。どちらを選んでも
            // 直交系にはなるので、ワールドの前方向から決めます。
            Vector3 fromWorld = Vector3.Cross(up, Vector3.forward);
            if (fromWorld.sqrMagnitude < 1e-6f)
            {
                fromWorld = Vector3.Cross(up, Vector3.right);
            }

            return fromWorld.normalized;
        }

        /// <summary>右と上から前方向を求めます。Unity の左手系で right x up = forward です。</summary>
        private Vector3 SolveFrameForward(Vector3 right, Vector3 up)
        {
            return Vector3.Cross(right, up).normalized;
        }

        /// <summary>
        /// ワールド座標を Canvas の正規化座標の 1 成分へ写す行ベクトル。
        /// <para>
        /// xyz が axis / halfExtent、w が -dot(axis, origin) / halfExtent で、
        /// dot(row, (p, 1)) が箱の中心で 0、面で ±1 になります。
        /// 行列ではなくベクトル 3 本で渡すのは、Material.SetVector だけで済ませるためです。
        /// </para>
        /// </summary>
        private Vector4 CanvasRow(Vector3 axis, Vector3 origin, float halfExtent)
        {
            float inverse = 1f / Mathf.Max(halfExtent, 1e-4f);
            return new Vector4(axis.x * inverse, axis.y * inverse, axis.z * inverse,
                -Vector3.Dot(axis, origin) * inverse);
        }

        /// <summary>ワールドの点を Canvas のローカル座標（メートル）へ変換します。</summary>
        private Vector3 ToCanvasLocalPoint(Vector3 world, Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
        {
            Vector3 d = world - origin;
            return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward));
        }

        /// <summary>ワールドの方向を Canvas のローカル方向へ変換します。</summary>
        private Vector3 ToCanvasLocalDirection(Vector3 world, Vector3 right, Vector3 up, Vector3 forward)
        {
            return new Vector3(Vector3.Dot(world, right), Vector3.Dot(world, up), Vector3.Dot(world, forward));
        }

        /// <summary>正規化座標で絶対値が最大の軸。0: X, 1: Y, 2: Z。</summary>
        private int DominantAxis(Vector3 v)
        {
            float ax = Mathf.Abs(v.x);
            float ay = Mathf.Abs(v.y);
            float az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az)
            {
                return 0;
            }

            return ay >= az ? 1 : 2;
        }

        private float AxisComponent(Vector3 v, int axis)
        {
            return axis == 0 ? v.x : (axis == 1 ? v.y : v.z);
        }

        private int FaceOf(int axis, float sign)
        {
            return axis * 2 + (sign < 0f ? 1 : 0);
        }

        /// <summary>面内の (u, v)。正規化座標 [-1, 1] を [0, 1] へ写します。</summary>
        private Vector2 TileUv(Vector3 q, int axis)
        {
            float u = axis == 0 ? q.z : q.x;
            float v = axis == 1 ? q.z : q.y;
            return new Vector2(u * 0.5f + 0.5f, v * 0.5f + 0.5f);
        }

        /// <summary>
        /// 面のタイル内 uv をアトラス uv へ写します。
        /// inset はタイルの縁に残す余白の割合で、バイリニア補間で隣の面が混ざるのを防ぎます。
        /// </summary>
        private Vector2 AtlasUv(int face, Vector2 tileUv, float inset)
        {
            int column = face % AtlasColumns;
            int row = face / AtlasColumns;
            float scale = 1f - 2f * inset;
            float lx = inset + Mathf.Clamp01(tileUv.x) * scale;
            float ly = inset + Mathf.Clamp01(tileUv.y) * scale;
            return new Vector2((column + lx) / AtlasColumns, (row + ly) / AtlasRows);
        }

        private int FaceAtAtlasUv(Vector2 uv)
        {
            int column = Mathf.Clamp(Mathf.FloorToInt(uv.x * AtlasColumns), 0, AtlasColumns - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(uv.y * AtlasRows), 0, AtlasRows - 1);
            return row * AtlasColumns + column;
        }

        private Vector2 TileUvAtAtlasUv(Vector2 uv, float inset)
        {
            float lx = uv.x * AtlasColumns - Mathf.Floor(uv.x * AtlasColumns);
            float ly = uv.y * AtlasRows - Mathf.Floor(uv.y * AtlasRows);
            float scale = 1f - 2f * inset;
            return new Vector2((lx - inset) / scale, (ly - inset) / scale);
        }

        /// <summary>
        /// 単位法線に対する面の重み。法線と反対側の面は 0 で、3 軸の和は 1 です。
        /// 表と裏が同じタイルを共有しないことは、この 0 で保証されます。
        /// </summary>
        private float FaceWeight(Vector3 normalCanvas, int face)
        {
            int axis = face / 2;
            float sign = face % 2 == 0 ? 1f : -1f;
            if (AxisComponent(normalCanvas, axis) * sign <= 0f)
            {
                return 0f;
            }

            float x2 = normalCanvas.x * normalCanvas.x;
            float y2 = normalCanvas.y * normalCanvas.y;
            float z2 = normalCanvas.z * normalCanvas.z;
            float sum = Mathf.Max(x2 * x2 + y2 * y2 + z2 * z2, 1e-6f);
            float c2 = axis == 0 ? x2 : (axis == 1 ? y2 : z2);
            return c2 * c2 / sum;
        }

        /// <summary>
        /// 付着した面の奥行きによる表示の重み。SabaLiquidCanvas.cginc の SabaLiquidDepthMask と同じ定義です。
        /// <para>
        /// 1 枚のタイルは、同じ向きを向いた表面のうち面内の座標が同じものを区別できません。
        /// 付着時に記録した面の奥行きと受け手の奥行きを比べ、tolerance 以内なら描き、
        /// その 2 倍を超えれば描きません。記録の無いテクセル（coverage 0）は制限しません。
        /// </para>
        /// </summary>
        private float DepthMask(float surfaceDepth, float storedDepth, float coverage, float tolerance)
        {
            float t = Mathf.Clamp01((Mathf.Abs(surfaceDepth - storedDepth) - tolerance) / Mathf.Max(tolerance, 1e-6f));
            float mask = 1f - t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, mask, Mathf.Clamp01(coverage));
        }

        /// <summary>
        /// 受け手の部位の重み。x: 衣服（体）, y: 髪, z: 肌。SabaLiquidCanvas.cginc の
        /// SabaLiquidRegionWeights と同じ定義です。
        /// <para>
        /// Projector は受け手のマテリアルを読めないため、部位は位置から推定します。頭の球の中は髪、
        /// ただし顔の向き側で頭頂でない所は肌、手の球の中は肌、それ以外は衣服です。
        /// head.w と手の w は半径で、0 ならその部位を使いません。
        /// </para>
        /// </summary>
        private Vector3 RegionWeights(Vector3 p, Vector4 head, Vector3 face, Vector3 up, Vector4 leftHand, Vector4 rightHand)
        {
            float hair = 0f;
            float skin = 0f;

            if (head.w > 0f)
            {
                Vector3 offset = p - new Vector3(head.x, head.y, head.z);
                float distance = offset.magnitude;
                float inside = 1f - Smoothstep(head.w * 0.95f, head.w * 1.25f, distance);
                Vector3 direction = offset / Mathf.Max(distance, 1e-5f);
                float front = Smoothstep(0.1f, 0.5f, Vector3.Dot(direction, face));
                float crown = Smoothstep(0.35f, 0.7f, Vector3.Dot(direction, up));
                float faceSkin = front * (1f - crown);
                hair = inside * (1f - faceSkin);
                skin = inside * faceSkin;
            }

            if (leftHand.w > 0f)
            {
                float d = (p - new Vector3(leftHand.x, leftHand.y, leftHand.z)).magnitude;
                skin = Mathf.Max(skin, 1f - Smoothstep(leftHand.w * 0.9f, leftHand.w * 1.4f, d));
            }

            if (rightHand.w > 0f)
            {
                float d = (p - new Vector3(rightHand.x, rightHand.y, rightHand.z)).magnitude;
                skin = Mathf.Max(skin, 1f - Smoothstep(rightHand.w * 0.9f, rightHand.w * 1.4f, d));
            }

            hair = Mathf.Min(hair, 1f - skin);
            return new Vector3(1f - hair - skin, hair, skin);
        }

        /// <summary>HLSL の smoothstep と同じ定義。</summary>
        private float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>DrawOp の中心からの距離に対する付着量の減衰。中心で 1、半径で 0。</summary>
        private float StampFalloff(float distance, float radius)
        {
            float t = Mathf.Clamp01(1f - (distance * distance) / Mathf.Max(radius * radius, 1e-8f));
            return t * t;
        }

        /// <summary>
        /// 水平な液面の高さを、Canvas の正規化 y へ変換します。
        /// <para>
        /// 液面はワールドで水平ですが、Canvas の y 軸は体に沿って傾くため、Canvas 内で
        /// 液面の高さは一定になりません。ここでは原点を通る体軸上で液面と交わる位置を
        /// 代表値とします。体が大きく傾いている（寝ている）場合は交点が不安定なため、
        /// 原点が液面より下なら全身、上なら無しとして扱います。
        /// </para>
        /// </summary>
        private float ImmersionLevel(float surfaceY, Vector3 origin, Vector3 up, float halfHeight)
        {
            float h = Mathf.Max(halfHeight, 1e-4f);
            if (up.y > 0.2f)
            {
                return (surfaceY - origin.y) / up.y / h;
            }

            return origin.y < surfaceY ? 2f : -2f;
        }

        /// <summary>
        /// 液から出た後に液膜の上端が下がる速さ。液膜が重力で流れ落ちる様子の近似で、
        /// 粘性が高いほど遅くなります。下端（-1）より下へは行きません。
        /// </summary>
        private float DrainLevel(float level, float viscosity, float drainPerSecond, float deltaSeconds)
        {
            float speed = drainPerSecond * (1f - Mathf.Clamp01(viscosity));
            return Mathf.Max(-1f, level - speed * deltaSeconds);
        }

        /// <summary>乾燥時間に対する液量の減少。0 以下の乾燥時間は乾かないことを表します。</summary>
        private float EvaporateAmount(float amount, float dryingSeconds, float deltaSeconds)
        {
            if (dryingSeconds <= 0f)
            {
                return amount;
            }

            return Mathf.Max(0f, amount - deltaSeconds / dryingSeconds);
        }

        /// <summary>
        /// 乾燥時間（秒）をフィルムの A チャネルへ符号化します。
        /// <para>
        /// 値は 1 秒あたりの蒸発率を maxRate で割ったものです。0 以下の乾燥時間は
        /// 「蒸発しない」を表し、0 に符号化します。
        /// </para>
        /// </summary>
        private float EncodeEvaporation(float seconds, float maxRate)
        {
            if (seconds <= 0f || maxRate <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f / (seconds * maxRate));
        }
    }
}
