using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod.Services {
    public static class NotificationManager {
        public static Notification Show(string title, string message, float duration = 5f, Texture2D icon = null, bool isLoading = false) {
            Notification notification = new Notification(title, message, duration, icon, isLoading);
            NotificationManager.activeNotifications.Add(notification);
            return notification;
        }

        public static void Update() {
            NotificationManager.rotationAngle += Time.unscaledDeltaTime * -200f;
            for(int i = NotificationManager.activeNotifications.Count - 1; i >= 0; i--) {
                Notification notification = NotificationManager.activeNotifications[i];
                float num = Time.unscaledTime - notification.StartTime;
                switch(notification.State) {
                    case Notification.NotificationState.FadingIn: {
                        notification.animationProgress = Mathf.Clamp01(num / 0.4f);
                        bool flag = notification.animationProgress >= 1f;
                        if(flag) {
                            notification.State = Notification.NotificationState.Visible;
                            notification.ResetTimer();
                        }
                        break;
                    }
                    case Notification.NotificationState.Visible: {
                        bool flag2 = notification.Duration > 0f && num >= notification.Duration;
                        if(flag2) {
                            notification.State = Notification.NotificationState.FadingOut;
                            notification.ResetTimer();
                        }
                        break;
                    }
                    case Notification.NotificationState.FadingOut: {
                        notification.animationProgress = 1f - Mathf.Clamp01(num / 0.4f);
                        bool flag3 = notification.animationProgress <= 0f;
                        if(flag3) {
                            notification.State = Notification.NotificationState.Done;
                        }
                        break;
                    }
                }
                bool flag4 = notification.State == Notification.NotificationState.Done;
                if(flag4) {
                    NotificationManager.activeNotifications.RemoveAt(i);
                }
            }
        }

        private static float EaseOutCubic(float x) {
            return 1f - Mathf.Pow(1f - x, 3f);
        }

        public static void OnGUI(GUISkin skin, Texture2D bgTexture, Texture2D closeIcon) {
            bool flag = NotificationManager.activeNotifications.Count == 0 || skin == null;
            if(!flag) {
                GUI.skin = skin;
                float num = 15f;
                for(int i = 0; i < NotificationManager.activeNotifications.Count; i++) {
                    Notification notification = NotificationManager.activeNotifications[i];
                    float num2 = NotificationManager.EaseOutCubic(notification.animationProgress);
                    float x = (float)Screen.width - 365f + 350f * (1f - num2);
                    notification.Rect = new Rect(x, num, 350f, 85f);
                    GUIStyle guistyle = new GUIStyle(skin.box);
                    bool flag2 = bgTexture != null;
                    if(flag2) {
                        guistyle.normal.background = bgTexture;
                        guistyle.border = new RectOffset(12, 12, 12, 12);
                    }
                    GUI.color = new Color(1f, 1f, 1f, 0.95f * num2);
                    GUI.Box(notification.Rect, GUIContent.none, guistyle);
                    GUI.color = Color.white;
                    Rect screenRect = new Rect(notification.Rect.x + 15f, notification.Rect.y + 15f, notification.Rect.width - 30f, notification.Rect.height - 30f);
                    GUILayout.BeginArea(screenRect);
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    bool flag3 = notification.Icon != null;
                    if(flag3) {
                        Rect position = new Rect(0f, 0f, 40f, 40f);
                        bool isLoadingNotif = notification.IsLoadingNotif;
                        if(isLoadingNotif) {
                            Matrix4x4 matrix = GUI.matrix;
                            GUIUtility.RotateAroundPivot(NotificationManager.rotationAngle, position.center);
                            GUI.DrawTexture(position, notification.Icon, ScaleMode.ScaleToFit);
                            GUI.matrix = matrix;
                        } else {
                            GUI.DrawTexture(position, notification.Icon, ScaleMode.ScaleToFit);
                        }
                        GUILayout.Space(50f);
                    }
                    GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
                    GUILayout.Label("<b>" + notification.Title + "</b>", new GUIStyle(skin.label) {
                        fontSize = 14
                    }, Array.Empty<GUILayoutOption>());
                    GUILayout.Label(notification.Message, new GUIStyle(skin.label) {
                        wordWrap = true,
                        fontSize = 12
                    }, Array.Empty<GUILayoutOption>());
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                    bool flag4 = notification.Duration > 0f;
                    if(flag4) {
                        float num3 = 4f;
                        float num4 = 5f;
                        Rect position2 = new Rect(notification.Rect.x + num4, notification.Rect.yMax - num3 - num4, notification.Rect.width - num4 * 2f, num3);
                        GUI.DrawTexture(position2, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.2f), 0f, 0f);
                        float num5 = (notification.State == Notification.NotificationState.Visible) ? (notification.TimeRemaining / notification.Duration) : 0f;
                        Rect position3 = new Rect(position2.x, position2.y, position2.width * num5, position2.height);
                        GUI.DrawTexture(position3, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 0.5f), 0f, 0f);
                    }
                    bool flag5 = closeIcon != null && GUI.Button(new Rect(notification.Rect.xMax - 30f, notification.Rect.y + 5f, 25f, 25f), new GUIContent(closeIcon), GUI.skin.label);
                    if(flag5) {
                        notification.Dismiss();
                    }
                    num += 92.5f;
                }
            }
        }

        private static List<Notification> activeNotifications = new List<Notification>();

        private const float NOTIFICATION_WIDTH = 350f;

        private const float NOTIFICATION_HEIGHT = 85f;

        private const float PADDING = 15f;

        private const float ANIMATION_DURATION = 0.4f;

        private static float rotationAngle = 0f;
    }
}
