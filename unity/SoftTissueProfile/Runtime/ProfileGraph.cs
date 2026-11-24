using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// uGUI graphic that draws the reconstructed lateral profile live, styled like a
    /// traced lateral photo: red silhouette, cyan E-line, green nasolabial and purple
    /// mentolabial angle legs, yellow landmarks. Put it on a RectTransform inside a Canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ProfileGraph : MaskableGraphic
    {
        public float lineWidth = 3f;
        public float pointSize = 8f;
        public float padding = 16f;
        public Color profileColor     = new Color(0.95f, 0.15f, 0.15f);
        public Color eLineColor       = new Color(0.2f, 0.85f, 0.95f);
        public Color nasolabialColor  = new Color(0.2f, 0.8f, 0.2f);
        public Color mentolabialColor = new Color(0.45f, 0.3f, 0.9f);
        public Color pointColor       = new Color(1f, 0.85f, 0.1f);

        ProfileResult _r;

        public void SetResult(ProfileResult r)
        {
            _r = r;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_r == null || !_r.Valid || _r.Profile == null || _r.Profile.Length < 2) return;

            // Fit profile bounds into the rect, uniform scale, keep aspect.
            Vector2 min = _r.Profile[0], max = _r.Profile[0];
            foreach (var p in _r.Profile) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            Rect rect = GetPixelAdjustedRect();
            Vector2 size = max - min;
            float sc = Mathf.Min((rect.width - 2 * padding) / Mathf.Max(size.x, 1e-3f),
                                 (rect.height - 2 * padding) / Mathf.Max(size.y, 1e-3f));
            Vector2 centerData = (min + max) * 0.5f;
            Vector2 M(Vector2 p) => rect.center + (p - centerData) * sc;

            // Silhouette
            for (int i = 0; i < _r.Profile.Length - 1; i++)
                Line(vh, M(_r.Profile[i]), M(_r.Profile[i + 1]), profileColor, lineWidth);

            // E-line, extended beyond Prn and Pg'
            Vector2 dir = (_r.Pg - _r.Prn).normalized;
            float ext = size.magnitude * 0.12f;
            Line(vh, M(_r.Prn - dir * ext), M(_r.Pg + dir * ext), eLineColor, lineWidth * 0.8f);

            // Nasolabial legs (Sn->Cm, Sn->Ls)
            Line(vh, M(_r.Sn), M(_r.Sn + (_r.Cm - _r.Sn).normalized * size.y * 0.12f), nasolabialColor, lineWidth);
            Line(vh, M(_r.Sn), M(_r.Sn + (_r.Ls - _r.Sn).normalized * size.y * 0.12f), nasolabialColor, lineWidth);

            // Mentolabial legs (B'->Li, B'->Pg')
            Line(vh, M(_r.Bp), M(_r.Bp + (_r.Li - _r.Bp).normalized * size.y * 0.10f), mentolabialColor, lineWidth);
            Line(vh, M(_r.Bp), M(_r.Bp + (_r.Pg - _r.Bp).normalized * size.y * 0.10f), mentolabialColor, lineWidth);

            foreach (var p in new[] { _r.Prn, _r.Sn, _r.Ls, _r.Li, _r.Bp, _r.Pg })
                Dot(vh, M(p), pointColor, pointSize);
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color c, float w)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Dot(VertexHelper vh, Vector2 p, Color c, float s)
        {
            const int seg = 12;
            int center = vh.currentVertCount;
            vh.AddVert(p, c, Vector2.zero);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                vh.AddVert(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (s * 0.5f), c, Vector2.zero);
                if (i > 0) vh.AddTriangle(center, center + i, center + i + 1);
            }
        }
    }
}
