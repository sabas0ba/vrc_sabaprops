using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    [CustomEditor(typeof(FlockSwarm))]
    public sealed class FlockSwarmEditor : Editor
    {
        private static string[] _presetLabels;
        private static string[] _presetIds;

        private bool _showSpecies;

        private static void EnsurePresetList()
        {
            if (_presetLabels != null)
            {
                return;
            }

            var labels = new List<string>();
            var ids = new List<string>();
            foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            {
                labels.Add($"{HabitatLabel(preset.Habitat)}/{preset.Species.displayName}");
                ids.Add(preset.Species.id);
            }

            _presetLabels = labels.ToArray();
            _presetIds = ids.ToArray();
        }

        public static string HabitatLabel(FlockHabitat habitat)
        {
            switch (habitat)
            {
                case FlockHabitat.Sea:
                    return "海";
                case FlockHabitat.Reef:
                    return "サンゴ礁";
                case FlockHabitat.Aquarium:
                    return "水槽・池";
                default:
                    return "空";
            }
        }

        public override void OnInspectorGUI()
        {
            EnsurePresetList();
            var swarm = (FlockSwarm)target;
            serializedObject.Update();

            int current = System.Array.IndexOf(_presetIds, swarm.presetId);
            EditorGUI.BeginChangeCheck();
            int chosen = EditorGUILayout.Popup("プリセット", Mathf.Max(current, 0), _presetLabels);
            if (EditorGUI.EndChangeCheck() && chosen >= 0)
            {
                FlockSwarmBuilder.ApplyPreset(swarm, _presetIds[chosen]);
                serializedObject.Update();
                FlockSwarmBuilder.Rebuild(swarm);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("material"));

            _showSpecies = EditorGUILayout.Foldout(_showSpecies, "種のパラメータ (形状・色・動き)");
            if (_showSpecies)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("species"), true);
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            if (GUILayout.Button("生成 / 更新"))
            {
                FlockSwarmBuilder.Rebuild(swarm);
            }

            EditorGUILayout.HelpBox(FlockSwarmBuilder.Describe(swarm), MessageType.None);
            EditorGUILayout.HelpBox(
                "個体の移動は Shader が時刻から計算します。GameObject を動かすと群れ全体が移動します。" +
                "生成した Renderer を Static (Batching) にすると動かなくなります。",
                MessageType.Info);
        }
    }
}
