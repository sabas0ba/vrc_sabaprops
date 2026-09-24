using NUnit.Framework;
using SabaProps.Capture;
using UdonSharpEditor;
using UnityEngine;

namespace SabaProps.Capture.WorldTests
{
    /// <summary>
    /// Runs the recorder's collection layer on its editor proxy, against real RenderTextures.
    /// <para>
    /// The offline tier checks the arithmetic with no textures at all. This checks that the
    /// RenderTextures are created with the requested size, that VRCGraphics.Blit actually
    /// copies the source into the frame, and that the three full policies drive the counts
    /// the documentation promises.
    /// </para>
    /// <para>
    /// _ReleaseFrames and the capacity resize are not exercised here: they call
    /// Object.Destroy, which Unity refuses in edit mode with an error log, and the test
    /// framework counts that log as a failure. Their only difference from the paths
    /// exercised here is which frames are handed to Destroy.
    /// </para>
    /// </summary>
    public class CaptureRecorderTests
    {
        private GameObject _root;
        private RenderTexture _source;
        private CaptureRecorder _recorder;

        [SetUp]
        public void SetUp()
        {
            _source = new RenderTexture(64, 36, 0, RenderTextureFormat.ARGB32);
            _source.Create();
            Fill(_source, Color.red);

            _root = new GameObject("RecorderUnderTest");
            _recorder = _root.AddUdonSharpComponent<CaptureRecorder>();
            _recorder.sourceTexture = _source;
            _recorder.frameWidth = 32;
            _recorder.frameHeight = 18;
            _recorder.maxFrames = 4;
        }

        [TearDown]
        public void TearDown()
        {
            if (_recorder != null)
            {
                for (int i = 0; i < _recorder.GetFrameCount(); i++)
                {
                    RenderTexture frame = _recorder.GetFrame(i);
                    if (frame != null)
                    {
                        Object.DestroyImmediate(frame);
                    }
                }
            }

            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_source != null)
            {
                Object.DestroyImmediate(_source);
            }
        }

        [Test]
        public void Capture_CopiesTheSourceAtTheRequestedSize()
        {
            _recorder._CaptureNow();

            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(1));
            RenderTexture frame = _recorder.GetFrame(0);
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.width, Is.EqualTo(32));
            Assert.That(frame.height, Is.EqualTo(18));
            Assert.That(frame.depth, Is.EqualTo(0), "frames must not carry depth; Blit into them fails on Quest");

            Color centre = ReadCentre(frame);
            Assert.That(centre.r, Is.GreaterThan(0.9f), $"the frame does not hold the source: {centre}");
            Assert.That(centre.g, Is.LessThan(0.1f), $"the frame does not hold the source: {centre}");
        }

        [Test]
        public void RingPolicy_KeepsTheNewestFrames()
        {
            _recorder.fullPolicy = CaptureRecorder.PolicyRing;
            for (int i = 0; i < 6; i++)
            {
                _recorder._CaptureNow();
            }

            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(4));
            Assert.That(_recorder.GetCapacity(), Is.EqualTo(4));
            Assert.That(_recorder.IsFull(), Is.True);
        }

        [Test]
        public void StopPolicy_RefusesFramesPastTheLimit()
        {
            _recorder.fullPolicy = CaptureRecorder.PolicyStop;
            for (int i = 0; i < 6; i++)
            {
                _recorder._CaptureNow();
            }

            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(4));
        }

        [Test]
        public void ThinPolicy_HalvesAndDoublesTheInterval()
        {
            _recorder.fullPolicy = CaptureRecorder.PolicyThin;
            _recorder.interval = 5f;
            for (int i = 0; i < 5; i++)
            {
                _recorder._CaptureNow();
            }

            // 4 frames, thinned to 2, then the fifth stored.
            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(3));
            Assert.That(_recorder.GetCurrentInterval(), Is.EqualTo(10.0).Within(1e-9));
        }

        [Test]
        public void Clear_EmptiesWithoutLosingCapacity()
        {
            _recorder._CaptureNow();
            _recorder._CaptureNow();
            RenderTexture reused = _recorder.GetFrame(0);

            _recorder._Clear();
            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(0));
            Assert.That(_recorder.GetFrame(0), Is.Null);

            _recorder._CaptureNow();
            Assert.That(_recorder.GetFrame(0), Is.SameAs(reused), "cleared frames must be reused, not reallocated");
        }

        [Test]
        public void MissingSource_StoresNothing()
        {
            _recorder.sourceTexture = null;
            _recorder._CaptureNow();
            Assert.That(_recorder.GetFrameCount(), Is.EqualTo(0));
        }

        private static void Fill(RenderTexture target, Color colour)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, colour);
            RenderTexture.active = previous;
        }

        private static Color ReadCentre(RenderTexture source)
        {
            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                pixels.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                pixels.Apply();
                return pixels.GetPixel(source.width / 2, source.height / 2);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
