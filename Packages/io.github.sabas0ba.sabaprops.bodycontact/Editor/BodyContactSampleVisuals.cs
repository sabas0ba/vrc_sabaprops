using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SabaProps.BodyContact.Editors
{
    /// <summary>検証用の外観と動きを生成します。接触判定と同じ標準体型を参照します。</summary>
    public static class BodyContactSampleVisuals
    {
        private const string Folder = "Assets/SabaProps/BodyContact";

        public static Material Material(string name, string shaderName, Color color)
        {
            EnsureFolder();
            string path = Folder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void AddMannequin(BodyContactSystem system, Transform root)
        {
            Material core = Material("MannequinCore", "Standard", new Color(0.8f, 0.63f, 0.36f));
            Material limbs = Material("MannequinLimbs", "Standard", new Color(0.30f, 0.37f, 0.43f));
            float height = system.dummyEyeHeight;
            for (int part = 0; part < BodyContactSystem.PartCount; part++)
            {
                Vector3 a = system.StandardJoint(system.PartJointA(part)) * height;
                Vector3 b = system.StandardJoint(system.PartJointB(part)) * height;
                float radius = system.PartUnitRadius(part) * height * system.radiusScale;
                Material surface = part < BodyContactSystem.PartLeftUpperArm ? core : limbs;
                // 円柱と両端の球でカプセルを構成し、非等方スケールによる半球の変形を避けます。
                Visual(root, "Part " + part + " A", PrimitiveType.Sphere, a, Vector3.one * radius * 2f, surface);
                if ((b - a).sqrMagnitude < 1e-8f) continue;
                Visual(root, "Part " + part + " B", PrimitiveType.Sphere, b, Vector3.one * radius * 2f, surface);
                GameObject segment = Visual(root, "Part " + part, PrimitiveType.Cylinder, (a + b) * 0.5f,
                    new Vector3(radius * 2f, (b - a).magnitude * 0.5f, radius * 2f), surface);
                segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }
            // 正面を判別する目印。見た目だけの部品なので接触判定は追加しません。
            Vector3 face = system.StandardJoint(BodyContactSystem.JointHead) * height;
            face.z += system.PartUnitRadius(BodyContactSystem.PartHead) * height * system.radiusScale;
            Visual(root, "Facing marker", PrimitiveType.Cube, face,
                new Vector3(0.07f, 0.025f, 0.015f) * height, limbs);
        }

        private static GameObject Visual(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            // 外観のColliderが壁判定に混入すると、押し戻しが取り消されてしまいます。
            Object.DestroyImmediate(item.GetComponent<Collider>());
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        public static void AddMotion(GameObject dummy, bool translate)
        {
            EnsureFolder();
            var anchor = new GameObject(dummy.name + " Anchor");
            anchor.transform.SetParent(dummy.transform.parent, false);
            anchor.transform.position = dummy.transform.position;
            dummy.transform.SetParent(anchor.transform, true);
            string name = translate ? "MannequinTranslate" : "MannequinTurn";
            string path = Folder + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.legacy = false;
            clip.wrapMode = WrapMode.Loop;
            clip.name = name;
            // 始点と終点の位置・速度を揃え、ループ境界での瞬間移動を避けます。
            AnimationCurve curve = translate
                ? new AnimationCurve(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(2f, 0.8f),
                    new Keyframe(4f, 0f, -0.6f, -0.6f), new Keyframe(6f, -0.8f), new Keyframe(8f, 0f, 0.6f, 0.6f))
                : new AnimationCurve(new Keyframe(0f, 0f, 45f, 45f), new Keyframe(2f, 60f),
                    new Keyframe(4f, 0f, -45f, -45f), new Keyframe(6f, -60f), new Keyframe(8f, 0f, 45f, 45f));
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), translate ? "m_LocalPosition.x" : "localEulerAnglesRaw.y"), curve);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            // Worldで許可されているAnimatorを使います。Legacy Animationは実クライアントで動きません。
            string controllerPath = Folder + "/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.defaultState;
            if (state == null) state = machine.AddState("Motion");
            state.motion = clip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            Animator animator = dummy.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        public static void Label(Transform parent, string name, string text, Vector3 position)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            TextMesh label = item.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 64;
            label.characterSize = 0.025f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/SabaProps")) AssetDatabase.CreateFolder("Assets", "SabaProps");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SabaProps", "BodyContact");
        }
    }
}
