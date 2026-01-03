using System;
using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    [Serializable]
    public class CastingModSettingsData {
        public string announcementsUrl;

        public string adminCustomTag;

        public bool holdableCameraEnabled;

        public float musicVolume;

        public bool isCustomTargetEnabled;

        public float freecamMoveSpeed;

        public float freecamLookSpeed;

        public bool freecamMouseLook;

        public float freecamSmoothTime;

        public float freecamFov;

        public float freecamRollSpeed;

        public float fov;

        public float playerLerp;

        public float farClipPlane;

        public bool fpPositionSmoothing_Enabled;

        public float fpPositionSmoothing_Factor;

        public bool fpRotationSmoothing_Enabled;

        public float fpRotationSmoothing_Factor;

        public CastingMod.ThirdPersonMode thirdPersonMode;

        public bool thirdPersonYLocked;

        public float thirdPersonRotationSmoothness;

        public float thirdPersonPositionSmoothness;

        public bool thirdPersonRotationLocked;

        public bool thirdPersonLookAtTarget;

        public bool thirdPersonCollision;

        public Vector3 thirdPersonOffset;

        public float cinematicOrbitSpeed;

        public float cinematicMinFov;

        public float cinematicMaxFov;

        public float cinematicMinDist;

        public float cinematicMaxDist;

        public bool areNametagsEnabled;

        public bool showCastingModUsers;

        public float nametagSize;

        public string selectedFontName;

        public float nametagHeadOffsetY;

        public bool nametagShowDistance;

        public bool nametagFadeWithDistance;

        public float nametagFadeStartDistance;

        public float nametagFadeEndDistance;

        public bool nametagShowBackground;

        public Color nametagBackgroundColor;

        public bool useGlobalNametagColor;

        public Color globalNametagColor;

        public bool nametagShowFps;

        public bool nametagShowPlatform;

        public bool isSpectatorIndicatorEnabled;

        public float spectatorIndicatorSize;

        public Color spectatorIndicatorColor;

        public int spectatorIndicatorShape;

        public bool pulseSpectatorIndicator;

        public bool billboardSpectatorIndicator;

        public float musicPlayerWidth;

        public float musicPlayerCoverArtSize;

        public Color musicPlayerBgColor;

        public Color musicPlayerTextColor;

        public Color musicPlayerProgressBgColor;

        public Color musicPlayerProgressColor;

        public CastingMod.ClockStyle clockStyle;

        public float dateTimeWidth;

        public float dateTimeHeight;

        public Color dateTimeBgColor;

        public Color dateTimeTextColor;

        public string dateTimeTimeFormat;

        public string dateTimeDateFormat;

        public bool isScoreboardVisible;

        public float scoreboardX;

        public float scoreboardY;

        public float scoreboardW;

        public float scoreboardH;

        public Color scoreboardBgColor;

        public Color scoreboardTextColor;

        public Color scoreboardRedColor;

        public Color scoreboardBlueColor;

        public float scoreboardCornerRadius;

        public string redTeamName;

        public string blueTeamName;

        public int scoreboardTeamNameFontSize;

        public int scoreboardScoreFontSize;

        public int scoreboardTimerFontSize;

        public string scoreboardBgPath;

        public bool scoreboardPositionsLocked;

        public bool isLeaderboardVisible;

        public Color leaderboardBgColor;

        public Color leaderboardTextColor;

        public string leaderboardFontName;

        public int leaderboardFontSize;

        public Color guiBackgroundColor;

        public Color guiPillColor;

        public Color guiActiveCategoryColor;

        public Color guiTextColor;

        public Color guiSliderThumbColor;

        public Color guiSliderBgColor;

        public Color guiButtonColor;

        public Color guiButtonHoverColor;

        public Color guiButtonActiveColor;

        public Color guiRedButtonColor;

        public Color guiRedButtonHoverColor;

        public Color guiRedButtonActiveColor;

        public float guiCornerRadius;

        public int guiFontSize;

        public string guiFontName;

        public float guiWidth;

        public float guiHeight;
    }
}
