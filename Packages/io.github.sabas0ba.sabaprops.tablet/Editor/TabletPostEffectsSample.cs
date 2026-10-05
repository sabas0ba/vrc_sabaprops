using System;
using System.Collections;
using System.Reflection;
using SabaProps.Tablet.Authoring;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SabaProps.Tablet.Editors
{
    /// <summary>SDK に同梱される Post Processing を使うローカル調整サンプル。</summary>
    public static class TabletPostEffectsSample
    {
        private static Type PostType(string name)
        {
            return Assembly.Load("Unity.Postprocessing.Runtime").GetType("UnityEngine.Rendering.PostProcessing." + name, true);
        }

        public static void Create(TabletDefinition definition, Camera camera)
        {
            // Reference Camera の設定は実行時のプレイヤーカメラへ SDK が複製します。
            Component layer = camera.gameObject.AddComponent(PostType("PostProcessLayer"));
            layer.GetType().GetField("volumeLayer").SetValue(layer, (LayerMask)(1 << 22));
            layer.GetType().GetField("volumeTrigger").SetValue(layer, camera.transform);
            string[] resources = AssetDatabase.FindAssets("t:PostProcessResources");
            if (resources.Length == 0) throw new InvalidOperationException("Post Processing resources were not found.");
            Object resource = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(resources[0]), PostType("PostProcessResources"));
            layer.GetType().GetMethod("Init").Invoke(layer, new object[] { resource });

            GameObject root = new GameObject("Tablet Post Effects");
            var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var module = root.AddUdonSharpComponent<TabletPostEffects>();
            module.animator = animator;

            Behaviour dark = Volume(root, "Dark", "ColorGrading", "postExposure", -2f);
            Behaviour bright = Volume(root, "Bright", "ColorGrading", "postExposure", 2f);
            Behaviour hueLow = Volume(root, "Hue Negative", "ColorGrading", "hueShift", -180f);
            Behaviour hueHigh = Volume(root, "Hue Positive", "ColorGrading", "hueShift", 180f);
            Behaviour glow = Volume(root, "Glow", "Bloom", "intensity", 8f);
            module.volumes = new[] { dark, bright, hueLow, hueHigh, glow };

            string path = TabletAssets.SampleFolder + "/PostEffects.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
            {
                controller.layers = new AnimatorControllerLayer[0];
                controller.parameters = new AnimatorControllerParameter[0];
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset != controller) Object.DestroyImmediate(asset, true);
            }
            else
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                controller.layers = new AnimatorControllerLayer[0];
            }
            Axis(controller, "Brightness", -2f, 2f, dark, bright);
            Axis(controller, "Hue", -180f, 180f, hueLow, hueHigh);
            Axis(controller, "Glow", 0f, 1f, null, glow);
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(controller);
            UdonSharpEditorUtility.CopyProxyToUdon(module);

            TabletPage page = definition.FindOrAddPage("Post Effects");
            page.entries.Add(new TabletEntry
            {
                label = "Effects ON / OFF", kind = TabletEntryKind.CustomEvent, target = module, eventName = "_Toggle",
                customPlacement = true, normalizedCenter = new Vector2(0, 0.37f), normalizedSize = new Vector2(0.6f, 0.18f),
            });
            AddSlider(page, "Brightness (EV)", module, "_SetBrightness", -2f, 2f, 0.1f);
            AddSlider(page, "Hue (degrees)", module, "_SetHue", -180f, 180f, -0.13f);
            AddSlider(page, "Glow", module, "_SetGlow", 0f, 1f, -0.36f);
        }

        private static void AddSlider(TabletPage page, string label, TabletPostEffects module, string eventName,
            float minimum, float maximum, float y)
        {
            page.entries.Add(new TabletEntry
            {
                label = label, kind = TabletEntryKind.Slider, target = module, eventName = eventName,
                minimum = minimum, maximum = maximum, initialValue = 0f,
                customPlacement = true, normalizedCenter = new Vector2(0, y), normalizedSize = new Vector2(0.96f, 0.2f),
            });
        }

        private static Behaviour Volume(GameObject parent, string name, string effectName, string parameter, float value)
        {
            var go = new GameObject(name) { layer = 22 };
            go.transform.SetParent(parent.transform, false);
            Component volume = go.AddComponent(PostType("PostProcessVolume"));
            Type type = volume.GetType();
            type.GetField("isGlobal").SetValue(volume, true);
            type.GetField("weight").SetValue(volume, 0f);
            type.GetField("priority").SetValue(volume, 100f + parent.transform.childCount);
            string path = TabletAssets.SampleFolder + "/Post" + name.Replace(" ", "") + ".asset";
            ScriptableObject oldProfile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (oldProfile != null)
                foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub != oldProfile) Object.DestroyImmediate(sub, true);
            ScriptableObject profile = TabletAssets.CreateOrReplace(ScriptableObject.CreateInstance(PostType("PostProcessProfile")), path);
            ScriptableObject effect = ScriptableObject.CreateInstance(PostType(effectName));
            effect.name = effectName;
            SetParameter(effect, "enabled", true);
            SetParameter(effect, parameter, value);
            if (effectName == "ColorGrading")
            {
                object grading = effect.GetType().GetField("gradingMode").GetValue(effect);
                FieldInfo mode = grading.GetType().GetField("value");
                SetParameter(effect, "gradingMode", Enum.Parse(mode.FieldType, "HighDefinitionRange"));
            }
            ((IList)profile.GetType().GetField("settings").GetValue(profile)).Add(effect);
            AssetDatabase.AddObjectToAsset(effect, profile);
            EditorUtility.SetDirty(profile);
            type.GetField("sharedProfile").SetValue(volume, profile);
            return (Behaviour)volume;
        }

        private static void SetParameter(ScriptableObject effect, string name, object value)
        {
            object parameter = effect.GetType().GetField(name).GetValue(effect);
            parameter.GetType().GetField("overrideState").SetValue(parameter, true);
            parameter.GetType().GetField("value").SetValue(parameter, value);
        }

        private static void Axis(AnimatorController controller, string parameter, float minimum, float maximum,
            Behaviour low, Behaviour high)
        {
            controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            controller.AddLayer(parameter);
            var layers = controller.layers;
            layers[layers.Length - 1].defaultWeight = 1f;
            controller.layers = layers;
            var tree = new BlendTree { name = parameter, blendParameter = parameter, useAutomaticThresholds = false,
                minThreshold = minimum, maxThreshold = maximum };
            AssetDatabase.AddObjectToAsset(tree, controller);
            if (low != null)
            {
                tree.AddChild(Clip(controller, parameter + " Low", low, high, 1f, 0f), minimum);
                tree.AddChild(Clip(controller, parameter + " Neutral", low, high, 0f, 0f), 0f);
            }
            else tree.AddChild(Clip(controller, parameter + " Off", null, high, 0f, 0f), minimum);
            tree.AddChild(Clip(controller, parameter + " High", low, high, 0f, 1f), maximum);
            AnimatorState state = layers[layers.Length - 1].stateMachine.AddState(parameter);
            state.writeDefaultValues = false;
            state.motion = tree;
            layers[layers.Length - 1].stateMachine.defaultState = state;
        }

        private static AnimationClip Clip(AnimatorController controller, string name, Behaviour low, Behaviour high,
            float lowWeight, float highWeight)
        {
            var clip = new AnimationClip { name = name };
            AssetDatabase.AddObjectToAsset(clip, controller);
            if (low != null) Curve(clip, low, lowWeight);
            Curve(clip, high, highWeight);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        private static void Curve(AnimationClip clip, Behaviour volume, float weight)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(volume.name, volume.GetType(), "weight");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, weight));
        }
    }
}
