using UnityEngine;

namespace Roam.SpatialReconstruction
{
    /// <summary>
    /// Configuration for spatial reconstruction frame capture
    /// </summary>
    [CreateAssetMenu(fileName = "SpatialReconstructionConfig", menuName = "Roam/Spatial Reconstruction/Config")]
    public class SpatialReconstructionConfig : ScriptableObject
    {
        [Header("Output Settings")]
        [Tooltip("Output folder path relative to project root")]
        public string outputFolder = "Assets/_Roam Systems/Spatial Reconstruction/Output";

        [Header("Capture Settings")]
        [Range(1, 4)]
        [Tooltip("Resolution multiplier (1 = screen resolution)")]
        public int resolutionMultiplier = 1;

        [Header("Object Filtering")]
        [Tooltip("Capture only objects on these layers")]
        public LayerMask captureLayerMask = -1;

        [Tooltip("Include inactive GameObjects")]
        public bool includeInactiveObjects = false;

        [Tooltip("Only capture objects visible in camera frustum")]
        public bool onlyVisibleObjects = true;
    }
}
