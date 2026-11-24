"""Soft-tissue lateral profile landmarks from a side photo, no training.
segmentation (MediaPipe selfie_multiclass) -> silhouette contour -> geometric rules."""
import glob, os, json
import cv2, numpy as np
from scipy.ndimage import gaussian_filter1d
from seg import person_prob, ROOT


def facing_right(person, face):
    rows = np.where(face.max(1) > 0.5)[0]
    gr, gl = [], []
    for y in rows[:: max(1, len(rows) // 60)]:
        px, fx = np.where(person[y] > 0.5)[0], np.where(face[y] > 0.5)[0]
        if len(px) and len(fx):
            gr.append(px.max() - fx.max()); gl.append(fx.min() - px.min())
    return np.median(gr) <= np.median(gl)


REFINE = False

def refine_mask(img, mask, face, fh):
    """Snap the coarse (256px) mask to the real full-res edge with GrabCut in a thin band."""
    ys, xs = np.where(face > 0.5)
    pad = int(0.25 * fh)
    y0, y1 = max(0, ys.min() - pad), min(img.shape[0], ys.max() + pad)
    x0, x1 = max(0, xs.min() - pad), min(img.shape[1], xs.max() + pad)
    roi, m = img[y0:y1, x0:x1], mask[y0:y1, x0:x1]
    band = max(4, int(0.012 * fh))
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * band + 1, 2 * band + 1))
    gc = np.full(m.shape, cv2.GC_PR_BGD, np.uint8)
    gc[m > 0] = cv2.GC_PR_FGD
    gc[cv2.erode(m, k) > 0] = cv2.GC_FGD
    gc[cv2.dilate(m, k) == 0] = cv2.GC_BGD
    bgd, fgd = np.zeros((1, 65)), np.zeros((1, 65))
    cv2.grabCut(roi, gc, None, bgd, fgd, 4, cv2.GC_INIT_WITH_MASK)
    out = mask.copy()
    out[y0:y1, x0:x1] = np.isin(gc, (cv2.GC_FGD, cv2.GC_PR_FGD)).astype(np.uint8)
    out = cv2.morphologyEx(out, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    return out


def arc_from(contour, start, step, stop):
    out, i, n = [], start, len(contour)
    for _ in range(n):
        p = contour[i]
        if stop(p): break
        out.append(p); i = (i + step) % n
    return np.array(out, float)


def resample_smooth(arc, sigma):
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(arc, axis=0), axis=1))]
    t = np.arange(0, d[-1], 1.0)
    pts = np.c_[np.interp(t, d, arc[:, 0]), np.interp(t, d, arc[:, 1])]
    return np.c_[gaussian_filter1d(pts[:, 0], sigma, mode="nearest"),
                 gaussian_filter1d(pts[:, 1], sigma, mode="nearest")]


def farthest_inside(arc, i0, i1):
    """Index between i0..i1 farthest behind (posterior to) the chord arc[i0]-arc[i1]."""
    a, b = arc[i0], arc[i1]
    d = b - a; n = np.array([d[1], -d[0]]) / (np.linalg.norm(d) + 1e-9)
    if n[0] > 0: n = -n                        # normal pointing posterior (-x)
    seg = arc[i0:i1 + 1]
    return i0 + int(np.argmax((seg - a) @ n))


def angle(a, v, b):
    u, w = a - v, b - v
    return float(np.degrees(np.arccos(np.clip(u @ w / (np.linalg.norm(u) * np.linalg.norm(w)), -1, 1))))


def signed_dist(p, a, b):
    d = b - a; n = np.array([d[1], -d[0]]) / np.linalg.norm(d)
    if n[0] < 0: n = -n
    return float((p - a) @ n)                  # + = anterior to line


