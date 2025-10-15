using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roam.SpatialReconstruction.Editor
{
    public class SpatialReconstructionWindow : EditorWindow
    {
        [MenuItem("Roam/Spatial Reconstruction/Frame Analyzer")]
        private static void OpenWindow()
        {
            var window = GetWindow<SpatialReconstructionWindow>("Frame Analyzer");
            window.minSize = new Vector2(450, 300);
        }

        [SerializeField] private SpatialReconstructionConfig config;
        [SerializeField] private bool isBatchProcessing = false;
        [SerializeField] private float batchInterval = 1f;

        [Header("Capture Options")]
        [SerializeField] private bool captureRgbImage = true;
        [SerializeField] private bool captureRawDepthExr = true;
        [SerializeField] private Shader depthReplacementShader; // 我们需要手动指定这个Shader

        private string lastCaptureInfo = "Ready to capture";
        private string lastOutputPath = "";
        private double lastBatchCaptureTime;

        private void OnEnable()
        {
            // 自动查找我们需要的替换着色器
            if (depthReplacementShader == null)
            {
                depthReplacementShader = Shader.Find("Roam/ViewSpaceDepthReplacement");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            config = (SpatialReconstructionConfig)EditorGUILayout.ObjectField("Config", config, typeof(SpatialReconstructionConfig), false);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Capture Options", EditorStyles.boldLabel);
            captureRgbImage = EditorGUILayout.ToggleLeft("Capture RGB Image (.png)", captureRgbImage);
            captureRawDepthExr = EditorGUILayout.ToggleLeft("Capture Raw Depth (.exr)", captureRawDepthExr);

            if (captureRawDepthExr)
            {
                depthReplacementShader = (Shader)EditorGUILayout.ObjectField("Depth Shader", depthReplacementShader, typeof(Shader), false);
                if (depthReplacementShader == null)
                {
                    EditorGUILayout.HelpBox("Crucial 'ViewSpaceDepthReplacement' shader not found or assigned!", MessageType.Error);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Capture Control", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
            if (GUILayout.Button("CAPTURE FRAME", GUILayout.Height(40)))
            {
                CaptureFrame();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Batch Processing", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (!isBatchProcessing)
            {
                if (GUILayout.Button("Start Batch", GUILayout.Height(30))) StartBatchProcess();
            }
            else
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("Stop Batch", GUILayout.Height(30))) StopBatchProcess();
                GUI.backgroundColor = Color.white;
            }
            batchInterval = EditorGUILayout.Slider("Interval (s)", batchInterval, 0.1f, 10f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Last Capture", lastCaptureInfo);
            EditorGUILayout.TextField("Last Output", lastOutputPath);
            EditorGUI.EndDisabledGroup();
        }

        private void CaptureFrame()
        {
            if (!Application.isPlaying && !EditorApplication.isPlaying)
            {
                if (!EditorUtility.DisplayDialog("Capture Frame", "Scene is not playing. Capture editor scene state?", "Yes", "Cancel"))
                {
                    return;
                }
            }
            ExecuteCapture();
        }

        private void StartBatchProcess()
        {
            isBatchProcessing = true;
            lastBatchCaptureTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += BatchProcessUpdate;
            Debug.Log($"[Batch Process] Started with {batchInterval}s interval.");
        }

        private void StopBatchProcess()
        {
            isBatchProcessing = false;
            EditorApplication.update -= BatchProcessUpdate;
            Debug.Log("[Batch Process] Stopped.");
        }

        private void BatchProcessUpdate()
        {
            if (!isBatchProcessing) return;
            if (EditorApplication.timeSinceStartup - lastBatchCaptureTime >= batchInterval)
            {
                ExecuteCapture();
                lastBatchCaptureTime = EditorApplication.timeSinceStartup;
            }
        }

        private void ExecuteCapture()
        {
            Camera mainCam = Camera.main ?? FindObjectOfType<Camera>();
            if (mainCam == null)
            {
                Debug.LogError("[Frame Analyzer] No camera found in scene.");
                return;
            }

            if (config == null) config = CreateDefaultConfig();
            if (!Directory.Exists(config.outputFolder)) Directory.CreateDirectory(config.outputFolder);

            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmssfff");

            // 1. Capture RGB Image (if enabled)
            if (captureRgbImage)
            {
                string rgbPath = Path.Combine(config.outputFolder, $"rgb_{timestamp}.png");
                CaptureRGB(mainCam, rgbPath);
                Debug.Log($"[Frame Analyzer] RGB image saved to: {rgbPath}");
            }

            // 2. Capture Raw Depth EXR (if enabled)
            if (captureRawDepthExr)
            {
                if (depthReplacementShader == null)
                {
                    Debug.LogError("[Frame Analyzer] Depth Replacement Shader is not assigned. Cannot capture depth. Aborting.");
                    return;
                }
                string depthPath = Path.Combine(config.outputFolder, $"raw_depth_{timestamp}.exr");
                CaptureRawDepth(mainCam, depthPath, depthReplacementShader);
                Debug.Log($"[Frame Analyzer] Raw Depth EXR saved to: {depthPath}");
            }

            // 3. Capture Metadata and save JSON
            FrameData frameData = new FrameData
            {
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                camera = CaptureCameraData(mainCam),
                objects = CaptureObjectPoses(mainCam)
            };
            frameData.objectCount = frameData.objects.Count;

            string jsonPath = Path.Combine(config.outputFolder, $"frame_{timestamp}.json");
            string json = JsonUtility.ToJson(frameData, true);
            File.WriteAllText(jsonPath, json);

            lastCaptureInfo = $"{frameData.objectCount} objects @ {frameData.timestamp}";
            lastOutputPath = GetRelativePath(jsonPath);

            AssetDatabase.Refresh();
            Debug.Log($"[Frame Analyzer] Capture sequence complete for timestamp {timestamp}.");
        }

        private void CaptureRGB(Camera sourceCamera, string outputPath)
        {
            int width = sourceCamera.pixelWidth * config.resolutionMultiplier;
            int height = sourceCamera.pixelHeight * config.resolutionMultiplier;
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            
            var originalTarget = sourceCamera.targetTexture;
            sourceCamera.targetTexture = rt;
            sourceCamera.Render(); // This is fine for RGB, as it does a standard render.
            sourceCamera.targetTexture = originalTarget;

            Texture2D rgbTexture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            rgbTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            rgbTexture.Apply();
            RenderTexture.active = null;

            byte[] bytes = rgbTexture.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(rgbTexture);
        }

        /// <summary>
        /// [AUTHORITATIVE FINAL METHOD] Manually constructs a command buffer to draw all scene
        /// renderers with a replacement shader, guaranteeing a true depth map output.
        /// </summary>
        private void CaptureRawDepth(Camera sourceCamera, string outputPath, Shader replacementShader)
        {
            Material replacementMaterial = new Material(replacementShader);
            
            int width = sourceCamera.pixelWidth * config.resolutionMultiplier;
            int height = sourceCamera.pixelHeight * config.resolutionMultiplier;
            RenderTexture depthRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.RFloat);

            // Get all renderers in the scene
            var allRenderers = FindObjectsOfType<Renderer>();
            var visibleRenderers = new List<Renderer>();
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(sourceCamera);

            // Frustum culling to only draw visible objects
            foreach (var renderer in allRenderers)
            {
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                {
                    visibleRenderers.Add(renderer);
                }
            }

            // Manually build the rendering commands
            CommandBuffer cmd = new CommandBuffer { name = "True Depth Capture" };
            cmd.SetRenderTarget(depthRT);
            cmd.ClearRenderTarget(true, true, Color.black); // Clear to 0 depth
            cmd.SetViewProjectionMatrices(sourceCamera.worldToCameraMatrix, sourceCamera.projectionMatrix);

            foreach (var renderer in visibleRenderers)
            {
                int submeshCount = 1;
                var smr = renderer as SkinnedMeshRenderer;
                var mr = renderer as MeshRenderer;
                if (mr != null && mr.sharedMaterials != null) submeshCount = mr.sharedMaterials.Length;
                else if (smr != null && smr.sharedMaterials != null) submeshCount = smr.sharedMaterials.Length;
                for (int si = 0; si < submeshCount; si++)
                {
                    cmd.DrawRenderer(renderer, replacementMaterial, si, 0); // 指定子网格和 pass 0
                }
            }

            // Execute the command buffer immediately
            Graphics.ExecuteCommandBuffer(cmd);

            // Read the data back from the GPU
            Texture2D finalTexture = new Texture2D(width, height, TextureFormat.RFloat, false);
            RenderTexture.active = depthRT;
            finalTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            finalTexture.Apply();
            RenderTexture.active = null;

            byte[] bytes = finalTexture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            cmd.Release();
            RenderTexture.ReleaseTemporary(depthRT);
            DestroyImmediate(finalTexture);
            DestroyImmediate(replacementMaterial);
        }

        // --- Other helper methods from the original script ---
        private CameraData CaptureCameraData(Camera cam) { /* ... same as before ... */ return new CameraData { position = cam.transform.position, rotation = cam.transform.rotation, eulerAngles = cam.transform.eulerAngles, fov = cam.fieldOfView, nearClip = cam.nearClipPlane, farClip = cam.farClipPlane, aspectRatio = cam.aspect, projectionMatrix = new Matrix4x4Data(cam.projectionMatrix), worldToCameraMatrix = new Matrix4x4Data(cam.worldToCameraMatrix), pixelWidth = cam.pixelWidth, pixelHeight = cam.pixelHeight }; }
        private List<ObjectPoseData> CaptureObjectPoses(Camera cam) { /* ... same as before ... */ List<ObjectPoseData> poses = new List<ObjectPoseData>(); GameObject[] allObjects = FindObjectsOfType<GameObject>(config.includeInactiveObjects); Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam); foreach (GameObject obj in allObjects) { if ((config.captureLayerMask.value & (1 << obj.layer)) == 0) continue; bool isVisible = true; if (config.onlyVisibleObjects) { Renderer renderer = obj.GetComponent<Renderer>(); if (renderer != null) { isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds); } else { isVisible = false; } } if (!isVisible && config.onlyVisibleObjects) continue; poses.Add(new ObjectPoseData { name = obj.name, path = GetHierarchyPath(obj.transform), position = obj.transform.position, rotation = obj.transform.rotation, scale = obj.transform.lossyScale, isVisible = isVisible, layer = LayerMask.LayerToName(obj.layer), tag = obj.tag }); } return poses; }
        private string GetHierarchyPath(Transform transform) { /* ... same as before ... */ string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }
        private string GetRelativePath(string absolutePath) { /* ... same as before ... */ string projectPath = Application.dataPath.Replace("/Assets", ""); return absolutePath.Replace(projectPath + "/", ""); }
        private SpatialReconstructionConfig CreateDefaultConfig() { /* ... same as before ... */ string configPath = "Assets/_Roam Systems/Spatial Reconstruction/SpatialReconstructionConfig.asset"; SpatialReconstructionConfig newConfig = CreateInstance<SpatialReconstructionConfig>(); string configDir = Path.GetDirectoryName(configPath); if (!Directory.Exists(configDir)) { Directory.CreateDirectory(configDir); } AssetDatabase.CreateAsset(newConfig, configPath); AssetDatabase.SaveAssets(); return newConfig; }
    }

    // --- Data structures from the original script (keep them for JSON serialization) ---
    [System.Serializable] public class FrameData { public string timestamp; public int objectCount; public CameraData camera; public List<ObjectPoseData> objects; public string screenshotPath; }
    [System.Serializable] public class CameraData { public Vector3 position; public Quaternion rotation; public Vector3 eulerAngles; public float fov; public float nearClip; public float farClip; public float aspectRatio; public Matrix4x4Data projectionMatrix; public Matrix4x4Data worldToCameraMatrix; public int pixelWidth; public int pixelHeight; }
    [System.Serializable] public class ObjectPoseData { public string name; public string path; public Vector3 position; public Quaternion rotation; public Vector3 scale; public bool isVisible; public string layer; public string tag; }
    [System.Serializable] public class Matrix4x4Data { public List<float> values; public Matrix4x4Data(Matrix4x4 m) { values = new List<float>(); for (int i = 0; i < 16; i++) { values.Add(m[i]); } } }
}
