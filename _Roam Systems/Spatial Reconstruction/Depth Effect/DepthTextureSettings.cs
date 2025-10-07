namespace Roam
{
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;

    [System.Serializable, VolumeComponentMenu("Roam/Depth Texture")]
    public sealed class DepthTextureSettings : VolumeComponent, IPostProcessComponent
    {
        public DepthTextureSettings()
        {
            displayName = "Depth Texture";
        }

        [Tooltip("Choose where to insert this pass in URP's render loop.")]
        public RenderPassEventParameter renderPassEvent = new RenderPassEventParameter(RenderPassEvent.BeforeRenderingPostProcessing);

        [Tooltip("Is the effect active?")]
        public BoolParameter enabled = new BoolParameter(false);

        [Tooltip("Color at the camera's near clip plane.")]
        public ColorParameter nearColor = new ColorParameter(new Color(1.0f, 1.0f, 1.0f, 1.0f));

        [Tooltip("Color at the camera's far clip plane.")]
        public ColorParameter farColor = new ColorParameter(new Color(0.0f, 0.0f, 0.0f, 1.0f));

        [Tooltip("Distance in world units where near color starts (white). Objects closer than this are fully near color.")]
        public ClampedFloatParameter nearDistance = new ClampedFloatParameter(0.01f, 0.01f, 100f);

        [Tooltip("Distance in world units where far color ends (black). Objects further than this are fully far color.")]
        public ClampedFloatParameter farDistance = new ClampedFloatParameter(117f, 0.1f, 1000f);

        [Tooltip("Custom depth falloff curve. X-axis = linear depth (0-1), Y-axis = remapped depth (0-1).")]
        public AnimationCurveParameter depthCurve = new AnimationCurveParameter(
            new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1))
        );

        [Tooltip("Resolution of the curve lookup texture. Higher = more precise but more memory.")]
        public ClampedIntParameter curveLUTResolution = new ClampedIntParameter(256, 64, 1024);

        [Tooltip("Controls how much shadows affect the depth texture. 0 = no shadow influence, 1 = full shadow influence.")]
        public ClampedFloatParameter shadowInfluence = new ClampedFloatParameter(0.5f, 0f, 1f);

        public bool IsActive()
        {
            return enabled.value && active;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }

    /// Custom parameter for AnimationCurve in volume system
    [System.Serializable]
    public sealed class AnimationCurveParameter : VolumeParameter<AnimationCurve>
    {
        public AnimationCurveParameter(AnimationCurve value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    /// Custom parameter for RenderPassEvent in volume system
    [System.Serializable]
    public sealed class RenderPassEventParameter : VolumeParameter<RenderPassEvent>
    {
        public RenderPassEventParameter(RenderPassEvent value, bool overrideState = false)
            : base(value, overrideState) { }
    }
}
