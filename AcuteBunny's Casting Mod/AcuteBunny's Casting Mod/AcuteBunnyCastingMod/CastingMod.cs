using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AcuteBunnyCastingMod.Services;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace AcuteBunnyCastingMod {
    [BepInPlugin("org.acutebunny.gorillatag.castingmod", "AMC Mods", "6.7.0")]
    public class CastingMod : BaseUnityPlugin {
        public static string Platform(VRRig rig) {
            bool flag = ((rig != null) ? rig.concatStringOfCosmeticsAllowed : null) == null;
            string result;
            if(flag) {
                result = "UNK";
            } else {
                bool flag2 = rig.concatStringOfCosmeticsAllowed.Contains("S. FIRST LOGIN");
                if(flag2) {
                    result = "Steam";
                } else {
                    bool flag3;
                    if(!rig.concatStringOfCosmeticsAllowed.Contains("FIRST LOGIN")) {
                        NetPlayer creator = rig.Creator;
                        if(creator == null) {
                            flag3 = false;
                        } else {
                            Player playerRef = creator.GetPlayerRef();
                            int? num = (playerRef != null) ? new int?(playerRef.CustomProperties.Count) : null;
                            int num2 = 2;
                            flag3 = (num.GetValueOrDefault() >= num2 & num != null);
                        }
                    } else {
                        flag3 = true;
                    }
                    bool flag4 = flag3;
                    if(flag4) {
                        result = "Oculus PC";
                    } else {
                        result = "Quest";
                    }
                }
            }
            return result;
        }

        public static string GetFPS(VRRig rig) {
            bool flag = rig == null;
            string result;
            if(flag) {
                result = "<color=#aaaaaa>N/A</color>";
            } else {
                Traverse traverse = Traverse.Create(rig).Field("fps");
                string s;
                if(traverse == null) {
                    s = null;
                } else {
                    object value = traverse.GetValue();
                    s = ((value != null) ? value.ToString() : null);
                }
                float num;
                bool flag2 = float.TryParse(s, out num);
                if(flag2) {
                    string arg = (num >= 60f) ? "#90EE90" : ((num >= 45f) ? "yellow" : "red");
                    result = string.Format("<color={0}>{1:F0}</color>", arg, num);
                } else {
                    result = "<color=#aaaaaa>N/A</color>";
                }
            }
            return result;
        }

        private void Awake() {
            CastingMod.Log = base.Logger;
            CastingMod.Log.LogInfo(string.Format("AMC Mods v{0}: Awakening...", base.Info.Metadata.Version));
            this.isInitializing = true;
            bool flag = PlayerPrefs.GetInt("AMCHasShownWelcome", 0) == 0;
            if(flag) {
                this.showWelcomePanel = true;
            }
            this.InitializeTrial();
            this.BindAllConfigs();
            base.StartCoroutine(this.FullInitializationSequence());
        }

        private void InitializeTrial() {
            string @string = PlayerPrefs.GetString("AMCTrialStartTime_v6", "0");
            bool flag = !long.TryParse(@string, out this.trialStartTime) || this.trialStartTime == 0L;
            if(flag) {
                this.trialStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                PlayerPrefs.SetString("AMCTrialStartTime_v6", this.trialStartTime.ToString());
                PlayerPrefs.Save();
                NotificationManager.Show("Welcome!", "Your 48-hour trial of AMC Mods has started.", 10f, null, false);
            }
            this.isTrialActive = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.trialStartTime < 172800L);
        }

        private IEnumerator AuthenticateWithServer() {
            this.loadingNotification.Message = "Authenticating...";
            this.isTrialActive = false;
            string authServerUrl = "acutebunny.pythonanywhere.com";
            string hwid = SystemInfo.deviceUniqueIdentifier;
            string jsonPayload = "{\"hwid\":\"" + hwid + "\"}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            using(UnityWebRequest www = new UnityWebRequest(authServerUrl + "/auth", "POST")) {
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();
                bool flag = www.result == UnityWebRequest.Result.Success;
                if(flag) {
                    try {
                        AuthResponse response = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);
                        bool flag2 = response.status == "active";
                        if(flag2) {
                            this.isTrialActive = true;
                            this.trialStartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (172800L - response.time_remaining);
                            CastingMod.Log.LogInfo(string.Format("Authentication successful. Time remaining: {0}s", response.time_remaining));
                        } else {
                            CastingMod.Log.LogWarning("Authentication failed: Server responded that trial has expired.");
                        }
                        response = null;
                    } catch(Exception ex) {
                        Exception e = ex;
                        CastingMod.Log.LogError("Failed to parse auth response: " + e.Message);
                    }
                } else {
                    CastingMod.Log.LogError("Authentication request failed: " + www.error + ". URL: " + authServerUrl);
                }
            }
            UnityWebRequest www = null;
            yield break;
            yield break;
        }

        private IEnumerator FullInitializationSequence() {
            this.loadingNotification = NotificationManager.Show("Initializing...", "Starting up AMC Mods...", -1f, this.loadingIcon, true);
            yield return null;
            yield return base.StartCoroutine(this.AuthenticateWithServer());
            bool flag = !this.isTrialActive;
            if(flag) {
                this.loadingNotification.Title = "Authentication Failed";
                this.loadingNotification.Message = "Your trial has expired. Please contact the developer.";
                this.loadingNotification.Duration = 10f;
                this.loadingNotification.IsLoadingNotif = false;
                this.isInitializing = false;
                yield break;
            }
            Dictionary<string, IEnumerator> loadingSteps = new Dictionary<string, IEnumerator>
            {
                {
                    "Checking Folders...",
                    this.CheckRequiredFolders()
                },
                {
                    "Loading Icons...",
                    this.LoadAllIcons()
                },
                {
                    "Initializing GUI...",
                    this.InitializeModernGUI_Coroutine()
                },
                {
                    "Creating Sprite Assets...",
                    this.CreateAllSpriteAssets()
                },
                {
                    "Loading Fonts...",
                    this.LoadAllFonts()
                },
                {
                    "Loading Presets...",
                    this.LoadPresets()
                },
                {
                    "Loading Watermarks...",
                    this.LoadAllWatermarks()
                },
                {
                    "Loading Settings...",
                    this.LoadSettings()
                },
                {
                    "Initializing Audio...",
                    AudioManager.Initialize(Path.GetDirectoryName(base.Info.Location), CastingMod.Log)
                },
                {
                    "Waiting for Game...",
                    this.WaitForGameReady()
                },
                {
                    "Authenticating With Server...",
                    this.DelayedInitialAuthentication()
                },
                {
                    "Setting up Cameras...",
                    this.ReinitializeCameras(true)
                },
                {
                    "Creating UI Elements...",
                    this.CreateRuntimeUI()
                },
                {
                    "Finalizing...",
                    this.FinalizeSetup()
                }
            };
            foreach(KeyValuePair<string, IEnumerator> step in loadingSteps) {
                this.loadingNotification.Message = step.Key;
                yield return base.StartCoroutine(step.Value);
                step = default(KeyValuePair<string, IEnumerator>);
            }
            Dictionary<string, IEnumerator>.Enumerator enumerator = default(Dictionary<string, IEnumerator>.Enumerator);
            this.loadingNotification.Title = "Initialization Complete!";
            this.loadingNotification.Message = "AMC Mods is ready.";
            this.loadingNotification.Duration = 5f;
            this.loadingNotification.IsLoadingNotif = false;
            this.loadingNotification = null;
            this.isInitializing = false;
            yield break;
            yield break;
        }

        private IEnumerator CheckRequiredFolders() {
            string pluginDirectory = Path.GetDirectoryName(base.Info.Location);
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "Music"));
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "SFX"));
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "Icons"));
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "Fonts"));
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "Assets"));
            this.CheckCreateFolder(Path.Combine(pluginDirectory, "Watermarks"));
            yield break;
        }

        private void CheckCreateFolder(string path) {
            bool flag = !Directory.Exists(path);
            if(flag) {
                Directory.CreateDirectory(path);
            }
        }

        private IEnumerator InitializeModernGUI_Coroutine() {
            this.InitializeModernGUI();
            yield break;
        }

        private IEnumerator LoadAllIcons() {
            this.pageIcons[CastingMod.CurrentGUIPage.Camera] = this.LoadIcon("camera.png");
            this.pageIcons[CastingMod.CurrentGUIPage.Visuals] = this.LoadIcon("paintbrush.png");
            this.pageIcons[CastingMod.CurrentGUIPage.Widgets] = this.LoadIcon("build.png");
            this.pageIcons[CastingMod.CurrentGUIPage.RoomAndPlayers] = this.LoadIcon("roomjoin.png");
            this.pageIcons[CastingMod.CurrentGUIPage.Services] = this.LoadIcon("chat.png");
            this.pageIcons[CastingMod.CurrentGUIPage.StyleAndSettings] = this.LoadIcon("camerasetting.png");
            this.backIcon = this.LoadIcon("back.png");
            this.closeIcon = this.LoadIcon("close.png");
            this.saveIcon = this.LoadIcon("save.png");
            this.deleteIcon = this.LoadIcon("delete.png");
            this.colorIcon = this.LoadIcon("color.png");
            this.enableIcon = this.LoadIcon("enable.png");
            this.disableIcon = this.LoadIcon("disable.png");
            this.selectIcon = this.LoadIcon("select.png");
            this.cancelIcon = this.LoadIcon("cancel.png");
            this.checkIcon = this.LoadIcon("check.png");
            this.refreshIcon = this.LoadIcon("refresh.png");
            this.addIcon = this.LoadIcon("add.png");
            this.playIcon = this.LoadIcon("play.png");
            this.pauseIcon = this.LoadIcon("pause.png");
            this.nextIcon = this.LoadIcon("next.png");
            this.prevIcon = this.LoadIcon("prev.png");
            this.trialClockIcon = this.LoadIcon("clock.png");
            this.resizeIcon = this.LoadIcon("resize.png");
            this.loadingIcon = this.LoadIcon("loading.png");
            this.searchIcon = this.LoadIcon("search.png");
            this.chatIcon = this.LoadIcon("chat.png");
            yield break;
        }

        private IEnumerator CreateAllSpriteAssets() {
            this.CreateModUserSpriteAsset();
            yield break;
        }

        private IEnumerator LoadAllFonts() {
            this.LoadFonts();
            this.LoadSelectedFontFromConfig();
            this.LoadLeaderboardFontFromConfig();
            this.LoadGuiFontFromConfig();
            this.LoadDesignerFont();
            yield break;
        }

        private IEnumerator LoadPresets() {
            this.LoadFullPresetsFromFile();
            yield break;
        }

        private IEnumerator LoadAllWatermarks() {
            this.LoadSavedWatermarks();
            yield break;
        }

        private IEnumerator LoadSettings() {
            this.LoadSettingsFromConfig();
            this.mainWindowRect.width = this.guiWidthConfig.Value;
            this.mainWindowRect.height = this.guiHeightConfig.Value;
            this.UpdateAllWidgetGraphics();
            this.LoadCustomScoreboardBg();
            this.LoadCustomWatermarkImage();
            AudioManager.MusicVolume = this.musicVolumeConfig.Value;
            this.previousLerping = this.Lerping;
            yield break;
        }

        private IEnumerator WaitForGameReady() {
            yield return new WaitUntil(() => GorillaTagger.Instance != null && GorillaTagger.Instance.offlineVRRig != null);
            List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, devices);
            bool flag = devices.Count > 0;
            if(flag) {
                this.leftHandDevice = devices[0];
            }
            yield break;
        }

        private IEnumerator CreateRuntimeUI() {
            this.CreateReadjustingLabel();
            bool flag = this.readjustingLabel != null;
            if(flag) {
                this.readjustingLabel.gameObject.SetActive(false);
            }
            yield break;
        }

        private IEnumerator FinalizeSetup() {
            GameObject handlerObject = new GameObject("CastingMod_PhotonHandler");
            UnityEngine.Object.DontDestroyOnLoad(handlerObject);
            this.photonHandler = handlerObject.AddComponent<PhotonCallbackHandler>();
            this.photonHandler.Initialize(this);
            AnnouncementService.StartService(this, this.announcementsUrlConfig.Value);
            base.StartCoroutine(this.InitializeHoldableCamera());
            yield break;
        }

        private IEnumerator DelayedInitialAuthentication() {
            yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.LocalPlayer != null && !string.IsNullOrEmpty(PhotonNetwork.LocalPlayer.UserId));
            yield return new WaitForSeconds(5f);
            this.DiscordLogUserAuth(PhotonNetwork.LocalPlayer);
            yield break;
        }

        private void BindAllConfigs() {
            this.announcementsUrlConfig = base.Config.Bind<string>("Services", "Announcements URL", "https://pastebin.com/raw/vFXkeKTJ", "The URL to fetch mod announcements from.");
            this.adminCustomTagConfig = base.Config.Bind<string>("Admin", "Custom Nametag", "[ADMIN]", "The nametag prefix to display for admins.");
            this.holdableCameraEnabledConfig = base.Config.Bind<bool>("Holdable Camera", "Enabled", true, "Enable the physical, holdable casting camera in VR.");
            this.freecamMoveSpeedConfig = base.Config.Bind<float>("Freecam", "Move Speed", 2f, "");
            this.freecamLookSpeedConfig = base.Config.Bind<float>("Freecam", "Look Speed", 50f, "");
            this.freecamMouseLookConfig = base.Config.Bind<bool>("Freecam", "Use Mouse Look", true, "");
            this.freecamSmoothTimeConfig = base.Config.Bind<float>("Freecam", "Movement Smooth Time", 0.1f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 0.5f), Array.Empty<object>()));
            this.freecamFovConfig = base.Config.Bind<float>("Freecam", "Field of View", 100f, new ConfigDescription("", new AcceptableValueRange<float>(30f, 140f), Array.Empty<object>()));
            this.freecamRollSpeedConfig = base.Config.Bind<float>("Freecam", "Roll Speed", 45f, new ConfigDescription("", new AcceptableValueRange<float>(10f, 90f), Array.Empty<object>()));
            this.fovConfig = base.Config.Bind<float>("Camera Options", "Field Of View", 100f, new ConfigDescription("", new AcceptableValueRange<float>(30f, 140f), Array.Empty<object>()));
            this.playerLerpConfig = base.Config.Bind<float>("Camera Options", "Player Lerp Multiplier", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0.1f, 5f), Array.Empty<object>()));
            this.farClipPlaneConfig = base.Config.Bind<float>("Camera Options", "Render Distance", 1500f, new ConfigDescription("", new AcceptableValueRange<float>(100f, 5000f), Array.Empty<object>()));
            this.fpPositionSmoothing_EnabledConfig = base.Config.Bind<bool>("First Person Camera", "Enable Position Smoothing", true, "");
            this.fpPositionSmoothing_FactorConfig = base.Config.Bind<float>("First Person Camera", "Position Smoothing Factor", 0.1f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), Array.Empty<object>()));
            this.fpRotationSmoothing_EnabledConfig = base.Config.Bind<bool>("First Person Camera", "Enable Rotation Smoothing", true, "");
            this.fpRotationSmoothing_FactorConfig = base.Config.Bind<float>("First Person Camera", "Rotation Smoothing Factor", 0.1f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), Array.Empty<object>()));
            this.thirdPersonModeConfig = base.Config.Bind<CastingMod.ThirdPersonMode>("Third Person Camera", "Mode", CastingMod.ThirdPersonMode.Static, "");
            this.thirdPersonYLockedConfig = base.Config.Bind<bool>("Third Person Camera", "Lock Y Axis & Follow Yaw", true, "");
            this.thirdPersonRotationSmoothnessConfig = base.Config.Bind<float>("Third Person Camera", "Rotation Smoothness", 0.1f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), Array.Empty<object>()));
            this.thirdPersonPositionSmoothnessConfig = base.Config.Bind<float>("Third Person Camera", "Position Smoothness", 0.1f, new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 1f), Array.Empty<object>()));
            this.thirdPersonRotationLockedConfig = base.Config.Bind<bool>("Third Person Camera", "Lock Rotation Absolute", false, "");
            this.thirdPersonLookAtTargetConfig = base.Config.Bind<bool>("Third Person Camera", "Always Look At Target", false, "");
            this.thirdPersonCollisionConfig = base.Config.Bind<bool>("Third Person Camera", "Enable Collision", true, "");
            this.thirdPersonOffsetConfig = base.Config.Bind<Vector3>("Third Person Camera", "Offset", new Vector3(0f, 0.5f, -2f), "");
            this.cinematicOrbitSpeedConfig = base.Config.Bind<float>("Third Person Cinematic", "Orbit Speed", 15f, "");
            this.cinematicMinFovConfig = base.Config.Bind<float>("Third Person Cinematic", "Min FOV", 60f, "");
            this.cinematicMaxFovConfig = base.Config.Bind<float>("Third Person Cinematic", "Max FOV", 110f, "");
            this.cinematicMinDistConfig = base.Config.Bind<float>("Third Person Cinematic", "Min Distance", 1.5f, "");
            this.cinematicMaxDistConfig = base.Config.Bind<float>("Third Person Cinematic", "Max Distance", 4f, "");
            this.areNametagsEnabledConfig = base.Config.Bind<bool>("Nametags", "Enable Nametags", false, "");
            this.nametagSizeConfig = base.Config.Bind<float>("Nametags", "Nametag Size", 1f, new ConfigDescription("", new AcceptableValueRange<float>(0.1f, 2.5f), Array.Empty<object>()));
            this.selectedFontNameConfig = base.Config.Bind<string>("Nametags", "Selected Font", "Default", "");
            this.nametagHeadOffsetYConfig = base.Config.Bind<float>("Nametags", "Head Offset Y", 0.45f, new ConfigDescription("", new AcceptableValueRange<float>(0.2f, 2f), Array.Empty<object>()));
            this.nametagShowDistanceConfig = base.Config.Bind<bool>("Nametags", "Show Distance", false, "");
            this.nametagShowFpsConfig = base.Config.Bind<bool>("Nametags", "Show FPS", true, "");
            this.nametagShowPlatformConfig = base.Config.Bind<bool>("Nametags", "Show Platform", true, "");
            this.showCastingModUsersConfig = base.Config.Bind<bool>("Nametags", "Show Casting Mod Users", true, "");
            this.nametagFadeWithDistanceConfig = base.Config.Bind<bool>("Nametags", "Fade With Distance", false, "");
            this.nametagFadeStartDistanceConfig = base.Config.Bind<float>("Nametags", "Fade Start Distance", 20f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 100f), Array.Empty<object>()));
            this.nametagFadeEndDistanceConfig = base.Config.Bind<float>("Nametags", "Fade End Distance", 40f, new ConfigDescription("", new AcceptableValueRange<float>(1f, 100f), Array.Empty<object>()));
            this.nametagShowBackgroundConfig = base.Config.Bind<bool>("Nametags", "Show Background Box", true, "");
            this.nametagBackgroundColorConfig = base.Config.Bind<Color>("Nametags", "Background Color", new Color(0f, 0f, 0f, 0.5f), "");
            this.useGlobalNametagColorConfig = base.Config.Bind<bool>("Nametags", "Use Global Color", false, "");
            this.globalNametagColorConfig = base.Config.Bind<Color>("Nametags", "Global Nametag Color", Color.white, "");
            this.isSpectatorIndicatorEnabledConfig = base.Config.Bind<bool>("Spectator", "Enable Indicator", false, "");
            this.spectatorIndicatorSizeConfig = base.Config.Bind<float>("Spectator", "Indicator Size", 0.15f, new ConfigDescription("", new AcceptableValueRange<float>(0.05f, 0.5f), Array.Empty<object>()));
            this.spectatorIndicatorColorConfig = base.Config.Bind<Color>("Spectator", "Indicator Color", Color.yellow, "");
            this.spectatorIndicatorShapeConfig = base.Config.Bind<int>("Spectator", "Indicator Shape", 0, new ConfigDescription("", new AcceptableValueList<int>(new int[]
            {
                0,
                1,
                2
            }), Array.Empty<object>()));
            this.pulseSpectatorIndicatorConfig = base.Config.Bind<bool>("Spectator", "Pulse Indicator", false, "");
            this.billboardSpectatorIndicatorConfig = base.Config.Bind<bool>("Spectator", "Billboard Indicator", true, "");
            this.musicPlayerWidthConfig = base.Config.Bind<float>("Music Player", "Widget Width", 300f, new ConfigDescription("", new AcceptableValueRange<float>(200f, 600f), Array.Empty<object>()));
            this.musicPlayerCoverArtSizeConfig = base.Config.Bind<float>("Music Player", "Cover Art Size", 250f, new ConfigDescription("", new AcceptableValueRange<float>(100f, 500f), Array.Empty<object>()));
            this.musicPlayerBgColorConfig = base.Config.Bind<Color>("Music Player", "Background Color", new Color(0.1f, 0.1f, 0.1f, 0.8f), "");
            this.musicPlayerTextColorConfig = base.Config.Bind<Color>("Music Player", "Text Color", Color.white, "");
            this.musicPlayerProgressBgColorConfig = base.Config.Bind<Color>("Music Player", "Progress Bar Background", new Color(0.2f, 0.2f, 0.2f, 1f), "");
            this.musicPlayerProgressColorConfig = base.Config.Bind<Color>("Music Player", "Progress Bar Foreground", new Color(0.6f, 0.3f, 0.9f, 1f), "");
            this.clockStyleConfig = base.Config.Bind<CastingMod.ClockStyle>("Date & Time Widget", "Clock Style", CastingMod.ClockStyle.DigitalBox, "");
            this.dateTimeWidthConfig = base.Config.Bind<float>("Date & Time Widget", "Width", 250f, new ConfigDescription("", new AcceptableValueRange<float>(150f, 500f), Array.Empty<object>()));
            this.dateTimeHeightConfig = base.Config.Bind<float>("Date & Time Widget", "Height", 100f, new ConfigDescription("", new AcceptableValueRange<float>(50f, 300f), Array.Empty<object>()));
            this.dateTimeBgColorConfig = base.Config.Bind<Color>("Date & Time Widget", "Background Color", new Color(0.1f, 0.1f, 0.1f, 0.8f), "");
            this.dateTimeTextColorConfig = base.Config.Bind<Color>("Date & Time Widget", "Text Color", Color.white, "");
            this.dateTimeTimeFormatConfig = base.Config.Bind<string>("Date & Time Widget", "Time Format", "HH:mm:ss", "");
            this.dateTimeDateFormatConfig = base.Config.Bind<string>("Date & Time Widget", "Date Format", "dddd, MMMM d", "");
            this.isScoreboardVisibleConfig = base.Config.Bind<bool>("Scoreboard", "Is Visible", false, "");
            this.scoreboardBgColorConfig = base.Config.Bind<Color>("Scoreboard", "Background Color", new Color(0.1f, 0.1f, 0.1f, 0.85f), "");
            this.scoreboardTextColorConfig = base.Config.Bind<Color>("Scoreboard", "Text Color", Color.white, "");
            this.scoreboardRedColorConfig = base.Config.Bind<Color>("Scoreboard", "Red Team Color", new Color(1f, 0.3f, 0.3f), "");
            this.scoreboardBlueColorConfig = base.Config.Bind<Color>("Scoreboard", "Blue Team Color", new Color(0.4f, 0.6f, 1f), "");
            this.scoreboardCornerRadiusConfig = base.Config.Bind<float>("Scoreboard", "Corner Radius", 10f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 30f), Array.Empty<object>()));
            this.redTeamNameConfig = base.Config.Bind<string>("Scoreboard", "Red Team Name", "RED", "");
            this.blueTeamNameConfig = base.Config.Bind<string>("Scoreboard", "Blue Team Name", "BLU", "");
            this.scoreboardTeamNameFontSizeConfig = base.Config.Bind<int>("Scoreboard", "Team Name Font Size", 22, new ConfigDescription("", new AcceptableValueRange<int>(10, 50), Array.Empty<object>()));
            this.scoreboardScoreFontSizeConfig = base.Config.Bind<int>("Scoreboard", "Score Font Size", 36, new ConfigDescription("", new AcceptableValueRange<int>(10, 72), Array.Empty<object>()));
            this.scoreboardTimerFontSizeConfig = base.Config.Bind<int>("Scoreboard", "Timer Font Size", 28, new ConfigDescription("", new AcceptableValueRange<int>(10, 72), Array.Empty<object>()));
            this.scoreboardBgPathConfig = base.Config.Bind<string>("Scoreboard", "Background Image Path", "", "");
            this.scoreboardPositionsLockedConfig = base.Config.Bind<bool>("Scoreboard", "Lock Positions", false, "Lock the positions of the draggable scoreboard elements.");
            this.isLeaderboardVisibleConfig = base.Config.Bind<bool>("Leaderboard", "Is Visible", false, "");
            this.leaderboardBgColorConfig = base.Config.Bind<Color>("Leaderboard", "Background Color", new Color(0.1f, 0.1f, 0.1f, 0.85f), "");
            this.leaderboardTextColorConfig = base.Config.Bind<Color>("Leaderboard", "Player Name Color", Color.white, "");
            this.leaderboardFontNameConfig = base.Config.Bind<string>("Leaderboard", "Font Name", "Default", "");
            this.leaderboardFontSizeConfig = base.Config.Bind<int>("Leaderboard", "Font Size", 22, new ConfigDescription("", new AcceptableValueRange<int>(10, 48), Array.Empty<object>()));
            this.musicVolumeConfig = base.Config.Bind<float>("GUI", "Music Volume", 0.5f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
            this.isCustomTargetEnabledConfig = base.Config.Bind<bool>("GUI", "Enable Click Select", false, "");
            this.guiBackgroundColorConfig = base.Config.Bind<Color>("GUI Style", "Background", new Color(0.13f, 0.14f, 0.18f, 0.97f), "");
            this.guiPillColorConfig = base.Config.Bind<Color>("GUI Style", "Title Pill Background", new Color(0.3f, 0.32f, 0.35f, 1f), "");
            this.guiActiveCategoryColorConfig = base.Config.Bind<Color>("GUI Style", "Active Category", new Color(0.2f, 0.4f, 0.9f, 1f), "");
            this.guiTextColorConfig = base.Config.Bind<Color>("GUI Style", "Text", new Color(0.9f, 0.9f, 0.9f, 1f), "");
            this.guiSliderThumbColorConfig = base.Config.Bind<Color>("GUI Style", "Slider Fill", new Color(0.4f, 0.6f, 1f, 1f), "");
            this.guiSliderBgColorConfig = base.Config.Bind<Color>("GUI Style", "Slider Background", new Color(0.8f, 0.8f, 0.8f, 0.2f), "");
            this.guiButtonColorConfig = base.Config.Bind<Color>("GUI Style", "Button", new Color(0.2f, 0.22f, 0.25f, 1f), "");
            this.guiButtonHoverColorConfig = base.Config.Bind<Color>("GUI Style", "Button Hover", new Color(0.25f, 0.27f, 0.3f, 1f), "");
            this.guiButtonActiveColorConfig = base.Config.Bind<Color>("GUI Style", "Button Active", new Color(0.15f, 0.17f, 0.2f, 1f), "");
            this.guiRedButtonColorConfig = base.Config.Bind<Color>("GUI Style", "Red Button", new Color(0.9f, 0.2f, 0.2f, 1f), "");
            this.guiRedButtonHoverColorConfig = base.Config.Bind<Color>("GUI Style", "Red Button Hover", new Color(1f, 0.3f, 0.3f, 1f), "");
            this.guiRedButtonActiveColorConfig = base.Config.Bind<Color>("GUI Style", "Red Button Active", new Color(0.7f, 0.1f, 0.1f, 1f), "");
            this.guiCornerRadiusConfig = base.Config.Bind<float>("GUI Style", "Corner Radius", 12f, new ConfigDescription("", new AcceptableValueRange<float>(0f, 30f), Array.Empty<object>()));
            this.guiFontSizeConfig = base.Config.Bind<int>("GUI Style", "Font Size", 14, new ConfigDescription("", new AcceptableValueRange<int>(10, 20), Array.Empty<object>()));
            this.guiFontNameConfig = base.Config.Bind<string>("GUI Style", "Font", "Default", "The font used for the mod's GUI.");
            this.guiWidthConfig = base.Config.Bind<float>("GUI Style", "Width", 950f, "The saved width of the main GUI window.");
            this.guiHeightConfig = base.Config.Bind<float>("GUI Style", "Height", 700f, "The saved height of the main GUI window.");
        }

        private void LoadSettingsFromConfig() {
            this.showCastingModUsers = this.showCastingModUsersConfig.Value;
            this.isHoldableCameraEnabled = this.holdableCameraEnabledConfig.Value;
            this.freecamMoveSpeedSetting = this.freecamMoveSpeedConfig.Value;
            this.freecamLookSpeedSetting = this.freecamLookSpeedConfig.Value;
            this.isMouseLookFreecam = this.freecamMouseLookConfig.Value;
            this.freecamSmoothTime = this.freecamSmoothTimeConfig.Value;
            this.freecamFov = this.freecamFovConfig.Value;
            this.freecamRollSpeed = this.freecamRollSpeedConfig.Value;
            this.FOV = this.fovConfig.Value;
            this.Lerping = this.playerLerpConfig.Value;
            this.farClipPlane = this.farClipPlaneConfig.Value;
            this.isFPPositionSmoothingEnabled = this.fpPositionSmoothing_EnabledConfig.Value;
            this.fpPositionSmoothingFactor = this.fpPositionSmoothing_FactorConfig.Value;
            this.isFPRotationSmoothingEnabled = this.fpRotationSmoothing_EnabledConfig.Value;
            this.fpRotationSmoothingFactor = this.fpRotationSmoothing_FactorConfig.Value;
            this.thirdPersonModeSetting = this.thirdPersonModeConfig.Value;
            this.isThirdPersonYLocked = this.thirdPersonYLockedConfig.Value;
            this.thirdPersonRotationSmoothness = this.thirdPersonRotationSmoothnessConfig.Value;
            this.thirdPersonPositionSmoothness = this.thirdPersonPositionSmoothnessConfig.Value;
            this.isThirdPersonRotationLocked = this.thirdPersonRotationLockedConfig.Value;
            this.thirdPersonLookAtTarget = this.thirdPersonLookAtTargetConfig.Value;
            this.thirdPersonCollision = this.thirdPersonCollisionConfig.Value;
            this.thirdPersonOffset = this.thirdPersonOffsetConfig.Value;
            this.cinematicOrbitSpeed = this.cinematicOrbitSpeedConfig.Value;
            this.cinematicMinFov = this.cinematicMinFovConfig.Value;
            this.cinematicMaxFov = this.cinematicMaxFovConfig.Value;
            this.cinematicMinDist = this.cinematicMinDistConfig.Value;
            this.cinematicMaxDist = this.cinematicMaxDistConfig.Value;
            this.areNametagsEnabled = this.areNametagsEnabledConfig.Value;
            this.nametagSize = this.nametagSizeConfig.Value;
            this.HeadOffsetY = this.nametagHeadOffsetYConfig.Value;
            this.nametagShowDistance = this.nametagShowDistanceConfig.Value;
            this.nametagShowFps = this.nametagShowFpsConfig.Value;
            this.nametagShowPlatform = this.nametagShowPlatformConfig.Value;
            this.nametagFadeWithDistance = this.nametagFadeWithDistanceConfig.Value;
            this.nametagFadeStartDistance = this.nametagFadeStartDistanceConfig.Value;
            this.nametagFadeEndDistance = this.nametagFadeEndDistanceConfig.Value;
            this.nametagShowBackground = this.nametagShowBackgroundConfig.Value;
            this.nametagBackgroundColor = this.nametagBackgroundColorConfig.Value;
            this.useGlobalNametagColor = this.useGlobalNametagColorConfig.Value;
            this.globalNametagColor = this.globalNametagColorConfig.Value;
            this.isSpectatorIndicatorEnabled = this.isSpectatorIndicatorEnabledConfig.Value;
            this.spectatorIndicatorSize = this.spectatorIndicatorSizeConfig.Value;
            this.spectatorIndicatorColor = this.spectatorIndicatorColorConfig.Value;
            this.spectatorIndicatorShape = this.spectatorIndicatorShapeConfig.Value;
            this.pulseSpectatorIndicator = this.pulseSpectatorIndicatorConfig.Value;
            this.billboardSpectatorIndicator = this.billboardSpectatorIndicatorConfig.Value;
            this.isCustomTargetEnabled = this.isCustomTargetEnabledConfig.Value;
            this.cornerRadius = this.guiCornerRadiusConfig.Value;
            this.guiFontSize = this.guiFontSizeConfig.Value;
            this.isScoreboardWidgetVisible = this.isScoreboardVisibleConfig.Value;
            this.redTeamName = this.redTeamNameConfig.Value;
            this.blueTeamName = this.blueTeamNameConfig.Value;
            this.isLeaderboardWidgetVisible = this.isLeaderboardVisibleConfig.Value;
            AudioManager.MusicVolume = this.musicVolumeConfig.Value;
        }

        private void UpdateAllWidgetGraphics() {
            this.UpdateMusicPlayerGraphics();
            this.UpdateDateTimeWidgetGraphics();
            this.UpdateNametagGraphics();
            this.UpdateScoreboardGraphics();
            this.UpdateLeaderboardGraphics();
        }

        private void UpdateMusicPlayerGraphics() {
            UnityEngine.Object.Destroy(this.musicPlayerBgTexture);
            UnityEngine.Object.Destroy(this.musicPlayerProgressBgTexture);
            UnityEngine.Object.Destroy(this.musicPlayerProgressTexture);
            this.musicPlayerBgTexture = this.CreateRoundedRectTexture(64, 64, this.cornerRadius, this.musicPlayerBgColorConfig.Value);
            this.musicPlayerProgressBgTexture = this.CreateSolidColorTexture(this.musicPlayerProgressBgColorConfig.Value);
            this.musicPlayerProgressTexture = this.CreateSolidColorTexture(this.musicPlayerProgressColorConfig.Value);
        }

        private void UpdateDateTimeWidgetGraphics() {
            UnityEngine.Object.Destroy(this.dateTimeBgTexture);
            this.dateTimeBgTexture = this.CreateRoundedRectTexture(64, 64, this.cornerRadius, this.dateTimeBgColorConfig.Value);
        }

        private void UpdateNametagGraphics() {
            UnityEngine.Object.Destroy(this.nametagBgTexture);
            this.nametagBgTexture = this.CreateRoundedRectTexture(16, 16, 4f, this.nametagBackgroundColorConfig.Value);
        }

        private void UpdateScoreboardGraphics() {
            UnityEngine.Object.Destroy(this.scoreboardBgTexture);
            this.scoreboardBgTexture = this.CreateRoundedRectTexture(64, 64, this.scoreboardCornerRadiusConfig.Value, this.scoreboardBgColorConfig.Value);
        }

        private void UpdateLeaderboardGraphics() {
            UnityEngine.Object.Destroy(this.leaderboardBgTexture);
            this.leaderboardBgTexture = this.CreateRoundedRectTexture(64, 64, 10f, this.leaderboardBgColorConfig.Value);
        }

        private void LoadCustomScoreboardBg() {
            bool flag = string.IsNullOrEmpty(this.scoreboardBgPathConfig.Value);
            if(flag) {
                this.customScoreboardBg = null;
            } else {
                string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), "Assets", this.scoreboardBgPathConfig.Value);
                bool flag2 = File.Exists(path);
                if(flag2) {
                    byte[] data = File.ReadAllBytes(path);
                    this.customScoreboardBg = new Texture2D(2, 2);
                    bool flag3 = !this.customScoreboardBg.LoadImage(data);
                    if(flag3) {
                        this.customScoreboardBg = null;
                    }
                } else {
                    this.customScoreboardBg = null;
                }
            }
        }

        private void LoadCustomWatermarkImage() {
            string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), "Assets", "watermark_image.png");
            bool flag = File.Exists(path);
            if(flag) {
                byte[] data = File.ReadAllBytes(path);
                this.customWatermarkImage = new Texture2D(2, 2);
                bool flag2 = !this.customWatermarkImage.LoadImage(data);
                if(flag2) {
                    this.customWatermarkImage = null;
                }
            } else {
                this.customWatermarkImage = null;
            }
        }

        private Texture2D CreateSolidColorTexture(Color color) {
            Texture2D texture2D = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture2D.SetPixel(0, 0, color);
            texture2D.Apply(false);
            return texture2D;
        }

        private Texture2D CreateRoundedRectTexture(int width, int height, float radius, Color color) {
            bool flag = width <= 0 || height <= 0;
            Texture2D result;
            if(flag) {
                Texture2D texture2D = new Texture2D(1, 1);
                texture2D.SetPixel(0, 0, Color.clear);
                texture2D.Apply();
                result = texture2D;
            } else {
                Texture2D texture2D2 = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color clear = Color.clear;
                radius = Mathf.Max(0f, Mathf.Min(radius, Mathf.Min((float)width / 2f, (float)height / 2f) - 1f));
                int num = Mathf.FloorToInt(radius);
                for(int i = 0; i < height; i++) {
                    for(int j = 0; j < width; j++) {
                        Color color2 = clear;
                        bool flag2 = j < num && i < num;
                        float num2;
                        if(flag2) {
                            num2 = ((float)(num - j) - 0.5f) * ((float)(num - j) - 0.5f) + ((float)(num - i) - 0.5f) * ((float)(num - i) - 0.5f);
                        } else {
                            bool flag3 = j >= width - num && i < num;
                            if(flag3) {
                                num2 = ((float)j - ((float)(width - num) - 0.5f)) * ((float)j - ((float)(width - num) - 0.5f)) + ((float)(num - i) - 0.5f) * ((float)(num - i) - 0.5f);
                            } else {
                                bool flag4 = j < num && i >= height - num;
                                if(flag4) {
                                    num2 = ((float)(num - j) - 0.5f) * ((float)(num - j) - 0.5f) + ((float)i - ((float)(height - num) - 0.5f)) * ((float)i - ((float)(height - num) - 0.5f));
                                } else {
                                    bool flag5 = j >= width - num && i >= height - num;
                                    if(flag5) {
                                        num2 = ((float)j - ((float)(width - num) - 0.5f)) * ((float)j - ((float)(width - num) - 0.5f)) + ((float)i - ((float)(height - num) - 0.5f)) * ((float)i - ((float)(height - num) - 0.5f));
                                    } else {
                                        num2 = -1f;
                                    }
                                }
                            }
                        }
                        bool flag6 = num2 < 0f || num2 <= radius * radius;
                        if(flag6) {
                            color2 = color;
                        }
                        texture2D2.SetPixel(j, i, color2);
                    }
                }
                texture2D2.Apply(false);
                result = texture2D2;
            }
            return result;
        }

        private void InitializeModernGUI() {
            bool flag = this.modernSkin == null;
            if(flag) {
                this.modernSkin = ScriptableObject.CreateInstance<GUISkin>();
                this.modernSkin.name = "AMCModernSkin";
            }
            int num = 64;
            this.cornerRadius = this.guiCornerRadiusConfig.Value;
            this.guiFontSize = this.guiFontSizeConfig.Value;
            Color value = this.guiBackgroundColorConfig.Value;
            Color value2 = this.guiPillColorConfig.Value;
            Color value3 = this.guiActiveCategoryColorConfig.Value;
            Color value4 = this.guiButtonColorConfig.Value;
            Color value5 = this.guiButtonHoverColorConfig.Value;
            Color value6 = this.guiButtonActiveColorConfig.Value;
            Color value7 = this.guiRedButtonColorConfig.Value;
            Color value8 = this.guiRedButtonHoverColorConfig.Value;
            Color value9 = this.guiRedButtonActiveColorConfig.Value;
            Color value10 = this.guiTextColorConfig.Value;
            Color value11 = this.guiSliderThumbColorConfig.Value;
            Color value12 = this.guiSliderBgColorConfig.Value;
            this.windowBgTexture = this.CreateRoundedRectTexture(num, num, this.cornerRadius, value);
            this.pillBgTexture = this.CreateRoundedRectTexture(num, num, 20f, value2);
            this.activeCategoryBgTexture = this.CreateRoundedRectTexture(num, num, 8f, value3);
            this.buttonTexture = this.CreateRoundedRectTexture(num, num, 8f, value4);
            this.buttonHoverTexture = this.CreateRoundedRectTexture(num, num, 8f, value5);
            this.buttonActiveTexture = this.CreateRoundedRectTexture(num, num, 8f, value6);
            this.redButtonTexture = this.CreateRoundedRectTexture(num, num, 8f, value7);
            this.redButtonHoverTexture = this.CreateRoundedRectTexture(num, num, 8f, value8);
            this.redButtonActiveTexture = this.CreateRoundedRectTexture(num, num, 8f, value9);
            this.boxBgTexture = this.CreateRoundedRectTexture(num, num, 8f, new Color(0f, 0f, 0f, 0.2f));
            this.sliderFillTexture = this.CreateRoundedRectTexture(num, num, 4f, value11);
            this.sliderBgTexture = this.CreateRoundedRectTexture(num, num, 4f, value12);
            TMP_FontAsset tmp_FontAsset = this.selectedGuiFont;
            Font font = ((tmp_FontAsset != null) ? tmp_FontAsset.sourceFontFile : null) ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            int num2 = Mathf.FloorToInt(Mathf.Max(1f, this.cornerRadius));
            this.modernSkin.window.normal.background = this.windowBgTexture;
            this.modernSkin.window.border = new RectOffset(num2, num2, num2, num2);
            this.modernSkin.window.normal.textColor = value10;
            this.modernSkin.window.padding = new RectOffset(20, 20, 20, 20);
            this.modernSkin.window.font = font;
            this.modernSkin.window.fontSize = (int)((float)this.guiFontSize * 1.5f);
            this.modernSkin.window.alignment = TextAnchor.UpperCenter;
            this.modernSkin.window.richText = true;
            this.modernSkin.button.normal.background = this.buttonTexture;
            this.modernSkin.button.hover.background = this.buttonHoverTexture;
            this.modernSkin.button.active.background = this.buttonActiveTexture;
            this.modernSkin.button.normal.textColor = value10;
            this.modernSkin.button.hover.textColor = Color.white;
            this.modernSkin.button.active.textColor = Color.white;
            this.modernSkin.button.padding = new RectOffset(10, 10, 8, 8);
            this.modernSkin.button.margin = new RectOffset(4, 4, 4, 4);
            this.modernSkin.button.border = new RectOffset(8, 8, 8, 8);
            this.modernSkin.button.alignment = TextAnchor.MiddleCenter;
            this.modernSkin.button.font = font;
            this.modernSkin.button.fontSize = this.guiFontSize;
            this.modernSkin.button.richText = true;
            this.modernSkin.button.imagePosition = ImagePosition.ImageLeft;
            this.modernSkin.label.normal.textColor = value10;
            this.modernSkin.label.padding = new RectOffset(2, 2, 5, 5);
            this.modernSkin.label.alignment = TextAnchor.MiddleLeft;
            this.modernSkin.label.font = font;
            this.modernSkin.label.fontSize = this.guiFontSize;
            this.modernSkin.label.richText = true;
            this.modernSkin.label.wordWrap = true;
            this.modernSkin.textField.font = font;
            this.modernSkin.textField.fontSize = this.guiFontSize;
            this.modernSkin.textField.normal.textColor = value10;
            this.modernSkin.textField.focused.textColor = Color.white;
            this.modernSkin.textField.normal.background = this.CreateRoundedRectTexture(20, 20, 6f, new Color(0f, 0f, 0f, 0.3f));
            this.modernSkin.textField.focused.background = this.CreateRoundedRectTexture(20, 20, 6f, new Color(0f, 0f, 0f, 0.4f));
            this.modernSkin.textField.padding = new RectOffset(8, 8, 8, 8);
            this.modernSkin.textField.border = new RectOffset(6, 6, 6, 6);
            this.modernSkin.horizontalSlider.normal.background = this.CreateSolidColorTexture(Color.clear);
            this.modernSkin.horizontalSliderThumb.normal.background = this.CreateSolidColorTexture(Color.clear);
            this.modernSkin.horizontalSlider.fixedHeight = 18f;
            this.modernSkin.horizontalSliderThumb.fixedWidth = 10f;
            this.modernSkin.horizontalSliderThumb.fixedHeight = 18f;
            this.modernSkin.horizontalSlider.margin = new RectOffset(4, 4, 8, 8);
            this.modernSkin.toggle.normal.background = this.buttonTexture;
            this.modernSkin.toggle.onNormal.background = this.activeCategoryBgTexture;
            this.modernSkin.toggle.hover.background = this.buttonHoverTexture;
            this.modernSkin.toggle.onHover.background = this.activeCategoryBgTexture;
            this.modernSkin.toggle.alignment = TextAnchor.MiddleCenter;
            this.modernSkin.box.normal.background = this.boxBgTexture;
            this.modernSkin.box.border = new RectOffset(10, 10, 10, 10);
            this.modernSkin.box.padding = new RectOffset(10, 10, 10, 10);
        }

        private Texture2D LoadIcon(string fileName) {
            string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), "Icons", fileName.ToLowerInvariant());
            bool flag = File.Exists(path);
            if(flag) {
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                bool flag2 = texture2D.LoadImage(data, false);
                if(flag2) {
                    texture2D.filterMode = FilterMode.Bilinear;
                    return texture2D;
                }
            }
            Texture2D texture2D2 = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture2D2.SetPixel(0, 0, Color.clear);
            texture2D2.Apply();
            return texture2D2;
        }

        private void LoadFonts() {
            this.availableFonts.Clear();
            List<string> list = new List<string>();
            bool flag = TMP_Settings.defaultFontAsset != null;
            if(flag) {
                this.availableFonts.Add(TMP_Settings.defaultFontAsset);
                list.Add("Default");
            }
            string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), "Fonts");
            bool flag2 = Directory.Exists(path);
            if(flag2) {
                string[] array = Directory.GetFiles(path, "*.ttf").Concat(Directory.GetFiles(path, "*.otf")).ToArray<string>();
                foreach(string text in array) {
                    bool flag3 = Path.GetFileName(text).Equals("Designer.otf", StringComparison.OrdinalIgnoreCase);
                    if(!flag3) {
                        Font font = new Font(text);
                        TMP_FontAsset tmp_FontAsset = TMP_FontAsset.CreateFontAsset(font);
                        bool flag4 = tmp_FontAsset != null;
                        if(flag4) {
                            tmp_FontAsset.name = Path.GetFileNameWithoutExtension(text);
                            this.availableFonts.Add(tmp_FontAsset);
                            list.Add(tmp_FontAsset.name);
                        }
                    }
                }
            }
            this.fontNames = list.ToArray();
            this.EnsureValidFontSelected();
        }

        private void EnsureValidFontSelected() {
            CastingMod.<> c__DisplayClass358_0 CS$<> 8__locals1 = new CastingMod.<> c__DisplayClass358_0();
            bool flag = this.availableFonts == null || this.availableFonts.Count == 0;
            if(!flag) {
                CastingMod.<> c__DisplayClass358_0 CS$<> 8__locals2 = CS$<> 8__locals1;
                ConfigEntry<string> configEntry = this.selectedFontNameConfig;
                CS$<> 8__locals2.savedFontName = (((configEntry != null) ? configEntry.Value : null) ?? "Default");
                this.selectedFontIndex = Array.FindIndex<string>(this.fontNames ?? new string[0], (string name) => name.Equals(CS$<> 8__locals1.savedFontName, StringComparison.OrdinalIgnoreCase));
                bool flag2 = this.selectedFontIndex == -1;
                if(flag2) {
                    this.selectedFontIndex = 0;
                }
                this.selectedFont = this.availableFonts[this.selectedFontIndex];
                this.selectedFontNameConfig.Value = this.fontNames[this.selectedFontIndex];
            }
        }

        private void LoadSelectedFontFromConfig() {
            string fontNameFromConfig = this.selectedFontNameConfig.Value ?? "Default";
            this.selectedFontIndex = Array.FindIndex<string>(this.fontNames ?? new string[0], (string name) => name.Equals(fontNameFromConfig, StringComparison.OrdinalIgnoreCase));
            bool flag = this.selectedFontIndex != -1;
            if(flag) {
                this.selectedFont = this.availableFonts[this.selectedFontIndex];
            } else {
                this.EnsureValidFontSelected();
            }
        }

        private void LoadGuiFontFromConfig() {
            bool flag = this.availableFonts == null || this.availableFonts.Count == 0 || this.fontNames == null;
            if(!flag) {
                string fontNameFromConfig = this.guiFontNameConfig.Value ?? "Default";
                this.selectedGuiFontIndex = Array.FindIndex<string>(this.fontNames, (string name) => name.Equals(fontNameFromConfig, StringComparison.OrdinalIgnoreCase));
                bool flag2 = this.selectedGuiFontIndex != -1;
                if(flag2) {
                    this.selectedGuiFont = this.availableFonts[this.selectedGuiFontIndex];
                } else {
                    this.selectedGuiFontIndex = 0;
                    bool flag3 = this.availableFonts.Count > 0;
                    if(flag3) {
                        this.selectedGuiFont = this.availableFonts[0];
                        this.guiFontNameConfig.Value = this.fontNames[0];
                    }
                }
                this.InitializeModernGUI();
            }
        }

        private void LoadLeaderboardFontFromConfig() {
            bool flag = this.availableFonts == null || this.availableFonts.Count == 0 || this.fontNames == null;
            if(!flag) {
                string fontNameFromConfig = this.leaderboardFontNameConfig.Value ?? "Default";
                this.leaderboardSelectedFontIndex = Array.FindIndex<string>(this.fontNames, (string name) => name.Equals(fontNameFromConfig, StringComparison.OrdinalIgnoreCase));
                bool flag2 = this.leaderboardSelectedFontIndex != -1;
                if(flag2) {
                    this.leaderboardSelectedFont = this.availableFonts[this.leaderboardSelectedFontIndex];
                } else {
                    this.leaderboardSelectedFontIndex = 0;
                    bool flag3 = this.availableFonts.Count > 0;
                    if(flag3) {
                        this.leaderboardSelectedFont = this.availableFonts[0];
                        this.leaderboardFontNameConfig.Value = this.fontNames[0];
                    }
                }
            }
        }

        private void LoadDesignerFont() {
            string text = Path.Combine(Path.GetDirectoryName(base.Info.Location), "Fonts", "Designer.otf");
            bool flag = File.Exists(text);
            if(flag) {
                Font font = new Font(text);
                this.Designer = TMP_FontAsset.CreateFontAsset(font);
                this.Designer.name = "DesignerCustom";
            }
            bool flag2 = this.Designer == null;
            if(flag2) {
                TMP_FontAsset designer;
                if((designer = this.selectedFont) == null) {
                    designer = (TMP_Settings.defaultFontAsset ?? this.availableFonts.FirstOrDefault<TMP_FontAsset>());
                }
                this.Designer = designer;
            }
        }

        private IEnumerator ReinitializeCameras(bool isInitialSetup = false) {
            bool flag = this.isReAdjusting;
            if(flag) {
                yield break;
            }
            this.isReAdjusting = true;
            bool flag2 = this.readjustingLabel != null;
            if(flag2) {
                this.readjustingLabel.text = (isInitialSetup ? "INITIALIZING CAMERAS..." : "RE-CONFIGURING CAMERAS...");
                this.readjustingLabel.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(0.5f);
            this.castingCameraObject = GameObject.Find("Shoulder Camera");
            bool flag3 = this.castingCameraObject == null;
            if(flag3) {
                bool flag4 = this.readjustingLabel != null;
                if(flag4) {
                    this.readjustingLabel.text = "ERROR: NO 'Shoulder Camera'!";
                }
                this.isReAdjusting = false;
                yield break;
            }
            bool flag5 = !this.CacheHmdCamera() || this.mainCamera == null;
            if(flag5) {
                bool flag6 = this.readjustingLabel != null;
                if(flag6) {
                    this.readjustingLabel.text = "ERROR: NO HMD CAMERA!";
                }
                this.isReAdjusting = false;
                yield break;
            }
            this.castingCamera = this.castingCameraObject.GetComponent<Camera>();
            bool flag7 = this.castingCamera == null;
            if(flag7) {
                bool flag8 = this.readjustingLabel != null;
                if(flag8) {
                    this.readjustingLabel.text = "ERROR: CASTING CAM HAS NO <Camera>!";
                }
                this.isReAdjusting = false;
                yield break;
            }
            this.blurController = this.castingCameraObject.GetComponent<CameraBlurController>();
            bool flag9 = this.blurController == null;
            if(flag9) {
                this.blurController = this.castingCameraObject.AddComponent<CameraBlurController>();
            }
            this.ConfigureCastingCamera();
            this.DisableOtherCameras();
            bool flag10 = this.readjustingLabel != null;
            if(flag10) {
                this.readjustingLabel.gameObject.SetActive(false);
            }
            this.isReAdjusting = false;
            yield break;
        }

        private bool CacheHmdCamera() {
            GorillaTagger instance = GorillaTagger.Instance;
            GameObject gameObject;
            if(instance == null) {
                gameObject = null;
            } else {
                GameObject gameObject2 = instance.mainCamera;
                gameObject = ((gameObject2 != null) ? gameObject2.gameObject : null);
            }
            this.mainCamera = gameObject;
            bool flag = this.mainCamera == null;
            if(flag) {
                this.mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
            bool flag2 = this.mainCamera == null;
            if(flag2) {
                TrackedPoseDriver[] array = UnityEngine.Object.FindObjectsOfType<TrackedPoseDriver>();
                foreach(TrackedPoseDriver trackedPoseDriver in array) {
                    bool flag3 = trackedPoseDriver.gameObject != this.castingCameraObject && trackedPoseDriver.GetComponent<Camera>() != null;
                    if(flag3) {
                        this.mainCamera = trackedPoseDriver.gameObject;
                        break;
                    }
                }
            }
            return this.mainCamera != null;
        }

        private void ConfigureCastingCamera() {
            bool flag = this.castingCamera == null;
            if(!flag) {
                foreach(object obj in this.castingCameraObject.transform) {
                    Transform transform = (Transform)obj;
                    transform.gameObject.SetActive(false);
                }
                bool flag2 = this.isHoldableCameraEnabled && this.cameraScreenRenderTexture != null;
                if(flag2) {
                    this.castingCamera.targetTexture = this.cameraScreenRenderTexture;
                } else {
                    this.castingCamera.targetTexture = null;
                    this.castingCamera.targetDisplay = 0;
                }
                this.castingCamera.clearFlags = CameraClearFlags.Skybox;
                this.castingCamera.backgroundColor = Color.black;
                this.castingCamera.cullingMask = -1;
                this.castingCamera.nearClipPlane = 0.05f;
                this.castingCamera.farClipPlane = this.farClipPlane;
                this.castingCamera.depth = 100f;
                this.castingCamera.allowHDR = false;
                this.castingCamera.allowMSAA = false;
                this.castingCamera.stereoTargetEye = StereoTargetEyeMask.None;
                bool flag3 = this.castingCamera.GetComponent<TrackedPoseDriver>() != null;
                if(flag3) {
                    UnityEngine.Object.Destroy(this.castingCamera.GetComponent<TrackedPoseDriver>());
                }
                this.castingCamera.fieldOfView = this.FOV;
                this.castingCamera.tag = "Untagged";
            }
        }

        private void CreateReadjustingLabel() {
            bool flag = this.readjustingLabel != null;
            if(!flag) {
                GameObject gameObject = new GameObject("AMC_ReadjustingCanvas");
                Canvas canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                UnityEngine.Object.DontDestroyOnLoad(gameObject);
                GameObject gameObject2 = new GameObject("ReadjustingLabel");
                gameObject2.transform.SetParent(gameObject.transform, false);
                this.readjustingLabel = gameObject2.AddComponent<TextMeshProUGUI>();
                this.readjustingLabel.rectTransform.anchorMin = Vector2.zero;
                this.readjustingLabel.rectTransform.anchorMax = Vector2.one;
                this.readjustingLabel.rectTransform.offsetMin = Vector2.zero;
                this.readjustingLabel.rectTransform.offsetMax = Vector2.zero;
                this.readjustingLabel.alignment = TextAlignmentOptions.Center;
                this.readjustingLabel.fontSize = 60f;
                this.readjustingLabel.color = Color.red;
                this.readjustingLabel.font = (this.Designer ?? TMP_Settings.defaultFontAsset);
                this.readjustingLabel.raycastTarget = false;
                gameObject2.SetActive(false);
            }
        }

        private void DisableOtherCameras() {
            bool flag = this.castingCamera == null || this.mainCamera == null;
            if(!flag) {
                Camera component = this.mainCamera.GetComponent<Camera>();
                bool flag2 = component == null;
                if(!flag2) {
                    Camera[] array = UnityEngine.Object.FindObjectsOfType<Camera>();
                    foreach(Camera camera in array) {
                        bool flag3 = camera == null;
                        if(!flag3) {
                            bool flag4 = camera == component;
                            if(flag4) {
                                camera.enabled = true;
                                camera.stereoTargetEye = StereoTargetEyeMask.Both;
                                camera.depth = -1f;
                            } else {
                                bool flag5 = camera != this.castingCamera;
                                if(flag5) {
                                    bool flag6 = camera.targetTexture == null && camera.GetComponentInParent<Canvas>() == null;
                                    if(flag6) {
                                        camera.enabled = false;
                                    }
                                }
                            }
                        }
                    }
                    this.castingCamera.enabled = true;
                }
            }
        }

        private float EaseOutCubic(float x) {
            return 1f - Mathf.Pow(1f - x, 3f);
        }

        private void Update() {
            bool flag = this.isInitializing;
            if(flag) {
                NotificationManager.Update();
            } else {
                bool flag2 = !this.isTrialActive;
                if(!flag2) {
                    this.LoadSettingsFromConfig();
                    PhotonNetworkController.Instance.disableAFKKick = true;
                    this.loadingIconRotation += Time.unscaledDeltaTime * -200f;
                    this.UpdateDiagnostics();
                    NotificationManager.Update();
                    AudioManager.Update();
                    this.UpdateHoldableCameraStatus();
                    this.HandleAdminControls();
                    this.UpdateAnimatedElements();
                    this.HandleSpectatorSelection();
                    bool flag3 = this.mainCamera == null || this.castingCameraObject == null || this.castingCamera == null || !this.castingCamera.enabled;
                    if(flag3) {
                        this.mainCameraCheckTimer += Time.deltaTime;
                        bool flag4 = this.mainCameraCheckTimer >= 5f && !this.isReAdjusting;
                        if(flag4) {
                            this.mainCameraCheckTimer = 0f;
                            base.StartCoroutine(this.ReinitializeCameras(false));
                        }
                        bool flag5 = this.isReAdjusting;
                        if(flag5) {
                            return;
                        }
                    } else {
                        bool flag6 = this.isReAdjusting;
                        if(flag6) {
                            bool flag7 = this.readjustingLabel != null;
                            if(flag7) {
                                this.readjustingLabel.gameObject.SetActive(false);
                            }
                            this.isReAdjusting = false;
                        }
                    }
                    bool flag8 = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && !this.showWelcomePanel;
                    if(flag8) {
                        bool flag9 = this.guiState == CastingMod.GuiAnimationState.Visible || this.guiState == CastingMod.GuiAnimationState.AnimatingIn;
                        if(flag9) {
                            this.guiState = CastingMod.GuiAnimationState.AnimatingOut;
                            AudioManager.PlayClose();
                        } else {
                            this.guiState = CastingMod.GuiAnimationState.AnimatingIn;
                            this.currentPage = CastingMod.CurrentGUIPage.MainMenu;
                            this.searchQuery = "";
                            AudioManager.PlayOpen();
                        }
                    }
                    float num = Time.unscaledDeltaTime / this.guiAnimationDuration;
                    bool flag10 = this.guiState == CastingMod.GuiAnimationState.AnimatingIn;
                    if(flag10) {
                        this.guiAnimationProgress = Mathf.Clamp01(this.guiAnimationProgress + num);
                        bool flag11 = this.guiAnimationProgress >= 1f;
                        if(flag11) {
                            this.guiState = CastingMod.GuiAnimationState.Visible;
                        }
                    } else {
                        bool flag12 = this.guiState == CastingMod.GuiAnimationState.AnimatingOut;
                        if(flag12) {
                            this.guiAnimationProgress = Mathf.Clamp01(this.guiAnimationProgress - num);
                            bool flag13 = this.guiAnimationProgress <= 0f;
                            if(flag13) {
                                this.guiState = CastingMod.GuiAnimationState.Hidden;
                            }
                        }
                    }
                    bool flag14 = this.isJoiningRoom && this.joinRoomFade < 1f;
                    if(flag14) {
                        this.joinRoomFade = Mathf.Clamp01(this.joinRoomFade + Time.unscaledDeltaTime * 2f);
                    } else {
                        bool flag15 = !this.isJoiningRoom && this.joinRoomFade > 0f;
                        if(flag15) {
                            this.joinRoomFade = Mathf.Clamp01(this.joinRoomFade - Time.unscaledDeltaTime * 2f);
                        }
                    }
                    this.HandleScoreboardHotkeys();
                    bool flag16 = this.guiState != CastingMod.GuiAnimationState.Hidden || this.isChatWindowVisible;
                    bool flag17 = GUIUtility.keyboardControl == 0 && GUIUtility.hotControl == 0 && !this.isReAdjusting && this.castingCamera != null && this.castingCamera.enabled;
                    if(flag17) {
                        bool flag18 = this.isFreecamEnabled;
                        if(flag18) {
                            this.HandleFreecamMovement();
                            this.HandleFreecamLook();
                        }
                        bool flag19 = this.isCustomTargetEnabled && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && flag16 && !this.IsMouseOverMainWindow();
                        if(flag19) {
                            this.SelectTargetByMouseClick();
                        }
                    }
                    bool flag20 = this.isSpectatorIndicatorEnabled && PhotonNetwork.InRoom;
                    if(flag20) {
                        this.UpdateSpectatorIndicators();
                    } else {
                        bool flag21 = (!this.isSpectatorIndicatorEnabled || !PhotonNetwork.InRoom) && this.spectatorIndicators.Count > 0;
                        if(flag21) {
                            this.DestroySpectatorIndicators();
                        }
                    }
                    bool flag22 = Mathf.Abs(this.previousLerping - this.Lerping) > 0.01f;
                    if(flag22) {
                        this.ApplyPlayerLerp();
                        this.previousLerping = this.Lerping;
                    }
                    bool flag23 = this.isTimerRunning;
                    if(flag23) {
                        this.scoreboardTimer += Time.deltaTime;
                    }
                }
            }
        }

        private void LateUpdate() {
            bool flag = !this.isTrialActive || this.isInitializing || this.isReAdjusting || this.castingCameraObject == null || this.castingCamera == null || !this.castingCamera.enabled;
            if(!flag) {
                bool flag2 = this.currentCameraMode == CastingMod.CameraMode.Freecam;
                if(flag2) {
                    this.castingCamera.fieldOfView = Mathf.Lerp(this.castingCamera.fieldOfView, this.freecamFov, Time.deltaTime * 10f);
                } else {
                    bool flag3 = this.currentCameraMode == CastingMod.CameraMode.FirstPerson;
                    if(flag3) {
                        this.HandleFirstPersonCamera();
                        this.castingCamera.fieldOfView = Mathf.Lerp(this.castingCamera.fieldOfView, this.FOV, Time.deltaTime * 10f);
                    } else {
                        bool flag4 = this.currentCameraMode == CastingMod.CameraMode.ThirdPerson;
                        if(flag4) {
                            this.HandleThirdPersonCameraPositionAndRotation();
                        }
                    }
                }
                bool flag5 = this.isHoldableCameraEnabled && this.holdableCameraController != null && this.holdableCameraController.IsHeld;
                if(flag5) {
                    this.castingCameraObject.transform.position = this.holdableCameraInstance.transform.position;
                    this.castingCameraObject.transform.rotation = this.holdableCameraInstance.transform.rotation;
                }
                bool flag6 = Mathf.Abs(this.castingCamera.farClipPlane - this.farClipPlane) > 0.1f;
                if(flag6) {
                    this.castingCamera.farClipPlane = this.farClipPlane;
                }
                bool flag7 = this.areNametagsEnabled && this.Designer != null && GorillaParent.instance != null && this.castingCamera.enabled;
                if(flag7) {
                    this.DrawNametags();
                }
            }
        }

        private void HandleFirstPersonCamera() {
            bool flag = this.mainCamera == null && !this.CacheHmdCamera();
            if(!flag) {
                bool flag2 = this.spectatedRig != null;
                Transform transform;
                if(flag2) {
                    transform = this.spectatedRig.head.rigTarget;
                } else {
                    bool flag3 = this.selectedTargetObject != null && this.selectedTargetObject.activeInHierarchy;
                    if(flag3) {
                        transform = this.selectedTargetObject.transform;
                    } else {
                        transform = this.mainCamera.transform;
                    }
                }
                bool flag4 = transform == null || this.castingCameraObject == null;
                if(!flag4) {
                    bool flag5 = this.isFPPositionSmoothingEnabled;
                    if(flag5) {
                        this.SmoothFollowTargetPosition(transform, this.fpPositionSmoothingFactor);
                    } else {
                        this.FollowTargetPosition(transform);
                    }
                    bool flag6 = this.isFPRotationSmoothingEnabled;
                    if(flag6) {
                        this.SmoothFollowTargetRotation(transform, this.fpRotationSmoothingFactor);
                    } else {
                        this.FollowTargetRotation(transform);
                    }
                }
            }
        }

        private void FollowTargetPosition(Transform target) {
            bool flag = target != null;
            if(flag) {
                this.castingCameraObject.transform.position = target.position;
            }
        }

        private void FollowTargetRotation(Transform target) {
            bool flag = target != null;
            if(flag) {
                this.castingCameraObject.transform.rotation = target.rotation;
            }
        }

        private void SmoothFollowTargetPosition(Transform target, float smoothFactor) {
            bool flag = target != null;
            if(flag) {
                this.castingCameraObject.transform.position = Vector3.SmoothDamp(this.castingCameraObject.transform.position, target.position, ref this.firstPersonPositionVelocity, Mathf.Max(0.001f, smoothFactor));
            }
        }

        private void SmoothFollowTargetRotation(Transform target, float smoothFactor) {
            bool flag = target != null;
            if(flag) {
                this.castingCameraObject.transform.rotation = Quaternion.Slerp(this.castingCameraObject.transform.rotation, target.rotation, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smoothFactor)));
            }
        }

        private void HandleFreecamMovement() {
            bool flag = this.castingCameraObject == null || Keyboard.current == null;
            if(!flag) {
                float d = this.freecamMoveSpeedSetting * (Keyboard.current.leftCtrlKey.isPressed ? 2.5f : 1f) * (Keyboard.current.leftAltKey.isPressed ? 0.4f : 1f);
                float x = (float)((Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0));
                float z = (float)((Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0));
                float y = (float)((Keyboard.current.spaceKey.isPressed ? 1 : 0) - (Keyboard.current.leftShiftKey.isPressed ? 1 : 0));
                Vector3 vector = new Vector3(x, y, z);
                Vector3 a = this.castingCameraObject.transform.TransformDirection(vector.normalized);
                this.castingCameraObject.transform.position = Vector3.SmoothDamp(this.castingCameraObject.transform.position, this.castingCameraObject.transform.position + a * d, ref this.freecamVelocity, this.freecamSmoothTime, float.MaxValue, Time.deltaTime);
                float num = (float)((Keyboard.current.eKey.isPressed ? 1 : 0) - (Keyboard.current.qKey.isPressed ? 1 : 0));
                bool flag2 = Mathf.Abs(num) > 0.01f;
                if(flag2) {
                    this.castingCameraObject.transform.Rotate(Vector3.forward, num * this.freecamRollSpeed * Time.deltaTime, Space.Self);
                }
            }
        }

        private void HandleFreecamLook() {
            bool flag = this.castingCameraObject == null || Mouse.current == null || Keyboard.current == null;
            if(!flag) {
                bool flag2 = this.isMouseLookFreecam;
                float angle;
                float angle2;
                if(flag2) {
                    bool isPressed = Mouse.current.rightButton.isPressed;
                    if(!isPressed) {
                        bool flag3 = Cursor.lockState > CursorLockMode.None;
                        if(flag3) {
                            Cursor.lockState = CursorLockMode.None;
                            Cursor.visible = true;
                        }
                        return;
                    }
                    bool flag4 = Cursor.lockState != CursorLockMode.Locked;
                    if(flag4) {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    Vector2 vector = Mouse.current.delta.ReadValue() * 0.05f;
                    angle = vector.x * this.freecamLookSpeedSetting * 20f * Time.deltaTime;
                    angle2 = -vector.y * this.freecamLookSpeedSetting * 20f * Time.deltaTime;
                } else {
                    bool flag5 = Cursor.lockState > CursorLockMode.None;
                    if(flag5) {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    angle = (float)((Keyboard.current.rightArrowKey.isPressed ? 1 : 0) - (Keyboard.current.leftArrowKey.isPressed ? 1 : 0)) * this.freecamLookSpeedSetting * Time.deltaTime;
                    angle2 = (float)((Keyboard.current.upArrowKey.isPressed ? 1 : 0) - (Keyboard.current.downArrowKey.isPressed ? 1 : 0)) * this.freecamLookSpeedSetting * Time.deltaTime;
                }
                this.castingCameraObject.transform.Rotate(Vector3.up, angle, Space.World);
                this.castingCameraObject.transform.Rotate(Vector3.right, angle2, Space.Self);
                Vector3 localEulerAngles = this.castingCameraObject.transform.localEulerAngles;
                float value = (localEulerAngles.x > 180f) ? (localEulerAngles.x - 360f) : localEulerAngles.x;
                this.castingCameraObject.transform.localEulerAngles = new Vector3(Mathf.Clamp(value, -89f, 89f), localEulerAngles.y, localEulerAngles.z);
            }
        }

        private void HandleThirdPersonCameraPositionAndRotation() {
            bool flag = this.castingCameraObject == null;
            if(!flag) {
                Transform thirdPersonTargetTransform = this.GetThirdPersonTargetTransform();
                bool flag2 = thirdPersonTargetTransform == null;
                if(!flag2) {
                    float b = this.FOV;
                    bool flag3 = this.thirdPersonModeSetting == CastingMod.ThirdPersonMode.Cinematic;
                    Vector3 vector;
                    Quaternion b3;
                    if(flag3) {
                        this.cinematicOrbitAngle += Time.deltaTime * this.cinematicOrbitSpeed;
                        Vector3 b2 = Quaternion.Euler(0f, this.cinematicOrbitAngle, 0f) * this.thirdPersonOffset;
                        vector = thirdPersonTargetTransform.position + b2;
                        float value = Vector3.Distance(vector, thirdPersonTargetTransform.position);
                        float t = Mathf.Clamp01(Mathf.InverseLerp(this.cinematicMinDist, this.cinematicMaxDist, value));
                        b = Mathf.Lerp(this.cinematicMaxFov, this.cinematicMinFov, t);
                        b3 = Quaternion.LookRotation(thirdPersonTargetTransform.position - vector);
                    } else {
                        Vector3 b4 = thirdPersonTargetTransform.rotation * this.thirdPersonOffset;
                        vector = thirdPersonTargetTransform.position + b4;
                        bool flag4 = this.isThirdPersonYLocked;
                        if(flag4) {
                            vector.y = thirdPersonTargetTransform.position.y + this.thirdPersonOffset.y;
                        }
                        bool flag5 = this.isThirdPersonRotationLocked;
                        if(flag5) {
                            b3 = this.lockedThirdPersonRotation;
                        } else {
                            bool flag6 = this.thirdPersonLookAtTarget;
                            if(flag6) {
                                b3 = Quaternion.LookRotation(thirdPersonTargetTransform.position - this.castingCameraObject.transform.position);
                            } else {
                                b3 = Quaternion.LookRotation(thirdPersonTargetTransform.position - vector);
                            }
                        }
                    }
                    bool flag7 = this.thirdPersonCollision && GTPlayer.Instance != null;
                    if(flag7) {
                        Vector3 vector2 = vector - thirdPersonTargetTransform.position;
                        RaycastHit raycastHit;
                        bool flag8 = Physics.Raycast(thirdPersonTargetTransform.position, vector2.normalized, out raycastHit, vector2.magnitude, GTPlayer.Instance.locomotionEnabledLayers.value);
                        if(flag8) {
                            vector = raycastHit.point - vector2.normalized * 0.1f;
                        }
                    }
                    this.castingCameraObject.transform.position = Vector3.SmoothDamp(this.castingCameraObject.transform.position, vector, ref this.currentThirdPersonPositionVelocity, this.thirdPersonPositionSmoothness);
                    this.castingCameraObject.transform.rotation = Quaternion.Slerp(this.castingCameraObject.transform.rotation, b3, 1f - Mathf.Exp(-Time.deltaTime / this.thirdPersonRotationSmoothness));
                    this.castingCamera.fieldOfView = Mathf.Lerp(this.castingCamera.fieldOfView, b, Time.deltaTime * 10f);
                }
            }
        }

        private Transform GetThirdPersonTargetTransform() {
            bool flag = this.spectatedRig != null;
            Transform result;
            if(flag) {
                result = this.spectatedRig.head.rigTarget;
            } else {
                bool flag2 = this.selectedTargetObject != null && this.selectedTargetObject.activeInHierarchy;
                if(flag2) {
                    result = this.selectedTargetObject.transform;
                } else {
                    GorillaTagger instance = GorillaTagger.Instance;
                    VRRig vrrig = (instance != null) ? instance.offlineVRRig : null;
                    bool flag3 = PhotonNetwork.InRoom && GorillaParent.instance != null;
                    if(flag3) {
                        foreach(VRRig vrrig2 in GorillaParent.instance.vrrigs) {
                            bool flag4;
                            if(vrrig2 == null) {
                                flag4 = false;
                            } else {
                                PhotonView component = vrrig2.GetComponent<PhotonView>();
                                flag4 = ((component != null) ? new bool?(component.IsMine) : null).GetValueOrDefault();
                            }
                            bool flag5 = flag4;
                            if(flag5) {
                                vrrig = vrrig2;
                                break;
                            }
                        }
                    }
                    bool flag6 = vrrig != null;
                    if(flag6) {
                        VRMap head = vrrig.head;
                        bool flag7 = ((head != null) ? head.rigTarget : null) != null;
                        if(flag7) {
                            return vrrig.head.rigTarget;
                        }
                    }
                    GameObject gameObject = this.mainCamera;
                    result = ((gameObject != null) ? gameObject.transform : null);
                }
            }
            return result;
        }

        private bool IsMouseOverMainWindow() {
            bool flag = this.guiState == CastingMod.GuiAnimationState.Hidden || Mouse.current == null;
            bool result;
            if(flag) {
                result = false;
            } else {
                Vector2 vector = Mouse.current.position.ReadValue();
                vector.y = (float)Screen.height - vector.y;
                result = this.mainWindowRect.Contains(vector);
            }
            return result;
        }

        private void SelectTargetByMouseClick() {
            bool flag = this.castingCamera != null && this.castingCamera.enabled && Mouse.current != null;
            if(flag) {
                RaycastHit raycastHit;
                bool flag2 = Physics.Raycast(this.castingCamera.ScreenPointToRay(Mouse.current.position.ReadValue()), out raycastHit, 1000f);
                if(flag2) {
                    this.StopSpectating();
                    this.selectedTargetObject = raycastHit.collider.gameObject;
                    this.selectedTargetObjectName = this.selectedTargetObject.name;
                    this.isCustomTargetEnabled = false;
                    this.isCustomTargetEnabledConfig.Value = false;
                }
            }
        }

        private void HandleScoreboardHotkeys() {
            bool flag = GUIUtility.keyboardControl != 0 || Keyboard.current == null;
            if(!flag) {
                bool wasPressedThisFrame = Keyboard.current.gKey.wasPressedThisFrame;
                if(wasPressedThisFrame) {
                    this.isTimerRunning = true;
                }
                bool wasPressedThisFrame2 = Keyboard.current.fKey.wasPressedThisFrame;
                if(wasPressedThisFrame2) {
                    this.isTimerRunning = false;
                }
                bool wasPressedThisFrame3 = Keyboard.current.tKey.wasPressedThisFrame;
                if(wasPressedThisFrame3) {
                    this.scoreboardTimer = 0f;
                }
                bool flag2 = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                bool wasPressedThisFrame4 = Keyboard.current.equalsKey.wasPressedThisFrame;
                if(wasPressedThisFrame4) {
                    bool flag3 = flag2;
                    if(flag3) {
                        this.redScore++;
                    } else {
                        this.blueScore++;
                    }
                }
                bool wasPressedThisFrame5 = Keyboard.current.minusKey.wasPressedThisFrame;
                if(wasPressedThisFrame5) {
                    bool flag4 = flag2;
                    if(flag4) {
                        this.redScore = Mathf.Max(0, this.redScore - 1);
                    } else {
                        this.blueScore = Mathf.Max(0, this.blueScore - 1);
                    }
                }
            }
        }

        private void ApplyPlayerLerp() {
            bool flag = GorillaParent.instance == null;
            if(!flag) {
                this.Lerping = Mathf.Clamp(this.Lerping, 0.01f, 5f);
                float num = 0.1f;
                float num2 = 0.1f;
                foreach(VRRig vrrig in GorillaParent.instance.vrrigs) {
                    bool flag2 = vrrig != null && !vrrig.isOfflineVRRig;
                    if(flag2) {
                        vrrig.lerpValueBody = num * this.Lerping;
                        vrrig.lerpValueFingers = num2 * this.Lerping;
                    }
                }
            }
        }

        private void DrawNametags() {
            List<VRRig> list = (from r in this.nametagObjects.Keys
                                where r == null || !r.isActiveAndEnabled
                                select r).ToList<VRRig>();
            foreach(VRRig key in list) {
                GameObject obj;
                bool flag = this.nametagObjects.TryGetValue(key, out obj);
                if(flag) {
                    UnityEngine.Object.Destroy(obj);
                }
                this.nametagObjects.Remove(key);
            }
            foreach(VRRig vrrig in GorillaParent.instance.vrrigs) {
                if(vrrig == null || !vrrig.isActiveAndEnabled || vrrig.isOfflineVRRig) {
                    goto IL_F4;
                }
                VRMap head = vrrig.head;
                if(((head != null) ? head.rigTarget : null) == null) {
                    goto IL_F4;
                }
                bool flag2 = string.IsNullOrEmpty(vrrig.playerNameVisible);
            IL_F5:
                bool flag3 = flag2;
                if(flag3) {
                    continue;
                }
                GameObject gameObject;
                bool flag4 = !this.nametagObjects.TryGetValue(vrrig, out gameObject);
                if(flag4) {
                    gameObject = new GameObject("Nametag_" + vrrig.playerNameVisible);
                    gameObject.transform.SetParent(vrrig.head.rigTarget, false);
                    TextMeshPro textMeshPro = gameObject.AddComponent<TextMeshPro>();
                    textMeshPro.alignment = TextAlignmentOptions.Center;
                    textMeshPro.font = this.selectedFont;
                    textMeshPro.enableAutoSizing = false;
                    textMeshPro.overflowMode = TextOverflowModes.Overflow;
                    textMeshPro.sortingOrder = 5;
                    textMeshPro.raycastTarget = false;
                    textMeshPro.richText = true;
                    textMeshPro.spriteAsset = this.modUserSpriteAsset;
                    GameObject gameObject2 = new GameObject("Nametag_Background");
                    gameObject2.transform.SetParent(gameObject.transform, false);
                    SpriteRenderer spriteRenderer = gameObject2.AddComponent<SpriteRenderer>();
                    spriteRenderer.sprite = Sprite.Create(this.nametagBgTexture, new Rect(0f, 0f, (float)this.nametagBgTexture.width, (float)this.nametagBgTexture.height), new Vector2(0.5f, 0.5f));
                    spriteRenderer.sortingOrder = 4;
                    this.nametagObjects[vrrig] = gameObject;
                }
                gameObject.transform.localPosition = new Vector3(0f, this.HeadOffsetY, 0f);
                gameObject.transform.rotation = Quaternion.LookRotation(gameObject.transform.position - this.castingCamera.transform.position);
                TextMeshPro component = gameObject.GetComponent<TextMeshPro>();
                bool flag5 = component.font != this.selectedFont;
                if(flag5) {
                    component.font = this.selectedFont;
                }
                bool flag6 = component.spriteAsset == null;
                if(flag6) {
                    component.spriteAsset = this.modUserSpriteAsset;
                }
                component.fontSize = this.nametagSize * 3f;
                PhotonView component2 = vrrig.GetComponent<PhotonView>();
                Player player = (component2 != null) ? component2.Owner : null;
                bool flag7 = player != null && this.IsPlayerAdmin(player);
                bool flag8 = flag7;
                string text2;
                if(flag8) {
                    string text;
                    string str = (this.customAdminTags.TryGetValue(player.ActorNumber, out text) && !string.IsNullOrWhiteSpace(text)) ? text : "[ADMIN]";
                    text2 = "<sprite=0> " + str + " " + vrrig.playerNameVisible.ToUpper();
                } else {
                    text2 = ((this.showCastingModUsers && player != null && this.castingModUsers.Contains(player.ActorNumber)) ? ("<sprite=0> " + vrrig.playerNameVisible.ToUpper()) : vrrig.playerNameVisible.ToUpper());
                }
                List<string> list2 = new List<string>();
                bool flag9 = this.nametagShowPlatform;
                if(flag9) {
                    list2.Add("[" + CastingMod.Platform(vrrig) + "]");
                }
                bool flag10 = this.nametagShowFps;
                if(flag10) {
                    list2.Add(CastingMod.GetFPS(vrrig));
                }
                bool flag11 = this.nametagShowDistance;
                if(flag11) {
                    list2.Add(string.Format("{0:F1}m", Vector3.Distance(this.castingCamera.transform.position, vrrig.head.rigTarget.position)));
                }
                bool flag12 = list2.Count > 0;
                if(flag12) {
                    text2 = text2 + "\n<size=70%>" + string.Join(" ", list2) + "</size>";
                }
                component.text = text2;
                Color color = flag7 ? Color.white : (this.useGlobalNametagColor ? this.globalNametagColor : vrrig.playerColor);
                bool flag13 = this.nametagFadeWithDistance;
                if(flag13) {
                    float value = Vector3.Distance(this.castingCamera.transform.position, vrrig.head.rigTarget.position);
                    color.a *= Mathf.Clamp01(Mathf.InverseLerp(this.nametagFadeEndDistance, this.nametagFadeStartDistance, value));
                }
                component.color = color;
                Transform transform = gameObject.transform.Find("Nametag_Background");
                SpriteRenderer spriteRenderer2 = (transform != null) ? transform.GetComponent<SpriteRenderer>() : null;
                bool flag14 = spriteRenderer2 != null;
                if(flag14) {
                    spriteRenderer2.enabled = this.nametagShowBackground;
                    bool flag15 = this.nametagShowBackground;
                    if(flag15) {
                        Color color2 = this.nametagBackgroundColor;
                        bool flag16 = this.nametagFadeWithDistance;
                        if(flag16) {
                            color2.a *= color.a;
                        }
                        spriteRenderer2.color = color2;
                        component.ForceMeshUpdate(false, false);
                        Vector2 renderedValues = component.GetRenderedValues(false);
                        spriteRenderer2.transform.localScale = new Vector3(renderedValues.x * 0.12f + 0.2f, renderedValues.y * 0.12f + 0.1f, 1f);
                    }
                }
                continue;
            IL_F4:
                flag2 = true;
                goto IL_F5;
            }
        }

        private VRRig FindPlayerVRRig(Player player) {
            bool flag = player == null;
            VRRig result;
            if(flag) {
                result = null;
            } else {
                bool flag2 = GorillaParent.instance != null;
                if(flag2) {
                    foreach(VRRig vrrig in GorillaParent.instance.vrrigs) {
                        int? num;
                        if(vrrig == null) {
                            num = null;
                        } else {
                            PhotonView component = vrrig.GetComponent<PhotonView>();
                            if(component == null) {
                                num = null;
                            } else {
                                Player owner = component.Owner;
                                num = ((owner != null) ? new int?(owner.ActorNumber) : null);
                            }
                        }
                        int? num2 = num;
                        int actorNumber = player.ActorNumber;
                        bool flag3 = num2.GetValueOrDefault() == actorNumber & num2 != null;
                        if(flag3) {
                            return vrrig;
                        }
                    }
                }
                result = null;
            }
            return result;
        }

        public void RefreshSpectatablePlayers() {
            this.spectatableRigs.Clear();
            bool flag = PhotonNetwork.InRoom && GorillaParent.instance != null;
            if(flag) {
                this.spectatableRigs = (from rig in GorillaParent.instance.vrrigs
                                        where rig != null && rig.gameObject.activeInHierarchy && rig.GetComponent<PhotonView>() != null && !rig.GetComponent<PhotonView>().IsMine
                                        orderby rig.GetComponent<PhotonView>().Owner.ActorNumber
                                        select rig).ToList<VRRig>();
            }
            bool flag2 = this.spectatedRig != null && !this.spectatableRigs.Contains(this.spectatedRig);
            if(flag2) {
                this.StopSpectating();
            }
        }

        private void HandleSpectatorSelection() {
            this.spectatorRefreshTimer += Time.deltaTime;
            bool flag = this.spectatorRefreshTimer >= 3f;
            if(flag) {
                this.spectatorRefreshTimer = 0f;
                this.RefreshSpectatablePlayers();
            }
            bool flag2 = GUIUtility.keyboardControl != 0 || this.spectatableRigs.Count == 0;
            if(!flag2) {
                Keyboard current = Keyboard.current;
                bool wasPressedThisFrame = current.pageUpKey.wasPressedThisFrame;
                if(wasPressedThisFrame) {
                    this.SpectateNextPlayer();
                }
                bool wasPressedThisFrame2 = current.pageDownKey.wasPressedThisFrame;
                if(wasPressedThisFrame2) {
                    this.SpectatePreviousPlayer();
                }
                bool wasPressedThisFrame3 = current.endKey.wasPressedThisFrame;
                if(wasPressedThisFrame3) {
                    this.StopSpectating();
                }
                bool wasPressedThisFrame4 = current.digit1Key.wasPressedThisFrame;
                if(wasPressedThisFrame4) {
                    this.SpectatePlayerByIndex(0);
                }
                bool wasPressedThisFrame5 = current.digit2Key.wasPressedThisFrame;
                if(wasPressedThisFrame5) {
                    this.SpectatePlayerByIndex(1);
                }
                bool wasPressedThisFrame6 = current.digit3Key.wasPressedThisFrame;
                if(wasPressedThisFrame6) {
                    this.SpectatePlayerByIndex(2);
                }
                bool wasPressedThisFrame7 = current.digit4Key.wasPressedThisFrame;
                if(wasPressedThisFrame7) {
                    this.SpectatePlayerByIndex(3);
                }
                bool wasPressedThisFrame8 = current.digit5Key.wasPressedThisFrame;
                if(wasPressedThisFrame8) {
                    this.SpectatePlayerByIndex(4);
                }
                bool wasPressedThisFrame9 = current.digit6Key.wasPressedThisFrame;
                if(wasPressedThisFrame9) {
                    this.SpectatePlayerByIndex(5);
                }
                bool wasPressedThisFrame10 = current.digit7Key.wasPressedThisFrame;
                if(wasPressedThisFrame10) {
                    this.SpectatePlayerByIndex(6);
                }
                bool wasPressedThisFrame11 = current.digit8Key.wasPressedThisFrame;
                if(wasPressedThisFrame11) {
                    this.SpectatePlayerByIndex(7);
                }
                bool wasPressedThisFrame12 = current.digit9Key.wasPressedThisFrame;
                if(wasPressedThisFrame12) {
                    this.SpectatePlayerByIndex(8);
                }
                bool wasPressedThisFrame13 = current.digit0Key.wasPressedThisFrame;
                if(wasPressedThisFrame13) {
                    this.SpectatePlayerByIndex(9);
                }
            }
        }

        public void SpectatePlayer(VRRig rig, CastingMod.CameraMode mode) {
            bool flag = rig == null;
            if(!flag) {
                this.spectatedRig = rig;
                this.currentCameraMode = mode;
                this.UpdateCameraModeFromState();
                this.currentSpectatorIndex = this.spectatableRigs.IndexOf(rig);
                this.selectedTargetObject = null;
                this.selectedTargetObjectName = this.spectatedRig.playerNameVisible;
            }
        }

        private void SpectatePlayerByIndex(int index) {
            bool flag = index >= 0 && index < this.spectatableRigs.Count;
            if(flag) {
                this.SpectatePlayer(this.spectatableRigs[index], this.currentCameraMode);
            }
        }

        private void SpectateNextPlayer() {
            bool flag = this.spectatableRigs.Count == 0;
            if(!flag) {
                this.currentSpectatorIndex++;
                bool flag2 = this.currentSpectatorIndex >= this.spectatableRigs.Count;
                if(flag2) {
                    this.currentSpectatorIndex = 0;
                }
                this.SpectatePlayerByIndex(this.currentSpectatorIndex);
            }
        }

        private void SpectatePreviousPlayer() {
            bool flag = this.spectatableRigs.Count == 0;
            if(!flag) {
                this.currentSpectatorIndex--;
                bool flag2 = this.currentSpectatorIndex < 0;
                if(flag2) {
                    this.currentSpectatorIndex = this.spectatableRigs.Count - 1;
                }
                this.SpectatePlayerByIndex(this.currentSpectatorIndex);
            }
        }

        public void StopSpectating() {
            this.spectatedRig = null;
            this.currentSpectatorIndex = -1;
            this.selectedTargetObjectName = "None";
        }

        private void DrawMusicPlayerWindow(int id) {
            bool isInitialized = AudioManager.IsInitialized;
            if(isInitialized) {
                Texture2D image = AudioManager.CurrentCoverArt ?? Texture2D.blackTexture;
                GUI.DrawTexture(new Rect((this.musicPlayerWindowRect.width - 200f) / 2f, 35f, 200f, 200f), image, ScaleMode.ScaleToFit);
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = this.guiTextColorConfig.Value
                    }
                };
                GUILayout.Space(210f);
                GUILayout.Label("<b>" + AudioManager.CurrentTrackName + "</b>", style, Array.Empty<GUILayoutOption>());
                Rect rect = GUILayoutUtility.GetRect(18f, 18f, new GUILayoutOption[]
                {
                    GUILayout.ExpandWidth(true)
                });
                GUI.DrawTexture(rect, this.musicPlayerProgressBgTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * AudioManager.PlaybackProgress, rect.height), this.musicPlayerProgressTexture);
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUILayout.FlexibleSpace();
                bool flag = this.MenuButton(new GUIContent("", this.prevIcon), new GUILayoutOption[]
                {
                    GUILayout.Width(40f),
                    GUILayout.Height(40f)
                });
                if(flag) {
                    AudioManager.PrevTrack();
                }
                bool flag2 = this.MenuButton(AudioManager.IsPlaying ? new GUIContent("", this.pauseIcon) : new GUIContent("", this.playIcon), new GUILayoutOption[]
                {
                    GUILayout.Width(50f),
                    GUILayout.Height(40f)
                });
                if(flag2) {
                    AudioManager.TogglePlayPause();
                }
                bool flag3 = this.MenuButton(new GUIContent("", this.nextIcon), new GUILayoutOption[]
                {
                    GUILayout.Width(40f),
                    GUILayout.Height(40f)
                });
                if(flag3) {
                    AudioManager.NextTrack();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            } else {
                GUILayout.Label("Audio player is initializing...", new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter
                }, Array.Empty<GUILayoutOption>());
            }
            GUI.DragWindow();
        }

        private void DrawDateTimeWindow(int id) {
            GUI.DrawTexture(new Rect(0f, 0f, this.dateTimeWindowRect.width, this.dateTimeWindowRect.height), this.dateTimeBgTexture, ScaleMode.StretchToFill);
            GUIStyle guistyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = this.dateTimeTextColorConfig.Value
                }
            };
            switch(this.clockStyleConfig.Value) {
                case CastingMod.ClockStyle.DigitalText:
                this.dateTimeWindowRect.height = 50f;
                guistyle.fontSize = 24;
                guistyle.fontStyle = FontStyle.Bold;
                GUI.Label(new Rect(0f, 0f, this.dateTimeWindowRect.width, this.dateTimeWindowRect.height), DateTime.Now.ToString(this.dateTimeTimeFormatConfig.Value), guistyle);
                goto IL_207;
                case CastingMod.ClockStyle.Full:
                this.dateTimeWindowRect.height = this.dateTimeHeightConfig.Value;
                guistyle.fontSize = 20;
                GUI.Label(new Rect(0f, 15f, this.dateTimeWindowRect.width, 30f), DateTime.Now.ToString(this.dateTimeTimeFormatConfig.Value), guistyle);
                guistyle.fontSize = 14;
                GUI.Label(new Rect(0f, 45f, this.dateTimeWindowRect.width, 30f), DateTime.Now.ToString(this.dateTimeDateFormatConfig.Value), guistyle);
                goto IL_207;
            }
            this.dateTimeWindowRect.height = this.dateTimeHeightConfig.Value;
            guistyle.fontSize = 28;
            guistyle.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(0f, 0f, this.dateTimeWindowRect.width, this.dateTimeWindowRect.height), DateTime.Now.ToString(this.dateTimeTimeFormatConfig.Value), guistyle);
        IL_207:
            GUI.DragWindow();
        }

        private void DrawDraggableScoreboard() {
            GUIStyle style = new GUIStyle
            {
                normal =
                {
                    background = (this.customScoreboardBg ?? this.scoreboardBgTexture)
                }
            };
            this.redNameRect = GUI.Window(10, this.redNameRect, delegate (int id) {
                this.DrawScoreboardElement(id, this.redTeamName, this.scoreboardTeamNameFontSizeConfig.Value, this.scoreboardRedColorConfig.Value);
            }, "", style);
            this.redScoreRect = GUI.Window(11, this.redScoreRect, delegate (int id) {
                this.DrawScoreboardElement(id, this.redScore.ToString(), this.scoreboardScoreFontSizeConfig.Value, this.scoreboardRedColorConfig.Value);
            }, "", style);
            this.blueNameRect = GUI.Window(12, this.blueNameRect, delegate (int id) {
                this.DrawScoreboardElement(id, this.blueTeamName, this.scoreboardTeamNameFontSizeConfig.Value, this.scoreboardBlueColorConfig.Value);
            }, "", style);
            this.blueScoreRect = GUI.Window(13, this.blueScoreRect, delegate (int id) {
                this.DrawScoreboardElement(id, this.blueScore.ToString(), this.scoreboardScoreFontSizeConfig.Value, this.scoreboardBlueColorConfig.Value);
            }, "", style);
            this.timerRect = GUI.Window(14, this.timerRect, delegate (int id) {
                this.DrawScoreboardElement(id, TimeSpan.FromSeconds((double)this.scoreboardTimer).ToString("mm\\:ss"), this.scoreboardTimerFontSizeConfig.Value, this.scoreboardTextColorConfig.Value);
            }, "", style);
        }

        private void DrawScoreboardElement(int id, string text, int fontSize, Color textColor) {
            GUIStyle style = new GUIStyle(this.modernSkin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                normal =
                {
                    textColor = textColor
                }
            };
            GUI.Label(new Rect(0f, 0f, GUI.Window(id, default(Rect), delegate (int _) {
            }, "").width, GUI.Window(id, default(Rect), delegate (int _) {
            }, "").height), text, style);
            bool flag = !this.scoreboardPositionsLockedConfig.Value;
            if(flag) {
                GUI.DragWindow();
            }
        }

        private void DrawLeaderboardWidget() {
            bool flag = !PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length == 0;
            if(!flag) {
                GUIStyle guistyle = new GUIStyle();
                TMP_FontAsset tmp_FontAsset = this.leaderboardSelectedFont;
                guistyle.font = ((tmp_FontAsset != null) ? tmp_FontAsset.sourceFontFile : null);
                guistyle.fontSize = this.leaderboardFontSizeConfig.Value;
                guistyle.richText = true;
                guistyle.alignment = TextAnchor.MiddleLeft;
                GUIStyle guistyle2 = guistyle;
                bool flag2 = guistyle2.font == null;
                if(flag2) {
                    GUIStyle guistyle3 = guistyle2;
                    TMP_FontAsset designer = this.Designer;
                    guistyle3.font = (((designer != null) ? designer.sourceFontFile : null) ?? GUI.skin.label.font);
                }
                float num = (float)this.leaderboardFontSizeConfig.Value * 1.2f;
                float num2 = 10f;
                float num3 = (float)PhotonNetwork.PlayerList.Length * num + num2 * 2f;
                float width = 300f;
                Rect position = new Rect(20f, (float)Screen.height - num3 - 20f, width, num3);
                bool flag3 = this.leaderboardBgTexture != null;
                if(flag3) {
                    GUI.color = this.leaderboardBgColorConfig.Value;
                    GUI.DrawTexture(position, this.leaderboardBgTexture, ScaleMode.StretchToFill);
                    GUI.color = Color.white;
                }
                GUILayout.BeginArea(new Rect(position.x + num2, position.y + num2, position.width - num2 * 2f, position.height - num2 * 2f));
                List<Player> list = (from p in PhotonNetwork.PlayerList
                                     orderby p.ActorNumber
                                     select p).ToList<Player>();
                foreach(Player player in list) {
                    VRRig vrrig = this.FindPlayerVRRig(player);
                    guistyle2.normal.textColor = ((vrrig != null) ? vrrig.playerColor : this.leaderboardTextColorConfig.Value);
                    guistyle2.fontStyle = (player.IsLocal ? FontStyle.Bold : FontStyle.Normal);
                    string text = string.IsNullOrWhiteSpace(player.NickName) ? "Joining..." : player.NickName;
                    GUILayout.Label(text, guistyle2, new GUILayoutOption[]
                    {
                        GUILayout.Height(num)
                    });
                }
                GUILayout.EndArea();
            }
        }

        private void DrawWatermark() {
            bool flag = this.currentWatermark == null || !this.currentWatermark.isEnabled || this.currentWatermark.elements.Count == 0;
            if(!flag) {
                foreach(WatermarkElement watermarkElement in this.currentWatermark.elements) {
                    Vector2 vector = new Vector2((float)Screen.width * this.currentWatermark.anchorPoint.x, (float)Screen.height * this.currentWatermark.anchorPoint.y);
                    vector += this.currentWatermark.screenOffset;
                    Rect position = new Rect(vector.x + watermarkElement.position.x, vector.y + watermarkElement.position.y, watermarkElement.size.x, watermarkElement.size.y);
                    Vector2 center = position.center;
                    Matrix4x4 matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(watermarkElement.rotation, center);
                    switch(watermarkElement.type) {
                        case WatermarkElement.ElementType.Text: {
                            GUIStyle guistyle = new GUIStyle(GUI.skin.label);
                            guistyle.fontSize = watermarkElement.fontSize;
                            guistyle.normal.textColor = watermarkElement.color;
                            TMP_FontAsset designer = this.Designer;
                            guistyle.font = ((designer != null) ? designer.sourceFontFile : null);
                            guistyle.alignment = watermarkElement.alignment;
                            guistyle.richText = true;
                            GUIStyle style = guistyle;
                            GUI.Label(position, watermarkElement.textContent, style);
                            break;
                        }
                        case WatermarkElement.ElementType.Rectangle:
                        GUI.DrawTexture(position, this.CreateSolidColorTexture(watermarkElement.color));
                        break;
                        case WatermarkElement.ElementType.RoundedRectangle: {
                            Texture2D texture2D = this.CreateRoundedRectTexture((int)watermarkElement.size.x, (int)watermarkElement.size.y, watermarkElement.cornerRadius, watermarkElement.color);
                            GUI.DrawTexture(position, texture2D);
                            UnityEngine.Object.Destroy(texture2D);
                            break;
                        }
                        case WatermarkElement.ElementType.Image: {
                            bool flag2 = this.customWatermarkImage != null;
                            if(flag2) {
                                GUI.color = watermarkElement.color;
                                GUI.DrawTexture(position, this.customWatermarkImage, ScaleMode.ScaleToFit);
                                GUI.color = Color.white;
                            }
                            break;
                        }
                    }
                    GUI.matrix = matrix;
                }
            }
        }

        private void CreateSpectatorIndicators() {
            bool flag = !this.isSpectatorIndicatorEnabled || !PhotonNetwork.InRoom;
            if(flag) {
                this.DestroySpectatorIndicators();
            } else {
                this.DestroySpectatorIndicators();
                foreach(Player player in PhotonNetwork.PlayerListOthers) {
                    bool flag2 = player != null && !player.IsInactive;
                    if(flag2) {
                        this.CreateSpectatorIndicatorForPlayer(player);
                    }
                }
            }
        }

        private void CreateSpectatorIndicatorForPlayer(Player player) {
            bool flag = player == null || player.IsLocal || this.spectatorIndicators.ContainsKey(player);
            if(!flag) {
                PrimitiveType type = (this.spectatorIndicatorShape == 1) ? PrimitiveType.Cube : ((this.spectatorIndicatorShape == 2) ? PrimitiveType.Capsule : PrimitiveType.Sphere);
                GameObject gameObject = GameObject.CreatePrimitive(type);
                gameObject.name = "SpectatorIndicator_" + player.NickName;
                UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
                Renderer component = gameObject.GetComponent<Renderer>();
                component.material = new Material(Shader.Find("Standard")) {
                    color = this.spectatorIndicatorColor
                };
                component.shadowCastingMode = ShadowCastingMode.Off;
                this.spectatorIndicators[player] = gameObject;
                gameObject.SetActive(false);
            }
        }

        private void DestroySpectatorIndicators() {
            foreach(GameObject gameObject in this.spectatorIndicators.Values) {
                bool flag = gameObject != null;
                if(flag) {
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
            this.spectatorIndicators.Clear();
        }

        private void UpdateSpectatorIndicators() {
            HashSet<Player> currentOthers = new HashSet<Player>(PhotonNetwork.PlayerListOthers);
            List<Player> list = (from p in this.spectatorIndicators.Keys
                                 where !currentOthers.Contains(p)
                                 select p).ToList<Player>();
            foreach(Player key in list) {
                GameObject obj;
                bool flag = this.spectatorIndicators.TryGetValue(key, out obj);
                if(flag) {
                    UnityEngine.Object.Destroy(obj);
                }
                this.spectatorIndicators.Remove(key);
            }
            foreach(Player player in currentOthers) {
                bool flag2 = !this.spectatorIndicators.ContainsKey(player);
                if(flag2) {
                    this.CreateSpectatorIndicatorForPlayer(player);
                }
            }
            foreach(KeyValuePair<Player, GameObject> keyValuePair in this.spectatorIndicators) {
                VRRig vrrig = this.FindPlayerVRRig(keyValuePair.Key);
                GameObject value = keyValuePair.Value;
                bool flag3;
                if(vrrig != null && vrrig.gameObject.activeInHierarchy) {
                    VRMap head = vrrig.head;
                    flag3 = (((head != null) ? head.rigTarget : null) != null);
                } else {
                    flag3 = false;
                }
                bool flag4 = flag3;
                if(flag4) {
                    value.SetActive(true);
                    value.transform.position = vrrig.head.rigTarget.position + Vector3.up * (0.5f + this.spectatorIndicatorSize / 2f);
                    float d = this.spectatorIndicatorSize * (this.pulseSpectatorIndicator ? (1f + Mathf.Sin(Time.time * 5f) * 0.1f) : 1f);
                    value.transform.localScale = Vector3.one * d;
                    bool flag5 = this.billboardSpectatorIndicator && this.castingCamera != null;
                    if(flag5) {
                        value.transform.rotation = Quaternion.LookRotation(value.transform.position - this.castingCamera.transform.position);
                    }
                } else {
                    value.SetActive(false);
                }
            }
        }

        private void UpdateDiagnostics() {
            this.diagnosticsUpdateTimer += Time.unscaledDeltaTime;
            bool flag = this.diagnosticsUpdateTimer >= 0.25f;
            if(flag) {
                this.diagnosticsUpdateTimer = 0f;
                this.fps = 1f / Time.unscaledDeltaTime;
                this.frameTime = Time.unscaledDeltaTime * 1000f;
                GorillaTagger instance = GorillaTagger.Instance;
                UnityEngine.Object x;
                if(instance == null) {
                    x = null;
                } else {
                    CapsuleCollider bodyCollider = instance.bodyCollider;
                    x = ((bodyCollider != null) ? bodyCollider.attachedRigidbody : null);
                }
                bool flag2 = x != null;
                if(flag2) {
                    this.playerVelocity = GorillaTagger.Instance.bodyCollider.attachedRigidbody.velocity.magnitude;
                }
            }
        }

        private void OnGUI() {
            bool flag = this.isInitializing;
            if(flag) {
                GUI.color = new Color(0f, 0f, 0f, 0.9f);
                GUI.DrawTexture(new Rect(0f, 0f, (float)Screen.width, (float)Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                NotificationManager.OnGUI(this.modernSkin, this.pillBgTexture, this.closeIcon);
            } else {
                bool flag2 = !this.isTrialActive;
                if(flag2) {
                    this.DrawTrialExpiredWindow();
                } else {
                    GUI.skin = this.modernSkin;
                    NotificationManager.OnGUI(this.modernSkin, this.pillBgTexture, this.closeIcon);
                    bool flag3 = this.isMusicWidgetVisible;
                    if(flag3) {
                        this.musicPlayerWindowRect = GUI.Window(2, this.musicPlayerWindowRect, new GUI.WindowFunction(this.DrawMusicPlayerWindow), "", this.modernSkin.window);
                    }
                    bool flag4 = this.isDateTimeWidgetVisible;
                    if(flag4) {
                        this.dateTimeWindowRect = GUI.Window(3, this.dateTimeWindowRect, new GUI.WindowFunction(this.DrawDateTimeWindow), "", new GUIStyle());
                    }
                    bool flag5 = this.isScoreboardWidgetVisible;
                    if(flag5) {
                        this.DrawDraggableScoreboard();
                    }
                    bool flag6 = this.isLeaderboardWidgetVisible;
                    if(flag6) {
                        this.DrawLeaderboardWidget();
                    }
                    bool flag7 = this.isChatWindowVisible;
                    if(flag7) {
                        this.chatWindowRect = GUILayout.Window(5, this.chatWindowRect, new GUI.WindowFunction(this.DrawChatWindow), "Global Chat", Array.Empty<GUILayoutOption>());
                    }
                    this.DrawGlobalChatButton();
                    bool flag8 = this.showWelcomePanel;
                    if(flag8) {
                        this.DrawWelcomePanel();
                    } else {
                        bool flag9 = this.guiState > CastingMod.GuiAnimationState.Hidden;
                        if(flag9) {
                            float num = this.EaseOutCubic(this.guiAnimationProgress);
                            GUI.color = new Color(1f, 1f, 1f, num);
                            Matrix4x4 matrix = GUI.matrix;
                            float num2 = 0.95f + 0.05f * num;
                            this.mainWindowRect.x = ((float)Screen.width - this.mainWindowRect.width) / 2f;
                            this.mainWindowRect.y = ((float)Screen.height - this.mainWindowRect.height) / 2f;
                            GUIUtility.ScaleAroundPivot(new Vector2(num2, num2), this.mainWindowRect.center);
                            this.mainWindowRect = GUILayout.Window(0, this.mainWindowRect, new GUI.WindowFunction(this.DrawMainWindow), string.Format("<b>Released By L + @currentlycracking</b> <size=12>v{0}</size>", base.Info.Metadata.Version), GUI.skin.window, Array.Empty<GUILayoutOption>());
                            GUI.matrix = matrix;
                            GUI.color = Color.white;
                        }
                    }
                    this.DrawTrialWatermark();
                    bool flag10 = this.joinRoomFade > 0.01f;
                    if(flag10) {
                        this.DrawJoiningRoomOverlay();
                    }
                    bool isEnabled = this.currentWatermark.isEnabled;
                    if(isEnabled) {
                        this.DrawWatermark();
                    }
                    bool flag11 = this.guiState > CastingMod.GuiAnimationState.Hidden;
                    bool flag12 = this.isFreecamEnabled && this.isMouseLookFreecam && Mouse.current != null && Mouse.current.rightButton.isPressed && !flag11;
                    if(flag12) {
                        bool flag13 = Cursor.lockState != CursorLockMode.Locked;
                        if(flag13) {
                            Cursor.lockState = CursorLockMode.Locked;
                        }
                        bool visible = Cursor.visible;
                        if(visible) {
                            Cursor.visible = false;
                        }
                    } else {
                        bool flag14 = Cursor.lockState > CursorLockMode.None;
                        if(flag14) {
                            Cursor.lockState = CursorLockMode.None;
                        }
                        bool flag15 = !Cursor.visible;
                        if(flag15) {
                            Cursor.visible = true;
                        }
                    }
                    bool flag16 = this.isHoldableCameraEnabled && this.cameraScreenRenderTexture != null;
                    if(flag16) {
                        GUI.DrawTexture(new Rect(0f, 0f, (float)Screen.width, (float)Screen.height), this.cameraScreenRenderTexture, ScaleMode.ScaleToFit, false);
                    }
                }
            }
        }

        private void DrawMainWindow(int windowID) {
            bool flag = this.currentPage == CastingMod.CurrentGUIPage.MainMenu;
            if(flag) {
                this.DrawMainMenuGrid();
            } else {
                this.DrawCategoryPage();
            }
            this.HandleGUIResizing();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 30f));
        }

        private void DrawMainMenuGrid() {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.Camera);
            GUILayout.Space(20f);
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.Visuals);
            GUILayout.Space(20f);
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.Widgets);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(20f);
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.RoomAndPlayers);
            GUILayout.Space(20f);
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.Services);
            GUILayout.Space(20f);
            this.DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage.StyleAndSettings);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawCategoryPage() {
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            bool flag = this.MenuButton(new GUIContent(" Back", this.backIcon), new GUILayoutOption[]
            {
                GUILayout.Width(100f)
            });
            if(flag) {
                this.currentPage = CastingMod.CurrentGUIPage.MainMenu;
                this.searchQuery = "";
            }
            this.searchQuery = this.TextFieldWithPlaceholder(this.searchQuery, " Search features...", this.searchIcon);
            GUILayout.EndHorizontal();
            this.DrawDivider(1f, 5f);
            this.contentScrollPosition = GUILayout.BeginScrollView(this.contentScrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar, Array.Empty<GUILayoutOption>());
            Color color = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, this.guiAnimationProgress);
            switch(this.currentPage) {
                case CastingMod.CurrentGUIPage.Camera:
                this.DrawCameraPage();
                break;
                case CastingMod.CurrentGUIPage.Visuals:
                this.DrawVisualsPage();
                break;
                case CastingMod.CurrentGUIPage.Widgets:
                this.DrawWidgetsPage();
                break;
                case CastingMod.CurrentGUIPage.RoomAndPlayers:
                this.DrawRoomAndPlayersPage();
                break;
                case CastingMod.CurrentGUIPage.Services:
                this.DrawServicesPage();
                break;
                case CastingMod.CurrentGUIPage.StyleAndSettings:
                this.DrawStyleAndSettingsPage();
                break;
            }
            GUI.color = color;
            GUILayout.EndScrollView();
        }

        private void DrawTrialExpiredWindow() {
            Rect clientRect = new Rect((float)((Screen.width - 400) / 2), (float)((Screen.height - 200) / 2), 400f, 200f);
            GUI.Window(999, clientRect, delegate (int id) {
                GUILayout.Label("<b>Trial Expired</b>", new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20
                }, Array.Empty<GUILayoutOption>());
                GUILayout.FlexibleSpace();
                GUILayout.Label("Your 48-hour trial for AMC Mods has ended. Please contact the developer for a full version.", new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                }, Array.Empty<GUILayoutOption>());
                GUILayout.FlexibleSpace();
                bool flag = GUILayout.Button("Quit Game", Array.Empty<GUILayoutOption>());
                if(flag) {
                    Application.Quit();
                }
            }, "AMC Mods");
        }

        private void DrawWelcomePanel() {
            float num = 500f;
            float num2 = 250f;
            Rect clientRect = new Rect(((float)Screen.width - num) / 2f, ((float)Screen.height - num2) / 2f, num, num2);
            GUI.Window(1000, clientRect, delegate (int id) {
                GUILayout.FlexibleSpace();
                GUILayout.Label("<b>Welcome to AMC Mods!</b>", new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 24
                }, Array.Empty<GUILayoutOption>());
                GUILayout.Space(10f);
                GUILayout.Label("Hello, " + Environment.UserName, new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18
                }, Array.Empty<GUILayoutOption>());
                GUILayout.Space(20f);
                GUILayout.Label("Press TAB to open and close the menu at any time.", new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter
                }, Array.Empty<GUILayoutOption>());
                GUILayout.FlexibleSpace();
                bool flag = GUILayout.Button("Continue", new GUILayoutOption[]
                {
                    GUILayout.Height(40f)
                });
                if(flag) {
                    this.showWelcomePanel = false;
                    PlayerPrefs.SetInt("AMCHasShownWelcome", 1);
                    PlayerPrefs.Save();
                }
            }, "");
        }

        private void DrawJoiningRoomOverlay() {
            GUI.color = new Color(0f, 0f, 0f, 0.8f * this.EaseOutCubic(this.joinRoomFade));
            GUI.DrawTexture(new Rect(0f, 0f, (float)Screen.width, (float)Screen.height), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, this.EaseOutCubic(this.joinRoomFade));
            bool flag = this.loadingIcon != null;
            if(flag) {
                Rect position = new Rect((float)(Screen.width / 2 - 50), (float)(Screen.height / 2 - 50), 100f, 100f);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(this.loadingIconRotation, position.center);
                GUI.DrawTexture(position, this.loadingIcon);
                GUI.matrix = matrix;
            }
            GUI.color = Color.white;
        }

        private void DrawTrialWatermark() {
            long num = 172800L - (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.trialStartTime);
            TimeSpan timeSpan = TimeSpan.FromSeconds((double)Mathf.Max(0f, (float)num));
            string str = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            string text = "amcmods.com\nTrial: " + str;
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 14,
                normal =
                {
                    textColor = new Color(1f, 1f, 1f, 0.5f)
                }
            };
            GUI.Label(new Rect(0f, 0f, (float)(Screen.width - 10), (float)(Screen.height - 10)), text, style);
        }

        private void DrawCameraPage() {
            bool flag = this.MatchesSearch("Camera Mode", "first person third person freecam 1p 3p fp tp");
            if(flag) {
                GUILayout.Label("<b>Camera Mode</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                CastingMod.CameraMode cameraMode = (CastingMod.CameraMode)GUILayout.Toolbar((int)this.currentCameraMode, new string[]
                {
                    "First Person",
                    "Third Person",
                    "Freecam"
                }, Array.Empty<GUILayoutOption>());
                bool flag2 = cameraMode != this.currentCameraMode;
                if(flag2) {
                    this.currentCameraMode = cameraMode;
                    this.UpdateCameraModeFromState();
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag3 = this.currentCameraMode == CastingMod.CameraMode.FirstPerson && this.MatchesSearch("First Person Camera", "1st person fp smoothing fov render distance clipping lerp");
            if(flag3) {
                GUILayout.Label("<b>First Person Camera Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.fovConfig.Value = this.DrawSlider("Field of View (FOV)", this.fovConfig.Value, 30f, 140f, "F2");
                this.farClipPlaneConfig.Value = this.DrawSlider("Render Distance", this.farClipPlaneConfig.Value, 100f, 5000f, "F0");
                this.playerLerpConfig.Value = this.DrawSlider("Player Lerp Multiplier", this.playerLerpConfig.Value, 0.1f, 5f, "F2");
                this.fpPositionSmoothing_EnabledConfig.Value = this.DrawIOSToggle("Position Smoothing", this.fpPositionSmoothing_EnabledConfig.Value);
                bool value = this.fpPositionSmoothing_EnabledConfig.Value;
                if(value) {
                    this.fpPositionSmoothing_FactorConfig.Value = this.DrawSlider("Position Smooth Factor", this.fpPositionSmoothing_FactorConfig.Value, 0.01f, 1f, "F2");
                }
                this.fpRotationSmoothing_EnabledConfig.Value = this.DrawIOSToggle("Rotation Smoothing", this.fpRotationSmoothing_EnabledConfig.Value);
                bool value2 = this.fpRotationSmoothing_EnabledConfig.Value;
                if(value2) {
                    this.fpRotationSmoothing_FactorConfig.Value = this.DrawSlider("Rotation Smooth Factor", this.fpRotationSmoothing_FactorConfig.Value, 0.01f, 1f, "F2");
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag4 = this.currentCameraMode == CastingMod.CameraMode.ThirdPerson && this.MatchesSearch("Third Person Camera", "3rd person tp static cinematic offset collision smoothing");
            if(flag4) {
                GUILayout.Label("<b>Third Person Camera Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.thirdPersonModeConfig.Value = (CastingMod.ThirdPersonMode)GUILayout.Toolbar((int)this.thirdPersonModeConfig.Value, Enum.GetNames(typeof(CastingMod.ThirdPersonMode)), Array.Empty<GUILayoutOption>());
                this.thirdPersonOffsetConfig.Value = this.DrawVector3Control("Offset (X, Y, Z)", this.thirdPersonOffsetConfig.Value);
                this.thirdPersonPositionSmoothnessConfig.Value = this.DrawSlider("Position Smoothness", this.thirdPersonPositionSmoothnessConfig.Value, 0.01f, 1f, "F2");
                this.thirdPersonRotationSmoothnessConfig.Value = this.DrawSlider("Rotation Smoothness", this.thirdPersonRotationSmoothnessConfig.Value, 0.01f, 1f, "F2");
                this.thirdPersonCollisionConfig.Value = this.DrawIOSToggle("Enable Collision", this.thirdPersonCollisionConfig.Value);
                this.thirdPersonLookAtTargetConfig.Value = this.DrawIOSToggle("Always Look at Target", this.thirdPersonLookAtTargetConfig.Value);
                this.thirdPersonYLockedConfig.Value = this.DrawIOSToggle("Lock Y-Axis & Follow Yaw", this.thirdPersonYLockedConfig.Value);
                bool flag5 = this.isThirdPersonRotationLocked;
                this.isThirdPersonRotationLocked = this.DrawIOSToggle("Lock Rotation (Absolute)", this.isThirdPersonRotationLocked);
                bool flag6 = this.isThirdPersonRotationLocked != flag5;
                if(flag6) {
                    this.thirdPersonRotationLockedConfig.Value = this.isThirdPersonRotationLocked;
                    bool flag7 = this.isThirdPersonRotationLocked;
                    if(flag7) {
                        this.lockedThirdPersonRotation = this.castingCamera.transform.rotation;
                    }
                }
                this.DrawDivider(1f, 10f);
                bool flag8 = this.thirdPersonModeConfig.Value == CastingMod.ThirdPersonMode.Cinematic;
                if(flag8) {
                    GUILayout.Label("<b>Cinematic Mode Settings</b>", new GUIStyle(this.modernSkin.label) {
                        fontSize = this.guiFontSize + 2
                    }, Array.Empty<GUILayoutOption>());
                    this.cinematicOrbitSpeedConfig.Value = this.DrawSlider("Orbit Speed", this.cinematicOrbitSpeedConfig.Value, 1f, 90f, "F2");
                    this.cinematicMinDistConfig.Value = this.DrawSlider("Min Distance", this.cinematicMinDistConfig.Value, 0.5f, 10f, "F2");
                    this.cinematicMaxDistConfig.Value = this.DrawSlider("Max Distance", this.cinematicMaxDistConfig.Value, 1f, 20f, "F2");
                    this.cinematicMinFovConfig.Value = this.DrawSlider("Min FOV", this.cinematicMinFovConfig.Value, 30f, 140f, "F2");
                    this.cinematicMaxFovConfig.Value = this.DrawSlider("Max FOV", this.cinematicMaxFovConfig.Value, 30f, 140f, "F2");
                    this.DrawDivider(1f, 10f);
                }
            }
            bool flag9 = this.currentCameraMode == CastingMod.CameraMode.Freecam && this.MatchesSearch("Freecam", "flycam noclip speed look mouse smoothing fov roll");
            if(flag9) {
                GUILayout.Label("<b>Freecam Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.freecamMoveSpeedConfig.Value = this.DrawSlider("Move Speed", this.freecamMoveSpeedConfig.Value, 0.5f, 20f, "F2");
                this.freecamLookSpeedConfig.Value = this.DrawSlider("Look Speed", this.freecamLookSpeedConfig.Value, 10f, 200f, "F2");
                this.freecamRollSpeedConfig.Value = this.DrawSlider("Roll Speed", this.freecamRollSpeedConfig.Value, 10f, 90f, "F2");
                this.freecamFovConfig.Value = this.DrawSlider("Field of View (FOV)", this.freecamFovConfig.Value, 30f, 140f, "F2");
                this.freecamSmoothTimeConfig.Value = this.DrawSlider("Movement Smoothing", this.freecamSmoothTimeConfig.Value, 0.01f, 0.5f, "F2");
                this.freecamMouseLookConfig.Value = this.DrawIOSToggle("Use Mouse for Look", this.freecamMouseLookConfig.Value);
            }
        }

        private void DrawVisualsPage() {
            bool flag = this.MatchesSearch("Nametags", "name tags esp players font size color background distance fade");
            if(flag) {
                GUILayout.Label("<b>Nametag Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.areNametagsEnabledConfig.Value = this.DrawIOSToggle("Enable Nametags", this.areNametagsEnabledConfig.Value);
                bool value = this.areNametagsEnabledConfig.Value;
                if(value) {
                    this.nametagSizeConfig.Value = this.DrawSlider("Nametag Size", this.nametagSizeConfig.Value, 0.1f, 2.5f, "F2");
                    this.nametagHeadOffsetYConfig.Value = this.DrawSlider("Head Y-Offset", this.nametagHeadOffsetYConfig.Value, 0.2f, 2f, "F2");
                    this.selectedFontIndex = this.DrawFontSelector("Nametag Font", this.selectedFontIndex, delegate (int newIndex) {
                        this.selectedFontNameConfig.Value = this.fontNames[newIndex];
                        this.LoadSelectedFontFromConfig();
                    });
                    this.showCastingModUsersConfig.Value = this.DrawIOSToggle("Show Mod User Icon", this.showCastingModUsersConfig.Value);
                    this.nametagShowPlatformConfig.Value = this.DrawIOSToggle("Show Platform", this.nametagShowPlatformConfig.Value);
                    this.nametagShowFpsConfig.Value = this.DrawIOSToggle("Show FPS", this.nametagShowFpsConfig.Value);
                    this.nametagShowDistanceConfig.Value = this.DrawIOSToggle("Show Distance", this.nametagShowDistanceConfig.Value);
                    this.nametagFadeWithDistanceConfig.Value = this.DrawIOSToggle("Fade with Distance", this.nametagFadeWithDistanceConfig.Value);
                    bool value2 = this.nametagFadeWithDistanceConfig.Value;
                    if(value2) {
                        this.nametagFadeStartDistanceConfig.Value = this.DrawSlider("Fade Start Distance", this.nametagFadeStartDistanceConfig.Value, 0f, 100f, "F2");
                        this.nametagFadeEndDistanceConfig.Value = this.DrawSlider("Fade End Distance", this.nametagFadeEndDistanceConfig.Value, 1f, 100f, "F2");
                    }
                    this.useGlobalNametagColorConfig.Value = this.DrawIOSToggle("Use Global Color (Overrides Player Color)", this.useGlobalNametagColorConfig.Value);
                    bool value3 = this.useGlobalNametagColorConfig.Value;
                    if(value3) {
                        bool flag2 = this.DrawColorControl(this.globalNametagColorConfig, "Global Nametag Color");
                        if(flag2) {
                            this.UpdateNametagGraphics();
                        }
                    }
                    this.nametagShowBackgroundConfig.Value = this.DrawIOSToggle("Show Background", this.nametagShowBackgroundConfig.Value);
                    bool value4 = this.nametagShowBackgroundConfig.Value;
                    if(value4) {
                        bool flag3 = this.DrawColorControl(this.nametagBackgroundColorConfig, "Background Color");
                        if(flag3) {
                            this.UpdateNametagGraphics();
                        }
                    }
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag4 = this.MatchesSearch("Spectator Indicator", "spectate highlight shape color pulse billboard");
            if(flag4) {
                GUILayout.Label("<b>Spectator Indicator Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.isSpectatorIndicatorEnabledConfig.Value = this.DrawIOSToggle("Enable Spectator Indicator", this.isSpectatorIndicatorEnabledConfig.Value);
                bool value5 = this.isSpectatorIndicatorEnabledConfig.Value;
                if(value5) {
                    this.spectatorIndicatorSizeConfig.Value = this.DrawSlider("Indicator Size", this.spectatorIndicatorSizeConfig.Value, 0.05f, 0.5f, "F2");
                    bool flag5 = this.DrawColorControl(this.spectatorIndicatorColorConfig, "Indicator Color");
                    if(flag5) {
                    }
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Indicator Shape", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.spectatorIndicatorShapeConfig.Value = GUILayout.Toolbar(this.spectatorIndicatorShapeConfig.Value, new string[]
                    {
                        "Sphere",
                        "Cube",
                        "Capsule"
                    }, Array.Empty<GUILayoutOption>());
                    GUILayout.EndHorizontal();
                    this.pulseSpectatorIndicatorConfig.Value = this.DrawIOSToggle("Pulse Indicator", this.pulseSpectatorIndicatorConfig.Value);
                    this.billboardSpectatorIndicatorConfig.Value = this.DrawIOSToggle("Billboard Indicator (Face Camera)", this.billboardSpectatorIndicatorConfig.Value);
                }
            }
        }

        private void DrawWidgetsPage() {
            bool flag = this.MatchesSearch("Music Player", "audio song track volume");
            if(flag) {
                GUILayout.Label("<b>Music Player</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.isMusicWidgetVisible = this.DrawIOSToggle("Show Music Player", this.isMusicWidgetVisible);
                bool flag2 = this.isMusicWidgetVisible;
                if(flag2) {
                    bool flag3 = this.DrawColorControl(this.musicPlayerBgColorConfig, "Background Color");
                    if(flag3) {
                        this.UpdateMusicPlayerGraphics();
                    }
                    bool flag4 = this.DrawColorControl(this.musicPlayerTextColorConfig, "Text Color");
                    if(flag4) {
                        this.UpdateMusicPlayerGraphics();
                    }
                    bool flag5 = this.DrawColorControl(this.musicPlayerProgressBgColorConfig, "Progress BG Color");
                    if(flag5) {
                        this.UpdateMusicPlayerGraphics();
                    }
                    bool flag6 = this.DrawColorControl(this.musicPlayerProgressColorConfig, "Progress Fill Color");
                    if(flag6) {
                        this.UpdateMusicPlayerGraphics();
                    }
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag7 = this.MatchesSearch("Date Time Clock", "time date clock widget format");
            if(flag7) {
                GUILayout.Label("<b>Date & Time Widget</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.isDateTimeWidgetVisible = this.DrawIOSToggle("Show Date & Time Widget", this.isDateTimeWidgetVisible);
                bool flag8 = this.isDateTimeWidgetVisible;
                if(flag8) {
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Clock Style", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.clockStyleConfig.Value = (CastingMod.ClockStyle)GUILayout.Toolbar((int)this.clockStyleConfig.Value, Enum.GetNames(typeof(CastingMod.ClockStyle)), Array.Empty<GUILayoutOption>());
                    GUILayout.EndHorizontal();
                    bool flag9 = this.DrawColorControl(this.dateTimeBgColorConfig, "Background Color");
                    if(flag9) {
                        this.UpdateDateTimeWidgetGraphics();
                    }
                    bool flag10 = this.DrawColorControl(this.dateTimeTextColorConfig, "Text Color");
                    if(flag10) {
                        this.UpdateDateTimeWidgetGraphics();
                    }
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Time Format", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.dateTimeTimeFormatConfig.Value = GUILayout.TextField(this.dateTimeTimeFormatConfig.Value, Array.Empty<GUILayoutOption>());
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Date Format", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.dateTimeDateFormatConfig.Value = GUILayout.TextField(this.dateTimeDateFormatConfig.Value, Array.Empty<GUILayoutOption>());
                    GUILayout.EndHorizontal();
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag11 = this.MatchesSearch("Scoreboard", "score board teams red blue timer");
            if(flag11) {
                GUILayout.Label("<b>Scoreboard</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.isScoreboardVisibleConfig.Value = this.DrawIOSToggle("Show Scoreboard", this.isScoreboardVisibleConfig.Value);
                bool value = this.isScoreboardVisibleConfig.Value;
                if(value) {
                    this.scoreboardPositionsLockedConfig.Value = this.DrawIOSToggle("Lock Positions", this.scoreboardPositionsLockedConfig.Value);
                    GUILayout.Label("<b>Live Controls</b>", Array.Empty<GUILayoutOption>());
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    bool flag12 = this.MenuButton(this.isTimerRunning ? "Pause Timer" : "Start Timer", Array.Empty<GUILayoutOption>());
                    if(flag12) {
                        this.isTimerRunning = !this.isTimerRunning;
                    }
                    bool flag13 = this.MenuButton("Reset Timer", Array.Empty<GUILayoutOption>());
                    if(flag13) {
                        this.scoreboardTimer = 0f;
                    }
                    bool flag14 = this.MenuButton("Reset Scores", Array.Empty<GUILayoutOption>());
                    if(flag14) {
                        this.redScore = 0;
                        this.blueScore = 0;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Red Score:", new GUILayoutOption[]
                    {
                        GUILayout.Width(80f)
                    });
                    this.redScore = this.DrawIntSlider("", this.redScore, 0, 99);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Blue Score:", new GUILayoutOption[]
                    {
                        GUILayout.Width(80f)
                    });
                    this.blueScore = this.DrawIntSlider("", this.blueScore, 0, 99);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10f);
                    GUILayout.Label("<b>Appearance</b>", Array.Empty<GUILayoutOption>());
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Red Team Name", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.redTeamNameConfig.Value = GUILayout.TextField(this.redTeamNameConfig.Value, new GUILayoutOption[]
                    {
                        GUILayout.ExpandWidth(true)
                    });
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("Blue Team Name", new GUILayoutOption[]
                    {
                        GUILayout.Width(150f)
                    });
                    this.blueTeamNameConfig.Value = GUILayout.TextField(this.blueTeamNameConfig.Value, new GUILayoutOption[]
                    {
                        GUILayout.ExpandWidth(true)
                    });
                    GUILayout.EndHorizontal();
                    bool flag15 = this.DrawColorControl(this.scoreboardBgColorConfig, "Background Color");
                    if(flag15) {
                        this.UpdateScoreboardGraphics();
                    }
                    bool flag16 = this.DrawColorControl(this.scoreboardRedColorConfig, "Red Team Color");
                    if(flag16) {
                        this.UpdateScoreboardGraphics();
                    }
                    bool flag17 = this.DrawColorControl(this.scoreboardBlueColorConfig, "Blue Team Color");
                    if(flag17) {
                        this.UpdateScoreboardGraphics();
                    }
                    bool flag18 = this.DrawColorControl(this.scoreboardTextColorConfig, "Timer Color");
                    if(flag18) {
                        this.UpdateScoreboardGraphics();
                    }
                    this.scoreboardCornerRadiusConfig.Value = this.DrawSlider("Corner Radius", this.scoreboardCornerRadiusConfig.Value, 0f, 30f, "F2");
                    this.scoreboardTeamNameFontSizeConfig.Value = this.DrawIntSlider("Team Name Font Size", this.scoreboardTeamNameFontSizeConfig.Value, 10, 50);
                    this.scoreboardScoreFontSizeConfig.Value = this.DrawIntSlider("Score Font Size", this.scoreboardScoreFontSizeConfig.Value, 10, 72);
                    this.scoreboardTimerFontSizeConfig.Value = this.DrawIntSlider("Timer Font Size", this.scoreboardTimerFontSizeConfig.Value, 10, 72);
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag19 = this.MatchesSearch("Leaderboard", "player list names");
            if(flag19) {
                GUILayout.Label("<b>Leaderboard</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.isLeaderboardVisibleConfig.Value = this.DrawIOSToggle("Show Player Leaderboard", this.isLeaderboardVisibleConfig.Value);
                bool value2 = this.isLeaderboardVisibleConfig.Value;
                if(value2) {
                    bool flag20 = this.DrawColorControl(this.leaderboardBgColorConfig, "Background Color");
                    if(flag20) {
                        this.UpdateLeaderboardGraphics();
                    }
                    bool flag21 = this.DrawColorControl(this.leaderboardTextColorConfig, "Text Color");
                    if(flag21) {
                        this.UpdateLeaderboardGraphics();
                    }
                    this.leaderboardFontSizeConfig.Value = this.DrawIntSlider("Font Size", this.leaderboardFontSizeConfig.Value, 10, 48);
                    this.leaderboardSelectedFontIndex = this.DrawFontSelector("Leaderboard Font", this.leaderboardSelectedFontIndex, delegate (int newIndex) {
                        this.leaderboardFontNameConfig.Value = this.fontNames[newIndex];
                        this.LoadLeaderboardFontFromConfig();
                    });
                }
            }
        }

        private void DrawRoomAndPlayersPage() {
            bool flag = this.MatchesSearch("Room Joiner", "join private public code");
            if(flag) {
                GUILayout.Label("<b>Room Management</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUILayout.Label("Room Code:", new GUILayoutOption[]
                {
                    GUILayout.Width(100f)
                });
                this.roomJoinerText = GUILayout.TextField(this.roomJoinerText.ToUpper(), 10, new GUILayoutOption[]
                {
                    GUILayout.Width(150f)
                });
                bool flag2 = this.MenuButton("Join Room", new GUILayoutOption[]
                {
                    GUILayout.ExpandWidth(false)
                }) && !string.IsNullOrWhiteSpace(this.roomJoinerText);
                if(flag2) {
                    this.JoinRoom(this.roomJoinerText);
                }
                bool flag3 = this.MenuButton("Disconnect", new GUILayoutOption[]
                {
                    GUILayout.ExpandWidth(false)
                });
                if(flag3) {
                    PhotonNetwork.Disconnect();
                }
                GUILayout.EndHorizontal();
                this.DrawDivider(1f, 10f);
            }
            bool flag4 = this.MatchesSearch("Player List Spectate", "players users lobby spectator");
            if(flag4) {
                GUILayout.Label("<b>Player List & Spectator</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                bool inRoom = PhotonNetwork.InRoom;
                if(inRoom) {
                    bool flag5 = this.spectatedRig != null && this.MenuButton("Stop Spectating " + this.spectatedRig.playerNameVisible, this.GetRedButtonStyle(), Array.Empty<GUILayoutOption>());
                    if(flag5) {
                        this.StopSpectating();
                    }
                    GUILayout.BeginScrollView(Vector2.zero, this.modernSkin.box);
                    foreach(Player player in PhotonNetwork.PlayerList) {
                        VRRig vrrig = this.FindPlayerVRRig(player);
                        bool flag6 = vrrig == null;
                        if(!flag6) {
                            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                            GUILayout.Label(string.Format("[{0}] {1} {2}", player.ActorNumber, player.NickName, player.IsLocal ? "(You)" : ""), Array.Empty<GUILayoutOption>());
                            GUILayout.FlexibleSpace();
                            bool flag7 = !player.IsLocal;
                            if(flag7) {
                                bool flag8 = this.MenuButton("Spectate (1P)", new GUILayoutOption[]
                                {
                                    GUILayout.Width(120f)
                                });
                                if(flag8) {
                                    this.SpectatePlayer(vrrig, CastingMod.CameraMode.FirstPerson);
                                }
                                bool flag9 = this.MenuButton("Spectate (3P)", new GUILayoutOption[]
                                {
                                    GUILayout.Width(120f)
                                });
                                if(flag9) {
                                    this.SpectatePlayer(vrrig, CastingMod.CameraMode.ThirdPerson);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.EndScrollView();
                } else {
                    GUILayout.Label("Not in a room.", Array.Empty<GUILayoutOption>());
                }
            }
        }

        private void DrawServicesPage() {
            bool flag = this.MatchesSearch("Announcements", "news updates info");
            if(flag) {
                GUILayout.Label("<b>Announcements</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.announcementsScrollPos = GUILayout.BeginScrollView(this.announcementsScrollPos, this.modernSkin.box, new GUILayoutOption[]
                {
                    GUILayout.Height(200f)
                });
                foreach(Announcement announcement in AnnouncementService.AllAnnouncements) {
                    GUILayout.Label("<b>" + announcement.title + "</b>", Array.Empty<GUILayoutOption>());
                    GUILayout.Label(announcement.message, Array.Empty<GUILayoutOption>());
                    this.DrawDivider(1f, 2f);
                }
                GUILayout.EndScrollView();
                this.DrawDivider(1f, 10f);
            }
            bool flag2 = this.MatchesSearch("Global Chat", "irc chat message send");
            if(flag2) {
                GUILayout.Label("<b>Global Chat</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                bool flag3 = this.MenuButton("Open Global Chat Window", Array.Empty<GUILayoutOption>());
                if(flag3) {
                    this.isChatWindowVisible = true;
                }
            }
        }

        private void DrawStyleAndSettingsPage() {
            bool flag = this.MatchesSearch("GUI Style", "theme color customization ui look feel");
            if(flag) {
                GUILayout.Label("<b>GUI Style Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.selectedGuiFontIndex = this.DrawFontSelector("GUI Font", this.selectedGuiFontIndex, delegate (int newIndex) {
                    this.guiFontNameConfig.Value = this.fontNames[newIndex];
                    this.LoadGuiFontFromConfig();
                });
                this.guiFontSizeConfig.Value = this.DrawIntSlider("GUI Font Size", this.guiFontSizeConfig.Value, 10, 20);
                this.guiCornerRadiusConfig.Value = this.DrawSlider("GUI Corner Radius", this.guiCornerRadiusConfig.Value, 0f, 30f, "F2");
                bool flag2 = this.DrawColorControl(this.guiBackgroundColorConfig, "Background");
                if(flag2) {
                    this.InitializeModernGUI();
                }
                bool flag3 = this.DrawColorControl(this.guiTextColorConfig, "Text");
                if(flag3) {
                    this.InitializeModernGUI();
                }
                bool flag4 = this.DrawColorControl(this.guiButtonColorConfig, "Button");
                if(flag4) {
                    this.InitializeModernGUI();
                }
                bool flag5 = this.DrawColorControl(this.guiButtonHoverColorConfig, "Button Hover");
                if(flag5) {
                    this.InitializeModernGUI();
                }
                bool flag6 = this.DrawColorControl(this.guiSliderThumbColorConfig, "Slider Fill");
                if(flag6) {
                    this.InitializeModernGUI();
                }
                bool flag7 = this.MenuButton("Reset Styles to Default", Array.Empty<GUILayoutOption>());
                if(flag7) {
                    this.guiBackgroundColorConfig.Value = (Color)this.guiBackgroundColorConfig.DefaultValue;
                    this.guiTextColorConfig.Value = (Color)this.guiTextColorConfig.DefaultValue;
                    this.guiButtonColorConfig.Value = (Color)this.guiButtonColorConfig.DefaultValue;
                    this.guiButtonHoverColorConfig.Value = (Color)this.guiButtonHoverColorConfig.DefaultValue;
                    this.guiSliderThumbColorConfig.Value = (Color)this.guiSliderThumbColorConfig.DefaultValue;
                    this.guiCornerRadiusConfig.Value = (float)this.guiCornerRadiusConfig.DefaultValue;
                    this.guiFontSizeConfig.Value = (int)this.guiFontSizeConfig.DefaultValue;
                    this.InitializeModernGUI();
                }
                this.DrawDivider(1f, 10f);
            }
            bool flag8 = this.MatchesSearch("Presets", "save load config settings profiles");
            if(flag8) {
                GUILayout.Label("<b>Presets</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                this.presetsScrollPosition = GUILayout.BeginScrollView(this.presetsScrollPosition, this.modernSkin.box, new GUILayoutOption[]
                {
                    GUILayout.Height(150f)
                });
                foreach(string text in this.fullPresets.Keys.ToList<string>()) {
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    GUILayout.Label(text, Array.Empty<GUILayoutOption>());
                    GUILayout.FlexibleSpace();
                    bool flag9 = this.MenuButton("Load", new GUILayoutOption[]
                    {
                        GUILayout.Width(60f)
                    });
                    if(flag9) {
                        this.LoadFullPreset(text);
                    }
                    bool flag10 = this.MenuButton("X", this.GetRedButtonStyle(), new GUILayoutOption[]
                    {
                        GUILayout.Width(30f)
                    });
                    if(flag10) {
                        this.DeleteFullPreset(text);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                this.newPresetName = GUILayout.TextField(this.newPresetName, Array.Empty<GUILayoutOption>());
                bool flag11 = this.MenuButton("Save Current as Preset", new GUILayoutOption[]
                {
                    GUILayout.Width(200f)
                });
                if(flag11) {
                    this.SaveFullPreset(this.newPresetName);
                }
                GUILayout.EndHorizontal();
                this.DrawDivider(1f, 10f);
            }
            bool flag12 = this.MatchesSearch("Import Export", "backup restore share settings");
            if(flag12) {
                GUILayout.Label("<b>Import / Export Settings</b>", new GUIStyle(this.modernSkin.label) {
                    fontSize = this.guiFontSize + 2
                }, Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                bool flag13 = this.MenuButton("Export All Settings to File", Array.Empty<GUILayoutOption>());
                if(flag13) {
                    this.ExportSettings();
                }
                bool flag14 = this.MenuButton("Import All Settings from File", Array.Empty<GUILayoutOption>());
                if(flag14) {
                    this.ImportSettings(this.settingsImportFileName);
                }
                this.settingsImportFileName = GUILayout.TextField(this.settingsImportFileName, Array.Empty<GUILayoutOption>());
                GUILayout.EndHorizontal();
            }
        }

        private bool MenuButton(string text, params GUILayoutOption[] options) {
            this.CheckHover(text);
            bool flag = GUILayout.Button(text, options);
            bool result;
            if(flag) {
                AudioManager.PlayClick();
                result = true;
            } else {
                result = false;
            }
            return result;
        }

        private bool MenuButton(GUIContent content, params GUILayoutOption[] options) {
            this.CheckHover(content.text);
            bool flag = GUILayout.Button(content, options);
            bool result;
            if(flag) {
                AudioManager.PlayClick();
                result = true;
            } else {
                result = false;
            }
            return result;
        }

        private bool MenuButton(string text, GUIStyle style, params GUILayoutOption[] options) {
            this.CheckHover(text);
            bool flag = GUILayout.Button(text, style, options);
            bool result;
            if(flag) {
                AudioManager.PlayClick();
                result = true;
            } else {
                result = false;
            }
            return result;
        }

        private bool MenuButton(GUIContent content, GUIStyle style, params GUILayoutOption[] options) {
            this.CheckHover(content.text);
            bool flag = GUILayout.Button(content, style, options);
            bool result;
            if(flag) {
                AudioManager.PlayClick();
                result = true;
            } else {
                result = false;
            }
            return result;
        }

        private void CheckHover(string controlName) {
            bool flag = Event.current.type == EventType.Repaint && GUI.enabled;
            if(flag) {
                string b = controlName + GUI.depth.ToString() + this.currentPage.ToString();
                bool flag2 = GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && this.lastHoveredControl != b;
                if(flag2) {
                    AudioManager.PlayHover();
                    this.lastHoveredControl = b;
                }
            }
        }

        private GUIStyle GetRedButtonStyle() {
            return new GUIStyle(this.modernSkin.button) {
                normal =
                {
                    background = this.redButtonTexture
                },
                hover =
                {
                    background = this.redButtonHoverTexture
                },
                active =
                {
                    background = this.redButtonActiveTexture
                }
            };
        }

        private void DrawDivider(float thickness = 1f, float padding = 10f) {
            GUILayout.Space(padding / 2f);
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUI.skin.box, new GUILayoutOption[]
            {
                GUILayout.Height(thickness),
                GUILayout.ExpandWidth(true)
            });
            GUI.Box(rect, GUIContent.none, new GUIStyle {
                normal =
                {
                    background = this.CreateSolidColorTexture(new Color(0.5f, 0.5f, 0.5f, 0.2f))
                }
            });
            GUILayout.Space(padding / 2f);
        }

        private bool DrawColorControl(ConfigEntry<Color> config, string label) {
            Color value = config.Value;
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(label, new GUILayoutOption[]
            {
                GUILayout.Width(150f)
            });
            Color backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = config.Value;
            bool flag = GUILayout.Button("", new GUILayoutOption[]
            {
                GUILayout.Width(100f),
                GUILayout.Height(20f)
            });
            if(flag) {
            }
            this.CheckHover(label);
            GUI.backgroundColor = backgroundColor;
            GUILayout.FlexibleSpace();
            string text = ColorUtility.ToHtmlStringRGBA(config.Value);
            string text2 = GUILayout.TextField(text, new GUILayoutOption[]
            {
                GUILayout.Width(100f)
            });
            Color value2;
            bool flag2 = text2 != text && ColorUtility.TryParseHtmlString("#" + text2, out value2);
            if(flag2) {
                config.Value = value2;
            }
            GUILayout.EndHorizontal();
            return value != config.Value;
        }

        private float DrawSlider(string label, float value, float min, float max, string format = "F2") {
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            bool flag = !string.IsNullOrEmpty(label);
            if(flag) {
                GUILayout.Label(label, new GUILayoutOption[]
                {
                    GUILayout.Width(150f)
                });
            }
            Rect rect = GUILayoutUtility.GetRect(100f, 18f, new GUILayoutOption[]
            {
                GUILayout.ExpandWidth(true)
            });
            rect.y += 4f;
            float width = rect.width * ((value - min) / (max - min));
            GUI.DrawTexture(rect, this.sliderBgTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, width, rect.height), this.sliderFillTexture);
            float result = GUI.HorizontalSlider(rect, value, min, max);
            this.CheckHover(label);
            string s = GUILayout.TextField(value.ToString(format), new GUILayoutOption[]
            {
                GUILayout.Width(50f)
            });
            float value2;
            bool flag2 = float.TryParse(s, out value2);
            if(flag2) {
                result = Mathf.Clamp(value2, min, max);
            }
            GUILayout.EndHorizontal();
            return result;
        }

        private Vector3 DrawVector3Control(string label, Vector3 value) {
            GUILayout.Label(label, Array.Empty<GUILayoutOption>());
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            value.x = this.DrawSlider("X", value.x, -10f, 10f, "F2");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            value.y = this.DrawSlider("Y", value.y, -10f, 10f, "F2");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            value.z = this.DrawSlider("Z", value.z, -10f, 10f, "F2");
            GUILayout.EndHorizontal();
            return value;
        }

        private int DrawIntSlider(string label, int value, int min, int max) {
            return (int)this.DrawSlider(label, (float)value, (float)min, (float)max, "F0");
        }

        private int DrawFontSelector(string label, int currentIndex, Action<int> onSelectionChanged) {
            GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
            GUILayout.Label(label, new GUILayoutOption[]
            {
                GUILayout.Width(150f)
            });
            float num = this.mainWindowRect.width - 80f;
            float num2 = 120f;
            int num3 = Mathf.Max(1, Mathf.FloorToInt(num / num2));
            int num4 = Mathf.CeilToInt((float)this.fontNames.Length / (float)num3);
            int num5 = 0;
            for(int i = 0; i < num4; i++) {
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                for(int j = 0; j < num3; j++) {
                    bool flag = num5 < this.fontNames.Length;
                    if(flag) {
                        GUIStyle guistyle;
                        if(num5 != currentIndex) {
                            guistyle = this.modernSkin.button;
                        } else {
                            (guistyle = new GUIStyle(this.modernSkin.button)).normal.background = this.activeCategoryBgTexture;
                        }
                        GUIStyle style = guistyle;
                        bool flag2 = GUILayout.Button(this.fontNames[num5], style, new GUILayoutOption[]
                        {
                            GUILayout.Width(num2)
                        });
                        if(flag2) {
                            currentIndex = num5;
                            if(onSelectionChanged != null) {
                                onSelectionChanged(currentIndex);
                            }
                        }
                        num5++;
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            return currentIndex;
        }

        private bool DrawIOSToggle(string label, bool value) {
            GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
            GUILayout.Label(label, Array.Empty<GUILayoutOption>());
            GUILayout.FlexibleSpace();
            Rect rect = GUILayoutUtility.GetRect(50f, 24f, new GUILayoutOption[]
            {
                GUILayout.ExpandWidth(false)
            });
            rect.y += 3f;
            this.CheckHover(label);
            bool flag = GUI.Button(rect, GUIContent.none, GUI.skin.label);
            bool flag2 = !this.toggleAnimStates.ContainsKey(label);
            if(flag2) {
                this.toggleAnimStates[label] = (value ? 1f : 0f);
            }
            float num = this.toggleAnimStates[label];
            float num2 = value ? 1f : 0f;
            bool flag3 = Mathf.Abs(num - num2) > 0.01f;
            if(flag3) {
                num = Mathf.Lerp(num, num2, Time.unscaledDeltaTime * 15f);
                this.toggleAnimStates[label] = num;
            }
            Color color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), this.guiActiveCategoryColorConfig.Value, num);
            GUI.DrawTexture(rect, this.CreateRoundedRectTexture((int)rect.width, (int)rect.height, 12f, color));
            float num3 = rect.height - 4f;
            float x = rect.x + 2f + (rect.width - num3 - 4f) * num;
            Rect position = new Rect(x, rect.y + 2f, num3, num3);
            GUI.DrawTexture(position, this.CreateRoundedRectTexture((int)num3, (int)num3, num3 / 2f, Color.white));
            GUILayout.EndHorizontal();
            bool flag4 = flag;
            bool result;
            if(flag4) {
                AudioManager.PlayClick();
                result = !value;
            } else {
                result = value;
            }
            return result;
        }

        private void UpdateAnimatedElements() {
            List<CastingMod.CurrentGUIPage> list = this.categoryButtons.Keys.ToList<CastingMod.CurrentGUIPage>();
            foreach(CastingMod.CurrentGUIPage key in list) {
                AnimatedButton animatedButton = this.categoryButtons[key];
                float b = animatedButton.IsHovered ? 1.05f : 1f;
                animatedButton.Scale = Mathf.Lerp(animatedButton.Scale, b, Time.unscaledDeltaTime * 10f);
                this.categoryButtons[key] = animatedButton;
            }
        }

        private void DrawAnimatedCategoryButton(CastingMod.CurrentGUIPage page) {
            bool flag = !this.categoryButtons.ContainsKey(page);
            if(flag) {
                this.categoryButtons[page] = new AnimatedButton {
                    Scale = 1f
                };
            }
            AnimatedButton animatedButton = this.categoryButtons[page];
            GUIStyle style = new GUIStyle(this.modernSkin.button)
            {
                fixedWidth = 200f,
                fixedHeight = 120f,
                padding = new RectOffset(15, 15, 10, 10),
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageLeft,
                fontStyle = FontStyle.Bold
            };
            GUIContent content = new GUIContent(string.Format(" {0}", page), this.pageIcons.ContainsKey(page) ? this.pageIcons[page] : null);
            Matrix4x4 matrix = GUI.matrix;
            GUILayout.Box(GUIContent.none, style, Array.Empty<GUILayoutOption>());
            animatedButton.Rect = GUILayoutUtility.GetLastRect();
            bool flag2 = animatedButton.Rect.Contains(Event.current.mousePosition);
            if(flag2) {
                bool flag3 = !animatedButton.IsHovered;
                if(flag3) {
                    AudioManager.PlayHover();
                    animatedButton.IsHovered = true;
                }
            } else {
                animatedButton.IsHovered = false;
            }
            GUIUtility.ScaleAroundPivot(new Vector2(animatedButton.Scale, animatedButton.Scale), animatedButton.Rect.center);
            bool flag4 = GUI.Button(animatedButton.Rect, content, style);
            if(flag4) {
                this.currentPage = page;
                AudioManager.PlayClick();
            }
            GUI.matrix = matrix;
            this.categoryButtons[page] = animatedButton;
        }

        private bool MatchesSearch(string title, string keywords) {
            bool flag = string.IsNullOrWhiteSpace(this.searchQuery);
            bool result;
            if(flag) {
                result = true;
            } else {
                string text = (title + " " + keywords).ToLowerInvariant();
                string[] array = this.searchQuery.ToLowerInvariant().Split(new char[]
                {
                    ' '
                }, StringSplitOptions.RemoveEmptyEntries);
                foreach(string value in array) {
                    bool flag2 = !text.Contains(value);
                    if(flag2) {
                        return false;
                    }
                }
                result = true;
            }
            return result;
        }

        private void DrawGlobalChatButton() {
            Rect position = new Rect(10f, (float)(Screen.height - 60), 50f, 50f);
            bool flag = GUI.Button(position, new GUIContent("", this.chatIcon, "Toggle Global Chat"));
            if(flag) {
                this.isChatWindowVisible = !this.isChatWindowVisible;
            }
        }

        private void DrawChatWindow(int windowID) {
            bool isConnected = ChatService.IsConnected;
            if(isConnected) {
                this.ircChatScrollPos = GUILayout.BeginScrollView(this.ircChatScrollPos, this.modernSkin.box, new GUILayoutOption[]
                {
                    GUILayout.ExpandHeight(true)
                });
                foreach(ChatMessage chatMessage in this.ircChatHistory) {
                    GUILayout.Label("<b>" + chatMessage.username + ":</b> " + chatMessage.message, Array.Empty<GUILayoutOption>());
                }
                GUILayout.EndScrollView();
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                this.ircMessageToSend = GUILayout.TextField(this.ircMessageToSend, new GUILayoutOption[]
                {
                    GUILayout.ExpandWidth(true)
                });
                bool flag = this.MenuButton("Send", new GUILayoutOption[]
                {
                    GUILayout.Width(80f)
                }) && !string.IsNullOrWhiteSpace(this.ircMessageToSend);
                if(flag) {
                    ChatService.SendMessage(this, new ChatMessage {
                        username = this.ircUsername,
                        message = this.ircMessageToSend
                    });
                    this.ircMessageToSend = "";
                    GUI.FocusControl(null);
                }
                GUILayout.EndHorizontal();
                bool flag2 = this.MenuButton("Disconnect from Chat", Array.Empty<GUILayoutOption>());
                if(flag2) {
                    ChatService.Disconnect(this);
                }
            } else {
                GUILayout.Label("Connect to Global Chat", Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUILayout.Label("Username:", new GUILayoutOption[]
                {
                    GUILayout.Width(80f)
                });
                this.ircUsername = GUILayout.TextField(this.ircUsername, Array.Empty<GUILayoutOption>());
                GUILayout.EndHorizontal();
                bool flag3 = this.MenuButton("Connect", Array.Empty<GUILayoutOption>());
                if(flag3) {
                    this.chatConnectionError = "";
                    ChatService.Connect(this, new Action<List<ChatMessage>>(this.OnMessagesReceived), new Action<string>(this.OnChatError));
                }
                bool flag4 = !string.IsNullOrEmpty(this.chatConnectionError);
                if(flag4) {
                    GUILayout.Label("<color=red>" + this.chatConnectionError + "</color>", Array.Empty<GUILayoutOption>());
                }
            }
            GUI.DragWindow();
        }

        private void OnMessagesReceived(List<ChatMessage> messages) {
            bool flag = messages.Count > this.ircChatHistory.Count;
            if(flag) {
                this.ircChatScrollPos.y = float.MaxValue;
            }
            this.ircChatHistory = messages;
        }

        private void OnChatError(string error) {
            this.chatConnectionError = error;
        }

        private void HandleGUIResizing() {
            Rect position = new Rect(this.mainWindowRect.width - 20f, this.mainWindowRect.height - 20f, 20f, 20f);
            bool flag = this.resizeIcon != null;
            if(flag) {
                GUI.DrawTexture(position, this.resizeIcon);
            }
            bool flag2 = Event.current.type == EventType.MouseDown && position.Contains(Event.current.mousePosition);
            if(flag2) {
                this.isResizingGUI = true;
                Event.current.Use();
            }
            bool flag3 = this.isResizingGUI;
            if(flag3) {
                bool flag4 = Event.current.type == EventType.MouseDrag;
                if(flag4) {
                    this.mainWindowRect.width = this.mainWindowRect.width + Event.current.delta.x;
                    this.mainWindowRect.height = this.mainWindowRect.height + Event.current.delta.y;
                    this.mainWindowRect.width = Mathf.Clamp(this.mainWindowRect.width, 600f, (float)Screen.width * 0.95f);
                    this.mainWindowRect.height = Mathf.Clamp(this.mainWindowRect.height, 500f, (float)Screen.height * 0.95f);
                    Event.current.Use();
                }
                bool flag5 = Event.current.type == EventType.MouseUp;
                if(flag5) {
                    this.isResizingGUI = false;
                    this.guiWidthConfig.Value = this.mainWindowRect.width;
                    this.guiHeightConfig.Value = this.mainWindowRect.height;
                }
            }
        }

        private string TextFieldWithPlaceholder(string text, string placeholder, Texture2D icon = null) {
            Rect rect = GUILayoutUtility.GetRect(250f, 35f, new GUILayoutOption[]
            {
                GUILayout.ExpandWidth(true)
            });
            GUI.DrawTexture(rect, this.modernSkin.textField.normal.background, ScaleMode.StretchToFill);
            Rect position = new Rect(rect.x + 5f, rect.y + (rect.height - 24f) / 2f, 24f, 24f);
            bool flag = icon != null;
            if(flag) {
                GUI.Label(position, icon);
            }
            Rect position2 = new Rect(rect.x + 35f, rect.y, rect.width - 40f, rect.height);
            GUI.SetNextControlName("SearchField");
            string text2 = GUI.TextField(position2, text, new GUIStyle(this.modernSkin.textField)
            {
                normal =
                {
                    background = null
                },
                focused =
                {
                    background = null
                }
            });
            bool flag2 = string.IsNullOrEmpty(text2) && GUI.GetNameOfFocusedControl() != "SearchField";
            if(flag2) {
                GUI.Label(position2, placeholder, new GUIStyle(this.modernSkin.label) {
                    alignment = TextAnchor.MiddleLeft,
                    normal =
                    {
                        textColor = Color.gray
                    }
                });
            }
            return text2;
        }

        private string FullPresetFilePath {
            get {
                return Path.Combine(Paths.ConfigPath, "AMCMods_FullPresets.json");
            }
        }

        private void SaveFullPreset(string name) {
            bool flag = string.IsNullOrWhiteSpace(name);
            if(!flag) {
                ModPresetData value = new ModPresetData
                {
                    freecamMoveSpeed = this.freecamMoveSpeedSetting,
                    freecamLookSpeed = this.freecamLookSpeedSetting,
                    freecamMouseLook = this.isMouseLookFreecam,
                    freecamSmoothTime = this.freecamSmoothTime,
                    freecamFov = this.freecamFov,
                    freecamRollSpeed = this.freecamRollSpeed,
                    fov = this.FOV,
                    playerLerp = this.Lerping,
                    farClipPlane = this.farClipPlane,
                    fpPositionSmoothing_Enabled = this.isFPPositionSmoothingEnabled,
                    fpPositionSmoothing_Factor = this.fpPositionSmoothingFactor,
                    fpRotationSmoothing_Enabled = this.isFPRotationSmoothingEnabled,
                    fpRotationSmoothing_Factor = this.fpRotationSmoothingFactor,
                    thirdPersonMode = this.thirdPersonModeSetting,
                    thirdPersonYLocked = this.isThirdPersonYLocked,
                    thirdPersonRotationSmoothness = this.thirdPersonRotationSmoothness,
                    thirdPersonPositionSmoothness = this.thirdPersonPositionSmoothness,
                    thirdPersonRotationLocked = this.isThirdPersonRotationLocked,
                    thirdPersonLookAtTarget = this.thirdPersonLookAtTarget,
                    thirdPersonCollision = this.thirdPersonCollision,
                    thirdPersonOffset = this.thirdPersonOffset,
                    cinematicOrbitSpeed = this.cinematicOrbitSpeed,
                    cinematicMinFov = this.cinematicMinFov,
                    cinematicMaxFov = this.cinematicMaxFov,
                    cinematicMinDist = this.cinematicMinDist,
                    cinematicMaxDist = this.cinematicMaxDist,
                    areNametagsEnabled = this.areNametagsEnabled,
                    nametagSize = this.nametagSize,
                    selectedFontName = this.selectedFontNameConfig.Value,
                    nametagHeadOffsetY = this.HeadOffsetY,
                    nametagShowDistance = this.nametagShowDistance,
                    nametagFadeWithDistance = this.nametagFadeWithDistance,
                    nametagFadeStartDistance = this.nametagFadeStartDistance,
                    nametagFadeEndDistance = this.nametagFadeEndDistance,
                    nametagShowBackground = this.nametagShowBackground,
                    nametagBackgroundColor = this.nametagBackgroundColor,
                    useGlobalNametagColor = this.useGlobalNametagColor,
                    globalNametagColor = this.globalNametagColor,
                    nametagShowFps = this.nametagShowFps,
                    nametagShowPlatform = this.nametagShowPlatform,
                    isSpectatorIndicatorEnabled = this.isSpectatorIndicatorEnabled,
                    spectatorIndicatorSize = this.spectatorIndicatorSize,
                    spectatorIndicatorColor = this.spectatorIndicatorColor,
                    spectatorIndicatorShape = this.spectatorIndicatorShape,
                    pulseSpectatorIndicator = this.pulseSpectatorIndicator,
                    billboardSpectatorIndicator = this.billboardSpectatorIndicator
                };
                this.fullPresets[name.Trim()] = value;
                this.SaveFullPresetsToFile();
            }
        }

        private void LoadFullPreset(string name) {
            ModPresetData modPresetData;
            bool flag = this.fullPresets.TryGetValue(name, out modPresetData);
            if(flag) {
                this.freecamMoveSpeedConfig.Value = modPresetData.freecamMoveSpeed;
                this.freecamLookSpeedConfig.Value = modPresetData.freecamLookSpeed;
                this.freecamMouseLookConfig.Value = modPresetData.freecamMouseLook;
                this.freecamSmoothTimeConfig.Value = modPresetData.freecamSmoothTime;
                this.freecamFovConfig.Value = modPresetData.freecamFov;
                this.freecamRollSpeedConfig.Value = modPresetData.freecamRollSpeed;
                this.fovConfig.Value = modPresetData.fov;
                this.playerLerpConfig.Value = modPresetData.playerLerp;
                this.farClipPlaneConfig.Value = modPresetData.farClipPlane;
                this.fpPositionSmoothing_EnabledConfig.Value = modPresetData.fpPositionSmoothing_Enabled;
                this.fpPositionSmoothing_FactorConfig.Value = modPresetData.fpPositionSmoothing_Factor;
                this.fpRotationSmoothing_EnabledConfig.Value = modPresetData.fpRotationSmoothing_Enabled;
                this.fpRotationSmoothing_FactorConfig.Value = modPresetData.fpRotationSmoothing_Factor;
                this.thirdPersonModeConfig.Value = modPresetData.thirdPersonMode;
                this.thirdPersonYLockedConfig.Value = modPresetData.thirdPersonYLocked;
                this.thirdPersonRotationSmoothnessConfig.Value = modPresetData.thirdPersonRotationSmoothness;
                this.thirdPersonPositionSmoothnessConfig.Value = modPresetData.thirdPersonPositionSmoothness;
                this.thirdPersonRotationLockedConfig.Value = modPresetData.thirdPersonRotationLocked;
                this.thirdPersonLookAtTargetConfig.Value = modPresetData.thirdPersonLookAtTarget;
                this.thirdPersonCollisionConfig.Value = modPresetData.thirdPersonCollision;
                this.thirdPersonOffsetConfig.Value = modPresetData.thirdPersonOffset;
                this.cinematicOrbitSpeedConfig.Value = modPresetData.cinematicOrbitSpeed;
                this.cinematicMinFovConfig.Value = modPresetData.cinematicMinFov;
                this.cinematicMaxFovConfig.Value = modPresetData.cinematicMaxFov;
                this.cinematicMinDistConfig.Value = modPresetData.cinematicMinDist;
                this.cinematicMaxDistConfig.Value = modPresetData.cinematicMaxDist;
                this.areNametagsEnabledConfig.Value = modPresetData.areNametagsEnabled;
                this.nametagSizeConfig.Value = modPresetData.nametagSize;
                this.selectedFontNameConfig.Value = modPresetData.selectedFontName;
                this.nametagHeadOffsetYConfig.Value = modPresetData.nametagHeadOffsetY;
                this.nametagShowDistanceConfig.Value = modPresetData.nametagShowDistance;
                this.nametagFadeWithDistanceConfig.Value = modPresetData.nametagFadeWithDistance;
                this.nametagFadeStartDistanceConfig.Value = modPresetData.nametagFadeStartDistance;
                this.nametagFadeEndDistanceConfig.Value = modPresetData.nametagFadeEndDistance;
                this.nametagShowBackgroundConfig.Value = modPresetData.nametagShowBackground;
                this.nametagBackgroundColorConfig.Value = modPresetData.nametagBackgroundColor;
                this.useGlobalNametagColorConfig.Value = modPresetData.useGlobalNametagColor;
                this.globalNametagColorConfig.Value = modPresetData.globalNametagColor;
                this.isSpectatorIndicatorEnabledConfig.Value = modPresetData.isSpectatorIndicatorEnabled;
                this.spectatorIndicatorSizeConfig.Value = modPresetData.spectatorIndicatorSize;
                this.spectatorIndicatorColorConfig.Value = modPresetData.spectatorIndicatorColor;
                this.spectatorIndicatorShapeConfig.Value = modPresetData.spectatorIndicatorShape;
                this.pulseSpectatorIndicatorConfig.Value = modPresetData.pulseSpectatorIndicator;
                this.billboardSpectatorIndicatorConfig.Value = modPresetData.billboardSpectatorIndicator;
                this.nametagShowFpsConfig.Value = modPresetData.nametagShowFps;
                this.nametagShowPlatformConfig.Value = modPresetData.nametagShowPlatform;
                this.LoadSettingsFromConfig();
                this.LoadSelectedFontFromConfig();
                this.UpdateNametagGraphics();
                this.UpdateCameraModeFromState();
            }
        }

        private void DeleteFullPreset(string name) {
            bool flag = this.fullPresets.Remove(name);
            if(flag) {
                this.SaveFullPresetsToFile();
            }
        }

        private void SaveFullPresetsToFile() {
            try {
                CastingMod.FullPresetListWrapper fullPresetListWrapper = new CastingMod.FullPresetListWrapper();
                foreach(KeyValuePair<string, ModPresetData> keyValuePair in this.fullPresets) {
                    fullPresetListWrapper.Presets.Add(new CastingMod.PresetEntry {
                        Name = keyValuePair.Key,
                        Data = keyValuePair.Value
                    });
                }
                File.WriteAllText(this.FullPresetFilePath, JsonUtility.ToJson(fullPresetListWrapper, true));
            } catch(Exception data) {
                CastingMod.Log.LogError(data);
            }
        }

        private void LoadFullPresetsFromFile() {
            this.fullPresets.Clear();
            bool flag = !File.Exists(this.FullPresetFilePath);
            if(!flag) {
                try {
                    CastingMod.FullPresetListWrapper fullPresetListWrapper = JsonUtility.FromJson<CastingMod.FullPresetListWrapper>(File.ReadAllText(this.FullPresetFilePath));
                    bool flag2 = ((fullPresetListWrapper != null) ? fullPresetListWrapper.Presets : null) != null;
                    if(flag2) {
                        foreach(CastingMod.PresetEntry presetEntry in fullPresetListWrapper.Presets) {
                            this.fullPresets[presetEntry.Name] = presetEntry.Data;
                        }
                    }
                } catch(Exception arg) {
                    CastingMod.Log.LogError(string.Format("Error loading presets: {0}", arg));
                }
            }
        }

        private string WatermarksFolderPath {
            get {
                return Path.Combine(Path.GetDirectoryName(base.Info.Location), "Watermarks");
            }
        }

        private void SaveWatermark(string name) {
            bool flag = string.IsNullOrWhiteSpace(name);
            if(!flag) {
                try {
                    string path = Path.Combine(this.WatermarksFolderPath, name + ".json");
                    string contents = JsonUtility.ToJson(this.currentWatermark, true);
                    File.WriteAllText(path, contents);
                    bool flag2 = !this.savedWatermarks.ContainsKey(name);
                    if(flag2) {
                        this.savedWatermarks.Add(name, this.currentWatermark);
                    } else {
                        this.savedWatermarks[name] = this.currentWatermark;
                    }
                } catch(Exception ex) {
                    CastingMod.Log.LogError("Failed to save watermark: " + ex.Message);
                }
            }
        }

        private void LoadWatermark(string name) {
            WatermarkData obj;
            bool flag = this.savedWatermarks.TryGetValue(name, out obj);
            if(flag) {
                this.currentWatermark = JsonUtility.FromJson<WatermarkData>(JsonUtility.ToJson(obj));
                this.selectedWatermarkElement = -1;
            }
        }

        private void DeleteWatermark(string name) {
            bool flag = this.savedWatermarks.Remove(name);
            if(flag) {
                string path = Path.Combine(this.WatermarksFolderPath, name + ".json");
                bool flag2 = File.Exists(path);
                if(flag2) {
                    File.Delete(path);
                }
            }
        }

        private void LoadSavedWatermarks() {
            this.savedWatermarks.Clear();
            bool flag = !Directory.Exists(this.WatermarksFolderPath);
            if(!flag) {
                string[] files = Directory.GetFiles(this.WatermarksFolderPath, "*.json");
                foreach(string text in files) {
                    try {
                        string json = File.ReadAllText(text);
                        WatermarkData value = JsonUtility.FromJson<WatermarkData>(json);
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
                        this.savedWatermarks[fileNameWithoutExtension] = value;
                    } catch(Exception ex) {
                        CastingMod.Log.LogError("Failed to load watermark " + text + ": " + ex.Message);
                    }
                }
            }
        }

        public void JoinRoom(string roomCode) {
            this.isJoiningRoom = true;
            PhotonNetworkController instance = PhotonNetworkController.Instance;
            if(instance != null) {
                instance.AttemptToJoinSpecificRoom(roomCode.Trim(), JoinType.Solo);
            }
        }

        public void OnRoomJoined() {
            this.isJoiningRoom = false;
        }

        public void OnRoomJoinFailed() {
            this.isJoiningRoom = false;
            NotificationManager.Show("Join Failed", "Could not join the room.", 5f, null, false);
        }

        private void PerformGameObjectSearch() {
            this.searchResults.Clear();
            bool flag = string.IsNullOrWhiteSpace(this.gameObjectSearchQuery);
            if(!flag) {
                string query = this.gameObjectSearchQuery.ToLowerInvariant();
                int num = 200;
                foreach(GameObject gameObject in SceneManager.GetActiveScene().GetRootGameObjects()) {
                    this.FindObjectsRecursive(gameObject.transform, query, this.searchResults, num);
                    bool flag2 = this.searchResults.Count >= num;
                    if(flag2) {
                        break;
                    }
                }
            }
        }

        private void FindObjectsRecursive(Transform parent, string query, List<GameObject> results, int max) {
            bool flag = parent == null || results.Count >= max;
            if(!flag) {
                bool flag2 = parent.name.ToLowerInvariant().Contains(query);
                if(flag2) {
                    results.Add(parent.gameObject);
                }
                foreach(object obj in parent) {
                    Transform parent2 = (Transform)obj;
                    bool flag3 = results.Count >= max;
                    if(flag3) {
                        break;
                    }
                    this.FindObjectsRecursive(parent2, query, results, max);
                }
            }
        }

        public void LogAllPlayerInfo() {
            bool flag = !PhotonNetwork.InRoom;
            if(flag) {
                this.playerLogData = "Not in a room.";
            } else {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("Room: " + PhotonNetwork.CurrentRoom.Name);
                foreach(Player player in from p in PhotonNetwork.PlayerList
                                         orderby p.ActorNumber
                                         select p) {
                    stringBuilder.AppendLine("====================================");
                    stringBuilder.AppendLine(string.Format("Name: {0}, ID: {1}, PlayFab: {2}", player.NickName, player.ActorNumber, player.UserId));
                }
                this.playerLogData = stringBuilder.ToString();
                CastingMod.Log.LogInfo("Player Info Logged:\n" + this.playerLogData);
            }
        }

        public void OnEnable() {
            SceneManager.sceneLoaded += this.OnSceneLoaded;
        }

        public void OnDisable() {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            this.searchResults.Clear();
            this.selectedTargetObject = null;
            this.selectedTargetObjectName = "None";
            this.StopSpectating();
            this.RefreshSpectatablePlayers();
            base.StartCoroutine(this.ReinitializeCameras(false));
            base.StartCoroutine(this.InitializeHoldableCamera());
            bool flag = this.isSpectatorIndicatorEnabled && PhotonNetwork.InRoom;
            if(flag) {
                this.CreateSpectatorIndicators();
            }
        }

        private IEnumerator InitializeHoldableCamera() {
            bool flag = this.holdableCameraInstance != null;
            if(flag) {
                UnityEngine.Object.Destroy(this.holdableCameraInstance);
            }
            bool flag2 = !this.isHoldableCameraEnabled;
            if(flag2) {
                this.ConfigureCastingCamera();
                yield break;
            }
            this.CreateProceduralCamera();
            bool flag3 = this.cameraScreenRenderTexture == null;
            if(flag3) {
                this.cameraScreenRenderTexture = new RenderTexture(1024, 576, 24);
                this.cameraScreenRenderTexture.Create();
            }
            this.ConfigureCastingCamera();
            yield break;
        }

        private void CreateProceduralCamera() {
            this.holdableCameraInstance = new GameObject("HoldableCastingCamera_Instance");
            this.holdableCameraInstance.transform.position = GorillaTagger.Instance.headCollider.transform.position + new Vector3(0f, 0.2f, 0.5f);
            Material material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.1f, 0.1f, 0.12f)
            };
            Material material2 = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.05f, 0.05f, 0.05f)
            };
            Material material3 = new Material(Shader.Find("Standard"))
            {
                color = Color.white
            };
            Material material4 = new Material(Shader.Find("Standard"))
            {
                color = this.guiButtonColorConfig.Value
            };
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.transform.SetParent(this.holdableCameraInstance.transform, false);
            gameObject.transform.localScale = new Vector3(0.12f, 0.1f, 0.2f);
            gameObject.GetComponent<Renderer>().material = material;
            GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameObject2.transform.SetParent(this.holdableCameraInstance.transform, false);
            gameObject2.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            gameObject2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gameObject2.transform.localScale = new Vector3(0.08f, 0.05f, 0.08f);
            gameObject2.GetComponent<Renderer>().material = material2;
            GameObject gameObject3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject3.transform.SetParent(this.holdableCameraInstance.transform, false);
            gameObject3.transform.localPosition = new Vector3(0f, 0f, -0.101f);
            gameObject3.transform.localScale = new Vector3(0.11f, 0.08f, 0.001f);
            gameObject3.GetComponent<Renderer>().material = material3;
            GameObject gameObject4 = new GameObject("OSD_Canvas");
            gameObject4.transform.SetParent(gameObject3.transform, false);
            gameObject4.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform component = gameObject4.GetComponent<RectTransform>();
            component.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            component.sizeDelta = new Vector2(110f, 80f);
            GameObject gameObject5 = new GameObject("OSD_Text");
            gameObject5.transform.SetParent(gameObject4.transform, false);
            TextMeshProUGUI textMeshProUGUI = gameObject5.AddComponent<TextMeshProUGUI>();
            textMeshProUGUI.font = this.Designer;
            textMeshProUGUI.fontSize = 12f;
            textMeshProUGUI.color = new Color(0f, 1f, 0f, 0.8f);
            textMeshProUGUI.alignment = TextAlignmentOptions.TopLeft;
            textMeshProUGUI.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 80f);
            GameObject gameObject6 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gameObject6.transform.SetParent(this.holdableCameraInstance.transform, false);
            gameObject6.transform.localPosition = new Vector3(0.04f, 0.06f, 0.02f);
            gameObject6.transform.localScale = new Vector3(0.02f, 0.005f, 0.02f);
            gameObject6.GetComponent<Renderer>().material = material4;
            UnityEngine.Object.Destroy(gameObject6.GetComponent<Collider>());
            BoxCollider boxCollider = gameObject6.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1.5f, 5f, 1.5f);
            VRPhysicalButton modeButton = gameObject6.AddComponent<VRPhysicalButton>();
            GameObject gameObject7 = new GameObject("RecLight");
            gameObject7.transform.SetParent(this.holdableCameraInstance.transform, false);
            gameObject7.transform.localPosition = new Vector3(-0.04f, 0f, 0.155f);
            Light light = gameObject7.AddComponent<Light>();
            light.color = Color.red;
            light.range = 0.1f;
            light.intensity = 1.5f;
            BoxCollider boxCollider2 = this.holdableCameraInstance.AddComponent<BoxCollider>();
            boxCollider2.size = new Vector3(0.12f, 0.1f, 0.25f);
            boxCollider2.center = new Vector3(0f, 0f, 0.025f);
            boxCollider2.isTrigger = true;
            Rigidbody rigidbody = this.holdableCameraInstance.AddComponent<Rigidbody>();
            rigidbody.mass = 0.5f;
            rigidbody.drag = 1f;
            rigidbody.angularDrag = 1f;
            this.holdableCameraController = this.holdableCameraInstance.AddComponent<HoldableCastingCamera>();
            this.holdableCameraController.Initialize(light, textMeshProUGUI, modeButton);
            this.holdableCameraController.OnModeCyclePressed += this.CycleCameraMode;
            bool flag = this.cameraScreenRenderTexture != null;
            if(flag) {
                material3.mainTexture = this.cameraScreenRenderTexture;
            }
            this.MakeHandsInteractive();
        }

        private void MakeHandsInteractive() {
            foreach(Transform parent in new Transform[]
            {
                GorillaTagger.Instance.leftHandTransform,
                GorillaTagger.Instance.rightHandTransform
            }) {
                GameObject gameObject = new GameObject("HandTriggerCollider");
                gameObject.transform.SetParent(parent, false);
                gameObject.layer = LayerMask.NameToLayer("Gorilla Hand");
                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.05f;
                sphereCollider.isTrigger = true;
                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        private void SetHoldableCameraActive(bool active) {
            bool flag = this.holdableCameraInstance != null;
            if(flag) {
                this.holdableCameraInstance.SetActive(active);
            }
            this.ConfigureCastingCamera();
        }

        private void CycleCameraMode() {
            this.currentCameraMode = (this.currentCameraMode + 1) % (CastingMod.CameraMode)Enum.GetValues(typeof(CastingMod.CameraMode)).Length;
            this.UpdateCameraModeFromState();
        }

        private void UpdateCameraModeFromState() {
            this.isThirdPersonCameraEnabled = (this.currentCameraMode == CastingMod.CameraMode.ThirdPerson);
            this.isFreecamEnabled = (this.currentCameraMode == CastingMod.CameraMode.Freecam);
        }

        private void UpdateHoldableCameraStatus() {
            bool flag = this.holdableCameraController == null;
            if(!flag) {
                float fov = (this.currentCameraMode == CastingMod.CameraMode.Freecam) ? this.freecamFov : this.FOV;
                this.holdableCameraController.UpdateStatusText(this.currentCameraMode.ToString(), fov, this.selectedTargetObjectName);
            }
        }

        private void CreateModUserSpriteAsset() {
            this.modUserSpriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            this.modUserSpriteAsset.name = "AMCModsSpriteAsset";
            this.modUserSpriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(this.modUserSpriteAsset.name);
            List<TMP_Sprite> list = new List<TMP_Sprite>();
            List<Texture2D> list2 = new List<Texture2D>();
            bool flag = this.pageIcons.ContainsKey(CastingMod.CurrentGUIPage.Services) && this.pageIcons[CastingMod.CurrentGUIPage.Services] != null;
            if(flag) {
                list.Add(new TMP_Sprite {
                    name = "moduser",
                    id = 0,
                    pivot = new Vector2(0.5f, 0.5f),
                    width = (float)this.pageIcons[CastingMod.CurrentGUIPage.Services].width,
                    height = (float)this.pageIcons[CastingMod.CurrentGUIPage.Services].height,
                    xAdvance = (float)this.pageIcons[CastingMod.CurrentGUIPage.Services].width
                });
                list2.Add(this.pageIcons[CastingMod.CurrentGUIPage.Services]);
            }
            bool flag2 = list2.Count > 0;
            if(flag2) {
                Texture2D texture2D = new Texture2D(2048, 2048);
                Rect[] array = texture2D.PackTextures(list2.ToArray(), 2, 4096);
                for(int i = 0; i < array.Length; i++) {
                    TMP_Sprite tmp_Sprite = list[i];
                    tmp_Sprite.x = array[i].x * (float)texture2D.width;
                    tmp_Sprite.y = array[i].y * (float)texture2D.height;
                    tmp_Sprite.width = array[i].width * (float)texture2D.width;
                    tmp_Sprite.height = array[i].height * (float)texture2D.height;
                    list[i] = tmp_Sprite;
                }
                this.modUserSpriteAsset.spriteSheet = texture2D;
            }
            this.modUserSpriteAsset.spriteInfoList = list;
            this.modUserSpriteAsset.UpdateLookupTables();
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            bool flag3 = shader != null;
            if(flag3) {
                Material material = new Material(shader);
                material.SetTexture(ShaderUtilities.ID_MainTex, this.modUserSpriteAsset.spriteSheet);
                this.modUserSpriteAsset.material = material;
            }
        }

        public bool IsLocalPlayerAdmin() {
            return this.IsPlayerAdmin(PhotonNetwork.LocalPlayer);
        }

        public bool IsPlayerAdmin(Player player) {
            bool flag = player == null || string.IsNullOrEmpty(player.UserId);
            return !flag && this.adminPlayFabIDs.Contains(player.UserId);
        }

        private void HandleAdminControls() {
            bool flag = !this.IsLocalPlayerAdmin() || !this.leftHandDevice.isValid;
            if(!flag) {
                bool flag3;
                bool flag2 = this.leftHandDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out flag3);
                if(flag2) {
                    bool flag4 = flag3 && !this.leftStickPressedLastFrame;
                    if(flag4) {
                        this.TeleportAllModUsers();
                    }
                    this.leftStickPressedLastFrame = flag3;
                }
            }
        }

        private void TeleportAllModUsers() {
            bool flag = !this.IsLocalPlayerAdmin() || !PhotonNetwork.InRoom;
            if(!flag) {
                NotificationManager.Show("Admin Action", "Teleporting all mod users.", 3f, null, false);
                object[] eventContent = new object[]
                {
                    GorillaTagger.Instance.headCollider.transform.position
                };
                PhotonNetwork.RaiseEvent(196, eventContent, new RaiseEventOptions {
                    Receivers = ReceiverGroup.Others
                }, SendOptions.SendReliable);
            }
        }

        internal void BroadcastAdminTag(int targetActor = -1) {
            bool flag = !this.IsLocalPlayerAdmin() || !PhotonNetwork.InRoom;
            if(!flag) {
                string value = this.adminCustomTagConfig.Value;
                this.customAdminTags[PhotonNetwork.LocalPlayer.ActorNumber] = value;
                RaiseEventOptions raiseEventOptions2;
                if(targetActor == -1) {
                    RaiseEventOptions raiseEventOptions = new RaiseEventOptions();
                    raiseEventOptions.Receivers = ReceiverGroup.Others;
                    raiseEventOptions2 = raiseEventOptions;
                    raiseEventOptions.CachingOption = EventCaching.AddToRoomCacheGlobal;
                } else {
                    raiseEventOptions2 = new RaiseEventOptions {
                        TargetActors = new int[]
                        {
                            targetActor
                        }
                    };
                }
                RaiseEventOptions raiseEventOptions3 = raiseEventOptions2;
                PhotonNetwork.RaiseEvent(197, value, raiseEventOptions3, SendOptions.SendReliable);
            }
        }

        internal void DiscordLogUserAuth(Player player) {
            bool flag = player == null;
            if(!flag) {
                bool flag2 = this.IsPlayerAdmin(player);
                string text = flag2 ? "https://discord.com/api/webhooks/1413018988618715226/rqJJ0FlpZSwfs-bIAxlYiVr2_BsRhlUe6U5wbbz3IgOOnC1Fvb4NTQL7beJWKR3jgVWh" : "https://discord.com/api/webhooks/1412992944398663771/PmzsIqGH5T8F8fHjuIj1Nq6oW8x5nQNV1LO5BZvELXUeRR4jY91rKqIYzw51vgHhiwgM";
                bool flag3 = string.IsNullOrEmpty(text) || text.Contains("YOUR_");
                if(!flag3) {
                    bool flag4 = string.IsNullOrEmpty(player.UserId);
                    if(!flag4) {
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine(flag2 ? "--- **Admin Authenticated** ---" : "--- **User Authenticated** ---");
                        stringBuilder.AppendLine("**Player:** " + player.NickName);
                        stringBuilder.AppendLine("**PlayFab ID:** `" + player.UserId + "`");
                        DiscordService.SendMessage(text, stringBuilder.ToString());
                    }
                }
            }
        }

        internal void DiscordLogRoomInfo() {
            bool flag = !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null;
            if(!flag) {
                string text = "https://discord.com/api/webhooks/1413019265883181147/WmlNzxeD7WnL3L7Ieahmpak7EwAyKPjKcXf-48KBmdkVCPHTx5sJ3-b0F4BgLZzEkrON";
                bool flag2 = string.IsNullOrEmpty(text) || text.Contains("YOUR_");
                if(!flag2) {
                    Room currentRoom = PhotonNetwork.CurrentRoom;
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine("**Room Update: `" + currentRoom.Name + "`**");
                    stringBuilder.AppendLine(string.Format("**Players:** {0}/{1}", currentRoom.PlayerCount, currentRoom.MaxPlayers));
                    DiscordService.SendMessage(text, stringBuilder.ToString());
                }
            }
        }

        private void ExportSettings() {
            CastingModSettingsData allSettings = this.GetAllSettings();
            string contents = JsonUtility.ToJson(allSettings, true);
            string text = string.Format("AMCMods_Settings_{0:yyyy-MM-dd_HH-mm-ss}.csettings", DateTime.Now);
            string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), text);
            try {
                File.WriteAllText(path, contents);
                NotificationManager.Show("Success", "Settings exported to:\n" + text, 7f, this.saveIcon, false);
            } catch(Exception ex) {
                NotificationManager.Show("Error", "Failed to export settings.", 5f, this.disableIcon, false);
            }
        }

        private void ImportSettings(string fileName) {
            bool flag = string.IsNullOrWhiteSpace(fileName);
            if(!flag) {
                string path = Path.Combine(Path.GetDirectoryName(base.Info.Location), fileName);
                bool flag2 = !File.Exists(path);
                if(flag2) {
                    NotificationManager.Show("Error", "File not found.", 5f, this.disableIcon, false);
                } else {
                    try {
                        string json = File.ReadAllText(path);
                        CastingModSettingsData s = JsonUtility.FromJson<CastingModSettingsData>(json);
                        this.ApplyAllSettings(s);
                        NotificationManager.Show("Success", "Settings imported from:\n" + fileName, 7f, this.checkIcon, false);
                    } catch(Exception ex) {
                        NotificationManager.Show("Error", "Failed to import settings.", 5f, this.disableIcon, false);
                    }
                }
            }
        }

        private CastingModSettingsData GetAllSettings() {
            return new CastingModSettingsData {
                announcementsUrl = this.announcementsUrlConfig.Value,
                adminCustomTag = this.adminCustomTagConfig.Value,
                holdableCameraEnabled = this.holdableCameraEnabledConfig.Value,
                musicVolume = this.musicVolumeConfig.Value,
                isCustomTargetEnabled = this.isCustomTargetEnabledConfig.Value,
                freecamMoveSpeed = this.freecamMoveSpeedConfig.Value,
                freecamLookSpeed = this.freecamLookSpeedConfig.Value,
                freecamMouseLook = this.freecamMouseLookConfig.Value,
                freecamSmoothTime = this.freecamSmoothTimeConfig.Value,
                freecamFov = this.freecamFovConfig.Value,
                freecamRollSpeed = this.freecamRollSpeedConfig.Value,
                fov = this.fovConfig.Value,
                playerLerp = this.playerLerpConfig.Value,
                farClipPlane = this.farClipPlaneConfig.Value,
                fpPositionSmoothing_Enabled = this.fpPositionSmoothing_EnabledConfig.Value,
                fpPositionSmoothing_Factor = this.fpPositionSmoothing_FactorConfig.Value,
                fpRotationSmoothing_Enabled = this.fpRotationSmoothing_EnabledConfig.Value,
                fpRotationSmoothing_Factor = this.fpRotationSmoothing_FactorConfig.Value,
                thirdPersonMode = this.thirdPersonModeConfig.Value,
                thirdPersonYLocked = this.thirdPersonYLockedConfig.Value,
                thirdPersonRotationSmoothness = this.thirdPersonRotationSmoothnessConfig.Value,
                thirdPersonPositionSmoothness = this.thirdPersonPositionSmoothnessConfig.Value,
                thirdPersonRotationLocked = this.thirdPersonRotationLockedConfig.Value,
                thirdPersonLookAtTarget = this.thirdPersonLookAtTargetConfig.Value,
                thirdPersonCollision = this.thirdPersonCollisionConfig.Value,
                thirdPersonOffset = this.thirdPersonOffsetConfig.Value,
                cinematicOrbitSpeed = this.cinematicOrbitSpeedConfig.Value,
                cinematicMinFov = this.cinematicMinFovConfig.Value,
                cinematicMaxFov = this.cinematicMaxFovConfig.Value,
                cinematicMinDist = this.cinematicMinDistConfig.Value,
                cinematicMaxDist = this.cinematicMaxDistConfig.Value,
                areNametagsEnabled = this.areNametagsEnabledConfig.Value,
                showCastingModUsers = this.showCastingModUsersConfig.Value,
                nametagSize = this.nametagSizeConfig.Value,
                selectedFontName = this.selectedFontNameConfig.Value,
                nametagHeadOffsetY = this.nametagHeadOffsetYConfig.Value,
                nametagShowDistance = this.nametagShowDistanceConfig.Value,
                nametagFadeWithDistance = this.nametagFadeWithDistanceConfig.Value,
                nametagFadeStartDistance = this.nametagFadeStartDistanceConfig.Value,
                nametagFadeEndDistance = this.nametagFadeEndDistanceConfig.Value,
                nametagShowBackground = this.nametagShowBackgroundConfig.Value,
                nametagBackgroundColor = this.nametagBackgroundColorConfig.Value,
                useGlobalNametagColor = this.useGlobalNametagColorConfig.Value,
                globalNametagColor = this.globalNametagColorConfig.Value,
                nametagShowFps = this.nametagShowFpsConfig.Value,
                nametagShowPlatform = this.nametagShowPlatformConfig.Value,
                isSpectatorIndicatorEnabled = this.isSpectatorIndicatorEnabledConfig.Value,
                spectatorIndicatorSize = this.spectatorIndicatorSizeConfig.Value,
                spectatorIndicatorColor = this.spectatorIndicatorColorConfig.Value,
                spectatorIndicatorShape = this.spectatorIndicatorShapeConfig.Value,
                pulseSpectatorIndicator = this.pulseSpectatorIndicatorConfig.Value,
                billboardSpectatorIndicator = this.billboardSpectatorIndicatorConfig.Value,
                musicPlayerWidth = this.musicPlayerWidthConfig.Value,
                musicPlayerCoverArtSize = this.musicPlayerCoverArtSizeConfig.Value,
                musicPlayerBgColor = this.musicPlayerBgColorConfig.Value,
                musicPlayerTextColor = this.musicPlayerTextColorConfig.Value,
                musicPlayerProgressBgColor = this.musicPlayerProgressBgColorConfig.Value,
                musicPlayerProgressColor = this.musicPlayerProgressColorConfig.Value,
                clockStyle = this.clockStyleConfig.Value,
                dateTimeWidth = this.dateTimeWidthConfig.Value,
                dateTimeHeight = this.dateTimeHeightConfig.Value,
                dateTimeBgColor = this.dateTimeBgColorConfig.Value,
                dateTimeTextColor = this.dateTimeTextColorConfig.Value,
                dateTimeTimeFormat = this.dateTimeTimeFormatConfig.Value,
                dateTimeDateFormat = this.dateTimeDateFormatConfig.Value,
                isScoreboardVisible = this.isScoreboardVisibleConfig.Value,
                scoreboardBgColor = this.scoreboardBgColorConfig.Value,
                scoreboardTextColor = this.scoreboardTextColorConfig.Value,
                scoreboardRedColor = this.scoreboardRedColorConfig.Value,
                scoreboardBlueColor = this.scoreboardBlueColorConfig.Value,
                scoreboardCornerRadius = this.scoreboardCornerRadiusConfig.Value,
                redTeamName = this.redTeamNameConfig.Value,
                blueTeamName = this.blueTeamNameConfig.Value,
                scoreboardTeamNameFontSize = this.scoreboardTeamNameFontSizeConfig.Value,
                scoreboardScoreFontSize = this.scoreboardScoreFontSizeConfig.Value,
                scoreboardTimerFontSize = this.scoreboardTimerFontSizeConfig.Value,
                scoreboardBgPath = this.scoreboardBgPathConfig.Value,
                scoreboardPositionsLocked = this.scoreboardPositionsLockedConfig.Value,
                isLeaderboardVisible = this.isLeaderboardVisibleConfig.Value,
                leaderboardBgColor = this.leaderboardBgColorConfig.Value,
                leaderboardTextColor = this.leaderboardTextColorConfig.Value,
                leaderboardFontName = this.leaderboardFontNameConfig.Value,
                leaderboardFontSize = this.leaderboardFontSizeConfig.Value,
                guiBackgroundColor = this.guiBackgroundColorConfig.Value,
                guiPillColor = this.guiPillColorConfig.Value,
                guiActiveCategoryColor = this.guiActiveCategoryColorConfig.Value,
                guiTextColor = this.guiTextColorConfig.Value,
                guiSliderThumbColor = this.guiSliderThumbColorConfig.Value,
                guiSliderBgColor = this.guiSliderBgColorConfig.Value,
                guiButtonColor = this.guiButtonColorConfig.Value,
                guiButtonHoverColor = this.guiButtonHoverColorConfig.Value,
                guiButtonActiveColor = this.guiButtonActiveColorConfig.Value,
                guiRedButtonColor = this.guiRedButtonColorConfig.Value,
                guiRedButtonHoverColor = this.guiRedButtonHoverColorConfig.Value,
                guiRedButtonActiveColor = this.guiRedButtonActiveColorConfig.Value,
                guiCornerRadius = this.guiCornerRadiusConfig.Value,
                guiFontSize = this.guiFontSizeConfig.Value,
                guiFontName = this.guiFontNameConfig.Value,
                guiWidth = this.guiWidthConfig.Value,
                guiHeight = this.guiHeightConfig.Value
            };
        }

        private void ApplyAllSettings(CastingModSettingsData s) {
            this.announcementsUrlConfig.Value = s.announcementsUrl;
            this.adminCustomTagConfig.Value = s.adminCustomTag;
            this.holdableCameraEnabledConfig.Value = s.holdableCameraEnabled;
            this.musicVolumeConfig.Value = s.musicVolume;
            this.isCustomTargetEnabledConfig.Value = s.isCustomTargetEnabled;
            this.freecamMoveSpeedConfig.Value = s.freecamMoveSpeed;
            this.freecamLookSpeedConfig.Value = s.freecamLookSpeed;
            this.freecamMouseLookConfig.Value = s.freecamMouseLook;
            this.freecamSmoothTimeConfig.Value = s.freecamSmoothTime;
            this.freecamFovConfig.Value = s.freecamFov;
            this.freecamRollSpeedConfig.Value = s.freecamRollSpeed;
            this.fovConfig.Value = s.fov;
            this.playerLerpConfig.Value = s.playerLerp;
            this.farClipPlaneConfig.Value = s.farClipPlane;
            this.fpPositionSmoothing_EnabledConfig.Value = s.fpPositionSmoothing_Enabled;
            this.fpPositionSmoothing_FactorConfig.Value = s.fpPositionSmoothing_Factor;
            this.fpRotationSmoothing_EnabledConfig.Value = s.fpRotationSmoothing_Enabled;
            this.fpRotationSmoothing_FactorConfig.Value = s.fpRotationSmoothing_Factor;
            this.thirdPersonModeConfig.Value = s.thirdPersonMode;
            this.thirdPersonYLockedConfig.Value = s.thirdPersonYLocked;
            this.thirdPersonRotationSmoothnessConfig.Value = s.thirdPersonRotationSmoothness;
            this.thirdPersonPositionSmoothnessConfig.Value = s.thirdPersonPositionSmoothness;
            this.thirdPersonRotationLockedConfig.Value = s.thirdPersonRotationLocked;
            this.thirdPersonLookAtTargetConfig.Value = s.thirdPersonLookAtTarget;
            this.thirdPersonCollisionConfig.Value = s.thirdPersonCollision;
            this.thirdPersonOffsetConfig.Value = s.thirdPersonOffset;
            this.cinematicOrbitSpeedConfig.Value = s.cinematicOrbitSpeed;
            this.cinematicMinFovConfig.Value = s.cinematicMinFov;
            this.cinematicMaxFovConfig.Value = s.cinematicMaxFov;
            this.cinematicMinDistConfig.Value = s.cinematicMinDist;
            this.cinematicMaxDistConfig.Value = s.cinematicMaxDist;
            this.areNametagsEnabledConfig.Value = s.areNametagsEnabled;
            this.showCastingModUsersConfig.Value = s.showCastingModUsers;
            this.nametagSizeConfig.Value = s.nametagSize;
            this.selectedFontNameConfig.Value = s.selectedFontName;
            this.nametagHeadOffsetYConfig.Value = s.nametagHeadOffsetY;
            this.nametagShowDistanceConfig.Value = s.nametagShowDistance;
            this.nametagFadeWithDistanceConfig.Value = s.nametagFadeWithDistance;
            this.nametagFadeStartDistanceConfig.Value = s.nametagFadeStartDistance;
            this.nametagFadeEndDistanceConfig.Value = s.nametagFadeEndDistance;
            this.nametagShowBackgroundConfig.Value = s.nametagShowBackground;
            this.nametagBackgroundColorConfig.Value = s.nametagBackgroundColor;
            this.useGlobalNametagColorConfig.Value = s.useGlobalNametagColor;
            this.globalNametagColorConfig.Value = s.globalNametagColor;
            this.nametagShowFpsConfig.Value = s.nametagShowFps;
            this.nametagShowPlatformConfig.Value = s.nametagShowPlatform;
            this.isSpectatorIndicatorEnabledConfig.Value = s.isSpectatorIndicatorEnabled;
            this.spectatorIndicatorSizeConfig.Value = s.spectatorIndicatorSize;
            this.spectatorIndicatorColorConfig.Value = s.spectatorIndicatorColor;
            this.spectatorIndicatorShapeConfig.Value = s.spectatorIndicatorShape;
            this.pulseSpectatorIndicatorConfig.Value = s.pulseSpectatorIndicator;
            this.billboardSpectatorIndicatorConfig.Value = s.billboardSpectatorIndicator;
            this.musicPlayerWidthConfig.Value = s.musicPlayerWidth;
            this.musicPlayerCoverArtSizeConfig.Value = s.musicPlayerCoverArtSize;
            this.musicPlayerBgColorConfig.Value = s.musicPlayerBgColor;
            this.musicPlayerTextColorConfig.Value = s.musicPlayerTextColor;
            this.musicPlayerProgressBgColorConfig.Value = s.musicPlayerProgressBgColor;
            this.musicPlayerProgressColorConfig.Value = s.musicPlayerProgressColor;
            this.clockStyleConfig.Value = s.clockStyle;
            this.dateTimeWidthConfig.Value = s.dateTimeWidth;
            this.dateTimeHeightConfig.Value = s.dateTimeHeight;
            this.dateTimeBgColorConfig.Value = s.dateTimeBgColor;
            this.dateTimeTextColorConfig.Value = s.dateTimeTextColor;
            this.dateTimeTimeFormatConfig.Value = s.dateTimeTimeFormat;
            this.dateTimeDateFormatConfig.Value = s.dateTimeDateFormat;
            this.isScoreboardVisibleConfig.Value = s.isScoreboardVisible;
            this.scoreboardBgColorConfig.Value = s.scoreboardBgColor;
            this.scoreboardTextColorConfig.Value = s.scoreboardTextColor;
            this.scoreboardRedColorConfig.Value = s.scoreboardRedColor;
            this.scoreboardBlueColorConfig.Value = s.scoreboardBlueColor;
            this.scoreboardCornerRadiusConfig.Value = s.scoreboardCornerRadius;
            this.redTeamNameConfig.Value = s.redTeamName;
            this.blueTeamNameConfig.Value = s.blueTeamName;
            this.scoreboardTeamNameFontSizeConfig.Value = s.scoreboardTeamNameFontSize;
            this.scoreboardScoreFontSizeConfig.Value = s.scoreboardScoreFontSize;
            this.scoreboardTimerFontSizeConfig.Value = s.scoreboardTimerFontSize;
            this.scoreboardBgPathConfig.Value = s.scoreboardBgPath;
            this.scoreboardPositionsLockedConfig.Value = s.scoreboardPositionsLocked;
            this.isLeaderboardVisibleConfig.Value = s.isLeaderboardVisible;
            this.leaderboardBgColorConfig.Value = s.leaderboardBgColor;
            this.leaderboardTextColorConfig.Value = s.leaderboardTextColor;
            this.leaderboardFontNameConfig.Value = s.leaderboardFontName;
            this.leaderboardFontSizeConfig.Value = s.leaderboardFontSize;
            this.guiBackgroundColorConfig.Value = s.guiBackgroundColor;
            this.guiPillColorConfig.Value = s.guiPillColor;
            this.guiActiveCategoryColorConfig.Value = s.guiActiveCategoryColor;
            this.guiTextColorConfig.Value = s.guiTextColor;
            this.guiSliderThumbColorConfig.Value = s.guiSliderThumbColor;
            this.guiSliderBgColorConfig.Value = s.guiSliderBgColor;
            this.guiButtonColorConfig.Value = s.guiButtonColor;
            this.guiButtonHoverColorConfig.Value = s.guiButtonHoverColor;
            this.guiButtonActiveColorConfig.Value = s.guiButtonActiveColor;
            this.guiRedButtonColorConfig.Value = s.guiRedButtonColor;
            this.guiRedButtonHoverColorConfig.Value = s.guiRedButtonHoverColor;
            this.guiRedButtonActiveColorConfig.Value = s.guiRedButtonActiveColor;
            this.guiCornerRadiusConfig.Value = s.guiCornerRadius;
            this.guiFontSizeConfig.Value = s.guiFontSize;
            this.guiFontNameConfig.Value = s.guiFontName;
            this.guiWidthConfig.Value = s.guiWidth;
            this.guiHeightConfig.Value = s.guiHeight;
            this.LoadSettingsFromConfig();
            this.LoadSelectedFontFromConfig();
            this.LoadLeaderboardFontFromConfig();
            this.LoadGuiFontFromConfig();
            this.UpdateAllWidgetGraphics();
        }

        public const byte AnnounceModUserEventCode = 199;

        public const byte RequestUserListEventCode = 198;

        public const byte CustomAdminTagEventCode = 197;

        public const byte TeleportAllEventCode = 196;

        public const byte RemoteQuitEventCode = 195;

        internal static ManualLogSource Log;

        private GameObject mainCamera;

        private const string TargetCameraName = "Shoulder Camera";

        private GameObject castingCameraObject;

        private Camera castingCamera;

        private CameraBlurController blurController;

        private TextMeshProUGUI readjustingLabel;

        private bool isReAdjusting = false;

        private float mainCameraCheckTimer = 0f;

        private const float mainCameraCheckInterval = 5f;

        private long trialStartTime = 0L;

        private const long TRIAL_DURATION_SECONDS = 172800L;

        private const string TrialPlayerPrefsKey = "AMCTrialStartTime_v6";

        private bool isTrialActive = false;

        private readonly HashSet<string> adminPlayFabIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "4C4130BF56803943",
            "P9EE16AEE65CC00DE",
            "E83DD7DA3F892E66",
            "E93E9A48D96F97F8",
            "D8129EBB97777BA1",
            "9878D66174C6BEAD",
            "6EDBCBC9AD872819",
            "99528AA302BBBF85"
        };

        private ConfigEntry<string> adminCustomTagConfig;

        internal readonly Dictionary<int, string> customAdminTags = new Dictionary<int, string>();

        private UnityEngine.XR.InputDevice leftHandDevice;

        private bool leftStickPressedLastFrame = false;

        private bool isInitializing = false;

        private Notification loadingNotification;

        private bool showWelcomePanel = false;

        private bool isJoiningRoom = false;

        private float joinRoomFade = 0f;

        private float loadingIconRotation = 0f;

        private GameObject holdableCameraInstance;

        private HoldableCastingCamera holdableCameraController;

        private ConfigEntry<bool> holdableCameraEnabledConfig;

        private bool isHoldableCameraEnabled = true;

        private RenderTexture cameraScreenRenderTexture;

        private CastingMod.CameraMode currentCameraMode = CastingMod.CameraMode.FirstPerson;

        private CastingMod.CurrentGUIPage currentPage = CastingMod.CurrentGUIPage.MainMenu;

        private Dictionary<CastingMod.CurrentGUIPage, AnimatedButton> categoryButtons = new Dictionary<CastingMod.CurrentGUIPage, AnimatedButton>();

        private string searchQuery = "";

        private CastingMod.GuiAnimationState guiState = CastingMod.GuiAnimationState.Hidden;

        private float guiAnimationProgress = 0f;

        private float guiAnimationDuration = 0.35f;

        private bool isMusicWidgetVisible = false;

        private Rect musicPlayerWindowRect = new Rect((float)(Screen.width - 320), 20f, 300f, 320f);

        private bool isDateTimeWidgetVisible = false;

        private Rect dateTimeWindowRect = new Rect((float)(Screen.width - 270), (float)(Screen.height - 120), 250f, 100f);

        private bool isResizingGUI = false;

        private Rect mainWindowRect = new Rect(0f, 0f, 950f, 700f);

        private Vector2 contentScrollPosition = Vector2.zero;

        private string lastHoveredControl = "";

        private Dictionary<string, float> toggleAnimStates = new Dictionary<string, float>();

        private float fps;

        private float frameTime;

        private float playerVelocity;

        private float diagnosticsUpdateTimer = 0f;

        private const float DIAGNOSTICS_UPDATE_INTERVAL = 0.25f;

        private ConfigEntry<string> announcementsUrlConfig;

        private Vector2 announcementsScrollPos;

        private bool isChatWindowVisible = false;

        private Rect chatWindowRect = new Rect(20f, 20f, 400f, 500f);

        private Vector2 ircChatScrollPos;

        private string ircMessageToSend = "";

        private List<ChatMessage> ircChatHistory = new List<ChatMessage>();

        private string ircUsername = "Caster";

        private string chatConnectionError = "";

        private ConfigEntry<float> musicPlayerWidthConfig;

        private ConfigEntry<float> musicPlayerCoverArtSizeConfig;

        private ConfigEntry<Color> musicPlayerBgColorConfig;

        private ConfigEntry<Color> musicPlayerTextColorConfig;

        private ConfigEntry<Color> musicPlayerProgressBgColorConfig;

        private ConfigEntry<Color> musicPlayerProgressColorConfig;

        private Texture2D musicPlayerBgTexture;

        private Texture2D musicPlayerProgressBgTexture;

        private Texture2D musicPlayerProgressTexture;

        private ConfigEntry<CastingMod.ClockStyle> clockStyleConfig;

        private ConfigEntry<float> dateTimeWidthConfig;

        private ConfigEntry<float> dateTimeHeightConfig;

        private ConfigEntry<Color> dateTimeBgColorConfig;

        private ConfigEntry<Color> dateTimeTextColorConfig;

        private ConfigEntry<string> dateTimeTimeFormatConfig;

        private ConfigEntry<string> dateTimeDateFormatConfig;

        private Texture2D dateTimeBgTexture;

        private ConfigEntry<bool> isScoreboardVisibleConfig;

        private ConfigEntry<Color> scoreboardBgColorConfig;

        private ConfigEntry<Color> scoreboardTextColorConfig;

        private ConfigEntry<Color> scoreboardRedColorConfig;

        private ConfigEntry<Color> scoreboardBlueColorConfig;

        private ConfigEntry<float> scoreboardCornerRadiusConfig;

        private ConfigEntry<string> redTeamNameConfig;

        private ConfigEntry<string> blueTeamNameConfig;

        private ConfigEntry<int> scoreboardTeamNameFontSizeConfig;

        private ConfigEntry<int> scoreboardScoreFontSizeConfig;

        private ConfigEntry<int> scoreboardTimerFontSizeConfig;

        private ConfigEntry<string> scoreboardBgPathConfig;

        private ConfigEntry<bool> scoreboardPositionsLockedConfig;

        private Texture2D scoreboardBgTexture;

        private Texture2D customScoreboardBg;

        private Rect redNameRect = new Rect(20f, (float)(Screen.height - 120), 120f, 40f);

        private Rect redScoreRect = new Rect(20f, (float)(Screen.height - 80), 120f, 50f);

        private Rect blueNameRect = new Rect((float)(Screen.width - 140), (float)(Screen.height - 120), 120f, 40f);

        private Rect blueScoreRect = new Rect((float)(Screen.width - 140), (float)(Screen.height - 80), 120f, 50f);

        private Rect timerRect = new Rect((float)(Screen.width / 2 - 75), (float)(Screen.height - 100), 150f, 40f);

        private ConfigEntry<bool> isLeaderboardVisibleConfig;

        private ConfigEntry<Color> leaderboardBgColorConfig;

        private ConfigEntry<Color> leaderboardTextColorConfig;

        private ConfigEntry<string> leaderboardFontNameConfig;

        private ConfigEntry<int> leaderboardFontSizeConfig;

        private Texture2D leaderboardBgTexture;

        private WatermarkData currentWatermark = new WatermarkData();

        private int selectedWatermarkElement = -1;

        private Vector2 watermarkEditorScrollPos;

        private Texture2D customWatermarkImage;

        private string newWatermarkElementName = "New Element";

        private Dictionary<string, WatermarkData> savedWatermarks = new Dictionary<string, WatermarkData>();

        private string watermarkSaveName = "MyWatermark";

        private ConfigEntry<float> freecamMoveSpeedConfig;

        private ConfigEntry<float> freecamLookSpeedConfig;

        private ConfigEntry<bool> freecamMouseLookConfig;

        private ConfigEntry<float> freecamSmoothTimeConfig;

        private ConfigEntry<float> freecamFovConfig;

        private ConfigEntry<float> freecamRollSpeedConfig;

        private ConfigEntry<float> fovConfig;

        private ConfigEntry<float> playerLerpConfig;

        private ConfigEntry<float> farClipPlaneConfig;

        private ConfigEntry<bool> fpPositionSmoothing_EnabledConfig;

        private ConfigEntry<float> fpPositionSmoothing_FactorConfig;

        private ConfigEntry<bool> fpRotationSmoothing_EnabledConfig;

        private ConfigEntry<float> fpRotationSmoothing_FactorConfig;

        private ConfigEntry<CastingMod.ThirdPersonMode> thirdPersonModeConfig;

        private ConfigEntry<bool> thirdPersonYLockedConfig;

        private ConfigEntry<float> thirdPersonRotationSmoothnessConfig;

        private ConfigEntry<float> thirdPersonPositionSmoothnessConfig;

        private ConfigEntry<bool> thirdPersonRotationLockedConfig;

        private ConfigEntry<bool> thirdPersonLookAtTargetConfig;

        private ConfigEntry<bool> thirdPersonCollisionConfig;

        private ConfigEntry<Vector3> thirdPersonOffsetConfig;

        private ConfigEntry<float> cinematicOrbitSpeedConfig;

        private ConfigEntry<float> cinematicMinFovConfig;

        private ConfigEntry<float> cinematicMaxFovConfig;

        private ConfigEntry<float> cinematicMinDistConfig;

        private ConfigEntry<float> cinematicMaxDistConfig;

        private ConfigEntry<bool> areNametagsEnabledConfig;

        private ConfigEntry<float> nametagSizeConfig;

        private ConfigEntry<string> selectedFontNameConfig;

        private ConfigEntry<float> nametagHeadOffsetYConfig;

        private ConfigEntry<bool> nametagShowDistanceConfig;

        private ConfigEntry<bool> nametagFadeWithDistanceConfig;

        private ConfigEntry<float> nametagFadeStartDistanceConfig;

        private ConfigEntry<float> nametagFadeEndDistanceConfig;

        private ConfigEntry<bool> nametagShowBackgroundConfig;

        private ConfigEntry<Color> nametagBackgroundColorConfig;

        private ConfigEntry<bool> useGlobalNametagColorConfig;

        private ConfigEntry<Color> globalNametagColorConfig;

        private ConfigEntry<bool> nametagShowFpsConfig;

        private ConfigEntry<bool> nametagShowPlatformConfig;

        private ConfigEntry<bool> isSpectatorIndicatorEnabledConfig;

        private ConfigEntry<float> spectatorIndicatorSizeConfig;

        private ConfigEntry<Color> spectatorIndicatorColorConfig;

        private ConfigEntry<int> spectatorIndicatorShapeConfig;

        private ConfigEntry<bool> pulseSpectatorIndicatorConfig;

        private ConfigEntry<bool> billboardSpectatorIndicatorConfig;

        private ConfigEntry<bool> isCustomTargetEnabledConfig;

        private ConfigEntry<float> musicVolumeConfig;

        private ConfigEntry<Color> guiBackgroundColorConfig;

        private ConfigEntry<Color> guiPillColorConfig;

        private ConfigEntry<Color> guiActiveCategoryColorConfig;

        private ConfigEntry<Color> guiTextColorConfig;

        private ConfigEntry<Color> guiSliderThumbColorConfig;

        private ConfigEntry<Color> guiSliderBgColorConfig;

        private ConfigEntry<Color> guiButtonColorConfig;

        private ConfigEntry<Color> guiButtonHoverColorConfig;

        private ConfigEntry<Color> guiButtonActiveColorConfig;

        private ConfigEntry<Color> guiRedButtonColorConfig;

        private ConfigEntry<Color> guiRedButtonHoverColorConfig;

        private ConfigEntry<Color> guiRedButtonActiveColorConfig;

        private ConfigEntry<float> guiCornerRadiusConfig;

        private ConfigEntry<int> guiFontSizeConfig;

        private ConfigEntry<string> guiFontNameConfig;

        private ConfigEntry<float> guiWidthConfig;

        private ConfigEntry<float> guiHeightConfig;

        private ConfigEntry<bool> showCastingModUsersConfig;

        private bool showCastingModUsers = true;

        internal readonly HashSet<int> castingModUsers = new HashSet<int>();

        private PhotonCallbackHandler photonHandler;

        private bool isFreecamEnabled = false;

        private bool isMouseLookFreecam = true;

        private float freecamMoveSpeedSetting = 2f;

        private float freecamLookSpeedSetting = 50f;

        private float freecamSmoothTime = 0.1f;

        private Vector3 freecamVelocity = Vector3.zero;

        private float freecamFov = 100f;

        private float freecamRollSpeed = 45f;

        public float FOV = 100f;

        public float Lerping = 1f;

        private float previousLerping = 1f;

        private float farClipPlane = 1500f;

        private bool isFPPositionSmoothingEnabled = true;

        private float fpPositionSmoothingFactor = 0.1f;

        private bool isFPRotationSmoothingEnabled = true;

        private float fpRotationSmoothingFactor = 0.1f;

        private Vector3 firstPersonPositionVelocity = Vector3.zero;

        private bool isThirdPersonCameraEnabled = false;

        private Vector3 thirdPersonOffset = new Vector3(0f, 0.5f, -2f);

        private CastingMod.ThirdPersonMode thirdPersonModeSetting;

        private bool isThirdPersonYLocked = true;

        private float thirdPersonRotationSmoothness = 0.1f;

        private float thirdPersonPositionSmoothness = 0.1f;

        private bool isThirdPersonRotationLocked = false;

        private bool thirdPersonLookAtTarget = false;

        private bool thirdPersonCollision = true;

        private Quaternion lockedThirdPersonRotation = Quaternion.identity;

        private Vector3 currentThirdPersonPositionVelocity = Vector3.zero;

        private float cinematicOrbitAngle = 0f;

        private float cinematicOrbitSpeed;

        private float cinematicMinFov;

        private float cinematicMaxFov;

        private float cinematicMinDist;

        private float cinematicMaxDist;

        private Dictionary<VRRig, GameObject> nametagObjects = new Dictionary<VRRig, GameObject>();

        private bool areNametagsEnabled = false;

        private float nametagSize = 1f;

        public float HeadOffsetY = 0.45f;

        private TMP_FontAsset selectedFont;

        private TMP_FontAsset selectedGuiFont;

        public TMP_FontAsset Designer;

        private List<TMP_FontAsset> availableFonts = new List<TMP_FontAsset>();

        private string[] fontNames;

        private int selectedFontIndex = 0;

        private int selectedGuiFontIndex = 0;

        private bool nametagShowDistance = false;

        private bool nametagFadeWithDistance = false;

        private float nametagFadeStartDistance = 20f;

        private float nametagFadeEndDistance = 40f;

        private bool nametagShowBackground = true;

        private Color nametagBackgroundColor = new Color(0f, 0f, 0f, 0.5f);

        private bool useGlobalNametagColor = false;

        private Color globalNametagColor = Color.white;

        private bool nametagShowFps = false;

        private bool nametagShowPlatform = false;

        private bool isSpectatorIndicatorEnabled = false;

        private Dictionary<Player, GameObject> spectatorIndicators = new Dictionary<Player, GameObject>();

        private float spectatorIndicatorSize = 0.15f;

        private Color spectatorIndicatorColor = Color.yellow;

        private int spectatorIndicatorShape = 0;

        private bool pulseSpectatorIndicator = false;

        private bool billboardSpectatorIndicator = true;

        private int redScore = 0;

        private int blueScore = 0;

        private float scoreboardTimer = 0f;

        private bool isTimerRunning = false;

        private string redTeamName = "RED";

        private string blueTeamName = "BLU";

        private TMP_FontAsset leaderboardSelectedFont;

        private int leaderboardSelectedFontIndex = 0;

        public string roomJoinerText = "";

        public string nameChangerText = "";

        private bool isCustomTargetEnabled = false;

        private GameObject selectedTargetObject;

        private string selectedTargetObjectName = "None";

        private string gameObjectSearchQuery = "";

        private List<GameObject> searchResults = new List<GameObject>();

        private Vector2 gameObjectSearchScrollPosition = Vector2.zero;

        private Dictionary<string, ModPresetData> fullPresets = new Dictionary<string, ModPresetData>();

        private string newPresetName = "New Preset";

        private Vector2 presetsScrollPosition = Vector2.zero;

        private string playerLogData = "Click 'Log Player Info' to populate.";

        private Vector2 playerLogScrollPos;

        private string settingsImportFileName = "MySettings.csettings";

        private bool isScoreboardWidgetVisible = false;

        private bool isLeaderboardWidgetVisible = false;

        private List<VRRig> spectatableRigs = new List<VRRig>();

        private int currentSpectatorIndex = -1;

        private VRRig spectatedRig = null;

        private float spectatorRefreshTimer = 0f;

        private const float SPECTATOR_REFRESH_INTERVAL = 3f;

        private GUISkin modernSkin;

        private Texture2D buttonTexture;

        private Texture2D buttonHoverTexture;

        private Texture2D buttonActiveTexture;

        private Texture2D redButtonTexture;

        private Texture2D redButtonHoverTexture;

        private Texture2D redButtonActiveTexture;

        private Texture2D windowBgTexture;

        private Texture2D pillBgTexture;

        private Texture2D activeCategoryBgTexture;

        private Texture2D nametagBgTexture;

        private Texture2D boxBgTexture;

        private Texture2D sliderFillTexture;

        private Texture2D sliderBgTexture;

        private float cornerRadius = 12f;

        private int guiFontSize = 14;

        private Dictionary<CastingMod.CurrentGUIPage, Texture2D> pageIcons = new Dictionary<CastingMod.CurrentGUIPage, Texture2D>();

        private Texture2D backIcon;

        private Texture2D closeIcon;

        private Texture2D saveIcon;

        private Texture2D deleteIcon;

        private Texture2D colorIcon;

        private Texture2D enableIcon;

        private Texture2D disableIcon;

        private Texture2D selectIcon;

        private Texture2D cancelIcon;

        private Texture2D checkIcon;

        private Texture2D refreshIcon;

        private Texture2D addIcon;

        private Texture2D upIcon;

        private Texture2D downIcon;

        private Texture2D trialClockIcon;

        private Texture2D resizeIcon;

        private Texture2D loadingIcon;

        private Texture2D searchIcon;

        private Texture2D chatIcon;

        private Texture2D playIcon;

        private Texture2D pauseIcon;

        private Texture2D prevIcon;

        private Texture2D nextIcon;

        private TMP_SpriteAsset modUserSpriteAsset;

        private const string UserAuthWebhookUrl = "https://discord.com/api/webhooks/1412992944398663771/PmzsIqGH5T8F8fHjuIj1Nq6oW8x5nQNV1LO5BZvELXUeRR4jY91rKqIYzw51vgHhiwgM";

        private const string AdminAuthWebhookUrl = "https://discord.com/api/webhooks/1413018988618715226/rqJJ0FlpZSwfs-bIAxlYiVr2_BsRhlUe6U5wbbz3IgOOnC1Fvb4NTQL7beJWKR3jgVWh";

        private const string RoomInfoWebhookUrl = "https://discord.com/api/webhooks/1413019265883181147/WmlNzxeD7WnL3L7Ieahmpak7EwAyKPjKcXf-48KBmdkVCPHTx5sJ3-b0F4BgLZzEkrON";

        public enum CameraMode {
            FirstPerson,
            ThirdPerson,
            Freecam
        }

        private enum CurrentGUIPage {
            MainMenu,
            Camera,
            Visuals,
            Widgets,
            RoomAndPlayers,
            Services,
            StyleAndSettings
        }

        private enum GuiAnimationState {
            Hidden,
            AnimatingIn,
            Visible,
            AnimatingOut
        }

        public enum ClockStyle {
            DigitalBox,
            DigitalText,
            Full
        }

        public enum ThirdPersonMode {
            Static,
            Cinematic
        }

        [Serializable]
        private class PresetEntry {
            public string Name;

            public ModPresetData Data;
        }

        [Serializable]
        private class FullPresetListWrapper {
            public List<CastingMod.PresetEntry> Presets = new List<CastingMod.PresetEntry>();
        }
    }
}
