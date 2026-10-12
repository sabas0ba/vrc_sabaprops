using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    public partial class LiquidCanvasPool
    {
        [Header("Limb Canvas")]
        public LiquidBodyCanvas[] limbs;
        [Tooltip("道具など、プレイヤー・Surface 以外の Canvas。ローカル表示設定に含めます。")]
        public LiquidBodyCanvas[] contactTools;
        [Range(0, 8)] public int maxLimbsPerPlayer = 4;
        [Header("接触の負荷上限（クライアントごと）")]
        [Range(1, 8)] public int maxContactsPerFrame = 2;
        [Range(1, 60)] public int maxContactsPerSecond = 20;
        private int _budgetFrame = -1;
        private int _frameContacts;
        private float _budgetStart;
        private int _secondContacts;
        private int _acceptedContacts;
        private int _droppedContacts;
        private bool _localDisplayEnabled = true;
        private int[] _limbBones = { (int)HumanBodyBones.LeftHand, (int)HumanBodyBones.RightHand,
            (int)HumanBodyBones.LeftFoot, (int)HumanBodyBones.RightFoot, (int)HumanBodyBones.Head,
            (int)HumanBodyBones.LeftLowerArm, (int)HumanBodyBones.RightLowerArm,
            (int)HumanBodyBones.LeftLowerLeg, (int)HumanBodyBones.RightLowerLeg };

        public LiquidBodyCanvas LimbAtPoint(VRCPlayerApi player, Vector3 point)
        {
            if (!Utilities.IsValid(player)) return null;
            float nearest = 0.18f * Mathf.Clamp(player.GetAvatarEyeHeightAsMeters() / 1.6f, 0.25f, 3f);
            int selected = -1;
            for (int i = 0; i < _limbBones.Length; i++)
            {
                Vector3 p = player.GetBonePosition((HumanBodyBones)_limbBones[i]);
                if (p.sqrMagnitude < 1e-12f) continue;
                float distance = Vector3.Distance(p, point);
                if (distance < nearest) { nearest = distance; selected = i; }
            }
            return selected < 0 ? null : LimbForPlayer(player, (HumanBodyBones)_limbBones[selected]);
        }

        /// <summary>Allocate on contact; keep active limbs until reset or avatar change.</summary>
        public LiquidBodyCanvas FindLimbAtPoint(int playerId, Vector3 point)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(playerId);
            if (limbs == null || !Utilities.IsValid(player)) return null;
            LiquidBodyCanvas found = null;
            float nearest = 0.22f * Mathf.Clamp(player.GetAvatarEyeHeightAsMeters() / 1.6f, 0.25f, 3f);
            for (int i = 0; i < limbs.Length; i++)
            {
                LiquidBodyCanvas limb = limbs[i];
                if (limb == null || limb.GetPlayerId() != playerId) continue;
                float distance = Vector3.Distance(point, player.GetBonePosition(limb.followBone));
                if (distance < nearest) { nearest = distance; found = limb; }
            }
            return found;
        }

        public void ImmerseLimbs(int playerId, float worldY, LiquidProfile profile, float seconds)
        {
            if (limbs == null) return;
            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] != null && limbs[i].GetPlayerId() == playerId) limbs[i].ApplyImmersion(worldY, profile, seconds);
        }

        public LiquidBodyCanvas LimbForPlayer(VRCPlayerApi player, HumanBodyBones bone)
        {
            if (!_localDisplayEnabled || !Utilities.IsValid(player)) return null;
            if (limbs == null || maxLimbsPerPlayer <= 0 || player.GetBonePosition(bone).sqrMagnitude < 1e-12f)
                return AcquireCanvas(player);
            int count = 0;
            LiquidBodyCanvas free = null;
            for (int i = 0; i < limbs.Length; i++)
            {
                LiquidBodyCanvas canvas = limbs[i];
                if (canvas == null) continue;
                if (canvas.GetPlayerId() == player.playerId)
                {
                    if (canvas.followBone == bone) return canvas;
                    count++;
                }
                else if (canvas.GetPlayerId() < 0 && free == null) free = canvas;
            }
            if (free == null || count >= maxLimbsPerPlayer) return AcquireCanvas(player);
            free.followBone = bone;
            free.Assign(player);
            return free;
        }

        public void ReleaseLimbs(int playerId)
        {
            if (limbs == null) return;
            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] != null && (playerId < 0 || limbs[i].GetPlayerId() == playerId)) limbs[i].Release();
        }

        public void SetLocalDisplay(bool enabled)
        {
            _localDisplayEnabled = enabled;
            SetCanvasDisplay(canvases, enabled);
            SetCanvasDisplay(limbs, enabled);
            SetCanvasDisplay(mannequins, enabled);
            SetCanvasDisplay(surfaces, enabled);
            SetCanvasDisplay(contactTools, enabled);
        }

        /// <summary>Bound GPU work before either side of a contact is modified.</summary>
        public bool TryBeginContact()
        {
            if (!_localDisplayEnabled) return false;
            if (_budgetFrame != Time.frameCount) { _budgetFrame = Time.frameCount; _frameContacts = 0; }
            if (Time.time - _budgetStart >= 1f) { _budgetStart = Time.time; _secondContacts = 0; }
            if (_frameContacts >= Mathf.Max(1, maxContactsPerFrame) || _secondContacts >= Mathf.Max(1, maxContactsPerSecond))
            {
                _droppedContacts++;
                return false;
            }
            _frameContacts++;
            _secondContacts++;
            _acceptedContacts++;
            return true;
        }

        public int GetAcceptedContacts() { return _acceptedContacts; }
        public int GetDroppedContacts() { return _droppedContacts; }

        private void SetCanvasDisplay(LiquidBodyCanvas[] group, bool enabled)
        {
            if (group == null) return;
            for (int i = 0; i < group.Length; i++)
                if (group[i] != null) group[i].localDisplayEnabled = enabled;
        }
    }
}
