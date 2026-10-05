using UnityEditor;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    public static class FlockMenu
    {
        [MenuItem("GameObject/SabaProps/Flock/Sky Swarm (ムクドリ)", false, 10)]
        public static void CreateSky(MenuCommand command) => CreateAndSelect("starling", command);

        [MenuItem("GameObject/SabaProps/Flock/V Formation (マガン)", false, 11)]
        public static void CreateVFormation(MenuCommand command) => CreateAndSelect("goose", command);

        [MenuItem("GameObject/SabaProps/Flock/Soaring (トビ)", false, 12)]
        public static void CreateSoaring(MenuCommand command) => CreateAndSelect("black-kite", command);

        [MenuItem("GameObject/SabaProps/Flock/Sea School (マイワシ)", false, 20)]
        public static void CreateSea(MenuCommand command) => CreateAndSelect("sardine", command);

        [MenuItem("GameObject/SabaProps/Flock/Reef School (キンギョハナダイ)", false, 21)]
        public static void CreateReef(MenuCommand command) => CreateAndSelect("anthias", command);

        [MenuItem("GameObject/SabaProps/Flock/Aquarium (ネオンテトラ)", false, 30)]
        public static void CreateAquarium(MenuCommand command) => CreateAndSelect("neon-tetra", command);

        [MenuItem("GameObject/SabaProps/Flock/Pond (錦鯉)", false, 31)]
        public static void CreatePond(MenuCommand command) => CreateAndSelect("koi", command);

        [MenuItem("Tools/SabaProps/Flock/Create Species Gallery Scene", false, 0)]
        public static void CreateGallery()
        {
            FlockGallery.CreateScene();
        }

        [MenuItem("Tools/SabaProps/Flock/Rebuild All Swarms In Scene", false, 1)]
        public static void RebuildAll()
        {
            FlockSwarm[] swarms = Object.FindObjectsOfType<FlockSwarm>();
            foreach (FlockSwarm swarm in swarms)
            {
                FlockSwarmBuilder.Rebuild(swarm);
            }

            Debug.Log($"[SabaProps Flock] {swarms.Length} 個の群れを再生成しました。");
        }

        [MenuItem("Tools/SabaProps/Flock/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.flock/README.md");
        }

        private static void CreateAndSelect(string presetId, MenuCommand command)
        {
            FlockSwarm swarm = FlockSwarmBuilder.Create(presetId, command.context as GameObject, Vector3.zero);
            Selection.activeGameObject = swarm.gameObject;
        }
    }
}
