import sys, glob, os
import cv2, numpy as np
import mediapipe as mp
from mediapipe.tasks.python import vision, BaseOptions

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
seg = vision.ImageSegmenter.create_from_options(vision.ImageSegmenterOptions(
    base_options=BaseOptions(model_asset_path=f"{ROOT}/models/selfie_multiclass_256x256.tflite"),
    output_confidence_masks=True, output_category_mask=False))

def person_prob(bgr):
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    res = seg.segment(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb))
    bg = res.confidence_masks[0].numpy_view()
    face = res.confidence_masks[3].numpy_view()
    h, w = bgr.shape[:2]
    up = lambda m: cv2.resize(m, (w, h), interpolation=cv2.INTER_LINEAR)
    return 1.0 - up(bg), up(face)

if __name__ == "__main__":
    for p in sorted(glob.glob(f"{ROOT}/in/*.jpg")):
        img = cv2.imread(p)
        person, face = person_prob(img)
        vis = img.copy()
        cnts, _ = cv2.findContours((person > 0.5).astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        cv2.drawContours(vis, cnts, -1, (0, 0, 255), 3)
        vis[face > 0.5] = (vis[face > 0.5] * 0.6 + np.array([0, 255, 0]) * 0.4).astype(np.uint8)
        cv2.imwrite(f"{ROOT}/out/seg_{os.path.basename(p)}", cv2.resize(vis, None, fx=0.4, fy=0.4))
