using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    public partial class LiquidBodyCanvas
    {
        public Vector3 ContactLocalPoint(Vector3 world)
        {
            return ToCanvasLocalPoint(world, _origin, _right, _up, _forward);
        }

        public Vector3 ContactLocalDirection(Vector3 world)
        {
            return ToCanvasLocalDirection(world, _right, _up, _forward);
        }

        public bool CanContact()
        {
            return _active && _frameValid && contactMaterial != null;
        }

        // Apply queued drawing before sampling; never sample a texture being written.
        public void FlushContact()
        {
            if (_active && (_stampCount > 0 || _washAmount > 0f)) AdvanceCanvas(0f);
            // A pickup can move before this frame's PostLateUpdate. Use its current
            // pose for the transfer, while previously queued marks keep their local coordinates.
            if (!_active) return;
            if (anchor != null) UpdateAnchorFrame();
            else if (Utilities.IsValid(_player)) UpdateFrame();
        }

        public RenderTexture ContactTexture(int channel)
        {
            if (channel == 0) return _frontIsA ? _pigmentA : _pigmentB;
            if (channel == 1) return _frontIsA ? _filmA : _filmB;
            if (channel == 2) return _frontIsA ? _depthA : _depthB;
            return _frontIsA ? _glowA : _glowB;
        }

        /// <summary>
        /// Transfer a metric-sized patch, then reduce and smear the donor. Positions,
        /// normal and motion are world space. A null recipient performs a wipe.
        /// </summary>
        public void TransferContact(LiquidBodyCanvas recipient, Vector3 point, Vector3 recipientPoint,
            Vector3 normal, Vector3 motion, float radius, float fraction, float smear, float seed)
        {
            if (!CanContact() || recipient == this || radius <= 0f || fraction <= 0f) return;
            if (recipient != null && !recipient.CanContact()) return;
            FlushContact();
            if (recipient != null) recipient.FlushContact();
            // A missing bone can release a canvas while its frame is refreshed.
            if (!CanContact() || (recipient != null && !recipient.CanContact())) return;

            Material material = contactMaterial;
            Vector3 sourcePoint = ContactLocalPoint(point);
            Vector3 sourceNormal = ContactLocalDirection(normal).normalized;
            material.SetVector("_DonorPoint", sourcePoint);
            material.SetVector("_DonorNormal", sourceNormal);
            material.SetVector("_DonorExtent", halfExtents);
            material.SetFloat("_DonorInset", atlasInset);
            material.SetVector("_Contact", new Vector4(Mathf.Clamp(radius, 0.005f, 0.3f),
                Mathf.Clamp01(fraction), Mathf.Clamp01(smear), seed));
            material.SetVector("_Motion", ContactLocalDirection(Vector3.ClampMagnitude(motion, radius)));
            material.SetTexture("_DonorPigment", ContactTexture(0));
            material.SetTexture("_DonorFilm", ContactTexture(1));
            material.SetTexture("_DonorDepth", ContactTexture(2));

            if (recipient != null)
            {
                material.SetVector("_ReceiverPoint", recipient.ContactLocalPoint(recipientPoint));
                material.SetVector("_ReceiverNormal", recipient.ContactLocalDirection(-normal).normalized);
                material.SetVector("_ReceiverExtent", recipient.halfExtents);
                material.SetFloat("_ReceiverInset", recipient.atlasInset);
                // Rows map recipient local metric offsets into donor local coordinates.
                material.SetVector("_MapX", ContactLocalDirection(recipient._right));
                material.SetVector("_MapY", ContactLocalDirection(recipient._up));
                material.SetVector("_MapZ", ContactLocalDirection(recipient._forward));
                for (int channel = 0; channel < 4; channel++)
                {
                    material.SetFloat("_Channel", channel);
                    material.SetTexture("_DonorTex", ContactTexture(channel));
                    recipient.BlitContact(material, channel, 0);
                }
                recipient.FinishContact();
            }
            for (int channel = 0; channel < 4; channel++)
            {
                material.SetFloat("_Channel", channel);
                BlitContact(material, channel, 1);
            }
            FinishContact();
        }

        public void BlitContact(Material material, int channel, int pass)
        {
            RenderTexture target;
            if (channel == 0) target = _frontIsA ? _pigmentB : _pigmentA;
            else if (channel == 1) target = _frontIsA ? _filmB : _filmA;
            else if (channel == 2) target = _frontIsA ? _depthB : _depthA;
            else target = _frontIsA ? _glowB : _glowA;
            VRCGraphics.Blit(ContactTexture(channel), target, material, pass);
        }

        public void FinishContact()
        {
            _frontIsA = !_frontIsA;
            _lastActivityTime = Time.time;
            BindTextures();
        }
    }
}
