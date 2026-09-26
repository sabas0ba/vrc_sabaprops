using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Presets for the surfaces liquid lands on, kept under one "Liquid Surfaces"
    /// object so every canvas in a scene shares them.
    /// </summary>
    public static class LiquidSurfaceBuilder
    {
        public const string SurfacesName = "Liquid Surfaces";

        public const string SoftClothName = "Soft Cloth";
        public const string HardClothName = "Hard Cloth";
        public const string LeatherName = "Leather";
        public const string HairName = "Hair";
        public const string SkinName = "Skin";
        public const string PlasticName = "Plastic";

        /// <summary>Every preset, in the order the comparison demo lines them up.</summary>
        public static readonly string[] PresetNames =
        {
            SoftClothName, HardClothName, LeatherName, HairName, SkinName, PlasticName,
        };

        /// <summary>
        /// The named surface under "Liquid Surfaces", created with its preset
        /// values when missing.
        /// </summary>
        public static LiquidSurfaceProfile GetSurface(string name)
        {
            GameObject surfaces = GameObject.Find(SurfacesName);
            if (surfaces == null)
            {
                surfaces = new GameObject(SurfacesName);
                Undo.RegisterCreatedObjectUndo(surfaces, "Create Liquid Surfaces");
            }

            Transform existing = surfaces.transform.Find(name);
            if (existing != null)
            {
                LiquidSurfaceProfile found = existing.GetComponent<LiquidSurfaceProfile>();
                if (found != null)
                {
                    return found;
                }
            }

            var surfaceObject = new GameObject(name);
            surfaceObject.transform.SetParent(surfaces.transform, false);
            LiquidSurfaceProfile surface = surfaceObject.AddUdonSharpComponent<LiquidSurfaceProfile>();
            ApplyPreset(surface, name);
            UdonSharpEditorUtility.CopyProxyToUdon(surface);
            EditorUtility.SetDirty(surface);
            return surface;
        }

        /// <summary>
        /// Preset values.
        /// <list type="bullet">
        /// <item>Soft cloth: soaks liquid in and darkens; hardly shines; paint bleeds along the fibres.</item>
        /// <item>Hard cloth: soaks some in, beads some on top, takes on a sheen when wet.</item>
        /// <item>Leather: mostly beads; glossy when wet; paint keeps a sharp edge.</item>
        /// <item>Hair: close to hard cloth; liquid gathers into vertical strands.</item>
        /// <item>Skin: close to leather, but liquid clings and spreads thinly instead of running off.</item>
        /// <item>Plastic: beads fully and absorbs nothing.</item>
        /// </list>
        /// </summary>
        public static void ApplyPreset(LiquidSurfaceProfile surface, string name)
        {
            switch (name)
            {
                case HardClothName:
                    Set(surface, 0.45f, 0.5f, 0.35f, 0.5f, 0.25f, 0f, 0.012f);
                    return;
                case LeatherName:
                    Set(surface, 0.12f, 0.8f, 0.65f, 0.35f, 0.08f, 0f, 0.014f);
                    return;
                case HairName:
                    Set(surface, 0.35f, 0.6f, 0.5f, 0.45f, 0.2f, 0.8f, 0.01f);
                    return;
                case SkinName:
                    Set(surface, 0.2f, 0.15f, 0.45f, 0.8f, 0.12f, 0f, 0.012f);
                    return;
                case PlasticName:
                    Set(surface, 0f, 0.85f, 0.7f, 0.2f, 0f, 0f, 0.018f);
                    return;
                default:
                    Set(surface, 0.9f, 0.05f, 0.05f, 0.65f, 0.7f, 0f, 0.012f);
                    return;
            }
        }

        private static void Set(LiquidSurfaceProfile surface, float absorbency, float repellency, float sheen,
            float friction, float bleed, float strands, float beadSize)
        {
            surface.absorbency = absorbency;
            surface.repellency = repellency;
            surface.sheen = sheen;
            surface.friction = friction;
            surface.bleed = bleed;
            surface.strands = strands;
            surface.beadSize = beadSize;
        }

        /// <summary>
        /// Gives a canvas the usual avatar assignment: clothing on the body, hair
        /// on the head and skin on the face and hands.
        /// </summary>
        public static void AssignAvatarDefaults(LiquidBodyCanvas canvas)
        {
            canvas.bodySurface = GetSurface(SoftClothName);
            canvas.hairSurface = GetSurface(HairName);
            canvas.skinSurface = GetSurface(SkinName);
            canvas.estimateRegions = true;
        }
    }
}
