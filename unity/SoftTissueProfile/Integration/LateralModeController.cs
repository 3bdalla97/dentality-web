// Adds a LATERAL (90°) mode next to the existing frontal pipeline
// (FaceLandmarkerRunner + FrontalAnalyzer + FrontalOverlay), without changing how the frontal works.
//
// Auto mode: patient faces the camera → frontal (FrontalOverlay as usual).
//            patient turns 90° (FrontalAnalyzer loses the face) → lateral:
//            segmentation runs on the same camera, the silhouette is traced and measured.
// "Capture lateral" sends the lip profile (from the E-line) to the frontal Ext tab.

using System.Collections;
using Mediapipe.Tasks.Vision.ImageSegmenter;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;
using BaseOptions = Mediapipe.Tasks.Core.BaseOptions;
using RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;
using ImageProcessingOptions = Mediapipe.Tasks.Vision.Core.ImageProcessingOptions;

namespace Dentality.ProfileAnalysis
{
    public class LateralModeController : MonoBehaviour
    {
        public enum ModeSelect { Auto, Frontal, Lateral }

        [Header("Existing frontal pipeline (found automatically if empty)")]
        public FrontalAnalyzer frontal;
        public FrontalOverlay frontalOverlay;
        public RectTransform target;                       // "Annotatable Screen"

        [Header("Model (same folder as face_landmarker_v2.bytes)")]
        public string segmentationModel = "selfie_multiclass_256x256.bytes";

        [Header("Mode")]
        public ModeSelect mode = ModeSelect.Auto;
        public float frontalMaxYaw = 30f;                  // |yaw| above this is not frontal
        public float lateralDelaySec = 0.5f;               // no frontal face for this long → lateral
        public bool masksBottomUp;                         // press "Flip V" if the tracing is upside down

        [Header("Lip profile thresholds (Ricketts E-line, mm; PLACEHOLDERS: use your reference)")]
        public float lsProtrusiveAbove = -2f, liProtrusiveAbove = 0f;
        public float lsRetrusiveBelow = -6f, liRetrusiveBelow = -4f;

        public bool IsLateral { get; private set; }

        ImageSegmenter _seg;
        ImageSegmenterResult _result;
        Mediapipe.Unity.Experimental.TextureFramePool _pool;
        SideProfileController _ctl;
        SideProfileOverlay _overlay;
        float[] _bg, _face;
        Color32[] _pixels;
        float _noFaceSince = -1f;
        float _pxPerMm, _rectW;                            // last good frontal scale (display px / mm)
        SideProfileResult _captured;
        float _capLs, _capLi; bool _capMm;
        string _error = "";
        GUIStyle _small, _title, _big, _ana;

        static readonly Color Ok = new Color(0.45f, 1f, 0.45f), Warn = new Color(1f, 0.65f, 0.2f),
                              Yellow = new Color(1f, 0.92f, 0.2f), Gray = new Color(0.8f, 0.8f, 0.8f);

        IEnumerator Start()
        {
            if (frontal == null) frontal = FindFirstObjectByType<FrontalAnalyzer>();
            if (frontalOverlay == null) frontalOverlay = FindFirstObjectByType<FrontalOverlay>();
            if (target == null) { var go = GameObject.Find("Annotatable Screen"); if (go) target = go.GetComponent<RectTransform>(); }
            if (frontal == null || target == null) { _error = "LateralModeController: FrontalAnalyzer or Annotatable Screen not found"; Debug.LogError(_error); yield break; }

            // wait until the sample's camera is running (the face runner starts it)
            yield return new WaitUntil(() => ImageSourceProvider.ImageSource != null && ImageSourceProvider.ImageSource.isPrepared);
            yield return AssetLoader.PrepareAssetAsync(segmentationModel);
            try
            {
                _seg = ImageSegmenter.CreateFromOptions(new ImageSegmenterOptions(
                    new BaseOptions(BaseOptions.Delegate.CPU, modelAssetPath: segmentationModel),
                    runningMode: RunningMode.VIDEO, outputConfidenceMasks: true, outputCategoryMask: false));
            }
            catch (System.Exception e)
            {
                _error = $"Lateral model failed to load ({segmentationModel}): {e.Message}";
                Debug.LogError(_error);
                yield break;
            }
            _result = ImageSegmenterResult.Alloc(true);

            // tracing overlay, stretched over the camera image
            var ov = new GameObject("LateralProfileOverlay", typeof(RectTransform));
            var rt = ov.GetComponent<RectTransform>();
            rt.SetParent(target, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _overlay = ov.AddComponent<SideProfileOverlay>();
            _overlay.raycastTarget = false;
            ov.SetActive(false);

            _ctl = gameObject.AddComponent<SideProfileController>();
            _ctl.overlay = _overlay;

            yield return Loop();
        }

        void OnDestroy()
        {
            _seg?.Close();
            _pool?.Dispose();
        }

        IEnumerator Loop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            var opts = new ImageProcessingOptions(rotationDegrees: 0);
            long lastTs = -1;
            while (true)
            {
                yield return endOfFrame;
                UpdateMode();
                if (!IsLateral || (frontal != null && frontal.frozen)) continue;

                var src = ImageSourceProvider.ImageSource;
                var tex = src.GetCurrentTexture();
                if (tex == null) continue;
                if (_pool == null) _pool = new Mediapipe.Unity.Experimental.TextureFramePool(src.textureWidth, src.textureHeight, TextureFormat.RGBA32, 4);
                if (!_pool.TryGetTextureFrame(out var frame)) continue;

                var t = src.GetTransformationOptions();             // same flips as the face runner
                frame.ReadTextureOnCPU(tex, t.flipHorizontally, t.flipVertically);
                var image = frame.BuildCPUImage();
                frame.Release();

                int pw = image.Width(), ph = image.Height();
                if (_pixels == null || _pixels.Length != pw * ph) _pixels = new Color32[pw * ph];
                bool havePixels = image.TryReadPixelData(_pixels);   // read before the task consumes the image

                long ts = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
                if (ts <= lastTs) ts = lastTs + 1;
                lastTs = ts;
                if (!_seg.TrySegmentForVideo(image, ts, opts, ref _result)) continue;

                var masks = _result.confidenceMasks;
                if (masks == null || masks.Count < 4) { _error = "Wrong model: use selfie_multiclass_256x256"; continue; }
                int w = masks[0].Width(), h = masks[0].Height();
                if (_bg == null || _bg.Length != w * h) { _bg = new float[w * h]; _face = new float[w * h]; }
                if (!masks[0].TryReadChannelNormalized(0, _bg) || !masks[3].TryReadChannelNormalized(0, _face)) continue;
                _ctl.SubmitFrame(_bg, _face, w, h, havePixels ? _pixels : null, pw, ph, pw);
            }
        }

