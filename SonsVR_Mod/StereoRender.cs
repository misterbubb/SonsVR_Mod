using MelonLoader;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Valve.VR;
using static SonsVRMod.SonsVRMod;
using Il2CppEndnight.Utilities;
using Mathf = UnityEngine.Mathf;

namespace SonsVRMod
{
    /// <summary>
    /// Manages stereo rendering for VR by creating left/right eye cameras,
    /// rendering to separate RenderTextures, and submitting frames to OpenVR Compositor.
    /// This class replaces Unity's built-in VR pipeline with a custom one compatible with modded scenes.
    /// </summary>
    internal class StereoRender : MonoBehaviour
    {
        public StereoRender(IntPtr value) : base(value) { }

        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        public static StereoRender Instance;

        /// <summary>
        /// Transform representing the HMD (Head-Mounted Display).
        /// This follows the real-world HMD orientation.
        /// </summary>
        public Transform Head;

        /// <summary>
        /// Reference to the original game camera (usually FPS view).
        /// Not directly used in rendering but may be needed for position synchronization.
        /// </summary>
        public Camera HeadCam;

        /// <summary>
        /// Dedicated cameras for left and right eyes.
        /// These render the scene from slightly offset viewpoints to create stereoscopy.
        /// </summary>
        public Camera LeftCam, RightCam;

        /// <summary>
        /// Render targets for left and right eyes.
        /// Submitted to OpenVR compositor for final display in the headset.
        /// </summary>
        public RenderTexture LeftRT, RightRT;

        /// <summary>
        /// Interpupillary distance (IPD) in meters. Default ~64mm.
        /// Controls the horizontal separation between left and right eye views.
        /// </summary>
        public float separation = 0.064f;

        /// <summary>
        /// Near clipping plane distance (in meters).
        /// </summary>
        private float clipStart = 0.1f;

        /// <summary>
        /// Far clipping plane distance (in meters).
        /// </summary>
        private float clipEnd = 1000f;

        /// <summary>
        /// Culling mask used by the original game camera.
        /// Applied to both eye cameras to ensure consistent visibility of game objects.
        /// </summary>
        public static int defaultCullingMask = -1; // -1 = everything

        /// <summary>
        /// Current render resolution (width/height) based on SteamVR’s recommended eye texture size.
        /// </summary>
        private int currentWidth, currentHeight;

        /// <summary>
        /// Custom render pass that submits left/right textures to the OpenVR compositor.
        /// </summary>
        public StereoRenderPass stereoRenderPass;

        // Pose arrays for OpenVR: one for rendering, one for game logic (unused here but required by API)
        private readonly TrackedDevicePose_t[] renderPoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private readonly TrackedDevicePose_t[] gamePoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        /// <summary>
        /// Unity callback called when the script instance is being loaded.
        /// Initializes the stereo rendering pipeline.
        /// </summary>
        public void Awake()
        {
            Instance = this;
            Setup();
        }

        /// <summary>
        /// Creates the HMD rig (Head + LeftEye/RightEye), configures cameras,
        /// sets up render textures, and initializes the stereo render pass.
        /// </summary>
        public void Setup()
        {
            MelonLogger.Msg($"[StereoRender] ### SETTING UP STEREO RENDERING PIPELINE ###");

            // Create or find the HMD transform
            Head = transform.Find("Head");
            if (!Head)
            {
                Head = new GameObject("Head").transform;
            }
            Head.SetParent(transform, false);
            Head.localPosition = Vector3.zero;
            Head.localRotation = Quaternion.identity;

            // Mark this object as tracked by SteamVR (HMD)
            Head.gameObject.GetOrAddComponent<SteamVR_TrackedObject>().index = SteamVR_TrackedObject.EIndex.Hmd;

            // --- LEFT EYE ---
            var leftEye = Head.Find("LeftEye");
            if (!leftEye)
            {
                leftEye = new GameObject("LeftEye").transform;
            }
            leftEye.SetParent(Head, false);
            leftEye.localPosition = new Vector3(-separation * 0.5f, 0, 0); // Offset left
            leftEye.localRotation = Quaternion.identity;

            LeftCam = leftEye.gameObject.GetOrAddComponent<Camera>();
            LeftCam.cullingMask = defaultCullingMask;
            LeftCam.stereoTargetEye = StereoTargetEyeMask.None; // We handle stereo manually
            LeftCam.clearFlags = CameraClearFlags.Skybox;
            LeftCam.nearClipPlane = clipStart;
            LeftCam.farClipPlane = clipEnd;
            LeftCam.fieldOfView = 109.363f; // Matches typical headset FoV
            LeftCam.depth = 0;
            LeftCam.enabled = true;

            // --- RIGHT EYE ---
            var rightEye = Head.Find("RightEye");
            if (!rightEye)
            {
                rightEye = new GameObject("RightEye").transform;
            }
            rightEye.SetParent(Head, false);
            rightEye.localPosition = new Vector3(separation * 0.5f, 0, 0); // Offset right
            rightEye.localRotation = Quaternion.identity;

            RightCam = rightEye.gameObject.GetOrAddComponent<Camera>();
            RightCam.cullingMask = defaultCullingMask;
            RightCam.stereoTargetEye = StereoTargetEyeMask.None;
            RightCam.clearFlags = CameraClearFlags.Skybox;
            RightCam.nearClipPlane = clipStart;
            RightCam.farClipPlane = clipEnd;
            RightCam.fieldOfView = 109.363f;
            RightCam.depth = 0;
            RightCam.enabled = true;

            // Apply projection matrices from SteamVR (non-linear, per-eye)
            UpdateProjectionMatrix();

            // Allocate render textures at recommended resolution
            UpdateResolution();

            // Initialize the OpenVR submission pass
            stereoRenderPass = new StereoRenderPass(this);
        }

