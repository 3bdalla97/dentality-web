// One camera, two MediaPipe tasks:
//   FaceLandmarker  → frontal analysis (and head yaw, which decides the mode)
//   ImageSegmenter  → lateral (90°) silhouette analysis
// Built on the MediaPipeUnityPlugin sample framework (Bootstrap, Screen, ImageSource),
// so drop it into a copy of the "Face Landmark Detection" sample scene.

using System.Collections;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Vision.ImageSegmenter;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using UnityEngine;
using MpImage = Mediapipe.Image;
using BaseOptions = Mediapipe.Tasks.Core.BaseOptions;
using RunningMode = Mediapipe.Tasks.Vision.Core.RunningMode;
using ImageProcessingOptions = Mediapipe.Tasks.Vision.Core.ImageProcessingOptions;

namespace Dentality.ProfileAnalysis
{
    public class SoftTissueRunner : VisionTaskApiRunner<FaceLandmarker>
    {
        [SerializeField] FaceLandmarkerResultAnnotationController faceAnnotation;   // optional mesh drawing
        public SoftTissueDiagnosisManager manager;

        [Header("Models (same folder as face_landmarker_v2.bytes)")]
        public string faceModel = "face_landmarker_v2.bytes";
        public string segmentationModel = "selfie_multiclass_256x256.bytes";
        public BaseOptions.Delegate delegateType = BaseOptions.Delegate.CPU;

        ImageSegmenter _segmenter;
        Mediapipe.Unity.Experimental.TextureFramePool _pool;

        public override void Stop()
        {
            base.Stop();
            _segmenter?.Close();
            _segmenter = null;
            _pool?.Dispose();
            _pool = null;
        }

        protected override IEnumerator Run()
        {
            yield return AssetLoader.PrepareAssetAsync(faceModel);
            yield return AssetLoader.PrepareAssetAsync(segmentationModel);

            taskApi = FaceLandmarker.CreateFromOptions(new FaceLandmarkerOptions(
                new BaseOptions(delegateType, modelAssetPath: faceModel),
                runningMode: RunningMode.VIDEO, numFaces: 1), GpuManager.GpuResources);

            _segmenter = ImageSegmenter.CreateFromOptions(new ImageSegmenterOptions(
                new BaseOptions(delegateType, modelAssetPath: segmentationModel),
                runningMode: RunningMode.VIDEO, outputConfidenceMasks: true, outputCategoryMask: false),
                GpuManager.GpuResources);

            var imageSource = ImageSourceProvider.ImageSource;
            yield return imageSource.Play();
            if (!imageSource.isPrepared) { Debug.LogError("SoftTissueRunner: camera failed to start"); yield break; }

            int iw = imageSource.textureWidth, ih = imageSource.textureHeight;
            _pool = new Mediapipe.Unity.Experimental.TextureFramePool(iw, ih, TextureFormat.RGBA32, 10);
            screen.Initialize(imageSource);
            if (faceAnnotation) SetupAnnotationController(faceAnnotation, imageSource);

            var t = imageSource.GetTransformationOptions();
            var faceOpts = new ImageProcessingOptions(rotationDegrees: (int)t.rotationAngle);
            var segOpts = new ImageProcessingOptions(rotationDegrees: 0);   // see plugin note: mask rotates otherwise
            var faceResult = FaceLandmarkerResult.Alloc(1);
            var segResult = ImageSegmenterResult.Alloc(true);
            var endOfFrame = new WaitForEndOfFrame();
            long lastTs = -1;

            while (true)
            {
                if (isPaused) yield return new WaitWhile(() => isPaused);
                if (!_pool.TryGetTextureFrame(out var frame)) { yield return null; continue; }

                yield return endOfFrame;
                frame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), t.flipHorizontally, t.flipVertically);
                MpImage faceImage = frame.BuildCPUImage();                       // each task gets its own copy
                MpImage segImage = manager.NeedsSegmentation ? frame.BuildCPUImage() : null;
                frame.Release();

                long ts = GetCurrentTimestampMillisec();
                if (ts <= lastTs) ts = lastTs + 1;                               // tasks need strictly increasing time
                lastTs = ts;

                bool gotFace = taskApi.TryDetectForVideo(faceImage, ts, faceOpts, ref faceResult)
                               && faceResult.faceLandmarks != null && faceResult.faceLandmarks.Count > 0;
                if (faceAnnotation) faceAnnotation.DrawNow(gotFace ? faceResult : default);
                manager.OnFace(faceResult, gotFace, iw, ih);

                if (segImage != null && _segmenter.TrySegmentForVideo(segImage, ts, segOpts, ref segResult))
                    manager.OnSegmentation(segResult, segImage);
            }
        }
    }
}