        void UpdateMode()
        {
            bool frontalOk = frontal.hasFace && Mathf.Abs(frontal.yawDeg) <= frontalMaxYaw;
            if (frontalOk && frontal.pxPerMm > 0f && frontal.imageSize.x > 0f) { _pxPerMm = frontal.pxPerMm; _rectW = frontal.imageSize.x; }

            bool lateral;
            if (mode == ModeSelect.Frontal) lateral = false;
            else if (mode == ModeSelect.Lateral) lateral = true;
            else if (frontal.frozen) lateral = false;
            else if (frontalOk) { _noFaceSince = -1f; lateral = false; }
            else
            {
                if (_noFaceSince < 0f) _noFaceSince = Time.unscaledTime;
                lateral = Time.unscaledTime - _noFaceSince >= lateralDelaySec;
            }

            IsLateral = lateral;
            if (frontalOverlay) frontalOverlay.suspended = lateral;
            if (_overlay)
            {
                _overlay.gameObject.SetActive(lateral);
                _overlay.mirrorX = frontalOverlay != null && frontalOverlay.mirrorDisplay;
                _overlay.transform.SetAsLastSibling();
            }
            if (_ctl)
            {
                _ctl.masksBottomUp = masksBottomUp;
                var src = ImageSourceProvider.ImageSource;
                _ctl.mmPerPixel = (_pxPerMm > 0f && src != null && src.textureWidth > 0) ? (_rectW / src.textureWidth) / _pxPerMm : 0f;
            }
        }

        // ---------------------------------------------------------------- lip profile

        int LipCode(float ls, float li)
        {
            if (ls > lsProtrusiveAbove || li > liProtrusiveAbove) return 3;
            if (ls < lsRetrusiveBelow && li < liRetrusiveBelow) return 1;
            return 2;
        }

        static readonly string[] LipNames = { "unknown", "retrusive", "normal", "protrusive" };

        void Capture()
        {
            var r = _ctl != null ? _ctl.Latest : null;
            if (r == null || !r.Valid) return;
            _captured = r;
            _capMm = _ctl.mmPerPixel > 0f;
            _capLs = r.UpperLipToELinePx * _ctl.mmPerPixel;
            _capLi = r.LowerLipToELinePx * _ctl.mmPerPixel;
            if (_capMm && frontalOverlay) frontalOverlay.SetLipProfile(LipCode(_capLs, _capLi));
        }

        // ---------------------------------------------------------------- GUI (same look as FrontalOverlay)

        void Styles()
        {
            if (_small != null) return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _ana = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, fontStyle = FontStyle.Bold };
        }

        void Line(ref float y, float x, float w, string text, Color c, GUIStyle st)
        {
            GUI.color = c;
            float h = st.CalcHeight(new GUIContent(text), w);
            GUI.Label(new Rect(x, y, w, h), text, st);
            y += h + 3f;
        }

        void OnGUI()
        {
            Styles();
            float uiScale = frontalOverlay ? frontalOverlay.uiScale : 1.2f;
            float s = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 540f) * uiScale;
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float vw = Screen.width / s, vh = Screen.height / s;

