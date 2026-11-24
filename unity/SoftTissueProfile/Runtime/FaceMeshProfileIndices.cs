namespace Dentality.ProfileAnalysis
{
    /// <summary>
    /// MediaPipe Face Mesh / FaceLandmarker (478-point, refined iris) indices used to
    /// rebuild the soft-tissue midsagittal profile from a FRONTAL camera view.
    ///
    /// The canonical face mesh has a vertex chain running exactly along the facial
    /// midline (forehead -> menton). Projecting that chain onto the head's sagittal
    /// plane gives the same silhouette you trace on a lateral photograph.
    /// </summary>
    public static class FaceMeshProfileIndices
    {
        // ---- Head frame -------------------------------------------------------
        public const int RightEyeOuter = 33;   // subject's right exocanthion
        public const int LeftEyeOuter  = 263;  // subject's left exocanthion
        public const int ForeheadTop   = 10;
        public const int Menton        = 152;

        // ---- Fixed soft-tissue landmarks -------------------------------------
        public const int Glabella  = 9;    // G'
        public const int Nasion    = 168;  // N'  (origin of the sagittal frame)
        public const int Subnasale = 2;    // Sn
        public const int Columella = 19;   // Cm  (columella tangent point, under the tip)

        // ---- Candidate sets (the most anterior / posterior one is picked live) --
        public static readonly int[] PronasaleCandidates = { 5, 4, 1, 19 };        // Prn = most anterior
        public static readonly int[] UpperLipCandidates  = { 164, 0, 11, 12 };     // Ls  = most anterior
        public static readonly int[] LowerLipCandidates  = { 15, 16, 17 };         // Li  = most anterior
        public static readonly int[] ChinChain           = { 17, 18, 200, 199, 175, 152 }; // B' then Pg'

        // ---- Iris contours (only present in the 478-point model) ---------------
        // Mean iris diameter (~11.7 mm) is used to convert mesh units to millimetres.
        public static readonly int[] RightIris = { 469, 470, 471, 472 };
        public static readonly int[] LeftIris  = { 474, 475, 476, 477 };
        public const float IrisDiameterMm = 11.7f;

        /// <summary>Ordered midline chain, forehead to menton (inner-lip points 13/14 omitted).</summary>
        public static readonly int[] MidlineProfile =
        {
            10, 151, 9, 8, 168, 6, 197, 195, 5, 4, 1, 19, 94, 2, 164,
            0, 11, 12, 15, 16, 17, 18, 200, 199, 175, 152
        };
    }
}
