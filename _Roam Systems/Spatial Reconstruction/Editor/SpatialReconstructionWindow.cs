using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roam.SpatialReconstruction.Editor
{
    /// <summary>
    /// Frame analyzer for capturing 6DoF poses, depth maps, and camera data
    /// </summary>
    public class SpatialReconstructionWindow : EditorWindow
    {
        private const float DEFAULT_NEAR_CUTOFF = 0.01f;
        private const float DEFAULT_FAR_CUTOFF = 100f;
        private const float DEFAULT_CONTRAST = 2.0f;

        [MenuItem("Roam/Spatial Reconstruction/Frame Analyzer")]
        private static void OpenWindow()
        {
            var window = GetWindow<SpatialReconstructionWindow>("Frame Analyzer");
            window.minSize = new Vector2(450, 400);
        }

        [SerializeField] private SpatialReconstructionConfig config;
        [SerializeField] private VolumeProfile depthVolumeProfile;
        [SerializeField] private bool isBatchProcessing = false;
        [SerializeField] private float batchInterval = 1f;

        private string lastCaptureInfo = "Ready to capture";
        private string lastOutputPath = "";

        private void ToggleRendererFeature()
        {
            if (IsRendererFeatureAdded())
            {
                EditorUtility.DisplayDialog("Already Added", 
                    "Depth Capture Renderer Feature is already added to the URP pipeline.", 
                    "OK");
            }
            else
            {
                AddRendererFeature();
            }
        }

        private void CaptureFrame()
        {
            if (!Application.isPlaying && !EditorApplication.isPlaying)
            {
                if (!EditorUtility.DisplayDialog("Capture Frame", 
                    "Scene is not playing. Capture editor scene state?", 
                    "Yes", "Cancel"))
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
            Debug.Log($"[Batch Process] Started with {batchInterval}s interval");
        }

        private void StopBatchProcess()
        {
            isBatchProcessing = false;
            EditorApplication.update -= BatchProcessUpdate;
            Debug.Log("[Batch Process] Stopped");
        }

        private double lastBatchCaptureTime;

        private void OnEnable()
        {
            // Auto-assign Global Volume Profile if not set
            if (depthVolumeProfile == null)
            {
                string profilePath = "Assets/_Roam Systems/Spatial Reconstruction/Global Volume Profile.asset";
                depthVolumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            }
        }

        /// <summary>
        /// Unity Editor GUI
        /// </summary>
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            config = (SpatialReconstructionConfig)EditorGUILayout.ObjectField("Config", config, typeof(SpatialReconstructionConfig), false);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Depth Visualization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Volume Profile with depth effect for visualization", MessageType.Info);
            depthVolumeProfile = (VolumeProfile)EditorGUILayout.ObjectField("Depth Volume Profile", depthVolumeProfile, typeof(VolumeProfile), false);
            
            if (depthVolumeProfile == null)
            {
                EditorGUILayout.HelpBox("Assign a Volume Profile with depth visualization effect", MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("URP Renderer Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Depth Capture Renderer Feature status", MessageType.Info);
            
            GUI.backgroundColor = IsRendererFeatureAdded() ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.2f, 0.2f);
            string buttonLabel = IsRendererFeatureAdded() ? "Renderer Feature Active" : "Add to Renderer Pipeline";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(30)))
            {
                ToggleRendererFeature();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Capture Status", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Last Capture Info", lastCaptureInfo);
            EditorGUILayout.TextField("Last Output Path", lastOutputPath);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Single Frame Capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Captures single frame: 6DoF Poses + Depth Map + Camera Pose", MessageType.Info);
            
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f);
            if (GUILayout.Button("CAPTURE FRAME", GUILayout.Height(50)))
            {
                CaptureFrame();
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Batch Processing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Continuously captures frames at specified intervals", MessageType.Info);
            
            EditorGUILayout.BeginHorizontal();
            isBatchProcessing = EditorGUILayout.ToggleLeft("Batch Processing", isBatchProcessing, GUILayout.Width(120));
            batchInterval = EditorGUILayout.Slider("Interval (s)", batchInterval, 0.1f, 20f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (!isBatchProcessing)
            {
                GUI.backgroundColor = new Color(0.2f, 1f, 0.2f);
                if (GUILayout.Button("Start Batch Process", GUILayout.Height(30)))
                {
                    StartBatchProcess();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.2f, 0.2f);
                if (GUILayout.Button("Stop Batch Process", GUILayout.Height(30)))
                {
                    StopBatchProcess();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            if (isBatchProcessing)
            {
                StopBatchProcess();
            }
        }

        /// <summary>
        /// Batch processing update loop
        /// </summary>
        private void BatchProcessUpdate()
        {
            if (!isBatchProcessing) return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - lastBatchCaptureTime >= batchInterval)
            {
                ExecuteCapture();
                lastBatchCaptureTime = currentTime;
            }
        }

        /// <summary>
        /// Main capture: 6DoF poses + depth map + camera pose
        /// </summary>
        private void ExecuteCapture()
        {
            Camera mainCam = Camera.main ?? FindObjectOfType<Camera>();
            if (mainCam == null)
            {
                Debug.LogError("[Frame Analyzer] No camera found in scene");
                return;
            }

            if (config == null)
            {
                config = CreateDefaultConfig();
            }

            if (!Directory.Exists(config.outputFolder))
            {
                Directory.CreateDirectory(config.outputFolder);
            }

            // Create frame data with 6DoF poses
            FrameData frameData = new FrameData
            {
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                camera = CaptureCameraData(mainCam),
                objects = CaptureObjectPoses(mainCam)
            };

            frameData.objectCount = frameData.objects.Count;

            // Capture depth map with post-processing effect
            string screenshotFilename = $"depth_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string screenshotPath = Path.Combine(config.outputFolder, screenshotFilename);
            
            if (depthVolumeProfile != null)
            {
                CaptureDepthWithPostProcessing(mainCam, screenshotPath);
            }
            else
            {
                Debug.LogWarning("[Frame Analyzer] No depth volume profile assigned, skipping depth capture");
            }

            frameData.screenshotPath = GetRelativePath(screenshotPath);

            // Export JSON
            string jsonFilename = $"frame_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string jsonPath = Path.Combine(config.outputFolder, jsonFilename);
            string json = JsonUtility.ToJson(frameData, true);
            File.WriteAllText(jsonPath, json);

            // Update status
            lastCaptureInfo = $"{frameData.objectCount} objects @ {frameData.timestamp}";
            lastOutputPath = GetRelativePath(jsonPath);

            AssetDatabase.Refresh();

            if (!isBatchProcessing)
            {
                Debug.Log($"[Frame Analyzer] Captured:\nJSON: {jsonPath}\nDepth: {screenshotPath}");
            }
        }

        /// <summary>
        /// Captures complete camera state data
        /// </summary>
        private CameraData CaptureCameraData(Camera cam)
        {
            return new CameraData
            {
                position = cam.transform.position,
                rotation = cam.transform.rotation,
                eulerAngles = cam.transform.eulerAngles,
                fov = cam.fieldOfView,
                nearClip = cam.nearClipPlane,
                farClip = cam.farClipPlane,
                aspectRatio = cam.aspect,
                projectionMatrix = new Matrix4x4Data(cam.projectionMatrix),
                worldToCameraMatrix = new Matrix4x4Data(cam.worldToCameraMatrix),
                pixelWidth = cam.pixelWidth,
                pixelHeight = cam.pixelHeight
            };
        }

        /// <summary>
        /// Collects 6DoF pose data for all objects in scene
        /// </summary>
        private List<ObjectPoseData> CaptureObjectPoses(Camera cam)
        {
            List<ObjectPoseData> poses = new List<ObjectPoseData>();
            GameObject[] allObjects = FindObjectsOfType<GameObject>(config.includeInactiveObjects);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

            foreach (GameObject obj in allObjects)
            {
                // Layer mask filter
                if ((config.captureLayerMask.value & (1 << obj.layer)) == 0)
                    continue;

                // Visibility check
                bool isVisible = true;
                if (config.onlyVisibleObjects)
                {
                    Renderer renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
                    }
                    else
                    {
                        isVisible = false;
                    }
                }

                if (!isVisible && config.onlyVisibleObjects)
                    continue;

                poses.Add(new ObjectPoseData
                {
                    name = obj.name,
                    path = GetHierarchyPath(obj.transform),
                    position = obj.transform.position,
                    rotation = obj.transform.rotation,
                    scale = obj.transform.lossyScale,
                    isVisible = isVisible,
                    layer = LayerMask.LayerToName(obj.layer),
                    tag = obj.tag
                });
            }

            return poses;
        }

        /// <summary>
        /// Gets full hierarchy path of a transform
        /// </summary>
        private string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Converts absolute path to project-relative path
        /// </summary>
        private string GetRelativePath(string absolutePath)
        {
            string projectPath = Application.dataPath.Replace("/Assets", "");
            return absolutePath.Replace(projectPath + "/", "");
        }

        /// <summary>
        /// Creates default config if none exists
        /// </summary>
        private SpatialReconstructionConfig CreateDefaultConfig()
        {
            string configPath = "Assets/_Roam Systems/Spatial Reconstruction/SpatialReconstructionConfig.asset";
            SpatialReconstructionConfig newConfig = CreateInstance<SpatialReconstructionConfig>();
            
            string configDir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            AssetDatabase.CreateAsset(newConfig, configPath);
            AssetDatabase.SaveAssets();
            return newConfig;
        }

        /// <summary>
        /// Creates parallel camera with depth post-processing, captures, then cleans up
        /// </summary>
        private void CaptureDepthWithPostProcessing(Camera sourceCamera, string outputPath)
        {
            int width = sourceCamera.pixelWidth * config.resolutionMultiplier;
            int height = sourceCamera.pixelHeight * config.resolutionMultiplier;

            GameObject tempCamObj = null;
            GameObject tempVolumeObj = null;

            try
            {
                // Create parallel camera (duplicate of source)
                tempCamObj = new GameObject("_TempDepthCamera");
                tempCamObj.hideFlags = HideFlags.HideAndDontSave;
                Camera tempCam = tempCamObj.AddComponent<Camera>();
                tempCam.CopyFrom(sourceCamera);
                tempCam.enabled = false;
                tempCam.transform.position = sourceCamera.transform.position;
                tempCam.transform.rotation = sourceCamera.transform.rotation;

                // Create global volume with depth effect profile
                tempVolumeObj = new GameObject("_TempDepthVolume");
                tempVolumeObj.hideFlags = HideFlags.HideAndDontSave;
                Volume tempVolume = tempVolumeObj.AddComponent<Volume>();
                tempVolume.isGlobal = true;
                tempVolume.priority = 999;
                tempVolume.profile = depthVolumeProfile;

                // Enable depth texture mode
                tempCam.depthTextureMode = DepthTextureMode.Depth;

                // Create render texture for camera output
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                tempCam.targetTexture = rt;

                // Render with post-processing
                tempCam.Render();

                // Capture screenshot from render texture
                RenderTexture.active = rt;
                Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                screenshot.Apply();
                RenderTexture.active = null;

                // Save to file
                byte[] bytes = screenshot.EncodeToPNG();
                File.WriteAllBytes(outputPath, bytes);

                // Cleanup textures
                Object.DestroyImmediate(screenshot);
                tempCam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
            }
            finally
            {
                // Always cleanup temp objects
                if (tempCamObj != null)
                    Object.DestroyImmediate(tempCamObj);
                if (tempVolumeObj != null)
                    Object.DestroyImmediate(tempVolumeObj);
            }
        }

        /// <summary>
        /// Checks if Depth Capture Renderer Feature is added to URP
        /// </summary>
        private bool IsRendererFeatureAdded()
        {
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset == null) return false;

            var renderer = urpAsset.GetRenderer(0);
            if (renderer == null) return false;

            var rendererDataType = renderer.GetType();
            var featuresProp = rendererDataType.GetProperty("rendererFeatures", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (featuresProp == null) return false;

            var features = featuresProp.GetValue(renderer) as System.Collections.IList;
            if (features == null) return false;

            foreach (var feature in features)
            {
                if (feature != null && feature.GetType().Name == "DepthCaptureRendererFeature")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds Depth Capture Renderer Feature to URP
        /// </summary>
        private void AddRendererFeature()
        {
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset == null)
            {
                EditorUtility.DisplayDialog("Error", "No URP asset found", "OK");
                return;
            }

            EditorUtility.DisplayDialog("Manual Step Required", 
                "Please add the 'Depth Capture' Renderer Feature manually:\n\n" +
                "1. Select your URP Renderer asset\n" +
                "2. Click 'Add Renderer Feature'\n" +
                "3. Select 'Depth Capture Renderer Feature'\n\n" +
                "This is located in:\nProject Settings > Graphics > URP Renderer", 
                "OK");
        }

        private string AddToRendererButtonLabel => IsRendererFeatureAdded() ? 
            "Renderer Feature Active" : "Add to Renderer Pipeline";

        private Color RendererButtonColor => IsRendererFeatureAdded() ? 
            new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.2f, 0.2f);
    }
}
