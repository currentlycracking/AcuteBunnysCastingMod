using System;
using UnityEngine;

namespace AcuteBunnyCastingMod.Services {
    public class Notification {
        public int Id { get; private set; }

        public string Title { get; set; }

        public string Message { get; set; }

        public float Duration { get; set; }

        public Texture2D Icon { get; set; }

        public bool IsLoadingNotif { get; set; }

        public float StartTime { get; private set; }

        public Notification(string title, string message, float duration, Texture2D icon, bool isLoading) {
            this.Id = Notification.nextId++;
            this.Title = title;
            this.Message = message;
            this.Duration = duration;
            this.Icon = icon;
            this.IsLoadingNotif = isLoading;
            this.StartTime = Time.unscaledTime;
        }

        public void ResetTimer() {
            this.StartTime = Time.unscaledTime;
        }

        public float TimeRemaining {
            get {
                return (this.Duration > 0f) ? Mathf.Max(0f, this.Duration - (Time.unscaledTime - this.StartTime)) : float.PositiveInfinity;
            }
        }

        public void Dismiss() {
            bool flag = this.State != Notification.NotificationState.FadingOut && this.State != Notification.NotificationState.Done;
            if(flag) {
                this.State = Notification.NotificationState.FadingOut;
                this.ResetTimer();
            }
        }

        private static int nextId;

        public Rect Rect;

        public Notification.NotificationState State = Notification.NotificationState.FadingIn;

        public float animationProgress = 0f;

        public enum NotificationState {
            FadingIn,
            Visible,
            FadingOut,
            Done
        }
    }
}
