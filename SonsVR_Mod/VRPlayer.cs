using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Valve.VR;
using Il2CppSimpleMouseRotator = Il2Cpp.SimpleMouseRotator;
using Il2CppFirstPersonCharacter = Il2Cpp.FirstPersonCharacter;
using Il2CppPlayerAnimationControl = Il2Cpp.PlayerAnimatorControl;
using static SonsVRMod.SonsVRMod;
using MelonCoroutines = MelonLoader.MelonCoroutines;
using Il2CppMono;
using System.Reflection;
using Mathf = UnityEngine.Mathf;

namespace SonsVRMod
{
    /// <summary>
    /// Manages the VR player representation by:
    /// - Creating a VR origin rig (VROrigin + Body + Head),
    /// - Linking it to the game's FPS camera and LocalPlayer,
    /// - Aligning player rotation to HMD direction,
    /// - Handling snap-turn and locomotion input,
    /// - Disabling mouse-based look controls.
    /// </summary>
    internal class VRPlayer : MonoBehaviour
    {
        public VRPlayer(IntPtr value) : base(value) { }

        /// <summary>
        /// Singleton instance for global access.
        /// </summary>
        public static VRPlayer Instance { get; set; }

        /// <summary>
        /// Root transform of the VR coordinate system (world anchor for the player).
        /// This is moved to match the game's FPS camera position.
        /// </summary>
        public Transform Origin { get; private set; }

        /// <summary>
        /// Body transform (child of Origin) that holds the StereoRender rig.
        /// Represents the player's torso/base in VR.
        /// </summary>
        public Transform Body { get; private set; }

               /// <summary>
        /// Reference to the main VR camera rig (usually unused directly; managed by StereoRender).
        /// </summary>
        public Camera Camera { get; private set; }

        /// <summary>
        /// Reference to the original game's FPS camera (used as position source).
        /// </summary>
        public Camera FPSCam { get; private set; }

        /// <summary>
        /// The custom stereo rendering component that drives left/right eye rendering.
        /// </summary>
        public StereoRender StereoRender { get; private set; }

        /// <summary>
        /// Name of the last camera used for debugging or fallback logic.
        /// </summary>
        public string lastCameraUsed = "";

        // Prevents multiple concurrent setup coroutines
        private static bool setupLock = false;

        /// <summary>
        /// True when the game is in UI/menu mode (e.g., inventory, map) and VR look should be disabled.
        /// Currently unused but reserved for future use.
        /// </summary>
        public bool isUIMode = false;

        /// <summary>
        /// Reference to the in-game LocalPlayer GameObject (root of the player character).
        /// </summary>
        public GameObject LocalPlayer;

        /// <summary>
        /// Internal reference to the currently used FPS camera (from game logic).
        /// </summary>
        private Camera UsedCamera = null;

        /// <summary>
        /// Name of the scene currently in use (for debugging).
        /// </summary>
        private string UsedSceneName = "";

        /// <summary>
        /// Name of the last camera assigned (for logging/debugging).
        /// </summary>
        public string lastCameraName = "";

        /// <summary>
        /// Indicates whether the VR rig has been fully initialized.
        /// </summary>
        private bool isSetupComplete = false;

        /// <summary>
        /// Cached reference to the game's first-person character controller.
        /// Used to control movement, running, and animation states.
        /// </summary>
        private Il2CppFirstPersonCharacter firstPersonCharacter;

        // === CONFIGURABLE ALIGNMENT OFFSETS ===

        /// <summary>
        /// Position offset (in camera-local space) applied to align the VR origin
        /// with the player's body. Adjust Y for height, Z for forward/backward.
        /// </summary>
        public Vector3 positionOffset = new Vector3(0.0f, 0.05f, -0.10f);

        /// <summary>
        /// Yaw (Y-axis) rotation offset in degrees.
        /// Can be used to correct 180° flips or fine-tune HMD-to-body alignment.
        /// Currently unused in code but kept for future tuning.
        /// </summary>
        public float yawOffset = 0f;

        /// <summary>
        /// Speed at which the player's body rotates to match HMD direction (degrees/second).
        /// Higher values = faster alignment.
        /// </summary>
        public float bodyRotationSpeed = 10f;

        // === INPUT SMOOTHING ===

