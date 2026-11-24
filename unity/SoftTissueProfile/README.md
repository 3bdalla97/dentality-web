# Real-time soft-tissue profile analysis in Unity (frontal camera + MediaPipe)

This rebuilds the **lateral soft-tissue profile** (as traced on a lateral photo: E-line, nasolabial
angle, mentolabial angle and so on) **live, from a frontal webcam**, using the 3D MediaPipe face mesh.

## Why it works from a frontal view

MediaPipe FaceLandmarker returns 478 landmarks with **x, y and z (depth)**. The canonical mesh has a
vertex chain along the facial midline (forehead → nasion → nose tip → subnasale → lips → chin → menton).
The steps are:

1. Build a **head coordinate frame** from the landmarks (eye corners 33/263 give the lateral axis,
   forehead 10 to menton 152 gives the up axis, and their cross product gives the forward axis). The
   results then don't depend on head pose, and mirrored webcams are handled automatically.
2. Project the midline chain onto the head's **mid-sagittal plane**, so x = anterior and y = superior.
   That gives the same silhouette as the red line on a lateral photo.
3. Convert to **millimetres** with the iris diameter (≈ 11.7 mm, landmarks 469–477).
4. Pick landmarks and compute the measurements on every frame, then smooth them with a One-Euro filter.

| Landmark | Mesh index | Rule |
|---|---|---|
| G' (glabella) | 9 | fixed |
| N' (nasion, origin) | 168 | fixed |
| Prn (pronasale) | 5, 4, 1, 19 | most anterior |
| Cm (columella) | 19 | fixed |
| Sn (subnasale) | 2 | fixed |
| Ls (labrale sup.) | 164, 0, 11, 12 | most anterior |
| Li (labrale inf.) | 15, 16, 17 | most anterior |
| B' (soft-tissue B) | 18, 200, 199, 175 | most posterior |
| Pg' (soft-tissue pogonion) | below B' | most anterior |

| Measurement | Definition | Reference range shown |
|---|---|---|
| Nasolabial angle | Cm–Sn–Ls | 90–110° |
| Mentolabial angle | Li–B'–Pg' | 110–140° |
| Facial convexity | 180° − ∠G'–Sn–Pg' (+ = convex) | 12 ± 4° (Legan) |
| Ls to E-line | signed, − = behind the Prn–Pg' line | −4 ± 2 mm (Ricketts) |
| Li to E-line | signed | −2 ± 2 mm |

Change the ranges in `ProfileAnalysisController.Row(...)` to match your population and age group.

## Files

```
Runtime/                        (no plugin dependency, so you can unit-test it)
  FaceMeshProfileIndices.cs     landmark index map
  SoftTissueProfileAnalyzer.cs  head frame → sagittal projection → angles/distances
  ProfileSmoother.cs            One-Euro filter for points and metrics
  ProfileGraph.cs               uGUI Graphic that draws the live lateral tracing
  ProfileAnalysisController.cs  thread-safe input, runs analysis on the main thread, shows the readout
MediaPipeBridge/
  FaceLandmarkerProfileBridge.cs  FaceLandmarkerResult → controller
```

## Setup

1. Install the [MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin) (Tasks API)
   and open its **Face Landmark Detection** sample scene (`Tasks/Face Landmark Detection`).
2. Copy the `SoftTissueProfile` folder into `Assets/`.
3. In the scene's Canvas:
   * add a UI object (for example 400×500) with the **ProfileGraph** component, which draws the tracing,
   * add a legacy **Text** for the readout,
   * add an empty GameObject with **ProfileAnalysisController** (assign the graph and text) and
     **FaceLandmarkerProfileBridge** (assign the controller).
4. In the sample's `FaceLandmarkerRunner.cs`:
   * set `outputFaceBlendshapes`/`outputFacialTransformationMatrixes` however you like (neither is needed),
   * add a field `public Dentality.ProfileAnalysis.FaceLandmarkerProfileBridge profileBridge;`
   * forward every result. In LIVE_STREAM mode, that goes in the callback:

```csharp
private void OnFaceLandmarkDetectionOutput(FaceLandmarkerResult result, Image image, long timestamp)
{
    _faceLandmarkerResultAnnotationController.DrawLater(result);
    profileBridge?.OnResult(result, image.Width(), image.Height());
}
```

   In IMAGE/VIDEO mode, call `profileBridge.OnResult(result, textureFrame.width, textureFrame.height)`
   after `DetectForVideo`/`Detect`.

`ProfileAnalysisController.SubmitLandmarks` accepts any normalised landmark list, so you can also feed it
from ARKit or another tracker.

## Accuracy notes (read before clinical use)

* **Depth from a single RGB camera is predicted, not measured.** The MediaPipe z value comes from a
  learned model that is biased toward an average face. It tends to **underestimate real deviations**
  (a retrusive chin, protrusive lips, a very acute nasolabial angle). Treat the numbers as screening or
  visual feedback until you validate them against lateral photos or cephs for your own patients.
* Accuracy improves when the patient turns about 20–30° (depth is then partly visible in x). The
  analyzer warns above 35° yaw, where the tracker starts to lose the face.
* For better depth, run on an iPhone with **TrueDepth (ARKit face tracking via AR Foundation)**. Map
  ARKit's vertices to the same midline points, or keep MediaPipe and use the depth map to correct z.
* The iris-based mm scale is about ±10% because iris diameter varies between people. For exact mm,
  calibrate once per patient with a known distance (a ruler or a sticker of known size).
* Lighting, a beard, or a scarf or hijab that covers the chin and menton degrades the chin landmarks,
  which affects Pg', B' and the E-line.

## Suggested validation

Capture a frontal live session and a standard lateral photo of the same patient. Measure the lateral
photo manually (your current 2D workflow) and compare it per metric (Bland–Altman). Use the bias to
calibrate offsets or to decide which metrics are reliable enough to show.
