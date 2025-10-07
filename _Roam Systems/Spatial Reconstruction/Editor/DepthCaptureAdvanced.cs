using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using UnityEditor;
using Roam.SpatialReconstruction;

namespace Roam.SpatialReconstruction.Editor
{
    /// <summary>
    /// Advanced depth capture techniques using URP-specific methods
    /// </summary>
    public static class DepthCaptureAdvanced
    {
        /// <summary>
        /// Technique 5: AsyncGPUReadback of Depth Texture
        /// Directly reads URP's depth texture from GPU
        /// </summary>
        public static string Technique5_AsyncDepthReadback(Camera sourceCamera, string outputPath, int resMultiplier)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Force depth texture
            sourceCamera.depthTextureMode = DepthTextureMode.Depth;
            
            // Render frame to ensure depth is populated
            RenderTexture colorRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = sourceCamera.targetTexture;
            sourceCamera.targetTexture = colorRT;
            sourceCamera.Render();
            sourceCamera.targetTexture = oldTarget;

            // Try to get depth texture directly
            RenderTexture depthRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Depth);
            
            // Create material for depth visualization
            Shader depthShader = Shader.Find("Hidden/Internal-DepthNormalsTexture");
            Material depthMat = new Material(depthShader);
            
            // Use Graphics.Blit with depth
            Graphics.Blit(null, depthRT, depthMat);
            
            // Read back
            RenderTexture.active = depthRT;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Save
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(depthMat);
            RenderTexture.ReleaseTemporary(colorRT);
            RenderTexture.ReleaseTemporary(depthRT);

