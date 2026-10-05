using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.PutItems.Editors
{
    public static class PlacementMenu
    {
        [MenuItem("Tools/SabaProps/Put Items/Developer/Create Minimal Test Rig")]
        public static void CreateDemo()
        {
            PlacementPrograms.Prepare();
            GameObject root = new GameObject("Put Items Demo");
            Undo.RegisterCreatedObjectUndo(root, "Create placement demo");
            PlacementSurface table = CreateSurface(root.transform, "Table", new Vector3(0f, 0.75f, 0f), Quaternion.identity);
            PlacementSurface wall = CreateSurface(root.transform, "Wall", new Vector3(0f, 1.4f, 0.7f), Quaternion.Euler(-90f, 0f, 0f));
            table.acceptedCategories = 1;
            wall.acceptedCategories = 2;
            UdonSharpEditorUtility.CopyProxyToUdon(table);
            UdonSharpEditorUtility.CopyProxyToUdon(wall);
            CreateItem(root.transform, "Table Prop", new Vector3(-0.25f, 1f, 0f), Quaternion.identity, table, 1);
            CreateItem(root.transform, "Wall Prop", new Vector3(0.25f, 1.4f, 0.4f), Quaternion.Euler(-90f, 0f, 0f), wall, 2);
            Selection.activeGameObject = root;
        }

        private static PlacementSurface CreateSurface(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            GameObject plane = new GameObject(name);
            plane.transform.SetParent(parent, false);
            plane.transform.SetPositionAndRotation(position, rotation);
            PlacementSurface surface = UdonSharpUndo.AddComponent<PlacementSurface>(plane);
            surface.size = new Vector2(1.2f, 0.8f);
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Surface Geometry";
            visual.transform.SetParent(plane.transform, false);
            visual.transform.localPosition = new Vector3(0f, -0.025f, 0f);
            visual.transform.localScale = new Vector3(1.2f, 0.05f, 0.8f);
            UdonSharpEditorUtility.CopyProxyToUdon(surface);
            return surface;
        }

        private static void CreateItem(Transform parent, string name, Vector3 position, Quaternion rotation,
            PlacementSurface surface, int category)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.SetPositionAndRotation(position, rotation);
            BoxCollider collider = item.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.12f, 0.16f, 0.12f);
            collider.center = new Vector3(0f, 0.08f, 0f);
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            item.AddComponent<VRCPickup>();
            VRCObjectSync sync = item.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;
            PlacementSolver solver = UdonSharpUndo.AddComponent<PlacementSolver>(item);
            solver.surfaces = new[] { surface };
            solver.footprintRadius = 0.09f;
            ObjectSyncPlacement placement = UdonSharpUndo.AddComponent<ObjectSyncPlacement>(item);
            placement.solver = solver;
            placement.contact = item.transform;
            placement.category = category;
            placement.normalKinematic = true;
            placement.normalGravity = false;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Prop Geometry";
            visual.transform.SetParent(item.transform, false);
            visual.transform.localPosition = collider.center;
            visual.transform.localScale = collider.size;
            UdonSharpEditorUtility.CopyProxyToUdon(solver);
            UdonSharpEditorUtility.CopyProxyToUdon(placement);
        }
    }
}
