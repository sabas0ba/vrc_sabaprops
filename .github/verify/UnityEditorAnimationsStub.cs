// Syntax verification only. Real animation behaviour is covered by Unity/ClientSim.
using UnityEngine;

namespace UnityEditor.Animations
{
    public class AnimatorController : RuntimeAnimatorController
    {
        public AnimatorControllerLayer[] layers;
        public AnimatorControllerParameter[] parameters;
        public static AnimatorController CreateAnimatorControllerAtPath(string path) => null;
        public void AddParameter(string name, AnimatorControllerParameterType type) { }
        public void AddLayer(string name) { }
    }

    public class AnimatorControllerLayer
    {
        public float defaultWeight;
        public AnimatorStateMachine stateMachine;
    }

    public class AnimatorStateMachine : ScriptableObject
    {
        public AnimatorState defaultState;
        public AnimatorState AddState(string name) => null;
    }

    public class AnimatorState : ScriptableObject
    {
        public bool writeDefaultValues;
        public Motion motion;
    }

    public class BlendTree : Motion
    {
        public string blendParameter;
        public bool useAutomaticThresholds;
        public float minThreshold;
        public float maxThreshold;
        public void AddChild(Motion motion, float threshold) { }
    }
}
