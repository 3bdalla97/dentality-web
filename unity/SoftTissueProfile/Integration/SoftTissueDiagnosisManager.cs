using System.Collections.Generic;
using System.Text;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.ImageSegmenter;
using UnityEngine;
using UnityEngine.UI;
using MpImage = Mediapipe.Image;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// تتبع الأنسجة الرخوة لتشخيص تقويم الأسنان: soft-tissue tracking for orthodontic diagnosis.
    ///
    /// Switches between the frontal mode (face mesh, profile ESTIMATED from depth) and the
    /// lateral mode (silhouette, profile MEASURED), based on head yaw. "Capture" stores a
    /// snapshot of each view and builds a combined screening summary.
    /// </summary>
    public class SoftTissueDiagnosisManager : MonoBehaviour
    {
        public enum Mode { None, Frontal, Lateral }

        [Header("Pipelines")]
        public FaceLandmarkerProfileBridge frontalBridge;    // → ProfileAnalysisController
        public ProfileAnalysisController frontal;
        public SideProfileSegmenterBridge lateralBridge;     // → SideProfileController
        public SideProfileController lateral;

        [Header("UI")]
        public GameObject frontalUI;                         // ProfileGraph + frontal readout
        public GameObject lateralUI;                         // SideProfileOverlay + lateral readout
        public Text modeLabel;
        public Text summary;
        public Button captureButton;

        [Header("Mode switching")]
        [Tooltip("|yaw| below this = frontal")]
        public float frontalMaxYaw = 30f;
        [Tooltip("Consecutive frames before switching")]
        public int switchFrames = 8;

        public Mode CurrentMode { get; private set; } = Mode.None;
        /// <summary>The runner skips segmentation while the patient faces the camera.</summary>
        public bool NeedsSegmentation => CurrentMode != Mode.Frontal || _candidate != Mode.Frontal;

        ProfileResult _frontalSnap;
        SideProfileResult _lateralSnap;
        Mode _candidate = Mode.None;
        int _votes;
        readonly SoftTissueProfileAnalyzer _yawProbe = new SoftTissueProfileAnalyzer();
        readonly List<Vector3> _lm = new List<Vector3>(478);

        void Awake()
        {
            if (captureButton) captureButton.onClick.AddListener(Capture);
            Apply(Mode.None);
        }

        // ---- called by SoftTissueRunner (main thread, VIDEO mode) -------------------
        public void OnFace(FaceLandmarkerResult result, bool gotFace, int width, int height)
        {
            Mode vote = Mode.Lateral;
            if (gotFace)
            {
                var lms = result.faceLandmarks[0].landmarks;
                _lm.Clear();
                for (int i = 0; i < lms.Count; i++) _lm.Add(new Vector3(lms[i].x, lms[i].y, lms[i].z));
                var probe = _yawProbe.Analyze(_lm, width, height);
                if (probe.Valid && Mathf.Abs(probe.HeadYawDeg) <= frontalMaxYaw) vote = Mode.Frontal;
            }
            Vote(vote);
            if (CurrentMode == Mode.Frontal && frontalBridge) frontalBridge.OnResult(result, width, height);
        }

        public void OnSegmentation(ImageSegmenterResult result, MpImage image)
        {
            if (CurrentMode == Mode.Lateral && lateralBridge) lateralBridge.OnResult(result, image);
        }

        void Vote(Mode m)
        {
            if (m == CurrentMode) { _candidate = m; _votes = 0; return; }
            if (m != _candidate) { _candidate = m; _votes = 0; }
            if (++_votes >= switchFrames) { _votes = 0; Apply(m); }
        }

        void Apply(Mode m)
        {
            CurrentMode = m;
            if (frontalUI) frontalUI.SetActive(m == Mode.Frontal);
            if (lateralUI) lateralUI.SetActive(m == Mode.Lateral);
            if (modeLabel)
                modeLabel.text = m == Mode.Frontal ? "Frontal view: profile is ESTIMATED from depth"
                               : m == Mode.Lateral ? "Lateral view (90°): profile is MEASURED"
                               : "Face the camera, or turn 90° for the lateral view";
        }

        // ---- capture + combined summary --------------------------------------------
        public void Capture()
        {
            if (CurrentMode == Mode.Frontal && frontal && frontal.Latest != null && frontal.Latest.Valid)
                _frontalSnap = frontal.Latest;
            if (CurrentMode == Mode.Lateral && lateral && lateral.Latest != null && lateral.Latest.Valid)
                _lateralSnap = lateral.Latest;
            if (summary) summary.text = BuildSummary();
        }

        public void ResetPatient() { _frontalSnap = null; _lateralSnap = null; if (summary) summary.text = ""; }

        string BuildSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>Soft-tissue screening</b> <size=80%>(for clinician review, not a diagnosis)</size>");
            if (_frontalSnap == null && _lateralSnap == null) { sb.Append("Capture a frontal and/or a lateral view."); return sb.ToString(); }

            // Prefer lateral (measured) values; fall back to frontal (estimated).
            bool lat = _lateralSnap != null;
            float nl = lat ? _lateralSnap.NasolabialDeg : _frontalSnap.NasolabialDeg;
            float ml = lat ? _lateralSnap.MentolabialDeg : _frontalSnap.MentolabialDeg;
            string src = lat ? "lateral, measured" : "frontal, estimated";

            sb.AppendLine($"Nasolabial {nl:0}° <size=80%>[{src}]</size>: " +
                (nl < 90 ? "acute: often proclined upper incisors / dentoalveolar protrusion"
                : nl > 110 ? "obtuse: often retroclined upper incisors / maxillary retrusion"
                : "within 90–110°"));
            sb.AppendLine($"Mentolabial {ml:0}° <size=80%>[{src}]</size>: " +
                (ml < 110 ? "deep sulcus: often lower-lip eversion (e.g. Class II)"
                : ml > 140 ? "flat sulcus: often lip strain / protrusive lower lip"
                : "within 110–140°"));

            // E-line in mm: lateral if calibrated, else frontal (iris-scaled, estimated).
            bool latMm = lat && lateral && lateral.mmPerPixel > 0f;
            if (latMm || (_frontalSnap != null && _frontalSnap.MmPerUnit > 0f))
            {
                float ls = latMm ? _lateralSnap.UpperLipToELinePx * lateral.mmPerPixel : _frontalSnap.UpperLipToELineMm;
                float li = latMm ? _lateralSnap.LowerLipToELinePx * lateral.mmPerPixel : _frontalSnap.LowerLipToELineMm;
                string s = latMm ? "lateral, measured" : "frontal, estimated";
                sb.AppendLine($"Ls→E {ls:+0.0;-0.0} mm, Li→E {li:+0.0;-0.0} mm <size=80%>[{s}]</size>: " +
                    (ls > -2 && li > 0 ? "lips protrusive relative to the E-line (bimaxillary protrusion?)"
                    : ls < -6 && li < -4 ? "lips retrusive relative to the E-line"
                    : "close to Ricketts norms"));
            }
            else if (lat)
                sb.AppendLine($"Ls→E {_lateralSnap.UpperLipToELinePct:+0.0;-0.0}%, Li→E {_lateralSnap.LowerLipToELinePct:+0.0;-0.0}% of E-line <size=80%>(set mm/px for norms)</size>");

            if (_frontalSnap != null)
            {
                float cv = _frontalSnap.FacialConvexityDeg;
                sb.AppendLine($"Facial convexity G'-Sn-Pg' {cv:0}° <size=80%>[frontal, estimated]</size>: " +
                    (cv > 16 ? "convex: Class II skeletal tendency" : cv < 8 ? "straight/concave: Class III skeletal tendency" : "within 12 ± 4°"));
            }
            sb.Append($"<size=80%>Views: {(_frontalSnap != null ? "frontal ✓" : "frontal –")}  {(lat ? "lateral ✓" : "lateral –")}</size>");
            return sb.ToString();
        }
    }
}
