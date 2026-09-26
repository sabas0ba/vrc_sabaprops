using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds mannequins: a stand-in body with its own Body Canvas, so a demo
    /// shows liquid landing, running and drying with nobody in the world.
    /// <para>
    /// A mannequin is drawn on its own layer and its projector lands only on
    /// that layer. Player canvases ignore everything but the avatar layers, so
    /// the two kinds of canvas never draw on each other's bodies.
    /// </para>
    /// </summary>
    public static class LiquidMannequinBuilder
    {
        /// <summary>
        /// The layer mannequins are drawn on. VRChat reserves layers 0 to 21;
        /// 22 and up are the world's own.
        /// </summary>
        public const int MannequinLayer = 23;

        /// <summary>Hip height of the 1.8 m figure, which is where the canvas is anchored.</summary>
        public const float HipHeight = 1f;

        public const int FaceResolution = 128;

        public const string LightSkinPath = LiquidAssets.MaterialFolder + "/MannequinLight.mat";
        public const string DarkSkinPath = LiquidAssets.MaterialFolder + "/MannequinDark.mat";

        public static string ProjectorMaterialPath(int index)
        {
            return LiquidAssets.MaterialFolder + "/LiquidMannequinProjector_" + index.ToString("00") + ".mat";
        }

        /// <summary>A pale, satin plastic. Shows pigment colour and wet darkening clearly.</summary>
        public static Material LightSkin()
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(LightSkinPath, new Color(0.82f, 0.79f, 0.74f), 0.35f);
        }

        /// <summary>A body material of the given colour, for the colour and surface comparisons.</summary>
        public static Material Skin(string name, Color colour, float smoothness)
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(
                LiquidAssets.MaterialFolder + "/Mannequin_" + name.Replace(" ", "") + ".mat", colour, smoothness);
        }

        /// <summary>A dark, matte cloth. Wet darkening barely shows here; the highlights do.</summary>
        public static Material DarkSkin()
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(DarkSkinPath, new Color(0.1f, 0.1f, 0.11f), 0.08f);
        }

        /// <summary>
        /// Creates a mannequin standing with its feet at the parent's origin,
        /// facing the parent's +Z. With <paramref name="turntable"/>, it stands on
        /// a slowly turning disc.
        /// </summary>
        public static LiquidBodyCanvas Create(Transform parent, string name, int index, Material skin, Material update,
            bool turntable)
        {
            return Create(parent, name, index, skin, update, turntable,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SoftClothName), false, null, null);
        }

        /// <summary>
        /// The same, with the surface the liquid lands on. With
        /// <paramref name="regions"/>, the head is hair and the face and hands are
        /// skin, as on an avatar; the head and hands can then be given their own
        /// materials so the regions read at a glance.
        /// </summary>
        public static LiquidBodyCanvas Create(Transform parent, string name, int index, Material skin, Material update,
            bool turntable, LiquidSurfaceProfile surface, bool regions, Material headMaterial, Material handMaterial)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            Transform body = root.transform;
            if (turntable)
            {
                GameObject disc = Part(root.transform, "Turntable Base", PrimitiveType.Cylinder,
                    new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.02f, 0.9f), Quaternion.identity, skin, 0);
                disc.GetComponent<Renderer>().sharedMaterial = LiquidAssets.CreateOrLoadSurfaceMaterial(
                    LiquidAssets.MaterialFolder + "/TurntableBase.mat", new Color(0.25f, 0.26f, 0.28f), 0.3f);

                var spinner = new GameObject("Turntable");
                spinner.transform.SetParent(root.transform, false);
                spinner.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                LiquidTurntable table = spinner.AddUdonSharpComponent<LiquidTurntable>();
                table.degreesPerSecond = index % 2 == 0 ? 6f : -6f;
                UdonSharpEditorUtility.CopyProxyToUdon(table);
                body = spinner.transform;
            }

            var hips = new GameObject("Hips");
            hips.transform.SetParent(body, false);
            hips.transform.localPosition = new Vector3(0f, HipHeight, 0f);

            BuildFigure(body, skin);
            Transform head = body.Find("Head");
            Transform leftHand = body.Find("Left Hand");
            Transform rightHand = body.Find("Right Hand");
            if (headMaterial != null)
            {
                head.GetComponent<Renderer>().sharedMaterial = headMaterial;
            }

            if (handMaterial != null)
            {
                leftHand.GetComponent<Renderer>().sharedMaterial = handMaterial;
                rightHand.GetComponent<Renderer>().sharedMaterial = handMaterial;
            }

            // Solid to walk into, on the mannequin layer so it never blocks liquid.
            var solid = new GameObject("Collider");
            solid.layer = MannequinLayer;
            solid.transform.SetParent(body, false);
            var capsule = solid.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.height = 1.8f;
            capsule.radius = 0.25f;

            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(root.transform, false);
            LiquidBodyCanvas canvas = canvasObject.AddUdonSharpComponent<LiquidBodyCanvas>();

            var projectorObject = new GameObject("Projector");
            projectorObject.transform.SetParent(canvasObject.transform, false);
            Material projectorMaterial = LiquidAssets.CreateOrLoadMaterial(
                ProjectorMaterialPath(index), LiquidAssets.BodyProjectorShader);
            LiquidCanvasPoolBuilder.ConfigureProjector(projectorObject, canvas.halfExtents, projectorMaterial,
                MannequinLayer, ~(1 << MannequinLayer));
            projectorObject.SetActive(false);

            canvas.canvasRoot = canvasObject.transform;
            canvas.projectorObject = projectorObject;
            canvas.projectorMaterial = projectorMaterial;
            canvas.updateMaterial = update;
            canvas.anchor = hips.transform;
            canvas.anchorFeetBelow = HipHeight;
            // The head top is at 1.78 m; a capsule reaching higher records hits above the head, and the depth mask then hides them.
            canvas.anchorHeadAbove = 0.78f;
            canvas.anchorBodyRadius = 0.2f;
            canvas.faceResolution = FaceResolution;
            canvas.bodySurface = surface;
            canvas.hairSurface = LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.HairName);
            canvas.skinSurface = LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SkinName);
            canvas.estimateRegions = regions;
            canvas.headAnchor = head;
            canvas.leftHandAnchor = leftHand;
            canvas.rightHandAnchor = rightHand;
            canvas.leftFootAnchor = body.Find("Left Foot");
            canvas.rightFootAnchor = body.Find("Right Foot");
            // The figure's head is 0.2 m across; the region sphere a little larger.
            canvas.headRadius = 0.13f;
            canvas.handRadius = 0.06f;
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            EditorUtility.SetDirty(canvas);
            return canvas;
        }

        /// <summary>
        /// An outfit for a clothed mannequin: the colour and surface of the top,
        /// the trousers and the shoes, and whether the forearms and lower legs
        /// are bare.
        /// </summary>
        public struct Outfit
        {
            public string Name;
            public Color Top;
            public string TopSurface;
            public Color Bottom;
            public string BottomSurface;
            public Color Shoes;
            public string ShoesSurface;
            public Color Hair;
            public bool ShortSleeves;
            public bool Shorts;
        }

        /// <summary>A light T-shirt, jeans and leather shoes.</summary>
        public static readonly Outfit Casual = new Outfit
        {
            Name = "Casual",
            Top = new Color(0.86f, 0.86f, 0.84f), TopSurface = LiquidSurfaceBuilder.SoftClothName,
            Bottom = new Color(0.2f, 0.27f, 0.42f), BottomSurface = LiquidSurfaceBuilder.HardClothName,
            Shoes = new Color(0.28f, 0.16f, 0.09f), ShoesSurface = LiquidSurfaceBuilder.LeatherName,
            Hair = new Color(0.12f, 0.09f, 0.07f), ShortSleeves = true,
        };

        /// <summary>A yellow rain jacket, dark trousers and rubber boots.</summary>
        public static readonly Outfit RainGear = new Outfit
        {
            Name = "Rain Gear",
            Top = new Color(0.92f, 0.72f, 0.1f), TopSurface = LiquidSurfaceBuilder.PlasticName,
            Bottom = new Color(0.12f, 0.12f, 0.13f), BottomSurface = LiquidSurfaceBuilder.HardClothName,
            Shoes = new Color(0.1f, 0.25f, 0.14f), ShoesSurface = LiquidSurfaceBuilder.PlasticName,
            Hair = new Color(0.45f, 0.3f, 0.16f),
        };

        /// <summary>A knit jumper, cloth shorts and leather boots, with bare legs.</summary>
        public static readonly Outfit Knit = new Outfit
        {
            Name = "Knit",
            Top = new Color(0.55f, 0.16f, 0.14f), TopSurface = LiquidSurfaceBuilder.SoftClothName,
            Bottom = new Color(0.42f, 0.38f, 0.3f), BottomSurface = LiquidSurfaceBuilder.SoftClothName,
            Shoes = new Color(0.16f, 0.1f, 0.06f), ShoesSurface = LiquidSurfaceBuilder.LeatherName,
            Hair = new Color(0.75f, 0.62f, 0.4f), Shorts = true,
        };

        /// <summary>
        /// A clothed mannequin: skin, hair, a top, trousers and shoes, each part
        /// with the surface its material implies, and avatar regions on so the
        /// canvas treats them the way it treats a player.
        /// </summary>
        public static LiquidBodyCanvas CreateClothed(Transform parent, string name, int index, Material update,
            bool turntable, Outfit outfit)
        {
            Material skin = Skin("Skin", new Color(0.86f, 0.68f, 0.58f), 0.35f);
            LiquidBodyCanvas canvas = Create(parent, name, index, skin, update, turntable,
                LiquidSurfaceBuilder.GetSurface(outfit.TopSurface), true,
                Skin(outfit.Name + "Hair", outfit.Hair, 0.4f), null);

            Transform body = canvas.anchor.parent;
            Material top = Skin(outfit.Name + "Top", outfit.Top, SurfaceSmoothness(outfit.TopSurface));
            Material bottom = Skin(outfit.Name + "Bottom", outfit.Bottom, SurfaceSmoothness(outfit.BottomSurface));
            Material shoes = Skin(outfit.Name + "Shoes", outfit.Shoes, SurfaceSmoothness(outfit.ShoesSurface));

            Paint(body, top, "Chest", "Waist", "Left Upper Arm", "Right Upper Arm");
            if (!outfit.ShortSleeves)
            {
                Paint(body, top, "Left Forearm", "Right Forearm");
            }

            Paint(body, bottom, "Pelvis", "Left Thigh", "Right Thigh");
            if (!outfit.Shorts)
            {
                Paint(body, bottom, "Left Shin", "Right Shin");
            }

            Paint(body, shoes, "Left Foot", "Right Foot");

            // Hair over the crown and the back of the head, set back so the face stays bare.
            Material hair = Skin(outfit.Name + "Hair", outfit.Hair, 0.4f);
            body.Find("Head").GetComponent<Renderer>().sharedMaterial = skin;
            Part(body, "Hair", PrimitiveType.Sphere, new Vector3(0f, 1.7f, -0.04f), new Vector3(0.21f, 0.2f, 0.2f),
                Quaternion.identity, hair, MannequinLayer);

            canvas.lowerSurface = LiquidSurfaceBuilder.GetSurface(outfit.BottomSurface);
            canvas.feetSurface = LiquidSurfaceBuilder.GetSurface(outfit.ShoesSurface);
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            EditorUtility.SetDirty(canvas);
            return canvas;
        }

        private static float SurfaceSmoothness(string surface)
        {
            switch (surface)
            {
                case LiquidSurfaceBuilder.PlasticName: return 0.75f;
                case LiquidSurfaceBuilder.LeatherName: return 0.55f;
                case LiquidSurfaceBuilder.HardClothName: return 0.2f;
                default: return 0.05f;
            }
        }

        private static void Paint(Transform body, Material material, params string[] parts)
        {
            foreach (string part in parts)
            {
                body.Find(part).GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        /// <summary>
        /// A 1.8 m figure from primitives: head, neck, chest, waist, pelvis, arms
        /// held slightly away from the body and legs. Arms and legs are separate
        /// from the torso, so the depth mask has something to separate.
        /// </summary>
        private static void BuildFigure(Transform body, Material skin)
        {
            int layer = MannequinLayer;
            Part(body, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.66f, 0f), new Vector3(0.2f, 0.24f, 0.22f), Quaternion.identity, skin, layer);
            Part(body, "Neck", PrimitiveType.Capsule, new Vector3(0f, 1.5f, 0f), new Vector3(0.1f, 0.08f, 0.1f), Quaternion.identity, skin, layer);
            Part(body, "Chest", PrimitiveType.Capsule, new Vector3(0f, 1.3f, 0f), new Vector3(0.36f, 0.22f, 0.22f), Quaternion.identity, skin, layer);
            Part(body, "Waist", PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), new Vector3(0.28f, 0.18f, 0.18f), Quaternion.identity, skin, layer);
            Part(body, "Pelvis", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.2f, 0.22f), Quaternion.identity, skin, layer);

            for (int side = -1; side <= 1; side += 2)
            {
                string label = side < 0 ? "Left" : "Right";
                Quaternion armTilt = Quaternion.Euler(0f, 0f, side * 10f);
                Part(body, label + " Upper Arm", PrimitiveType.Capsule, new Vector3(side * 0.25f, 1.24f, 0f), new Vector3(0.09f, 0.16f, 0.09f), armTilt, skin, layer);
                Part(body, label + " Forearm", PrimitiveType.Capsule, new Vector3(side * 0.31f, 0.96f, 0.02f), new Vector3(0.075f, 0.15f, 0.075f), armTilt, skin, layer);
                Part(body, label + " Hand", PrimitiveType.Sphere, new Vector3(side * 0.34f, 0.78f, 0.03f), new Vector3(0.07f, 0.1f, 0.05f), Quaternion.identity, skin, layer);
                Part(body, label + " Thigh", PrimitiveType.Capsule, new Vector3(side * 0.1f, 0.7f, 0f), new Vector3(0.14f, 0.22f, 0.14f), Quaternion.identity, skin, layer);
                Part(body, label + " Shin", PrimitiveType.Capsule, new Vector3(side * 0.1f, 0.3f, 0f), new Vector3(0.11f, 0.22f, 0.11f), Quaternion.identity, skin, layer);
                Part(body, label + " Foot", PrimitiveType.Capsule, new Vector3(side * 0.1f, 0.04f, 0.06f), new Vector3(0.09f, 0.1f, 0.09f), Quaternion.Euler(90f, 0f, 0f), skin, layer);
            }
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material, int layer)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.layer = layer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