        private Vector2 smoothedInput = Vector2.zero;
        private float inputSmoothSpeed = 8f;

        // === RUN STATE MANAGEMENT ===

        private bool isRunning = false;
        private float lastRunActivationTime = 0f;
        private float runActivationCooldown = 0.25f; // Prevents accidental run activation

        // === SNAP TURN CONFIGURATION ===

        private float snapTurnOffset = 0f;     // Cumulative yaw offset from snap turns
        private bool snapTurnReset = true;     // Prevents continuous rotation while stick is held
        private float snapAngle = 30f;         // Degrees per snap turn
        private float stickThreshold = 0.7f;   // Minimum stick deflection to trigger turn

        /// <summary>
        /// Cached reference to the player's animation controller (unused but reserved).
        /// </summary>
        private Il2CppPlayerAnimationControl _cachedAnimController;

        /// <summary>
        /// Indicates whether Harmony patches (if any) are ready.
        /// Currently unused.
        /// </summary>
        private bool harmonyIsReady = false;

        /// <summary>
        /// Unity callback called when the object is initialized.
        /// Prevents duplicate instances and starts asynchronous setup.
        /// </summary>
        private void Awake()
        {
            if (Instance != null)
            {
                MelonLogger.Error("[VRPlayer] ## Duplicate VRPlayer detected! Destroying instance. ##");
                GameObject.Destroy(gameObject);
                return;
            }

            MelonLogger.Msg("[VRPlayer] ## Creating VRPlayer instance ##");
            Instance = this;

            // Subscribe to scene load events (currently unused but kept for extensibility)
            SonsVRMod.onSceneLoaded += OnSceneLoaded;

            setupLock = false;
            isSetupComplete = false;

            // Start async setup after a short delay to ensure game systems are ready
            MelonCoroutines.Start(Setup());
        }

        /// <summary>
        /// Callback triggered when a new scene is loaded.
        /// Currently unused but reserved for future scene-specific logic.
        /// </summary>
        private void OnSceneLoaded(int buildIndex, string sceneName)
        {
            // Intentionally empty
        }

        /// <summary>
        /// Asynchronous setup coroutine that:
        /// - Destroys any existing StereoRender,
        /// - Creates the VR rig hierarchy (Origin → Body → StereoRender),
        /// - Marks the object to survive scene changes.
        /// </summary>
        public IEnumerator Setup()
        {
            if (setupLock) yield break;
            setupLock = true;

            // Clean up previous StereoRender if it exists
            if (StereoRender != null)
            {
                Destroy(StereoRender);
                MelonLogger.Msg("[VRPlayer] #### STEREORENDER DESTROYED ####");
            }

            // Wait for game to stabilize (e.g., camera setup)
            yield return new WaitForSeconds(2.0f);

            Body = transform;
            Origin = transform.parent;

            // Ensure the Origin exists as the world root of the VR rig
            if (Origin == null)
            {
                Origin = new GameObject("[VROrigin]").transform;
                transform.SetParent(Origin, false);
            }

            // Ensure VR rig persists across scene loads
            DontDestroyOnLoad(Origin);

            // Attach stereo rendering system to the body
            StereoRender = Body.gameObject.AddComponent<StereoRender>();
            isSetupComplete = true;
        }

        /// <summary>
        /// Links this VR player to a specific game camera and scene.
        /// Finds the LocalPlayer object from the camera hierarchy and disables mouse look.
        /// </summary>
        /// <param name="activeSceneAndCam">Scene and camera data from game logic.</param>
        public void SetSceneAndCamera(ActiveSceneWithCamera activeSceneAndCam)
        {
            UsedSceneName = activeSceneAndCam.scene.name;
            UsedCamera = activeSceneAndCam.camera;

            if (UsedCamera != null)
            {
                // Detach camera from render target (we render via StereoRender instead)
                UsedCamera.targetTexture = null;
                UsedCamera.pixelRect = new Rect(0, 0, 1080, 720); // Optional: small preview

                // Locate the player character in the transform hierarchy
                LocalPlayer = FindLocalPlayerFromCamera(UsedCamera);
                if (LocalPlayer != null)
                {
                    firstPersonCharacter = LocalPlayer.GetComponent<Il2CppFirstPersonCharacter>();
                }

                // Disable mouse-based rotation components
                DisableMouseRotation();
            }
        }

