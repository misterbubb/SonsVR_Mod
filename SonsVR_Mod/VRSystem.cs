using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonsVRMod
{
    /// <summary>
    /// Central manager responsible for detecting the active gameplay scene and camera,
    /// and spawning/refreshing the VR player rig when needed.
    /// Acts as the bridge between the game's dynamic scene/camera system and the VR subsystem.
    /// </summary>
    internal class VRSystem : MonoBehaviour
    {
        public VRSystem(IntPtr value) : base(value) { }

        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        public static VRSystem Instance { get; private set; }

        // Frame counter to limit how often we scan for scene/camera changes (performance optimization)
        private int UpdateCounter = 0;

        /// <summary>
        /// Reference to the currently active in-game camera (e.g., MainCameraFP).
        /// Used as the positional source for the VR rig.
        /// </summary>
        private Camera UsedCamera = null;

        /// <summary>
        /// Name of the scene currently being used for gameplay.
        /// Helps detect scene transitions (e.g., loading screen → main world).
        /// </summary>
        private string UsedSceneName = "";

        /// <summary>
        /// Flag indicating whether the player rig needs to be respawned after a scene change.
        /// Currently unused but reserved for future logic.
        /// </summary>
        private bool RespawnPlayer = false;

        /// <summary>
        /// Indicates whether the VR camera rig has already been created.
        /// Prevents duplicate rig instantiation.
        /// </summary>
        private bool RigCreated = false;

        /// <summary>
        /// Unity callback called when the object is initialized.
        /// Sets up singleton, ensures persistence across scenes, and subscribes to scene events.
        /// </summary>
        private void Awake()
        {
            MelonLogger.Msg("########### INITIALIZING VR SYSTEM #############");

            // Enforce singleton pattern
            if (Instance != null)
            {
                MelonLogger.Error("Trying to create duplicate VRSystem instance! Disabling this one.");
                enabled = false;
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // Survive scene transitions

            // Subscribe to scene load events (currently used for cleanup/extensibility)
            SonsVRMod.onSceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Main logic loop, executed after all Update() calls.
        /// Periodically scans for the active gameplay scene and camera,
        /// and triggers VR rig creation or update when they change.
        /// </summary>
        private void LateUpdate()
        {
            UpdateCounter++;

            // Only check every ~50 frames (~0.8s at 60 FPS) to reduce overhead
            if (UpdateCounter >= 50)
            {
                UpdateCounter = 0;

                // Locate the most relevant active scene and its main camera
                var sceneAndCam = FindActiveGameplayScene();

                // Only proceed in known gameplay scenes (avoid menus, loading screens, etc.)
                if ("BlankScene SonsMain SonsTitleScene".Contains(sceneAndCam.scene.name))
                {
                    var activeCam = sceneAndCam.camera;
                    if (activeCam != null)
                    {
                        // Detect scene or camera change
                        if (sceneAndCam.scene.name != UsedSceneName || UsedCamera == null || activeCam != UsedCamera)
                        {
                            MelonLogger.Warning($"[VRSystem] Scene or camera changed — respawning VR player rig");
                            UsedSceneName = sceneAndCam.scene.name;
                            UsedCamera = activeCam;

                            // Create the VR rig if not already present
                            if (!RigCreated)
                            {
                                CreateCameraRig(UsedCamera);
                            }
                        }
                        else
                        {
                            // Scene and camera are stable — just refresh the VR player's reference
                            if (VRPlayer.Instance != null && sceneAndCam.camera != null)
                            {
                                VRPlayer.Instance.SetSceneAndCamera(sceneAndCam);
                            }
                        }
                    }
                    else
                    {
                        MelonLogger.Msg($"[VRSystem] No active camera found in scene: {sceneAndCam.scene.name}");
                    }
                }
                else
                {
                    // Not a gameplay scene — clear references
                    UsedCamera = null;
                    // Optional: hide VR rig here if needed
                }
            }
        }

        /// <summary>
        /// Scans all loaded scenes to find the one containing an active gameplay camera.
        /// Prioritizes "MainCameraFP" (the game's standard first-person camera),
        /// but falls back to any enabled camera if not found.
        /// </summary>
        /// <returns>A struct containing the scene and its active camera (or null if none found).</returns>
        private ActiveSceneWithCamera FindActiveGameplayScene()
        {
            ActiveSceneWithCamera result = new ActiveSceneWithCamera();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                result.scene = scene;
                bool hasActiveCamera = false;

                // Search all root objects in the scene for cameras
                var rootObjects = scene.GetRootGameObjects();
                foreach (var root in rootObjects)
                {
                    var cameras = root.GetComponentsInChildren<Camera>(true);
                    foreach (var cam in cameras)
                    {
                        if (!cam.isActiveAndEnabled)
                            continue;

                        // Prefer the game's known FPS camera
                        if (cam.name.Equals("MainCameraFP"))
                        {
                            result.camera = cam;
                            hasActiveCamera = true;
                            break;
                        }

                        // Fallback: accept any active camera
                        result.camera = cam;
                        hasActiveCamera = true;
                        break;
                    }
                    if (hasActiveCamera)
                        break;
                }

                if (hasActiveCamera)
                    break; // Return the first valid scene with a camera
            }

            return result;
        }

        /// <summary>
        /// Cleans up any pre-existing VR camera rig under this object to avoid duplicates.
        /// Looks for children named "[VRCameraRig]" and destroys them.
        /// </summary>
        private void CleanupExistingRigs()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == "[VRCameraRig]")
                {
                    MelonLogger.Msg("Cleaning up old VRCameraRig");
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Creates a new VR player rig as a child of this object and links it to the provided camera.
        /// Only creates one rig (guarded by RigCreated flag).
        /// </summary>
        /// <param name="usedCamera">The in-game camera to use as positional reference.</param>
        public void CreateCameraRig(Camera usedCamera)
        {
            CleanupExistingRigs();

            if (VRPlayer.Instance == null)
            {
                MelonLogger.Warning($"[VRSystem] #### CREATING NEW VR CAMERA RIG ####");
                GameObject rig = new GameObject("[VRCameraRig]");
                rig.transform.SetParent(transform, false);
                rig.AddComponent<VRPlayer>();
                RigCreated = true;
            }
        }

        /// <summary>
        /// Callback triggered when a new scene finishes loading.
        /// Currently unused but kept for future scene-specific logic or cleanup.
        /// </summary>
        private void OnSceneLoaded(int buildIndex, string sceneName)
        {
            // Intentionally empty
        }

        /// <summary>
        /// Temporarily toggles rendering of the VR eye cameras by changing their culling mask.
        /// Useful for debugging or disabling VR view in UI contexts.
        /// </summary>
        /// <param name="toggle">If true, hides all layers (mask = 0); if false, restores default visibility.</param>
        private void TogglePlayerCam(bool toggle)
        {
            if (VRPlayer.Instance == null || VRPlayer.Instance.StereoRender == null)
                return;

            int mask = toggle ? 0 : StereoRender.defaultCullingMask;
            VRPlayer.Instance.StereoRender.LeftCam.cullingMask = mask;
            VRPlayer.Instance.StereoRender.RightCam.cullingMask = mask;
        }

        /// <summary>
        /// Cleanup when the object is destroyed.
        /// Unsubscribes from events to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            SonsVRMod.onSceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Called when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            MelonLogger.Msg("[VRSystem] VRSystem ENABLED");
        }

        /// <summary>
        /// Called when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            MelonLogger.Msg("[VRSystem] VRSystem DISABLED");
        }
    }
}