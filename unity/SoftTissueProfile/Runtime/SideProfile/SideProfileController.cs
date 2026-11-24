using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Thread-safe entry point for live lateral analysis. A segmenter callback (any thread)
    /// calls <see cref="SubmitFrame"/>; analysis, smoothing and UI run in Update.
    /// </summary>
    public class SideProfileController : MonoBehaviour
    {
        [Header("Output")]
        public SideProfileOverlay overlay;
        public Text readout;

        [Header("Input orientation")]
        [Tooltip("Tick if the masks arrive bottom-row-first (the tracing would otherwise be upside down).")]
        public bool masksBottomUp;

        [Header("Calibration")]
        [Tooltip("Millimetres per source-image pixel (from a ruler in the shot). 0 = show % of E-line length.")]
        public float mmPerPixel;

        [Header("Analysis")]
        [Range(256, 1024)] public int maxWorkSize = 768;
        public bool smooth = true;
        public float minCutoff = 1.0f, beta = 0.05f;

        public SideProfileResult Latest { get; private set; }

        readonly SideProfileAnalyzer _analyzer = new SideProfileAnalyzer();
        readonly object _lock = new object();
        float[] _bg, _face, _gray;              // pending (written by the callback thread)
        Color32[] _pixels;
        int _w, _h, _pw, _ph, _imageWidth;
        bool _pending, _lost;

        float[] _person, _faceTD, _grayTD;      // main-thread work buffers (top-down)
        int _grayOrient = -1, _framesSinceOrient;
        OneEuroFilter[] _f;
        float _lastTime;

        /// <param name="backgroundConf">confidence mask 0 of selfie_multiclass (background)</param>
        /// <param name="faceConf">confidence mask 3 of selfie_multiclass (face skin)</param>
        /// <param name="pixels">optional camera pixels (any size/orientation; auto-aligned to the masks)</param>
        public void SubmitFrame(float[] backgroundConf, float[] faceConf, int w, int h,
                                Color32[] pixels, int pw, int ph, int imageWidth)
        {
            lock (_lock)
            {
                if (backgroundConf == null) { _lost = true; _pending = false; return; }
                Copy(ref _bg, backgroundConf); Copy(ref _face, faceConf);
                if (pixels != null) { if (_pixels == null || _pixels.Length != pixels.Length) _pixels = new Color32[pixels.Length]; System.Array.Copy(pixels, _pixels, pixels.Length); }
                else _pixels = null;
                _w = w; _h = h; _pw = pw; _ph = ph; _imageWidth = imageWidth;
                _pending = true; _lost = false;
            }
        }

        static void Copy(ref float[] dst, float[] src)
        {
            if (dst == null || dst.Length != src.Length) dst = new float[src.Length];
            System.Array.Copy(src, dst, src.Length);
        }

        void Update()
        {
            int w, h, pw, ph, iw; bool hasPixels;
            lock (_lock)
            {
                if (_lost) { _lost = false; Show(null); return; }
                if (!_pending) return;
                _pending = false;
                w = _w; h = _h; pw = _pw; ph = _ph; iw = _imageWidth;
                int n = w * h;
                if (_person == null || _person.Length != n) { _person = new float[n]; _faceTD = new float[n]; _grayTD = new float[n]; }
                for (int y = 0; y < h; y++)
                {
                    int sy = masksBottomUp ? h - 1 - y : y;
                    for (int x = 0; x < w; x++)
                    {
                        int s = sy * w + x, d = y * w + x;
                        _person[d] = 1f - _bg[s];
                        _faceTD[d] = _face[s];
                    }
                }
                hasPixels = _pixels != null && _pixels.Length >= pw * ph && pw > 0;
                if (hasPixels) FillGray(w, h, pw, ph);
            }

            _analyzer.MaxWorkSize = maxWorkSize;
            var r = _analyzer.Analyze(_person, _faceTD, hasPixels ? _grayTD : null, w, h, iw);
            float now = Time.unscaledTime;
            if (smooth && r.Valid) Smooth(r, _lastTime > 0 ? now - _lastTime : 0f);
            _lastTime = now;
            Show(r);
        }

        // ---- camera pixels → gray at mask resolution, orientation auto-detected ----
        void FillGray(int w, int h, int pw, int ph)
        {
            if (_grayOrient < 0 || ++_framesSinceOrient > 90)
            {
                _grayOrient = BestOrientation(w, h, pw, ph);
                _framesSinceOrient = 0;
            }
            SampleGray(_grayOrient, w, h, pw, ph, _grayTD);
        }

        void SampleGray(int orient, int w, int h, int pw, int ph, float[] dst)
        {
            bool fx = (orient & 1) != 0, fy = (orient & 2) != 0;
            for (int y = 0; y < h; y++)
            {
                int py = (int)((y + 0.5f) * ph / h); if (fy) py = ph - 1 - py;
                for (int x = 0; x < w; x++)
                {
                    int px = (int)((x + 0.5f) * pw / w); if (fx) px = pw - 1 - px;
                    var c = _pixels[py * pw + px];
                    dst[y * w + x] = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                }
            }
        }

        /// <summary>Pick the flip that best matches the person mask (person differs most from the background).</summary>
        int BestOrientation(int w, int h, int pw, int ph)
        {
            int best = 0; float bestScore = float.MinValue;
            var tmp = new float[w * h];
            for (int o = 0; o < 4; o++)
            {
                SampleGray(o, w, h, pw, ph, tmp);
                // background luminance = mean over background pixels; score = contrast between classes
                double bgSum = 0, fgSum = 0; int bgN = 0, fgN = 0;
                for (int i = 0; i < tmp.Length; i += 3)
                {
                    if (_person[i] > 0.5f) { fgSum += tmp[i]; fgN++; } else { bgSum += tmp[i]; bgN++; }
                }
                if (bgN == 0 || fgN == 0) continue;
                double bgMean = bgSum / bgN, bgVar = 0, fgDiff = 0;
                for (int i = 0; i < tmp.Length; i += 3)
                {
                    double d = tmp[i] - bgMean;
                    if (_person[i] > 0.5f) fgDiff += System.Math.Abs(d); else bgVar += System.Math.Abs(d);
                }
                float score = (float)(fgDiff / fgN - bgVar / bgN);   // uniform background, different person
                if (score > bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        // ---- smoothing --------------------------------------------------------------
        void Smooth(SideProfileResult r, float dt)
        {
            const int pts = 8, vals = 4;
            if (_f == null) { _f = new OneEuroFilter[pts * 2 + vals]; for (int i = 0; i < _f.Length; i++) _f[i] = new OneEuroFilter(minCutoff, beta); }
            int k = 0;
            Vector2 F(Vector2 v) => new Vector2(_f[k++].Filter(v.x, dt), _f[k++].Filter(v.y, dt));
            Vector2 prn = r.Prn;
            r.Prn = F(r.Prn); r.Cm = F(r.Cm); r.Sn = F(r.Sn); r.Ls = F(r.Ls);
            r.St = F(r.St); r.Li = F(r.Li); r.Bp = F(r.Bp); r.Pg = F(r.Pg);
            // shift the silhouette with the smoothed Prn so the line and dots stay together
            Vector2 off = r.Prn - prn;
            for (int i = 0; i < r.Profile.Length; i++) r.Profile[i] += off;
            r.NasolabialDeg      = _f[k++].Filter(r.NasolabialDeg, dt);
            r.MentolabialDeg     = _f[k++].Filter(r.MentolabialDeg, dt);
            r.UpperLipToELinePct = _f[k++].Filter(r.UpperLipToELinePct, dt);
            r.LowerLipToELinePct = _f[k++].Filter(r.LowerLipToELinePct, dt);
        }

        // ---- readout ----------------------------------------------------------------
        void Show(SideProfileResult r)
        {
            Latest = r;
            if (overlay) overlay.SetResult(r);
            if (!readout) return;
            if (r == null || !r.Valid) { readout.text = r?.Warning ?? "No patient detected — turn the patient 90° to the camera"; return; }

            var sb = new StringBuilder();
            sb.AppendLine(Row("Nasolabial", r.NasolabialDeg, "°", 90f, 110f));
            sb.AppendLine(Row("Mentolabial", r.MentolabialDeg, "°", 110f, 140f));
            if (mmPerPixel > 0f)
            {
                sb.AppendLine(Row("Ls → E-line", r.UpperLipToELinePx * mmPerPixel, " mm", -6f, -2f));
                sb.AppendLine(Row("Li → E-line", r.LowerLipToELinePx * mmPerPixel, " mm", -4f, 0f));
            }
            else
            {
                sb.AppendLine($"Ls → E-line: {r.UpperLipToELinePct:+0.0;-0.0}% of E-line");
                sb.AppendLine($"Li → E-line: {r.LowerLipToELinePct:+0.0;-0.0}% of E-line");
                sb.Append("<size=80%>(set mm/px for millimetres)</size>");
            }
            readout.supportRichText = true;
            readout.text = sb.ToString();
        }

        static string Row(string name, float v, string unit, float lo, float hi)
        {
            string col = v < lo || v > hi ? "#ff6060" : "#60ff60";
            return $"{name}: <color={col}>{v:0.0}{unit}</color> <size=80%>({lo:0}–{hi:0})</size>";
        }
    }
}
