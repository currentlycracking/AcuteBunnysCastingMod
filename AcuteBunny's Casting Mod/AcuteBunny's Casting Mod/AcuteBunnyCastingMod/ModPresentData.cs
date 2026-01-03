using System;
using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    [Serializable]
    public class ModPresetData {
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
    }
}
