using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roam.SpatialReconstruction
{
    /// <summary>
    /// Complete frame capture data with camera info, object poses, and depth screenshot
    /// </summary>
    [Serializable]
    public class FrameData
    {
        public string timestamp;
        public CameraData camera;
        public List<ObjectPoseData> objects;
        public string screenshotPath;
        public int objectCount;
    }

    /// <summary>
    /// 6DoF pose data for a single object
    /// </summary>
    [Serializable]
    public class ObjectPoseData
    {
        public string name;
        public string path; // Full hierarchy path
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool isVisible;
        public string layer;
        public string tag;
    }

    /// <summary>
    /// Complete camera state including position, rotation, and projection data
    /// </summary>
    [Serializable]
    public class CameraData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 eulerAngles;
        public float fov;
        public float nearClip;
        public float farClip;
        public float aspectRatio;
        public Matrix4x4Data projectionMatrix;
        public Matrix4x4Data worldToCameraMatrix;
        public int pixelWidth;
        public int pixelHeight;
    }

    /// <summary>
    /// Serializable Matrix4x4 wrapper
    /// </summary>
    [Serializable]
    public class Matrix4x4Data
    {
        public float[] values = new float[16];

        public Matrix4x4Data(Matrix4x4 matrix)
        {
            values[0] = matrix.m00; values[1] = matrix.m01; values[2] = matrix.m02; values[3] = matrix.m03;
            values[4] = matrix.m10; values[5] = matrix.m11; values[6] = matrix.m12; values[7] = matrix.m13;
            values[8] = matrix.m20; values[9] = matrix.m21; values[10] = matrix.m22; values[11] = matrix.m23;
            values[12] = matrix.m30; values[13] = matrix.m31; values[14] = matrix.m32; values[15] = matrix.m33;
        }
    }
}
