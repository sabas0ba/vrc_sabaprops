using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;

namespace SabaProps.SoftProps.WorldTests
{
    public class SoftPropsMotionTests
    {
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        private static Component Udon(GameObject surface)
        {
            return surface.GetComponents<Component>().First(c => c.GetType().Name == "UdonBehaviour");
        }
        private static object Read(Component udon, string name)
        {
            return udon.GetType().GetMethods().Single(m => m.Name == "GetProgramVariable"
                    && !m.IsGenericMethod && m.GetParameters().Length == 1)
                .Invoke(udon, new object[] { name });
        }
        private static void Write(Component udon, string name, object value)
        {
            udon.GetType().GetMethod("SetProgramVariable", new[] { typeof(string), typeof(object) })
                .Invoke(udon, new[] { (object)name, value });
        }

        private static void Capture(Type demo, GameObject root, string name)
        {
            var camera = new GameObject("Motion test camera").AddComponent<Camera>();
            camera.transform.position = root.transform.position + new Vector3(0.25f, 1.5f, -1.5f);
            camera.transform.LookAt(root.transform.position + Vector3.up * 0.18f);
            camera.fieldOfView = 60f;
            camera.cullingMask = ~(1 << 19); // ClientSim menu layerをレビュー画像から除外する。
            try
            {
                demo.GetMethod("Capture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { camera, name });
            }
            finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Colliders_RequirePenetration_AutomationMoves_PlayerLoadsFuton()
        {
            Type demo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SabaProps.SoftProps.Editors.SoftPropsDemo"))
                .First(t => t != null);
            demo.GetMethod("ImportSample").Invoke(null, null);
            EditorSceneManager.OpenScene("Assets/SabaProps/SoftPropsDemoMotion/SoftPropsDemo.unity");
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings {
                enableClientSim = true, initializationDelay = 0f, spawnPlayer = true,
                localPlayerIsMaster = true, displayLogs = false });
            // SDK 3.10.4の入力初期化raceに限りstartup中のlogを許容する。
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int i = 0; i < 180; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
            Assert.IsNotNull(Networking.LocalPlayer);
            var menu = UnityEngine.Object.FindObjectOfType<ClientSimMenu>(true);
            if (menu != null) menu.CloseMenu();

            var root = GameObject.Find("Automatic comparison 1");
            var surface = root.transform.Find("SkinSurface").gameObject;
            Component udon = Udon(surface);
            Write(udon, "automaticProbe", false);
            Collider probe = root.transform.Find("Finger Probe").GetComponent<Collider>();
            Collider[] probes = { probe, root.transform.Find("Rod Probe").GetComponent<Collider>(),
                root.transform.Find("Plate Probe").GetComponent<Collider>() };
            float[] support = { 0.035f, 0.025f, 0.0175f };
            float plane = surface.transform.position.y + 0.09f;
            for (int i = 0; i < probes.Length; i++)
                probes[i].transform.position = new Vector3(probes[i].transform.position.x,
                    plane + support[i] + 0.10f, probes[i].transform.position.z);
            float waitUntil = Time.time + 2.5f;
            while (Time.time < waitUntil) yield return null;
            Material material = surface.GetComponent<Renderer>().material;
            for (int i = 0; i < probes.Length; i++)
                Assert.Less(material.GetVector("_Contact" + i).w, 0.003f, "Air gap: " + probes[i].name);
            Capture(demo, root, "motion-separated.png");

            foreach (var collider in probes) collider.transform.position -= Vector3.up * 0.0995f;
            waitUntil = Time.time + 0.2f;
            while (Time.time < waitUntil) yield return null;
            for (int i = 0; i < probes.Length; i++)
                Assert.Less(material.GetVector("_Contact" + i).w, 0.003f, "0.5 mm gap: " + probes[i].name);
            foreach (var collider in probes) collider.transform.position -= Vector3.up * 0.0205f;
            waitUntil = Time.time + 0.5f;
            while (Time.time < waitUntil) yield return null;
            float pressed = material.GetVector("_Contact0").w;
            for (int i = 0; i < probes.Length; i++)
                Assert.Greater(material.GetVector("_Contact" + i).w, 0.1f, "Penetration: " + probes[i].name);
            Assert.LessOrEqual(pressed * 0.085f * Mathf.Lerp(1f, 0.28f, 0.45f), 0.0205f,
                "Compression must not exceed collider penetration");
            Capture(demo, root, "motion-pressed.png");
            foreach (var collider in probes) collider.transform.position += Vector3.up * 0.12f;
            waitUntil = Time.time + 1.5f;
            while (Time.time < waitUntil) yield return null;
            Assert.Less(material.GetVector("_Contact0").w, pressed * 0.1f, "Released surface must recover");
            Capture(demo, root, "motion-recovered.png");
            Write(udon, "automaticProbe", true);
            float minimum = float.MaxValue, maximum = float.MinValue;
            float end = Time.time + 6f;
            while (Time.time < end)
            {
                minimum = Mathf.Min(minimum, probe.transform.position.y);
                maximum = Mathf.Max(maximum, probe.transform.position.y);
                yield return null;
            }
            Assert.Greater(maximum - minimum, 0.12f, "Automatic demo must move a real Collider");

            var futon = GameObject.Find("Futon").transform.Find("FutonSurface").gameObject;
            menu = UnityEngine.Object.FindObjectOfType<ClientSimMenu>(true);
            if (menu != null) menu.CloseMenu();
            Networking.LocalPlayer.TeleportTo(futon.transform.position + Vector3.up * 0.3f, Quaternion.identity);
            waitUntil = Time.time + 2f;
            while (Time.time < waitUntil) yield return null;
            Assert.Greater(Convert.ToInt32(Read(Udon(futon), "standingSupportSamples")), 0,
                "ClientSim player must be grounded on the futon; position=" + Networking.LocalPlayer.GetPosition()
                + ", grounded=" + Networking.LocalPlayer.IsPlayerGrounded());
            Material futonMaterial = futon.GetComponent<Renderer>().material;
            float load = 0f;
            for (int i = 0; i < 8; i++) load = Mathf.Max(load, futonMaterial.GetVector("_Contact" + i).w);
            Assert.Greater(load, 0.1f, "Standing player must deform the futon without avatar Contacts");
        }
    }
}