        /// <summary>
        /// Dynamically updates the culling mask of both eye cameras.
        /// Useful for temporarily hiding UI, weapons, or other layers in VR.
        /// </summary>
        /// <param name="mask">New culling mask (bitwise layer mask).</param>
        public void SetCameraMask(int mask)
        {
            LeftCam.cullingMask = mask;
            RightCam.cullingMask = mask;
        }

        /// <summary>
        /// Fetches per-eye projection matrices directly from OpenVR.
        /// Ensures correct lens distortion and field-of-view matching the physical headset.
        /// </summary>
        public void UpdateProjectionMatrix()
        {
            var leftProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Left, clipStart, clipEnd);
            var rightProj = OpenVR.System.GetProjectionMatrix(EVREye.Eye_Right, clipStart, clipEnd);

            LeftCam.projectionMatrix = leftProj.ConvertToMatrix4x4();
            RightCam.projectionMatrix = rightProj.ConvertToMatrix4x4();
        }

        /// <summary>
        /// Recreates left/right RenderTextures at the current recommended SteamVR resolution.
        /// Called at startup and when resolution changes (e.g., dynamic resolution scaling).
        /// </summary>
        public void UpdateResolution()
        {
            // Use SteamVR's recommended eye texture size, with fallbacks
            currentWidth = (SteamVR.instance.sceneWidth > 0) ? (int)SteamVR.instance.sceneWidth : 2208;
            currentHeight = (SteamVR.instance.sceneHeight > 0) ? (int)SteamVR.instance.sceneHeight : 2452;

            // Clean up old textures
            if (LeftRT != null) Destroy(LeftRT);
            if (RightRT != null) Destroy(RightRT);

            // Create new render textures
            LeftRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };
            RightRT = new RenderTexture(currentWidth, currentHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2
            };

            // Assign as render targets
            LeftCam.targetTexture = LeftRT;
            RightCam.targetTexture = RightRT;
        }

        /// <summary>
        /// Called when the component is destroyed.
        /// Cleans up the singleton reference.
        /// </summary>
        public void OnDestroy()
        {
            Instance = null;
        }

        /// <summary>
        /// Checks every physics frame if the recommended render resolution has changed,
        /// and updates textures if needed (e.g., due to dynamic resolution or supersampling changes).
        /// </summary>
        public void FixedUpdate()
        {
            // Allow a small tolerance (-1 pixel) to avoid unnecessary updates
            if (currentWidth < (int)SteamVR.instance.sceneWidth - 1 ||
                currentHeight < (int)SteamVR.instance.sceneHeight - 1)
            {
                UpdateResolution();
            }
        }

        /// <summary>
        /// Updates the HMD orientation from OpenVR and refreshes projection matrices.
        /// Called after all Update() methods, ensuring camera pose is up-to-date before rendering.
        /// </summary>
        public void LateUpdate()
        {
            // Fetch latest device poses from OpenVR compositor
            OpenVR.Compositor.WaitGetPoses(renderPoseArray, gamePoseArray);

            // Apply HMD rotation to the Head transform
            var hmdPose = renderPoseArray[(int)OpenVR.k_unTrackedDeviceIndex_Hmd];
            if (hmdPose.bPoseIsValid)
            {
                Head.localRotation = hmdPose.mDeviceToAbsoluteTracking.GetRotation();
            }

            // Refresh projection matrices (they can change with IPD or calibration)
            UpdateProjectionMatrix();
        }

        /// <summary>
        /// Custom render pass that submits left and right eye textures to the OpenVR compositor.
        /// This is the final step that makes the rendered frames visible in the VR headset.
        /// </summary>
        public class StereoRenderPass
        {
            private readonly StereoRender stereoRender;
            public bool isRendering;

            public StereoRenderPass(StereoRender stereoRender)
            {
                this.stereoRender = stereoRender;
            }

            /// <summary>
            /// Submits the left and right eye RenderTextures to OpenVR for display.
            /// Note: Texture UV bounds are flipped vertically (vMin=1, vMax=0) because
            /// Unity’s texture coordinate system is top-left origin, while OpenVR expects bottom-left.
            /// </summary>
            public void Execute()
            {
                if (!stereoRender.enabled)
                    return;

                // Wrap Unity render texture handles into OpenVR-compatible Texture_t structs
                var leftTex = new Texture_t
                {
                    handle = stereoRender.LeftRT.GetNativeTexturePtr(),
                    eType = SteamVR.instance.textureType,
                    eColorSpace = EColorSpace.Auto
                };

                var rightTex = new Texture_t
                {
                    handle = stereoRender.RightRT.GetNativeTexturePtr(),
                    eType = SteamVR.instance.textureType,
                    eColorSpace = EColorSpace.Auto
                };

                // Flip V coordinates to match OpenVR's expected texture layout
                var textureBounds = new VRTextureBounds_t
                {
                    uMin = 0,
                    vMin = 1, // Top in Unity = bottom in OpenVR
                    uMax = 1,
                    vMax = 0  // Bottom in Unity = top in OpenVR
                };

                // Submit frames to compositor
                EVRCompositorError errorL = OpenVR.Compositor.Submit(EVREye.Eye_Left, ref leftTex, ref textureBounds, EVRSubmitFlags.Submit_Default);
                EVRCompositorError errorR = OpenVR.Compositor.Submit(EVREye.Eye_Right, ref rightTex, ref textureBounds, EVRSubmitFlags.Submit_Default);

                // Optional: log errors during development
                // if (errorL != EVRCompositorError.None) MelonLogger.Warning($"Left eye submit error: {errorL}");
                // if (errorR != EVRCompositorError.None) MelonLogger.Warning($"Right eye submit error: {errorR}");
            }
        }
    }
}