            // mode button, always visible (bottom centre)
            GUI.color = Color.white;
            if (GUI.Button(new Rect(vw * 0.5f - 80f, vh - 36f, 160f, 30f), "View: " + (mode == ModeSelect.Auto ? (IsLateral ? "Auto (lateral)" : "Auto (frontal)") : mode.ToString())))
                mode = (ModeSelect)(((int)mode + 1) % 3);

            if (IsLateral) DrawPanel(new Rect(6f, 6f, vw * (frontalOverlay ? frontalOverlay.panelWidthFraction : 0.36f), vh - 50f));
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        void DrawPanel(Rect r)
        {
            GUI.color = new Color(0f, 0f, 0f, frontalOverlay ? frontalOverlay.panelOpacity : 0.65f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            float x = r.x + 10f, w = r.width - 20f, y = r.y + 6f;

            Line(ref y, x, w, "LATERAL VIEW (90°): measured silhouette", Yellow, _title);
            if (!string.IsNullOrEmpty(_error)) { Line(ref y, x, w, _error, Warn, _ana); return; }

            var res = _ctl != null ? _ctl.Latest : null;
            if (res == null || !res.Valid)
            {
                Line(ref y, x, w, res?.Warning ?? "Turn the patient 90° to the camera. Use a plain background, and keep the chin and neck line uncovered.", Warn, _ana);
            }
            else
            {
                bool mm = _ctl.mmPerPixel > 0f;
                Value(ref y, x, w, "Nasolabial", $"{res.NasolabialDeg:0}°", res.NasolabialDeg >= 90f && res.NasolabialDeg <= 110f,
                    res.NasolabialDeg < 90f ? "Acute: often proclined upper incisors / dentoalveolar protrusion"
                    : res.NasolabialDeg > 110f ? "Obtuse: often retroclined upper incisors / maxillary retrusion" : "Within 90–110°");
                Value(ref y, x, w, "Mentolabial", $"{res.MentolabialDeg:0}°", res.MentolabialDeg >= 110f && res.MentolabialDeg <= 140f,
                    res.MentolabialDeg < 110f ? "Deep sulcus: often lower-lip eversion (e.g. Class II)"
                    : res.MentolabialDeg > 140f ? "Flat sulcus: often lip strain / protrusive lower lip" : "Within 110–140°");
                if (mm)
                {
                    float ls = res.UpperLipToELinePx * _ctl.mmPerPixel, li = res.LowerLipToELinePx * _ctl.mmPerPixel;
                    int code = LipCode(ls, li);
                    Value(ref y, x, w, "Ls / Li → E-line", $"{ls:+0.0;-0.0} / {li:+0.0;-0.0} mm", code == 2, "Lip profile: " + LipNames[code].ToUpper());
                    Line(ref y, x, w, "mm scale taken from the last frontal frame (same standard distance): approximate.", Gray, _small);
                }
                else
                {
                    Value(ref y, x, w, "Ls / Li → E-line", $"{res.UpperLipToELinePct:+0.0;-0.0} / {res.LowerLipToELinePct:+0.0;-0.0} %", true,
                        "No mm scale yet: look at the camera once (frontal) so the scale is known, then turn 90° again.");
                }
            }

            // buttons
            y += 4f;
            GUI.color = Color.white;
            float bw = (w - 6f) / 3f;
            if (GUI.Button(new Rect(x, y, bw, 28f), "Capture")) Capture();
            if (GUI.Button(new Rect(x + bw + 3f, y, bw, 28f), masksBottomUp ? "Flip V: on" : "Flip V: off")) masksBottomUp = !masksBottomUp;
            if (GUI.Button(new Rect(x + 2f * (bw + 3f), y, bw, 28f), "Clear")) _captured = null;
            y += 34f;

            if (_captured != null)
            {
                Line(ref y, x, w, "CAPTURED", Yellow, _title);
                Line(ref y, x, w, $"Nasolabial {_captured.NasolabialDeg:0}°   Mentolabial {_captured.MentolabialDeg:0}°", Gray, _small);
                Line(ref y, x, w, _capMm ? $"Ls {_capLs:+0.0;-0.0} mm   Li {_capLi:+0.0;-0.0} mm  →  lips {LipNames[LipCode(_capLs, _capLi)]} (sent to the Ext tab)"
                                         : $"Ls {_captured.UpperLipToELinePct:+0.0;-0.0}%   Li {_captured.LowerLipToELinePct:+0.0;-0.0}% (no mm: not sent to Ext)", Gray, _small);
            }
            Line(ref y, x, w, "Decision support only: confirm with the lateral ceph.", Gray, _small);
        }

        void Value(ref float y, float x, float w, string name, string val, bool ok, string analysis)
        {
            GUI.color = Gray;
            GUI.Label(new Rect(x, y + 4f, w * 0.5f, 22f), name, _small);
            GUI.color = ok ? Ok : Warn;
            GUI.Label(new Rect(x + w * 0.45f, y, w * 0.55f, 28f), val, _big);
            y += 28f;
            Line(ref y, x, w, analysis, Yellow, _ana);
        }
    }
}
