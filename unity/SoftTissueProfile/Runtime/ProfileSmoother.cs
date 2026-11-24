using UnityEngine;

namespace Dentality.ProfileAnalysis
{
    /// <summary>One-Euro filter: low jitter when still, low lag when moving.</summary>
    public sealed class OneEuroFilter
    {
        public float MinCutoff, Beta, DCutoff;
        float _x, _dx;
        bool _init;

        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.05f, float dCutoff = 1.0f)
        { MinCutoff = minCutoff; Beta = beta; DCutoff = dCutoff; }

        static float Alpha(float cutoff, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }

        public float Filter(float x, float dt)
        {
            if (!_init || dt <= 0f) { _x = x; _dx = 0f; _init = true; return x; }
            float dx = (x - _x) / dt;
            _dx += Alpha(DCutoff, dt) * (dx - _dx);
            float cutoff = MinCutoff + Beta * Mathf.Abs(_dx);
            _x += Alpha(cutoff, dt) * (x - _x);
            return _x;
        }

        public void Reset() => _init = false;
    }

    /// <summary>Smooths every point and metric of a <see cref="ProfileResult"/> in place.</summary>
    public sealed class ProfileSmoother
    {
        readonly float _minCutoff, _beta;
        OneEuroFilter[] _f;

        public ProfileSmoother(float minCutoff = 1.0f, float beta = 0.05f)
        { _minCutoff = minCutoff; _beta = beta; }

        public void Reset() => _f = null;

        public void Apply(ProfileResult r, float dt)
        {
            if (r == null || !r.Valid) return;
            int pts = r.Profile.Length + 8;
            int count = pts * 2 + 5;
            if (_f == null || _f.Length != count)
            {
                _f = new OneEuroFilter[count];
                for (int i = 0; i < count; i++) _f[i] = new OneEuroFilter(_minCutoff, _beta);
            }

            int k = 0;
            Vector2 F(Vector2 v) => new Vector2(_f[k++].Filter(v.x, dt), _f[k++].Filter(v.y, dt));

            for (int i = 0; i < r.Profile.Length; i++) r.Profile[i] = F(r.Profile[i]);
            r.G = F(r.G); r.Prn = F(r.Prn); r.Cm = F(r.Cm); r.Sn = F(r.Sn);
            r.Ls = F(r.Ls); r.Li = F(r.Li); r.Bp = F(r.Bp); r.Pg = F(r.Pg);

            r.NasolabialDeg      = _f[k++].Filter(r.NasolabialDeg, dt);
            r.MentolabialDeg     = _f[k++].Filter(r.MentolabialDeg, dt);
            r.FacialConvexityDeg = _f[k++].Filter(r.FacialConvexityDeg, dt);
            r.UpperLipToELineMm  = _f[k++].Filter(r.UpperLipToELineMm, dt);
            r.LowerLipToELineMm  = _f[k++].Filter(r.LowerLipToELineMm, dt);
        }
    }
}
