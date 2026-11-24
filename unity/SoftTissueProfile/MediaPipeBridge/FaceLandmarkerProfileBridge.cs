// Requires homuler/MediaPipeUnityPlugin (Tasks API, v0.14+):
// https://github.com/homuler/MediaPipeUnityPlugin
// Keep this file in its own folder so the Runtime scripts compile without the plugin.

using System.Collections.Generic;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using UnityEngine;

namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// Converts a MediaPipe <see cref="FaceLandmarkerResult"/> into the plain
    /// Vector3 list the analyzer expects and forwards it to the controller.
    /// </summary>
    public class FaceLandmarkerProfileBridge : MonoBehaviour
    {
        public ProfileAnalysisController controller;

        readonly List<Vector3> _buffer = new List<Vector3>(478);

        /// <summary>
        /// Call from FaceLandmarkerRunner's result callback (any thread).
        /// width/height = size of the image fed to MediaPipe (e.g. the webcam texture).
        /// </summary>
        public void OnResult(FaceLandmarkerResult result, int width, int height)
        {
            if (controller == null) return;
            if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
            {
                controller.SubmitLandmarks(null, width, height);
                return;
            }

            var lms = result.faceLandmarks[0].landmarks;
            lock (_buffer)
            {
                _buffer.Clear();
                for (int i = 0; i < lms.Count; i++)
                    _buffer.Add(new Vector3(lms[i].x, lms[i].y, lms[i].z));
                controller.SubmitLandmarks(_buffer, width, height);
            }
        }
    }
}
