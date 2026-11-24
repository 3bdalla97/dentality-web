// Requires homuler/MediaPipeUnityPlugin (Tasks API) and the selfie_multiclass_256x256 model:
// https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_multiclass_256x256/float32/latest/selfie_multiclass_256x256.tflite
// Classes: 0 background, 1 hair, 2 body skin, 3 face skin, 4 clothes, 5 other.

using Mediapipe;
using Mediapipe.Tasks.Vision.ImageSegmenter;
using Mediapipe.Unity;
using UnityEngine;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Copies the background and face-skin confidence masks (plus camera pixels, which the
    /// stomion detection uses) out of an ImageSegmenterResult and forwards them to
    /// <see cref="SideProfileController"/>. Call <see cref="OnResult"/> BEFORE the runner disposes the masks.
    /// </summary>
    public class SideProfileSegmenterBridge : MonoBehaviour
    {
        public SideProfileController controller;
        [Tooltip("Read camera pixels for the stomion cue (CPU images only). Recommended.")]
        public bool readPixels = true;

        float[] _bg, _face;
        Color32[] _pixels;

        public void OnResult(ImageSegmenterResult result, Image image)
        {
            if (controller == null) return;
            var masks = result.confidenceMasks;
            if (masks == null || masks.Count < 4)
            {
                if (masks != null && masks.Count > 0)
                    Debug.LogWarning("SideProfile: use the selfie_multiclass_256x256 model with confidence masks enabled.");
                controller.SubmitFrame(null, null, 0, 0, null, 0, 0, 0);
                return;
            }

            int w = masks[0].Width(), h = masks[0].Height();
            if (_bg == null || _bg.Length != w * h) { _bg = new float[w * h]; _face = new float[w * h]; }
            if (!masks[0].TryReadChannelNormalized(0, _bg) || !masks[3].TryReadChannelNormalized(0, _face)) return;

            Color32[] px = null; int pw = 0, ph = 0, iw = w;
            if (image != null)
            {
                iw = image.Width();
                if (readPixels && !image.UsesGpu())
                {
                    pw = image.Width(); ph = image.Height();
                    if (_pixels == null || _pixels.Length != pw * ph) _pixels = new Color32[pw * ph];
                    if (image.TryReadPixelData(_pixels)) px = _pixels;
                }
            }
            controller.SubmitFrame(_bg, _face, w, h, px, pw, ph, iw);
        }
    }
}
