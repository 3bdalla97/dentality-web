using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// One frame of lateral (90°) soft-tissue analysis.
    /// Points are normalised image coordinates of the ORIGINAL frame: u in [0,1] left→right,
    /// v in [0,1] top→bottom.
    /// </summary>
    public sealed class SideProfileResult
    {
        public bool Valid;
        public string Warning;
        public bool FacingRight;

        public Vector2[] Profile;                       // silhouette from Prn down past the chin
        public Vector2 Prn, Cm, Sn, Ls, St, Li, Bp, Pg;

        public float NasolabialDeg;                     // Cm-Sn-Ls
        public float MentolabialDeg;                    // Li-B'-Pg'
        public float UpperLipToELinePct;                // % of E-line length, − = behind the line
        public float LowerLipToELinePct;
        public float UpperLipToELinePx;                 // in source-image pixels (multiply by mm/px)
        public float LowerLipToELinePx;
    }

    /// <summary>
    /// Lateral-photo landmarks with no trained model:
    /// person/face segmentation → silhouette contour → geometric rules.
    /// Port of Tools/side_profile_poc/profile.py.
    ///
    /// Inputs are row-major, row 0 = TOP of the image.
    /// </summary>
    public sealed class SideProfileAnalyzer
    {
        public int MaxWorkSize = 768;           // masks are downsampled to this; 512 = faster, 1024 = finer
        public float Threshold = 0.5f;

        // working buffers
        bool[] _p, _f;
        float[] _g;
        int _w, _h;
        float _scale;                            // source-mask pixels per working pixel

        static readonly int[] DX = { 1, 1, 0, -1, -1, -1, 0, 1 };   // clockwise on screen (y down)
        static readonly int[] DY = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <param name="person">P(person) per pixel (= 1 − background confidence)</param>
        /// <param name="face">P(face skin) per pixel</param>
        /// <param name="gray">optional luminance 0..1, same size as the masks (improves stomion)</param>
        /// <param name="imageWidth">source image width, only used to report distances in image pixels</param>
        public SideProfileResult Analyze(float[] person, float[] face, float[] gray, int w, int h, int imageWidth)
        {
            var r = new SideProfileResult();
            if (person == null || face == null || person.Length < w * h || face.Length < w * h)
            { r.Warning = "No segmentation"; return r; }

            // ---- 1. Facing direction (on the full-size masks) ---------------------
            bool right = FacingRight(person, face, w, h);
            r.FacingRight = right;

            // ---- 2. Working grid, mirrored so the patient always faces +x ----------
            int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(w, h) / (float)MaxWorkSize));
            _scale = step;
            _w = w / step; _h = h / step;
            int n = _w * _h;
            if (_p == null || _p.Length != n) { _p = new bool[n]; _f = new bool[n]; _g = new float[n]; }
            bool hasGray = gray != null && gray.Length >= w * h;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int sx = (right ? x : _w - 1 - x) * step, sy = y * step;
                int si = sy * w + sx, di = y * _w + x;
                _p[di] = person[si] > Threshold;
                _f[di] = face[si] > Threshold;
                _g[di] = hasGray ? gray[si] : 0f;
            }

            // ---- 3. Face extent ----------------------------------------------------
            int fy0 = -1, fy1 = -1;
            for (int y = 0; y < _h; y++)
            {
                bool any = false;
                for (int x = 0; x < _w && !any; x++) any = _f[y * _w + x];
                if (any) { if (fy0 < 0) fy0 = y; fy1 = y; }
            }
            float fh = fy1 - fy0;
            if (fy0 < 0 || fh < 20) { r.Warning = "Face not found"; return r; }

            // ---- 4. Pronasale = most anterior silhouette pixel in the mid-face band --
            int prnX = -1, prnY = -1;
            for (int y = (int)(fy0 + 0.25f * fh); y < (int)(fy1 - 0.15f * fh); y++)
                for (int x = _w - 1; x > prnX; x--)
                    if (_p[y * _w + x]) { prnX = x; prnY = y; break; }
            if (prnX < 0 || prnX >= _w - 1) { r.Warning = "Nose touches the frame edge"; return r; }

            // ---- 5. Trace the outer contour and take the arc Prn → below chin -------
            var contour = TraceContour(prnX, prnY, 8 * (_w + _h));
            int nC = contour.Count;
            if (nC < 50) { r.Warning = "Contour too short"; return r; }
            int down = contour[(5) % nC].y > contour[(nC - 5) % nC].y ? 1 : -1;
            var raw = new List<Vector2>();
            for (int k = 0, i = 0; k < nC; k++, i = ((i + down) % nC + nC) % nC)
            {
                var c = contour[i];
                if (c.y > fy1 + 0.12f * fh || c.x < prnX - 0.55f * fh) break;
                raw.Add(new Vector2(c.x, c.y));
            }
            if (raw.Count < 30) { r.Warning = "Profile too short"; return r; }
            Vector2[] arc = ResampleSmooth(raw, Mathf.Max(1.5f, 0.004f * fh));
            int N = arc.Length;

            // ---- 6. Landmarks -----------------------------------------------------
            float prom = 0.006f * fh;
            int iA = FirstExtremum(arc, 0, -1, prom);                 // A' (deepest upper-lip concavity)
            int iSn = FarthestInside(arc, 0, iA);                      // columella-lip junction
            int iLs = FirstExtremum(arc, iA, +1, prom);
            if (iLs >= N - 10) { r.Warning = "Lips not found"; return r; }

            int iPg = ChinTangent(arc, iLs, arc[iLs].y + 0.15f * fh);
            if (iPg < 0) { r.Warning = "Chin not found (uncover the chin)"; return r; }

            // depth relative to the provisional E-line, + anterior
            Vector2 nrm = AnteriorNormal(arc[0], arc[iPg]);
            float span = arc[iPg].y - arc[iLs].y;
            float Depth(int i) => Vector2.Dot(arc[i] - arc[0], nrm);

            // B' = deepest point in the lower part of Ls..Pg'
            int iB = -1; float best = float.MaxValue;
            for (int i = iLs; i <= iPg; i++)
            {
                float t = (arc[i].y - arc[iLs].y) / span;
                if (t >= 0.45f && t <= 0.97f && Depth(i) < best) { best = Depth(i); iB = i; }
            }
            if (iB < 0) { r.Warning = "Labiomental fold not found"; return r; }

            int iSt = Stomion(arc, iLs, iB, span, fh, Depth, hasGray);
            if (iSt < 0) { r.Warning = "Stomion not found"; return r; }
            int iLi = FarthestOutside(arc, iSt, iB);
            iB = FarthestInside(arc, iLi, iPg);
            int iPg2 = ChinTangent(arc, iB, arc[iB].y);
            if (iPg2 >= 0) iPg = iPg2;

            int iCm = Columella(arc, iSn);

            // ---- 7. Measurements --------------------------------------------------
            Vector2 Prn = arc[0], Sn = arc[iSn], Cm = arc[iCm], Ls = arc[iLs], Li = arc[iLi], Bp = arc[iB], Pg = arc[iPg];
            r.NasolabialDeg = Vector2.Angle(Cm - Sn, Ls - Sn);
            r.MentolabialDeg = Vector2.Angle(Li - Bp, Pg - Bp);
            float eLen = (Pg - Prn).magnitude;
            float ls = SignedAnterior(Ls, Prn, Pg), li = SignedAnterior(Li, Prn, Pg);
            r.UpperLipToELinePct = 100f * ls / eLen;
            r.LowerLipToELinePct = 100f * li / eLen;
            float toImagePx = _scale * imageWidth / (float)w;
            r.UpperLipToELinePx = ls * toImagePx;
            r.LowerLipToELinePx = li * toImagePx;

            // ---- 8. Back to normalised source coordinates -------------------------
            Vector2 U(Vector2 p) => new Vector2(((right ? p.x : _w - 1 - p.x) + 0.5f) / _w, (p.y + 0.5f) / _h);
            r.Profile = new Vector2[N];
            for (int i = 0; i < N; i++) r.Profile[i] = U(arc[i]);
            r.Prn = U(Prn); r.Sn = U(Sn); r.Cm = U(Cm); r.Ls = U(Ls); r.St = U(arc[iSt]);
            r.Li = U(Li); r.Bp = U(Bp); r.Pg = U(Pg);
            r.Valid = true;
            return r;
        }

        // ------------------------------------------------------------------------

        static bool FacingRight(float[] person, float[] face, int w, int h)
        {
            var gr = new List<int>(); var gl = new List<int>();
            for (int y = 0; y < h; y += Mathf.Max(1, h / 120))
            {
                int pMin = -1, pMax = -1, fMin = -1, fMax = -1;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (person[i] > 0.5f) { if (pMin < 0) pMin = x; pMax = x; }
                    if (face[i] > 0.5f) { if (fMin < 0) fMin = x; fMax = x; }
                }
                if (pMin >= 0 && fMin >= 0) { gr.Add(pMax - fMax); gl.Add(fMin - pMin); }
            }
            if (gr.Count == 0) return true;
            gr.Sort(); gl.Sort();
            return gr[gr.Count / 2] <= gl[gl.Count / 2];
        }

        bool Fg(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h && _p[y * _w + x];

        /// <summary>Moore-neighbour tracing of the outer boundary, starting at a pixel whose right neighbour is background.</summary>
        List<Vector2Int> TraceContour(int sx, int sy, int maxSteps)
        {
            var pts = new List<Vector2Int> { new Vector2Int(sx, sy) };
            int cx = sx, cy = sy, back = 0;                 // backtrack = east (background)
            for (int s = 0; s < maxSteps; s++)
            {
                int found = -1;
                for (int k = 1; k <= 8; k++)
                {
                    int d = (back + k) & 7;
                    if (Fg(cx + DX[d], cy + DY[d])) { found = d; break; }
                }
                if (found < 0) break;                       // isolated pixel
                int prev = (found + 7) & 7;                 // last background neighbour checked
                int nx = cx + DX[found], ny = cy + DY[found];
                int bx = cx + DX[prev] - nx, by = cy + DY[prev] - ny;
                back = DirIndex(bx, by);
                cx = nx; cy = ny;
                if (cx == sx && cy == sy) break;
                pts.Add(new Vector2Int(cx, cy));
            }
            return pts;
        }

        static int DirIndex(int dx, int dy)
        {
            for (int d = 0; d < 8; d++) if (DX[d] == dx && DY[d] == dy) return d;
            return 0;
        }

        static Vector2[] ResampleSmooth(List<Vector2> pts, float sigma)
        {
            var dist = new float[pts.Count];
            for (int i = 1; i < pts.Count; i++) dist[i] = dist[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            int n = Mathf.Max(2, (int)dist[pts.Count - 1]);
            var res = new Vector2[n];
            int j = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i;
                while (j < pts.Count - 2 && dist[j + 1] < t) j++;
                float seg = dist[j + 1] - dist[j];
                res[i] = Vector2.Lerp(pts[j], pts[j + 1], seg > 0 ? (t - dist[j]) / seg : 0f);
            }
            // gaussian smoothing, edge = nearest
            int rad = Mathf.CeilToInt(sigma * 4);
            var kern = new float[2 * rad + 1]; float ks = 0;
            for (int k = -rad; k <= rad; k++) { kern[k + rad] = Mathf.Exp(-0.5f * k * k / (sigma * sigma)); ks += kern[k + rad]; }
            var outp = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 acc = Vector2.zero;
                for (int k = -rad; k <= rad; k++) acc += res[Mathf.Clamp(i + k, 0, n - 1)] * kern[k + rad];
                outp[i] = acc / ks;
            }
            return outp;
        }

        /// <summary>First local min (sign −1) / max (+1) of x after start, with prominence.</summary>
        static int FirstExtremum(Vector2[] a, int start, int sign, float prom)
        {
            int best = start;
            for (int i = start + 1; i < a.Length; i++)
            {
                if (sign * (a[i].x - a[best].x) > 0) best = i;
                else if (sign * (a[best].x - a[i].x) > prom) return best;
            }
            return best;
        }

        static Vector2 AnteriorNormal(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            var n = new Vector2(d.y, -d.x);
            return n.x < 0 ? -n : n;
        }

        static float SignedAnterior(Vector2 p, Vector2 a, Vector2 b) => Vector2.Dot(p - a, AnteriorNormal(a, b));

        static int FarthestInside(Vector2[] a, int i0, int i1)
        {
            if (i1 <= i0) return i0;
            Vector2 n = -AnteriorNormal(a[i0], a[i1]);
            int best = i0; float bv = float.MinValue;
            for (int i = i0; i <= i1; i++) { float v = Vector2.Dot(a[i] - a[i0], n); if (v > bv) { bv = v; best = i; } }
            return best;
        }

        static int FarthestOutside(Vector2[] a, int i0, int i1)
        {
            if (i1 <= i0) return i0;
            Vector2 n = AnteriorNormal(a[i0], a[i1]);
            int best = i0; float bv = float.MinValue;
            for (int i = i0; i <= i1; i++) { float v = Vector2.Dot(a[i] - a[i0], n); if (v > bv) { bv = v; best = i; } }
            return best;
        }

        /// <summary>Pg' = point after `after`, below minY, most anterior as seen from Prn (E-line tangent).</summary>
        static int ChinTangent(Vector2[] a, int after, float minY)
        {
            int best = -1; float bv = float.MinValue;
            for (int i = after + 1; i < a.Length; i++)
            {
                if (a[i].y <= minY) continue;
                Vector2 d = a[i] - a[0];
                float ang = Mathf.Atan2(d.x, d.y);
                if (ang > bv) { bv = ang; best = i; }
            }
            return best;
        }

        int Stomion(Vector2[] a, int iLs, int iB, float span, float fh, Func<int, float> depth, bool useGray)
        {
            var idx = new List<int>();
            for (int i = iLs + 1; i < iB; i++) if (a[i].y <= a[iLs].y + 0.55f * span) idx.Add(i);
            if (idx.Count == 0) return -1;

            var dk = new float[idx.Count]; var dp = new float[idx.Count];
            for (int k = 0; k < idx.Count; k++)
            {
                int i = idx[k];
                dp[k] = depth(i);
                if (!useGray) continue;
                Vector2 t = a[Mathf.Min(i + 1, a.Length - 1)] - a[Mathf.Max(i - 1, 0)];
                Vector2 inward = new Vector2(-t.y, t.x).normalized;
                if (inward.x > 0) inward = -inward;
                float s = 0;
                for (int j = 0; j < 8; j++)
                {
                    Vector2 q = a[i] + inward * (Mathf.Lerp(0.01f, 0.045f, j / 7f) * fh);
                    int qx = Mathf.Clamp((int)q.x, 0, _w - 1), qy = Mathf.Clamp((int)q.y, 0, _h - 1);
                    s += _g[qy * _w + qx];
                }
                dk[k] = s / 8f;
            }
            Normalize(dk); Normalize(dp);
            int best = 0; float bv = float.MaxValue;
            for (int k = 0; k < idx.Count; k++)
            {
                float score = (useGray ? dk[k] : 0f) + 0.5f * dp[k];   // dark AND concave
                if (score < bv) { bv = score; best = k; }
            }
            return idx[best];
        }

        static void Normalize(float[] v)
        {
            float m = 0, s = 0;
            foreach (var x in v) m += x;
            m /= v.Length;
            foreach (var x in v) s += (x - m) * (x - m);
            s = Mathf.Sqrt(s / v.Length) + 1e-6f;
            for (int i = 0; i < v.Length; i++) v[i] = (v[i] - m) / s;
        }

        /// <summary>Columella tangent from Sn: lower-nose point with the flattest direction from Sn.</summary>
        static int Columella(Vector2[] a, int iSn)
        {
            Vector2 sn = a[iSn];
            float minD = 0.25f * (a[0] - sn).magnitude;
            int best = 0; float bv = float.MaxValue;
            for (int i = 0; i < iSn; i++)
            {
                if ((a[i] - sn).magnitude <= minD) continue;
                float elev = Mathf.Atan2(sn.y - a[i].y, a[i].x - sn.x);
                if (elev < bv) { bv = elev; best = i; }
            }
            return best;
        }
    }
}
