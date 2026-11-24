using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Thread-safe entry point. Any face tracker (MediaPipe, ARKit, ...) calls
    /// <see cref="SubmitLandmarks"/> from any thread; analysis + UI run on the main thread.
    /// </summary>
    public class ProfileAnalysisController : MonoBehaviour
    {
        [Header("Output")]
        public ProfileGraph graph;
        public Text readout;                     // swap for TMP_Text if you use TextMeshPro

        [Header("Smoothing (One-Euro)")]
        public bool smooth = true;
        public float minCutoff = 1.0f;
        public float beta = 0.05f;

        public ProfileResult Latest { get; private set; }

        readonly SoftTissueProfileAnalyzer _analyzer = new SoftTissueProfileAnalyzer();
        ProfileSmoother _smoother;
        readonly object _lock = new object();
        Vector3[] _pending;
        int _w, _h;
        bool _hasPending, _faceLost;
        float _lastTime;

        void Awake() => _smoother = new ProfileSmoother(minCutoff, beta);

        /// <summary>Call from the tracker callback (worker thread OK). Pass null when no face.</summary>
        public void SubmitLandmarks(IReadOnlyList<Vector3> normalized, int imageWidth, int imageHeight)
        {
            lock (_lock)
            {
                if (normalized == null) { _faceLost = true; _hasPending = false; return; }
                if (_pending == null || _pending.Length != normalized.Count) _pending = new Vector3[normalized.Count];
                for (int i = 0; i < normalized.Count; i++) _pending[i] = normalized[i];
                _w = imageWidth; _h = imageHeight;
                _hasPending = true; _faceLost = false;
            }
        }

        void Update()
        {
            Vector3[] lm; int w, h;
            lock (_lock)
            {
                if (_faceLost) { _faceLost = false; _smoother.Reset(); Show(null); return; }
                if (!_hasPending) return;
                lm = (Vector3[])_pending.Clone(); w = _w; h = _h;
                _hasPending = false;
            }

            var r = _analyzer.Analyze(lm, w, h);
            float now = Time.unscaledTime;
            if (smooth) _smoother.Apply(r, _lastTime > 0f ? now - _lastTime : 0f);
            _lastTime = now;
            Show(r);
        }

        void Show(ProfileResult r)
        {
            Latest = r;
            if (graph) graph.SetResult(r);
            if (!readout) return;
            if (r == null || !r.Valid) { readout.text = r?.Warning ?? "No face detected"; return; }

            string unit = r.MmPerUnit > 0f ? "mm" : "u";
            var sb = new StringBuilder();
            sb.AppendLine(Row("Nasolabial",   r.NasolabialDeg,      "°", 90f, 110f));
            sb.AppendLine(Row("Mentolabial",  r.MentolabialDeg,     "°", 110f, 140f));
            sb.AppendLine(Row("Convexity G'-Sn-Pg'", r.FacialConvexityDeg, "°", 8f, 16f));
            sb.AppendLine(Row("Ls → E-line",  r.UpperLipToELineMm, unit, -6f, -2f));
            sb.AppendLine(Row("Li → E-line",  r.LowerLipToELineMm, unit, -4f, 0f));
            sb.Append($"<size=80%>yaw {r.HeadYawDeg:0}°  pitch {r.HeadPitchDeg:0}°</size>");
            if (!string.IsNullOrEmpty(r.Warning)) sb.Append($"\n<color=#ffa500>{r.Warning}</color>");
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
