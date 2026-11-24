using UnityEngine;
using UnityEngine.UI;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Draws the lateral tracing ON TOP of the camera image. Give it the same
    /// RectTransform size/position as the RawImage that shows the camera.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SideProfileOverlay : MaskableGraphic
    {
        [Tooltip("Tick if the camera image on screen is mirrored relative to the frame sent to MediaPipe.")]
        public bool mirrorX;
        [Tooltip("Tick if the tracing appears upside down.")]
        public bool flipY;
        public float lineWidth = 4f;
        public float pointSize = 12f;
        public Color profileColor     = new Color(0.95f, 0.15f, 0.15f);
        public Color eLineColor       = new Color(0.2f, 0.85f, 0.95f);
        public Color nasolabialColor  = new Color(0.2f, 0.8f, 0.2f);
        public Color mentolabialColor = new Color(0.45f, 0.3f, 0.9f);
        public Color pointColor       = new Color(1f, 0.85f, 0.1f);

        SideProfileResult _r;

        public void SetResult(SideProfileResult r) { _r = r; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_r == null || !_r.Valid) return;
            Rect rect = GetPixelAdjustedRect();
            Vector2 M(Vector2 uv)
            {
                float u = mirrorX ? 1f - uv.x : uv.x;
                float v = flipY ? uv.y : 1f - uv.y;          // result v is top-down, UI y is bottom-up
                return new Vector2(rect.xMin + u * rect.width, rect.yMin + v * rect.height);
            }

            for (int i = 0; i < _r.Profile.Length - 1; i++)
                Line(vh, M(_r.Profile[i]), M(_r.Profile[i + 1]), profileColor, lineWidth);

            Vector2 a = M(_r.Prn), b = M(_r.Pg), d = (b - a).normalized;
            float L = (b - a).magnitude;
            Line(vh, a - d * 0.25f * L, b + d * 0.25f * L, eLineColor, lineWidth * 0.8f);

            Leg(vh, M(_r.Sn), M(_r.Cm), nasolabialColor, 0.14f * L);
            Leg(vh, M(_r.Sn), M(_r.Ls), nasolabialColor, 0.14f * L);
            Leg(vh, M(_r.Bp), M(_r.Li), mentolabialColor, 0.12f * L);
            Leg(vh, M(_r.Bp), M(_r.Pg), mentolabialColor, 0.12f * L);

            foreach (var p in new[] { _r.Prn, _r.Sn, _r.Ls, _r.St, _r.Li, _r.Bp, _r.Pg })
                Dot(vh, M(p), pointColor, pointSize);
        }

        void Leg(VertexHelper vh, Vector2 from, Vector2 to, Color c, float len)
            => Line(vh, from, from + (to - from).normalized * len, c, lineWidth);

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, Color c, float w)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
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
