using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Result of one frame of analysis. All 2D points are in the head's midsagittal
    /// plane, in millimetres, origin at soft-tissue nasion:
    ///   x = anterior (towards the patient's face direction), y = superior.
    /// So plotting (x, y) draws the patient facing RIGHT, like a lateral photo.
    /// </summary>
    public sealed class ProfileResult
    {
        public bool Valid;
        public string Warning;

        public Vector2[] Profile;                 // midline silhouette, forehead -> menton
        public Vector2 G, Prn, Cm, Sn, Ls, Li, Bp, Pg;

        public float NasolabialDeg;               // Cm-Sn-Ls
        public float MentolabialDeg;              // Li-B'-Pg'
        public float FacialConvexityDeg;          // G'-Sn-Pg' (positive = convex)
        public float UpperLipToELineMm;           // Ricketts, negative = behind E-line
        public float LowerLipToELineMm;

        public float HeadYawDeg, HeadPitchDeg, HeadRollDeg;
        public float MmPerUnit;                   // scale derived from iris (0 if unavailable)
    }

    /// <summary>
    /// Pure-C# soft-tissue profile analysis from MediaPipe face landmarks.
    /// Engine-agnostic apart from Vector2/Vector3, so it can be unit-tested in EditMode.
    /// </summary>
    public sealed class SoftTissueProfileAnalyzer
    {
        /// <summary>Above this yaw the reconstructed depth gets unreliable.</summary>
        public float MaxYawDeg = 35f;

        readonly Vector3[] _p = new Vector3[478];

        /// <param name="normalized">MediaPipe NormalizedLandmarks (x,y in [0,1], z relative depth).</param>
        /// <param name="imageWidth">Width of the image the landmarks were computed on (pixels).</param>
        /// <param name="imageHeight">Height of that image (pixels).</param>
        public ProfileResult Analyze(IReadOnlyList<Vector3> normalized, int imageWidth, int imageHeight)
        {
            var r = new ProfileResult();
            if (normalized == null || normalized.Count < 468)
            {
                r.Warning = "No face";
                return r;
            }

            int n = Math.Min(normalized.Count, _p.Length);
            // To a right-handed pixel space: X right, Y up, Z towards the camera.
            // MediaPipe z uses roughly the same scale as x and is negative towards the camera.
            for (int i = 0; i < n; i++)
            {
                var l = normalized[i];
                _p[i] = new Vector3(l.x * imageWidth, -l.y * imageHeight, -l.z * imageWidth);
            }

            // ---- 1. Head frame (makes the result independent of head pose) --------
            Vector3 lateral = (_p[FaceMeshProfileIndices.LeftEyeOuter] - _p[FaceMeshProfileIndices.RightEyeOuter]).normalized;
            Vector3 upRaw = _p[FaceMeshProfileIndices.ForeheadTop] - _p[FaceMeshProfileIndices.Menton];
            Vector3 up = (upRaw - Vector3.Dot(upRaw, lateral) * lateral).normalized;
            Vector3 forward = Vector3.Cross(lateral, up);
            // Mirrored webcams flip "lateral"; the face always points towards the camera.
            if (forward.z < 0f) { forward = -forward; lateral = -lateral; }

            r.HeadYawDeg   = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            r.HeadPitchDeg = Mathf.Atan2(forward.y, forward.z) * Mathf.Rad2Deg;
            r.HeadRollDeg  = Mathf.Atan2(lateral.y, lateral.x) * Mathf.Rad2Deg;

            // ---- 2. Scale: iris diameter -> mm ------------------------------------
            float unitsPerIris = 0f;
            if (normalized.Count >= 478)
                unitsPerIris = 0.5f * (IrisDiameter(FaceMeshProfileIndices.RightIris) +
                                       IrisDiameter(FaceMeshProfileIndices.LeftIris));
            r.MmPerUnit = unitsPerIris > 1e-4f ? FaceMeshProfileIndices.IrisDiameterMm / unitsPerIris : 0f;
            float s = r.MmPerUnit > 0f ? r.MmPerUnit : 1f;

            // ---- 3. Project midline onto the sagittal plane ----------------------
            Vector3 origin = _p[FaceMeshProfileIndices.Nasion];
            Vector2 Sag(int idx)
            {
                Vector3 d = _p[idx] - origin;
                return new Vector2(Vector3.Dot(d, forward), Vector3.Dot(d, up)) * s;
            }

            var chain = FaceMeshProfileIndices.MidlineProfile;
            r.Profile = new Vector2[chain.Length];
            for (int i = 0; i < chain.Length; i++) r.Profile[i] = Sag(chain[i]);

            // ---- 4. Landmarks -----------------------------------------------------
            r.G   = Sag(FaceMeshProfileIndices.Glabella);
            r.Sn  = Sag(FaceMeshProfileIndices.Subnasale);
            r.Cm  = Sag(FaceMeshProfileIndices.Columella);
            r.Prn = MostAnterior(FaceMeshProfileIndices.PronasaleCandidates, Sag);
            r.Ls  = MostAnterior(FaceMeshProfileIndices.UpperLipCandidates, Sag);
            r.Li  = MostAnterior(FaceMeshProfileIndices.LowerLipCandidates, Sag);

            // B' = deepest point of the chin chain below Li; Pg' = most anterior below B'.
            var chin = FaceMeshProfileIndices.ChinChain;
            int bIdx = 1;
            for (int i = 1; i < chin.Length - 1; i++)
                if (Sag(chin[i]).x < Sag(chin[bIdx]).x) bIdx = i;
            r.Bp = Sag(chin[bIdx]);
            r.Pg = Sag(chin[bIdx + 1]);
            for (int i = bIdx + 1; i < chin.Length; i++)
                if (Sag(chin[i]).x > r.Pg.x) r.Pg = Sag(chin[i]);

            // ---- 5. Measurements --------------------------------------------------
            r.NasolabialDeg  = Vector2.Angle(r.Cm - r.Sn, r.Ls - r.Sn);
            r.MentolabialDeg = Vector2.Angle(r.Li - r.Bp, r.Pg - r.Bp);

            float convexAbs = 180f - Vector2.Angle(r.G - r.Sn, r.Pg - r.Sn);
            r.FacialConvexityDeg = SignedDistanceAnterior(r.Sn, r.G, r.Pg) >= 0f ? convexAbs : -convexAbs;

            r.UpperLipToELineMm = SignedDistanceAnterior(r.Ls, r.Prn, r.Pg);
            r.LowerLipToELineMm = SignedDistanceAnterior(r.Li, r.Prn, r.Pg);

            r.Valid = true;
            if (Mathf.Abs(r.HeadYawDeg) > MaxYawDeg)
                r.Warning = "Turn head towards camera";
            else if (r.MmPerUnit <= 0f)
                r.Warning = "No iris landmarks: distances not in mm";
            return r;
        }

        float IrisDiameter(int[] ring)
        {
            // Ring is 4 points: two opposing pairs. Average both chords.
            return 0.5f * (Vector3.Distance(_p[ring[0]], _p[ring[2]]) +
                           Vector3.Distance(_p[ring[1]], _p[ring[3]]));
        }

        static Vector2 MostAnterior(int[] idx, Func<int, Vector2> sag)
        {
            Vector2 best = sag(idx[0]);
            for (int i = 1; i < idx.Length; i++)
            {
                var v = sag(idx[i]);
                if (v.x > best.x) best = v;
            }
            return best;
        }

        /// <summary>Distance of p from line a-b; positive when p lies anterior (+x side) of the line.</summary>
        public static float SignedDistanceAnterior(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(dir.y, -dir.x);
            if (normal.x < 0f) normal = -normal;
            return Vector2.Dot(p - a, normal);
        }
    }
}
