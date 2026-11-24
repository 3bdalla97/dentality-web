# Side-profile landmarks without training (proof of concept)

Lateral photo → MediaPipe `selfie_multiclass_256x256` segmentation → silhouette contour → geometric rules:

- **Prn**: most anterior contour point in the mid-face band
- **Sn**: columella–lip junction (deepest point under the chord Prn→A')
- **Ls**: first anterior bulge below Sn
- **Pg'**: chin point tangent to a line from Prn (this is the E-line definition)
- **B'**: deepest point relative to the E-line in the lower part of Ls..Pg'
- **St**: mouth slit (dark pixels just inside the edge combined with concavity)
- **Li**: most anterior point between St and B'
- **Cm**: columella tangent from Sn

Setup:

```
pip install opencv-python-headless numpy scipy mediapipe
mkdir -p models && curl -Lo models/selfie_multiclass_256x256.tflite \
  https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_multiclass_256x256/float32/latest/selfie_multiclass_256x256.tflite
mkdir in out   # put lateral photos in in/
cd src && python3 profile.py   # scripts expect ROOT/{models,in,out} with the scripts in ROOT/src
```

Patient photos are not stored in the repo.