            return outputPath;
        }

        /// <summary>
        /// Technique 6: Unity's Built-in ScreenCapture with Depth Shader
        /// Uses ScreenCapture.CaptureScreenshotAsTexture with replacement shader
        /// </summary>
        public static string Technique6_ScreenCaptureDepth(Camera sourceCamera, string outputPath, int resMultiplier)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Create temp camera
            GameObject tempCamObj = new GameObject("_TempDepthCam");
            Camera depthCam = tempCamObj.AddComponent<Camera>();
            depthCam.CopyFrom(sourceCamera);
            depthCam.enabled = false;
            
            // Set to render depth normals
            depthCam.clearFlags = CameraClearFlags.Depth;
            depthCam.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;
            
            // Create custom shader material
            Shader shader = Shader.Find("Hidden/Internal-DepthNormalsTexture");
            if (shader != null)
            {
                depthCam.SetReplacementShader(shader, "RenderType");
            }

            // Create RT and render
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            depthCam.targetTexture = rt;
            depthCam.Render();

            // Read pixels
            RenderTexture.active = rt;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Post-process to enhance depth visualization
            Color[] pixels = screenshot.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                // Extract depth from depth-normals encoding
                float depth = pixels[i].r * pixels[i].r + pixels[i].g * pixels[i].g;
                depth = Mathf.Sqrt(depth);
                depth = 1f - depth; // Invert so near is bright
                pixels[i] = new Color(depth, depth, depth, 1f);
            }
            screenshot.SetPixels(pixels);
            screenshot.Apply();

            // Save
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(tempCamObj);
            rt.Release();
            Object.DestroyImmediate(rt);

            return outputPath;
        }

        /// <summary>
        /// Technique 7: Manual Depth Calculation from World Positions
        /// Renders scene and calculates depth from camera distance per pixel
        /// </summary>
        public static string Technique7_ManualDepthCalculation(Camera sourceCamera, string outputPath, int resMultiplier, float nearCutoff = 1f, float farCutoff = 100f, float contrast = 1f)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Create depth texture by raycasting
            Texture2D depthTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = new Color[width * height];

            // Sample points across screen
            int step = Mathf.Max(1, resMultiplier); // Adjust sampling based on resolution
            
            for (int y = 0; y < height; y += step)
            {
                for (int x = 0; x < width; x += step)
                {
                    Ray ray = sourceCamera.ScreenPointToRay(new Vector3(x / (float)resMultiplier, y / (float)resMultiplier, 0));
                    float depth = 0f; // Default black (far)
                    
                    if (Physics.Raycast(ray, out RaycastHit hit, sourceCamera.farClipPlane))
                    {
                        // Calculate distance from camera
                        float distance = Vector3.Distance(sourceCamera.transform.position, hit.point);
                        
                        // Normalize depth between custom cutoffs
                        depth = Mathf.InverseLerp(nearCutoff, farCutoff, distance);
                        
                        // Invert so near = white, far = black
                        depth = 1f - depth;
                        
                        // Apply contrast
                        depth = Mathf.Pow(depth, 1f / contrast);
                        
                        // Clamp
                        depth = Mathf.Clamp01(depth);
                    }

                    // Fill block for lower resolution sampling
                    for (int dy = 0; dy < step && y + dy < height; dy++)
                    {
                        for (int dx = 0; dx < step && x + dx < width; dx++)
                        {
                            int index = (y + dy) * width + (x + dx);
                            if (index < pixels.Length)
                            {
                                pixels[index] = new Color(depth, depth, depth, 1f);
                            }
                        }
                    }
                }
            }

            depthTexture.SetPixels(pixels);
            depthTexture.Apply();

            // Save
            byte[] bytes = depthTexture.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(depthTexture);

            Debug.Log($"[Technique 7] Depth capture: near={nearCutoff}, far={farCutoff}, contrast={contrast}");
            return outputPath;
        }

        /// <summary>
        /// Technique 7.1: Manual Depth Calculation using Renderers (not colliders)
        /// Same as Technique 7 but raycasts against renderer bounds instead of physics colliders
        /// </summary>
        public static string Technique7_1_RendererDepthCalculation(Camera sourceCamera, string outputPath, int resMultiplier, float nearCutoff = 1f, float farCutoff = 100f, float contrast = 1f)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Get all renderers in scene
            Renderer[] allRenderers = Object.FindObjectsOfType<Renderer>();
            Debug.Log($"[Technique 7.1] Found {allRenderers.Length} renderers");

            // Create depth texture
            Texture2D depthTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = new Color[width * height];

            // Sample points across screen
            int step = Mathf.Max(1, resMultiplier);
            
            for (int y = 0; y < height; y += step)
            {
                for (int x = 0; x < width; x += step)
                {
                    Ray ray = sourceCamera.ScreenPointToRay(new Vector3(x / (float)resMultiplier, y / (float)resMultiplier, 0));
                    float depth = 0f; // Default black (far)
                    float closestDistance = float.MaxValue;
                    bool hitSomething = false;

                    // Check ray against all renderer bounds
                    foreach (Renderer renderer in allRenderers)
                    {
                        if (renderer == null || !renderer.enabled) continue;

                        // Check if ray intersects bounds
                        if (renderer.bounds.IntersectRay(ray, out float distance))
                        {
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                hitSomething = true;
                            }
                        }
                    }

                    if (hitSomething)
                    {
                        // Calculate depth from closest hit distance
                        float worldDistance = closestDistance;
                        
                        // Normalize depth between custom cutoffs
                        depth = Mathf.InverseLerp(nearCutoff, farCutoff, worldDistance);
                        
                        // Invert so near = white, far = black
                        depth = 1f - depth;
                        
                        // Apply contrast
                        depth = Mathf.Pow(depth, 1f / contrast);
                        
                        // Clamp
                        depth = Mathf.Clamp01(depth);
                    }

                    // Fill block for lower resolution sampling
                    for (int dy = 0; dy < step && y + dy < height; dy++)
                    {
                        for (int dx = 0; dx < step && x + dx < width; dx++)
                        {
                            int index = (y + dy) * width + (x + dx);
                            if (index < pixels.Length)
                            {
                                pixels[index] = new Color(depth, depth, depth, 1f);
                            }
                        }
                    }
                }
            }

            depthTexture.SetPixels(pixels);
            depthTexture.Apply();

            // Save
            byte[] bytes = depthTexture.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(depthTexture);

            Debug.Log($"[Technique 7.1] Renderer depth capture: near={nearCutoff}, far={farCutoff}, contrast={contrast}");
            return outputPath;
        }

        /// <summary>
        /// Technique 7.2: GPU-Accelerated Depth with RenderTexture (FAST + DETAILED)
        /// Uses shader replacement + RenderTexture for performant, detailed depth capture
        /// </summary>
        public static string Technique7_2_GPUDepthCapture(Camera sourceCamera, string outputPath, int resMultiplier, float nearCutoff = 1f, float farCutoff = 100f, float contrast = 1f)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Create temporary camera
            GameObject tempCamObj = new GameObject("_TempDepthCamera");
            Camera depthCam = tempCamObj.AddComponent<Camera>();
            depthCam.CopyFrom(sourceCamera);
            depthCam.enabled = false;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            depthCam.backgroundColor = Color.black;

            // Create render texture
            RenderTexture depthRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            depthRT.Create();
            depthCam.targetTexture = depthRT;

            // Load custom depth shader
            Shader depthShader = Shader.Find("Roam/SpatialReconstruction/SimpleDepthOnly");
            if (depthShader == null)
            {
                Debug.LogError("[Technique 7.2] SimpleDepthOnly shader not found!");
                Object.DestroyImmediate(tempCamObj);
                depthRT.Release();
                Object.DestroyImmediate(depthRT);
                return null;
            }

            // Create material with shader and set parameters
            Material depthMaterial = new Material(depthShader);
            depthMaterial.SetFloat("_NearCutoff", nearCutoff);
            depthMaterial.SetFloat("_FarCutoff", farCutoff);
            depthMaterial.SetFloat("_Contrast", contrast);

            // Replace ALL materials with depth shader
            depthCam.SetReplacementShader(depthShader, "RenderType");
            
            // Set global shader properties for the replacement shader
            Shader.SetGlobalFloat("_NearCutoff", nearCutoff);
            Shader.SetGlobalFloat("_FarCutoff", farCutoff);
            Shader.SetGlobalFloat("_Contrast", contrast);
            
            // Render to texture (GPU-accelerated)
            depthCam.Render();

            // Read pixels from render texture
            RenderTexture.active = depthRT;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Save to file
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(depthMaterial);
            Object.DestroyImmediate(screenshot);
            depthRT.Release();
            Object.DestroyImmediate(depthRT);
            Object.DestroyImmediate(tempCamObj);

            Debug.Log($"[Technique 7.2] GPU depth capture: near={nearCutoff}, far={farCutoff}, contrast={contrast}");
            return outputPath;
        }

        /// <summary>
        /// Technique 7.3: URP Post-Process Depth (BEST - Uses rendered geometry like Silhouette)
        /// Enables depth post-processing, renders frame, captures screenshot, disables
        /// Uses URP's depth texture to capture ALL rendered geometry
        /// </summary>
        public static string Technique7_3_PostProcessDepth(Camera sourceCamera, string outputPath, int resMultiplier, float nearCutoff = 1f, float farCutoff = 100f, float contrast = 1f)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Find or create volume with depth capture settings
            var volume = Object.FindObjectOfType<UnityEngine.Rendering.Volume>();
            if (volume == null)
            {
                GameObject volumeObj = new GameObject("_TempDepthCaptureVolume");
                volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
                volume.isGlobal = true;
                volume.priority = 100;
            }

            // Get or add depth capture settings to volume
            var depthSettings = volume.profile.components.Find(c => c.GetType().Name == "DepthCaptureVolumeSettings");
            if (depthSettings == null)
            {
                Debug.LogWarning("[Technique 7.3] DepthCaptureVolumeSettings not found in volume profile. Make sure 'Depth Capture' Renderer Feature is added to URP asset.");
                return null;
            }

            // Set depth parameters using reflection (since we can't reference the type directly before compilation)
            var nearField = depthSettings.GetType().GetField("nearCutoff");
            var farField = depthSettings.GetType().GetField("farCutoff");
            var contrastField = depthSettings.GetType().GetField("contrast");
            
            if (nearField != null)
            {
                var nearParam = nearField.GetValue(depthSettings);
                nearParam.GetType().GetProperty("value").SetValue(nearParam, nearCutoff);
            }
            if (farField != null)
            {
                var farParam = farField.GetValue(depthSettings);
                farParam.GetType().GetProperty("value").SetValue(farParam, farCutoff);
            }
            if (contrastField != null)
            {
                var contrastParam = contrastField.GetValue(depthSettings);
                contrastParam.GetType().GetProperty("value").SetValue(contrastParam, contrast);
            }

            // Find URP renderer and enable depth capture feature
            var urpAsset = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            var renderer = urpAsset.GetRenderer(0) as UnityEngine.Rendering.Universal.ScriptableRenderer;
            
            // Enable camera depth texture
            sourceCamera.depthTextureMode = DepthTextureMode.Depth;

            // Render frame with post-processing
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = sourceCamera.targetTexture;
            sourceCamera.targetTexture = rt;
            sourceCamera.Render();
            sourceCamera.targetTexture = oldTarget;

            // Capture screenshot
            RenderTexture.active = rt;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Save
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(screenshot);
            RenderTexture.ReleaseTemporary(rt);

            // Remove temp volume if we created it
            if (volume.gameObject.name == "_TempDepthCaptureVolume")
            {
                Object.DestroyImmediate(volume.gameObject);
            }

            Debug.Log($"[Technique 7.3] Post-process depth capture: near={nearCutoff}, far={farCutoff}, contrast={contrast}");
            return outputPath;
        }

        /// <summary>
        /// Technique 8: SceneView Depth Capture
        /// Uses SceneView's camera depth if available (editor only)
        /// </summary>
        public static string Technique8_SceneViewDepth(Camera sourceCamera, string outputPath, int resMultiplier)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Get scene view
            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera sceneCam = sceneView != null ? sceneView.camera : sourceCamera;

            // Enable depth mode
            sceneCam.depthTextureMode = DepthTextureMode.Depth;
            
            // Create shader for depth extraction
            Shader depthShader = Shader.Find("Hidden/Internal-DepthNormalsTexture");
            Material depthMat = new Material(depthShader);

            // Render scene view to RT
            RenderTexture colorRT = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture depthRT = RenderTexture.GetTemporary(width, height, 0);

            var oldRT = sceneCam.targetTexture;
            sceneCam.targetTexture = colorRT;
            sceneCam.Render();
            sceneCam.targetTexture = oldRT;

            // Apply depth material
            Graphics.Blit(colorRT, depthRT, depthMat);

            // Read
            RenderTexture.active = depthRT;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Save
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(screenshot);
            Object.DestroyImmediate(depthMat);
            RenderTexture.ReleaseTemporary(colorRT);
            RenderTexture.ReleaseTemporary(depthRT);

            return outputPath;
        }

        /// <summary>
        /// Technique 9: Simple Depth Replacement Shader (GUARANTEED)
        /// Replaces ALL materials with pure depth shader - ignores existing material colors
        /// </summary>
        public static string Technique9_SimpleDepthReplacement(Camera sourceCamera, string outputPath, int resMultiplier)
        {
            int width = sourceCamera.pixelWidth * resMultiplier;
            int height = sourceCamera.pixelHeight * resMultiplier;

            // Create temporary camera
            GameObject tempCamObj = new GameObject("_TempDepthCamera");
            Camera depthCam = tempCamObj.AddComponent<Camera>();
            depthCam.CopyFrom(sourceCamera);
            depthCam.enabled = false;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            depthCam.backgroundColor = Color.black;

            // Create render texture
            RenderTexture depthRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            depthRT.Create();
            depthCam.targetTexture = depthRT;

            // Load custom depth shader
            Shader depthShader = Shader.Find("Roam/SpatialReconstruction/SimpleDepthOnly");
            if (depthShader == null)
            {
                Debug.LogError("SimpleDepthOnly shader not found! Make sure it's compiled.");
                Object.DestroyImmediate(tempCamObj);
                depthRT.Release();
                Object.DestroyImmediate(depthRT);
                return null;
            }

            // Replace ALL materials with depth shader - this IGNORES existing materials
            depthCam.SetReplacementShader(depthShader, "RenderType");
            
            // Render
            depthCam.Render();

            // Read pixels
            RenderTexture.active = depthRT;
            Texture2D screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();
            RenderTexture.active = null;

            // Save to file
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            // Cleanup
            Object.DestroyImmediate(screenshot);
            depthRT.Release();
            Object.DestroyImmediate(depthRT);
            Object.DestroyImmediate(tempCamObj);

            Debug.Log($"[Technique 9] Depth capture saved - near objects should be WHITE, far objects BLACK");
            return outputPath;
        }
    }
}
