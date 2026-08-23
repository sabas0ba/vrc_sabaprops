using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.ClientSim.Editor;

namespace SabaProps.Foliage.WorldSetup
{
    /// <summary>
    /// Brings the verification project up to the configuration a VRChat world
    /// project is expected to have.
    /// <para>
    /// A project VCC creates from the world template already has this; one
    /// assembled from bare Unity defaults, as assemble.sh does, does not, and
    /// the SDK control panel reports it under "Review Any Alerts". It matters
    /// beyond the warning: the layer list and the collision matrix decide what
    /// the player collides with, so a world tested without them is not being
    /// tested against VRChat's physics.
    /// </para>
    /// <para>
    /// Driven by run-tests.sh through -executeMethod, in its own editor
    /// session: changing the active input handler only takes effect on the next
    /// launch.
    /// </para>
    /// </summary>
    public static class FoliageWorldProjectSetup
    {
        /// <summary>
        /// Entry point for -executeMethod. Exits non-zero if anything is still
        /// unconfigured afterwards, so a silent failure cannot be mistaken for
        /// a configured project.
        /// </summary>
        public static void ConfigureForVrchat()
        {
            var applied = new List<string>();

            if (!UpdateLayers.AreLayersSetup())
            {
                UpdateLayers.SetupEditorLayers();
                applied.Add("layers");
            }

            if (!UpdateLayers.IsCollisionLayerMatrixSetup())
            {
                UpdateLayers.SetupCollisionLayerMatrix();
                applied.Add("collision matrix");
            }

            if (!ClientSimProjectSettingsSetup.IsUsingCorrectInputAxesSettings())
            {
                ClientSimProjectSettingsSetup.ApplyClientSimInputAxes();
                applied.Add("input axes");
            }

            if (!ClientSimProjectSettingsSetup.IsUsingCorrectInputTypeSettings())
            {
                ClientSimProjectSettingsSetup.SetInputTypeSettings();
                applied.Add("input handling");
            }

            if (!ClientSimProjectSettingsSetup.IsUsingCorrectAudioSettings())
            {
                ClientSimProjectSettingsSetup.SetAudioSettings();
                applied.Add("audio");
            }

            AssetDatabase.SaveAssets();

            Debug.Log(applied.Count == 0
                ? "[SabaProps Foliage] プロジェクト設定は既に VRChat 準拠です。"
                : "[SabaProps Foliage] VRChat 準拠へ設定しました: " + string.Join(", ", applied));

            List<string> remaining = Unconfigured();
            if (remaining.Count > 0)
            {
                Debug.LogError(
                    "[SabaProps Foliage] 設定できなかった項目があります: " + string.Join(", ", remaining));
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Everything the SDK or ClientSim would still complain about.</summary>
        private static List<string> Unconfigured()
        {
            var problems = new List<string>();

            if (!UpdateLayers.AreLayersSetup())
            {
                problems.Add("layers");
            }

            if (!UpdateLayers.IsCollisionLayerMatrixSetup())
            {
                problems.Add("collision matrix");
            }

            // The input handler only switches over on the next editor launch,
            // so this one is expected to still read as wrong in this session.
            if (!ClientSimProjectSettingsSetup.IsUsingCorrectInputAxesSettings())
            {
                problems.Add("input axes");
            }

            if (!ClientSimProjectSettingsSetup.IsUsingCorrectAudioSettings())
            {
                problems.Add("audio");
            }

            return problems;
        }
    }
}