        /// <summary>
        /// Unity Update (called once per frame). Currently empty (logic in LateUpdate).
        /// </summary>
        private void Update()
        {
            // All VR logic runs in LateUpdate to ensure camera positions are final
        }

        /// <summary>
        /// Main VR logic loop, executed after all Update() calls.
        /// Handles:
        /// - Positioning the VR origin based on FPS camera,
        /// - Snap-turn input,
        /// - Body rotation alignment,
        /// - Run state management.
        /// </summary>
        private void LateUpdate()
        {
            HandleUpdate();
        }

        /// <summary>
        /// Core update logic for VR player behavior.
        /// </summary>
        private void HandleUpdate()
        {
            // --- POSITIONING: Sync VR origin to game camera ---
            if (UsedCamera != null && Origin != null)
            {
                Vector3 basePosition = UsedCamera.transform.position;
                // Apply offset in camera-local space (e.g., lower height, shift back)
                Vector3 finalPosition = basePosition + UsedCamera.transform.TransformDirection(positionOffset);
                Origin.position = finalPosition;
            }

            // --- SNAP TURN: Right thumbstick horizontal input ---
            UnityEngine.Vector2 rightStick = NativeVRInput.GetVector2("ThumbstickRight");
            if (Mathf.Abs(rightStick.x) > stickThreshold)
            {
                if (snapTurnReset)
                {
                    // Determine turn direction (+30° or -30°)
                    float direction = (rightStick.x > 0) ? snapAngle : -snapAngle;
                    snapTurnOffset += direction;

                    // Rotate the entire VR rig (StereoRender.transform is the root under Body)
                    if (StereoRender != null)
                    {
                        StereoRender.transform.Rotate(0, direction, 0, Space.World);
                    }

                    snapTurnReset = false;
                }
            }
            else if (Mathf.Abs(rightStick.x) < 0.2f)
            {
                // Reset flag when stick is centered, allowing next turn
                snapTurnReset = true;
            }

            // --- BODY ALIGNMENT: Rotate player to match HMD forward direction ---
            if (LocalPlayer != null && StereoRender?.Head != null)
            {
                // StereoRender.Head already includes snap-turn rotation (inherits from parent)
                if (UsedCamera != null)
                {
                    // Sync game camera rotation to HMD (for UI, weapon alignment, etc.)
                    UsedCamera.transform.rotation = StereoRender.Head.rotation;
                }

                // Project HMD forward onto horizontal plane (ignore vertical look)
                Vector3 headForward = StereoRender.Head.forward;
                headForward.y = 0f;

                if (headForward.magnitude > 0.01f)
                {
                    headForward.Normalize();
                    Quaternion targetRotation = Quaternion.LookRotation(headForward, Vector3.up);

                    // Smoothly rotate the player character toward HMD direction
                    LocalPlayer.transform.rotation = Quaternion.Slerp(
                        LocalPlayer.transform.rotation,
                        targetRotation,
                        Time.deltaTime * bodyRotationSpeed
                    );
                }
            }

            // --- LOCOMOTION & RUNNING ---
            if (firstPersonCharacter != null)
            {
                UnityEngine.Vector2 leftStick = NativeVRInput.GetVector2("ThumbstickLeft");

                // Deadzone and clamping
                if (leftStick.magnitude < 0.15f)
                    leftStick = Vector2.zero;
                else
                    leftStick = Vector2.ClampMagnitude(leftStick, 1f);

                // Activate running if right stick is pushed forward (y > 0.7) and not turning
                if (!isRunning && rightStick.y > 0.7f && Mathf.Abs(rightStick.x) < 0.3f)
                {
                    float currentTime = Time.time;
                    if (currentTime - lastRunActivationTime > runActivationCooldown)
                    {
                        isRunning = true;
                        lastRunActivationTime = currentTime;
                        MelonLogger.Msg("[VRPlayer] Running activated");
                    }
                }

                // Deactivate running if moving backward or stopping
                if (isRunning && (leftStick.magnitude < 0.01f || leftStick.y < 0f))
                {
                    isRunning = false;
                }

                // Forward run state to game character controller
                firstPersonCharacter.SetRunning(isRunning);
            }
        }

