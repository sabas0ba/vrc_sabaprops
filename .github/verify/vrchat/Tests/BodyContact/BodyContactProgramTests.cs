using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.BodyContact.Editors;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.BodyContact.WorldTests
{
    /// <summary>
    /// What the offline tier cannot settle for the body contact package.
    /// <para>
    /// <c>.github/verify/verify.sh</c> compiles the behaviours as C# against the real
    /// VRCSDKBase.dll, and runs the solver. Neither says whether UdonSharp accepts the
    /// code, nor whether Udon exposes the Mesh and Physics calls the behaviours make.
    /// A call Udon does not expose is a compile error here and nowhere earlier.
    /// </para>
    /// </summary>
    public class BodyContactProgramTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [OneTimeSetUp]
        public void CompileUdonSharpPrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void System_CompilesAndExportsItsEvents()
        {
            IUdonProgram program = Compile<BodyContactSystem>();
            List<string> exported = new List<string>(program.EntryPoints.GetExportedSymbols());

            // Bones have to be read after IK. If PostLateUpdate stops being an Udon
            // event, the system silently never runs.
            CollectionAssert.Contains(exported, "_postLateUpdate", "PostLateUpdate is not exported as an Udon event");
            CollectionAssert.Contains(exported, "_onPlayerJoined");
            CollectionAssert.Contains(exported, "_onPlayerLeft");
            CollectionAssert.Contains(exported, "_onPlayerRespawn");

            // The local-only API a world calls from stations and settings panels.
            CollectionAssert.Contains(exported, "_Step");
            CollectionAssert.Contains(exported, "_Suspend");
            CollectionAssert.Contains(exported, "_Resume");
            CollectionAssert.Contains(exported, "_ToggleEnabled");
        }

        [Test]
        public void System_ExposesItsInspectorFieldsToUdon()
        {
            IUdonProgram program = Compile<BodyContactSystem>();
            List<string> symbols = new List<string>(program.SymbolTable.GetExportedSymbols());

            foreach (string field in new[]
                     {
                         "contactEnabled",
                         "moveMode",
                         "radiusScale",
                         "tolerance",
                         "responseSeconds",
                         "maxSpeed",
                         "passThroughSeconds",
                         "broadPhaseFactor",
                         "worldCollisionMask",
                         "dummies",
                         "dummyEyeHeight",
                         "debugView",
                     })
            {
                CollectionAssert.Contains(symbols, field, field + " is not exposed to Udon");
            }
        }

        [Test]
        public void DebugView_CompilesAndExportsItsEvents()
        {
            IUdonProgram program = Compile<BodyContactDebugView>();
            List<string> exported = new List<string>(program.EntryPoints.GetExportedSymbols());

            CollectionAssert.Contains(exported, "_start");
            CollectionAssert.Contains(exported, "_Refresh");
            CollectionAssert.Contains(exported, "_Show");
            CollectionAssert.Contains(exported, "_Hide");
            CollectionAssert.Contains(exported, "_ToggleVisible");
        }

        [Test]
        public void ControlsAndStation_CompileWithTheirEvents()
        {
            Compile<BodyContactControl>();
            IUdonProgram station = Compile<BodyContactStationRelay>();
            var stationEvents = new List<string>(station.EntryPoints.GetExportedSymbols());
            CollectionAssert.Contains(stationEvents, "_onStationEntered");
            CollectionAssert.Contains(stationEvents, "_onStationExited");
        }

        [Test]
        public void Behaviours_DeclareNoNetworkedState()
        {
            // The design relies on each client moving only itself. A synced field
            // would be a second source of position and is not part of it.
            foreach (System.Type type in new[] { typeof(BodyContactSystem), typeof(BodyContactDebugView) })
            {
                var attributes = (UdonBehaviourSyncModeAttribute[])type
                    .GetCustomAttributes(typeof(UdonBehaviourSyncModeAttribute), true);

                Assert.AreEqual(1, attributes.Length, type.Name + " should declare its sync mode explicitly");
                Assert.AreEqual(BehaviourSyncMode.None, attributes[0].behaviourSyncMode, type.Name + " must not sync");
            }
        }

        [Test]
        public void Menu_CreatesAWiredSystemAtTheOrigin()
        {
            GameObject root = BodyContactMenu.CreateSystem();
            _created.Add(root);

            BodyContactSystem system = root.GetComponent<BodyContactSystem>();
            Assert.IsNotNull(system, "the root has no BodyContactSystem");
            Assert.IsNotNull(system.debugView, "the system is not connected to its debug view");

            BodyContactDebugView view = system.debugView;
            Assert.AreSame(system, view.source, "the debug view does not point back at the system");
            Assert.IsNotNull(view.lineFilter, "the debug view has no MeshFilter");
            Assert.IsNotNull(view.lineRenderer, "the debug view has no MeshRenderer");
            Assert.IsNotNull(view.lineRenderer.sharedMaterial, "the line renderer has no material");
            Assert.IsNotNull(view.statusLabel, "the debug view has no status label");
            Assert.IsNotNull(view.hudRoot, "the debug view has no HUD root");

            // The line mesh holds world space vertices, so anything other than an
            // identity transform draws every shape in the wrong place.
            Transform lines = view.lineFilter.transform;
            Assert.AreEqual(Vector3.zero, lines.position);
            Assert.AreEqual(Quaternion.identity, lines.rotation);
            Assert.AreEqual(Vector3.one, lines.lossyScale);
        }

        [Test]
        public void Menu_AddsDummiesWithoutDroppingEarlierOnes()
        {
            GameObject root = BodyContactMenu.CreateSystem();
            _created.Add(root);
            BodyContactSystem system = root.GetComponent<BodyContactSystem>();

            GameObject first = BodyContactMenu.AddDummy(system, new Vector3(0f, 0f, 1.5f));
            GameObject second = BodyContactMenu.AddDummy(system, new Vector3(1f, 0f, 1.5f));

            Assert.AreEqual(2, system.dummies.Length);
            Assert.AreSame(first.transform, system.dummies[0]);
            Assert.AreSame(second.transform, system.dummies[1]);
            Assert.AreEqual(new Vector3(1f, 0f, 1.5f), second.transform.position);
        }

        private IUdonProgram Compile<T>() where T : UdonSharpBehaviour
        {
            var host = new GameObject(typeof(T).Name + "UnderTest");
            _created.Add(host);

            T behaviour = host.AddUdonSharpComponent<T>();
            Assert.IsNotNull(behaviour, "AddUdonSharpComponent returned nothing");

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            Assert.IsNotNull(backing, "the proxy has no backing UdonBehaviour");

            UdonSharpProgramAsset asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.IsNotNull(asset, "no UdonSharpProgramAsset was created for " + typeof(T).Name);
            Assert.IsNotNull(asset.SerializedProgramAsset, "the program asset holds no serialized program");

            IUdonProgram program = asset.SerializedProgramAsset.RetrieveProgram();
            Assert.IsNotNull(
                program,
                "UdonSharp produced no program for " + typeof(T).Name + ". The Unity console holds the diagnostics.");
            return program;
        }
    }
}
