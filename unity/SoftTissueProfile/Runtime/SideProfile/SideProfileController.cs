using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Thread-safe entry point for live lateral analysis. A segmenter (any thread) calls
    /// <see cref="SubmitFrame"/>; the landmark analysis runs on a WORKER thread so the
    /// camera and UI stay smooth; smoothing and drawing happen on the main thread.
    /// </summary>
    public class SideProfileController : MonoBehaviour
    {
        [Header("Output")]
        public SideProfileOverlay overlay;
        public Text readout;                    // optional

        [Header("Input orientation")]
        [Tooltip("Tick if the masks arrive bottom-row-first (the tracing would otherwise be upside down).")]
        public bool masksBottomUp = true;

        [Header("Calibration")]
        [Tooltip("Millimetres per source-image pixel. 0 = show % of E-line length.")]
        public float mmPerPixel;

        [Header("Analysis")]
        [Range(256, 1024)] public int maxWorkSize = 640;
        public bool smooth = true;
        public float minCutoff = 1.0f, beta = 0.05f;

        public SideProfileResult Latest { get; private set; }
        /// <summary>Time the last analysis took on the worker thread (ms).</summary>
        public float AnalysisMs { get; private set; }

        readonly SideProfileAnalyzer _analyzer = new SideProfileAnalyzer();
        readonly object _lock = new object();
        float[] _bg, _face;                     // pending (written by the caller)
        Color32[] _pixels;
        int _w, _h, _pw, _ph, _imageWidth;
        bool _pending, _lost, _pendingHasPixels;

        float[] _person, _faceTD, _grayTD;      // job inputs (top-down), only touched while no job runs
        bool _haveGray;
        int _grayOrient = -1;
        Task<SideProfileResult> _job;
        OneEuroFilter[] _f;
        float _lastTime;

        /// <param name="backgroundConf">confidence mask 0 of selfie_multiclass (background)</param>
        /// <param name="faceConf">confidence mask 3 of selfie_multiclass (face skin)</param>
        /// <param name="pixels">optional camera pixels; pass null to keep the previous gray image</param>
        public void SubmitFrame(float[] backgroundConf, float[] faceConf, int w, int h,
                                Color32[] pixels, int pw, int ph, int imageWidth)
        {
            lock (_lock)
            {
                if (backgroundConf == null) { _lost = true; _pending = false; return; }
                Copy(ref _bg, backgroundConf); Copy(ref _face, faceConf);
                _pendingHasPixels = pixels != null && pw > 0 && pixels.Length >= pw * ph;
                if (_pendingHasPixels)
                {
                    if (_pixels == null || _pixels.Length != pixels.Length) _pixels = new Color32[pixels.Length];
                    System.Array.Copy(pixels, _pixels, pixels.Length);
                    _pw = pw; _ph = ph;
                }
                _w = w; _h = h; _imageWidth = imageWidth;
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
            // 1) collect a finished job
            if (_job != null)
            {
                if (!_job.IsCompleted) return;              // still working: keep showing the last result
                SideProfileResult r = null;
                if (_job.Status == TaskStatus.RanToCompletion) r = _job.Result;
                else if (_job.Exception != null) UnityEngine.Debug.LogException(_job.Exception);
                _job = null;
                if (r != null)
                {
                    float now = Time.unscaledTime;
                    if (smooth && r.Valid) Smooth(r, _lastTime > 0 ? now - _lastTime : 0f);
                    _lastTime = now;
                    Show(r);
                }
            }

            // 2) start a new job with the newest frame
            int w, h, iw;
            lock (_lock)
            {
                if (_lost) { _lost = false; Show(null); return; }
                if (!_pending) return;
                _pending = false;
                w = _w; h = _h; iw = _imageWidth;
                int n = w * h;
                if (_person == null || _person.Length != n) { _person = new float[n]; _faceTD = new float[n]; _grayTD = new float[n]; _haveGray = false; _grayOrient = -1; }
                for (int y = 0; y < h; y++)
                {
                    int sy = masksBottomUp ? h - 1 - y : y;
                    int s0 = sy * w, d0 = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        _person[d0 + x] = 1f - _bg[s0 + x];
                        _faceTD[d0 + x] = _face[s0 + x];
                    }
                }
                if (_pendingHasPixels)
                {
                    if (_grayOrient < 0) _grayOrient = BestOrientation(w, h, _pw, _ph);   // once; cached afterwards
                    SampleGray(_grayOrient, w, h, _pw, _ph, _grayTD);
                    _haveGray = true;
                }
            }

            _analyzer.MaxWorkSize = maxWorkSize;
            float[] person = _person, face = _faceTD, gray = _haveGray ? _grayTD : null;
            _job = Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var res = _analyzer.Analyze(person, face, gray, w, h, iw);
                AnalysisMs = (float)sw.Elapsed.TotalMilliseconds;
                return res;
            });
        }

        void OnDisable() { _job = null; }

        // ---- camera pixels → gray at mask resolution, orientation auto-detected once ----
        void SampleGray(int orient, int w, int h, int pw, int ph, float[] dst)
        {
            bool fx = (orient & 1) != 0, fy = (orient & 2) != 0;
            for (int y = 0; y < h; y++)
            {
                int py = (int)((y + 0.5f) * ph / h); if (fy) py = ph - 1 - py;
                int row = py * pw;
                for (int x = 0; x < w; x++)
                {
                    int px = (int)((x + 0.5f) * pw / w); if (fx) px = pw - 1 - px;
                    var c = _pixels[row + px];
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
                double bgSum = 0; int bgN = 0, fgN = 0;
                for (int i = 0; i < tmp.Length; i += 3)
                {
                    if (_person[i] > 0.5f) fgN++; else { bgSum += tmp[i]; bgN++; }
                }
                if (bgN == 0 || fgN == 0) continue;
                double bgMean = bgSum / bgN, bgVar = 0, fgDiff = 0;
                for (int i = 0; i < tmp.Length; i += 3)
                {
                    double d = tmp[i] - bgMean;
                    if (_person[i] > 0.5f) fgDiff += System.Math.Abs(d); else bgVar += System.Math.Abs(d);
                }
                float score = (float)(fgDiff / fgN - bgVar / bgN);
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
            Vector2 off = r.Prn - prn;
            for (int i = 0; i < r.Profile.Length; i++) r.Profile[i] += off;
            r.NasolabialDeg      = _f[k++].Filter(r.NasolabialDeg, dt);
            r.MentolabialDeg     = _f[k++].Filter(r.MentolabialDeg, dt);
            r.UpperLipToELinePct = _f[k++].Filter(r.UpperLipToELinePct, dt);
            r.LowerLipToELinePct = _f[k++].Filter(r.LowerLipToELinePct, dt);
        }

        // ---- readout (optional) -----------------------------------------------------
        void Show(SideProfileResult r)
        {
            Latest = r;
            if (overlay) overlay.SetResult(r);
            if (!readout) return;
            if (r == null || !r.Valid) { readout.text = r?.Warning ?? "No patient detected: turn the patient 90° to the camera"; return; }

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