        /// <summary>
        /// Attempts to locate the LocalPlayer GameObject by walking up the camera's transform hierarchy.
        /// Expects a structure like: Camera → ... → LookObject → ... → LocalPlayer.
        /// </summary>
        /// <param name="camera">The FPS camera to start from.</param>
        /// <returns>The LocalPlayer GameObject, or null if not found.</returns>
        private GameObject FindLocalPlayerFromCamera(Camera camera)
        {
            if (camera == null) return null;

            Transform current = camera.transform;
            for (int i = 0; i < 3; i++)
            {
                current = current.parent;
                if (current == null) break;

                if (current.name == "LookObject")
                {
                    // Climb up until we find "LocalPlayer" or reach the root
                    while (current.parent != null && current.parent.name != "LocalPlayer" && current.parent.parent != null)
                    {
                        current = current.parent;
                    }

                    if (current.parent != null && current.parent.name == "LocalPlayer")
                    {
                        MelonLogger.Msg($"[VRPlayer] Found player root: {current.parent.name}");
                        return current.parent.gameObject;
                    }
                }
            }

            MelonLogger.Warning("[VRPlayer] Could not find LocalPlayer from camera.");
            return null;
        }

        /// <summary>
        /// Disables all instances of SimpleMouseRotator on the player and camera.
        /// Prevents mouse input from interfering with HMD-based look.
        /// </summary>
        private void DisableMouseRotation()
        {
            if (LocalPlayer != null)
            {
                var rotator = LocalPlayer.GetComponent<Il2CppSimpleMouseRotator>();
                if (rotator != null)
                {
                    rotator.enabled = false;
                }
            }

            if (UsedCamera != null)
            {
                var camRotator = UsedCamera.GetComponent<Il2CppSimpleMouseRotator>();
                if (camRotator != null)
                {
                    camRotator.enabled = false;
                }
            }
        }

        /// <summary>
        /// Cleanup when the object is destroyed.
        /// Unsubscribes from events to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            MelonLogger.Warning("[VRPlayer] *** VRPlayer DESTROYED ***");
            SonsVRMod.onSceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Resets the VR origin to a default home position and rotation.
        /// Currently unused but kept for debugging or comfort features.
        /// </summary>
        private void SetOriginHome()
        {
            SetOriginPosRotScl(new Vector3(0f, 0f, 0f), new Vector3(0, 90, 0), new Vector3(1, 1, 1));
        }

        /// <summary>
        /// Sets the position, rotation (as Euler angles), and scale of the VR origin.
        /// </summary>
        public void SetOriginPosRotScl(Vector3 pos, Vector3 euler, Vector3 scale)
        {
            Origin.position = pos;
            Origin.localEulerAngles = euler;
            Origin.localScale = scale;
        }

        /// <summary>
        /// Sets a uniform scale on the VR origin (useful for comfort/height adjustment).
        /// </summary>
        public void SetOriginScale(float scale)
        {
            Origin.localScale = new Vector3(scale, scale, scale);
        }

        /// <summary>
        /// Returns the current forward direction of the HMD (including snap-turn rotation).
        /// </summary>
        public Vector3 GetWorldForward()
        {
            return StereoRender?.Head?.forward ?? Vector3.forward;
        }

        /// <summary>
        /// Flattens a direction vector onto the horizontal plane (Y = 0) and normalizes it.
        /// </summary>
        public Vector3 GetFlatForwardDirection(Vector3 forward)
        {
            forward.y = 0;
            return forward.normalized;
        }

        /// <summary>
        /// Returns the current eye height of the VR user (approximate player height).
        /// </summary>
        public float GetPlayerHeight()
        {
            if (StereoRender?.Head == null)
                return 1.8f; // Default human height

            return Mathf.Abs(StereoRender.Head.localPosition.y);
        }

        /// <summary>
        /// Called when the component is enabled.
        /// </summary>
        private void OnEnable()
        {
            MelonLogger.Msg("[VRPlayer] VRPlayer ENABLED");
        }

        /// <summary>
        /// Called when the component is disabled.
        /// </summary>
        private void OnDisable()
        {
            MelonLogger.Msg("[VRPlayer] VRPlayer DISABLED");
        }
    }
}