def analyze(img):
    person, face = person_prob(img)
    right = facing_right(person, face)
    if not right:
        img, person, face = img[:, ::-1].copy(), person[:, ::-1].copy(), face[:, ::-1].copy()

    rows = np.where(face.max(1) > 0.5)[0]
    fy0, fy1 = rows.min(), rows.max(); fh = fy1 - fy0

    mask = refine_mask(img, (person > 0.5).astype(np.uint8), face, fh) if REFINE else (person > 0.5).astype(np.uint8)
    cnts, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    c = max(cnts, key=cv2.contourArea)[:, 0, :].astype(float)

    band = (c[:, 1] > fy0 + 0.25 * fh) & (c[:, 1] < fy1 - 0.15 * fh)
    iprn = int(np.flatnonzero(band)[np.argmax(c[band, 0])])
    down = 1 if c[(iprn + 5) % len(c), 1] > c[(iprn - 5) % len(c), 1] else -1

    prn_x = c[iprn, 0]
    arc = arc_from(c, iprn, down, lambda p: p[1] > fy1 + 0.12 * fh or p[0] < prn_x - 0.55 * fh)
    arc = resample_smooth(arc, sigma=max(1.5, 0.004 * fh))
    x, y = arc[:, 0], arc[:, 1]
    n = len(arc)

    def first_ext(start, kind, min_prom):
        """First local min/max of x after `start` that is prominent enough."""
        sgn = 1 if kind == "max" else -1
        best = start
        for i in range(start + 1, n):
            if sgn * (x[i] - x[best]) > 0: best = i
            elif sgn * (x[best] - x[i]) > min_prom: return best
        return best

    prom = 0.006 * fh
    iA  = first_ext(0, "min", prom)                       # soft-tissue A' (deepest upper-lip concavity)
    iSn = farthest_inside(arc, 0, iA)                     # columella-lip junction
    iLs = first_ext(iA, "max", prom)
    def chin_tangent(after, min_y):
        """Pg' = chin point most anterior as seen from Prn (= E-line tangent)."""
        idx = np.arange(after + 1, n)
        idx = idx[y[idx] > min_y]
        rel = arc[idx] - arc[0]
        return int(idx[np.argmax(np.arctan2(rel[:, 0], rel[:, 1]))])

    def farthest_outside(i0, i1):
        a, b = arc[i0], arc[i1]
        d = b - a; nn = np.array([d[1], -d[0]]) / (np.linalg.norm(d) + 1e-9)
        if nn[0] < 0: nn = -nn
        return i0 + int(np.argmax((arc[i0:i1 + 1] - a) @ nn))

    # Chin first (well below the lips): E-line tangent from Prn.
    iPg = chin_tangent(iLs, y[iLs] + 0.15 * fh)
    # Depth relative to the provisional E-line: + anterior.
    a, b = arc[0], arc[iPg]
    dd = b - a; nrm = np.array([dd[1], -dd[0]]) / np.linalg.norm(dd)
    if nrm[0] < 0: nrm = -nrm
    depth = (arc - a) @ nrm
    span = y[iPg] - y[iLs]
    def window(f0, f1):
        idx = np.arange(iLs, iPg + 1)
        return idx[(y[idx] >= y[iLs] + f0 * span) & (y[idx] <= y[iLs] + f1 * span)]

    # B' = deepest point in the lower part of Ls..Pg' (labiomental fold).
    wB = window(0.45, 0.97)
    iB = int(wB[np.argmin(depth[wB])])

    # Stomion = mouth slit: darkest pixels just inside the silhouette, upper part of Ls..B'.
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY).astype(float)
    tang = np.gradient(arc, axis=0); tang /= np.linalg.norm(tang, axis=1, keepdims=True) + 1e-9
    inward = np.c_[-tang[:, 1], tang[:, 0]]
    inward[inward[:, 0] > 0] *= -1                         # point posterior
    def darkness(i):
        ks = np.linspace(0.01, 0.045, 8) * fh
        pts = (arc[i] + ks[:, None] * inward[i]).astype(int)
        pts[:, 0] = np.clip(pts[:, 0], 0, gray.shape[1] - 1); pts[:, 1] = np.clip(pts[:, 1], 0, gray.shape[0] - 1)
        return gray[pts[:, 1], pts[:, 0]].mean()
    wS = np.arange(iLs + 1, iB)
    wS = wS[(y[wS] <= y[iLs] + 0.55 * span)]
    dark = np.array([darkness(i) for i in wS])
    # prefer dark AND concave: combine normalised darkness with depth
    score = (dark - dark.mean()) / (dark.std() + 1e-6) + 0.5 * (depth[wS] - depth[wS].mean()) / (depth[wS].std() + 1e-6)
    iSt = int(wS[np.argmin(score)])
    iLi = farthest_outside(iSt, iB)                       # lower-lip bulge
    iB  = farthest_inside(arc, iLi, iPg)
    iPg = chin_tangent(iB, y[iB])

    # Columella tangent from Sn: point on the lower nose with the flattest direction from Sn.
    Sn = arc[iSn]
    cand = np.arange(0, iSn)
    cand = cand[np.linalg.norm(arc[cand] - Sn, axis=1) > 0.25 * np.linalg.norm(arc[0] - Sn)]
    elev = np.arctan2(Sn[1] - arc[cand, 1], arc[cand, 0] - Sn[0])
    iCm = int(cand[np.argmin(elev)]) if len(cand) else 0

    P = {k: arc[i] for k, i in dict(Prn=0, Cm=iCm, Sn=iSn, A=iA, Ls=iLs, St=iSt, Li=iLi, B=iB, Pg=iPg).items()}
    m = dict(
        nasolabial=angle(P["Cm"], P["Sn"], P["Ls"]),
        mentolabial=angle(P["Li"], P["B"], P["Pg"]),
        ls_eline_px=signed_dist(P["Ls"], P["Prn"], P["Pg"]),
        li_eline_px=signed_dist(P["Li"], P["Prn"], P["Pg"]),
    )
    # scale-free version: % of E-line length (Prn-Pg')
    el = np.linalg.norm(P["Pg"] - P["Prn"])
    m["ls_eline_pct"] = 100 * m["ls_eline_px"] / el
    m["li_eline_pct"] = 100 * m["li_eline_px"] / el
    return img, arc, P, m, right


def draw(img, arc, P, m):
    v = img.copy(); s = max(1, img.shape[0] // 500)
    cv2.polylines(v, [arc.astype(np.int32)], False, (40, 40, 230), 2 * s, cv2.LINE_AA)
    d = (P["Pg"] - P["Prn"]) / np.linalg.norm(P["Pg"] - P["Prn"]); L = np.linalg.norm(P["Pg"] - P["Prn"])
    cv2.line(v, tuple((P["Prn"] - d * 0.25 * L).astype(int)), tuple((P["Pg"] + d * 0.25 * L).astype(int)), (230, 220, 40), 2 * s, cv2.LINE_AA)
    for a, b, col in [("Cm", "Sn", (60, 200, 60)), ("Ls", "Sn", (60, 200, 60)), ("Li", "B", (200, 80, 120)), ("Pg", "B", (200, 80, 120))]:
        e = P[b] + (P[a] - P[b]) / np.linalg.norm(P[a] - P[b]) * 0.12 * L
        cv2.line(v, tuple(P[b].astype(int)), tuple(e.astype(int)), col, 2 * s, cv2.LINE_AA)
    for k in ["Prn", "Sn", "Ls", "St", "Li", "B", "Pg"]:
        p = tuple(P[k].astype(int))
        cv2.circle(v, p, 5 * s, (30, 220, 255), -1, cv2.LINE_AA)
        cv2.putText(v, k if k != "B" else "B'", (p[0] + 9 * s, p[1] + 4 * s), cv2.FONT_HERSHEY_SIMPLEX, 0.55 * s, (255, 255, 255), 2 * s, cv2.LINE_AA)
    txt = [f"Nasolabial {m['nasolabial']:.0f} deg", f"Mentolabial {m['mentolabial']:.0f} deg",
           f"Ls-E {m['ls_eline_pct']:+.1f}%  Li-E {m['li_eline_pct']:+.1f}%"]
    for i, t in enumerate(txt):
        cv2.putText(v, t, (12 * s, (30 + 28 * i) * s), cv2.FONT_HERSHEY_SIMPLEX, 0.7 * s, (0, 0, 0), 5 * s, cv2.LINE_AA)
        cv2.putText(v, t, (12 * s, (30 + 28 * i) * s), cv2.FONT_HERSHEY_SIMPLEX, 0.7 * s, (255, 255, 255), 2 * s, cv2.LINE_AA)
    return v


if __name__ == "__main__":
    allm = {}
    tiles, zooms = [], []
    for p in sorted(glob.glob(f"{ROOT}/in/*.jpg")):
        name = os.path.basename(p)
        try:
            img, arc, P, m, right = analyze(cv2.imread(p))
            v = draw(img, arc, P, m)
            allm[name] = {k: round(val, 1) for k, val in m.items()} | {"faces_right": bool(right)}
        except Exception as e:
            v = cv2.imread(p); allm[name] = {"error": repr(e)}
        cv2.imwrite(f"{ROOT}/out/prof_{name}", v)
        # crop around the lower face for the grid
        tiles.append(cv2.resize(v, (400, 450)))
        if "error" not in allm[name]:   # zoomed crop around nose-chin for close inspection
            ys, xs = [q[1] for q in P.values()], [q[0] for q in P.values()]
            h = max(ys) - min(ys); y0 = int(max(0, min(ys) - 0.3 * h)); y1 = int(max(ys) + 0.3 * h)
            x1 = int(min(v.shape[1], max(xs) + 0.35 * h)); x0 = int(max(0, x1 - 1.3 * h))
            zooms.append(cv2.resize(v[y0:y1, x0:x1], (300, 380)))
    cv2.imwrite(f"{ROOT}/out/prof_grid.jpg", np.vstack([np.hstack(tiles[:5]), np.hstack(tiles[5:])]))
    zooms += [np.zeros_like(zooms[0])] * ((5 - len(zooms) % 5) % 5)
    cv2.imwrite(f"{ROOT}/out/zoom_grid.jpg", np.vstack([np.hstack(zooms[i:i + 5]) for i in range(0, len(zooms), 5)]))
    print(json.dumps(allm, indent=1))
