using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using TMPro;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    public class HoldableCastingCamera : MonoBehaviour {
        public bool IsHeld { get; private set; } = false;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public event Action OnModeCyclePressed;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public event Action OnPickup;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public event Action OnDrop;

        public void Initialize(Light recLight, TextMeshProUGUI osdText, VRPhysicalButton modeButton) {
            this.rb = base.GetComponent<Rigidbody>();
            GorillaTagger instance = GorillaTagger.Instance;
            this.rightHand = ((instance != null) ? instance.rightHandTransform : null);
            GorillaTagger instance2 = GorillaTagger.Instance;
            this.leftHand = ((instance2 != null) ? instance2.leftHandTransform : null);
            this.recordingLight = recLight;
            this.statusText = osdText;
            bool flag = modeButton != null;
            if(flag) {
                modeButton.OnButtonPressed += delegate () {
                    Action onModeCyclePressed = this.OnModeCyclePressed;
                    if(onModeCyclePressed != null) {
                        onModeCyclePressed();
                    }
                };
            }
            base.StartCoroutine(this.IdleAnimation());
            osdText.gameObject.SetActive(true);
        }

        private void Update() {
            this.HandleInteraction();
        }

        private void HandleInteraction() {
            bool rightGrab = ControllerInputPoller.instance.rightGrab;
            bool leftGrab = ControllerInputPoller.instance.leftGrab;
            bool isHeld = this.IsHeld;
            if(isHeld) {
                bool flag = (this.pickupHand == this.rightHand) ? rightGrab : leftGrab;
                bool flag2 = !flag;
                if(flag2) {
                    this.Drop();
                }
            } else {
                bool flag3 = this.isGrabbable;
                if(flag3) {
                    bool flag4 = rightGrab && Vector3.Distance(base.transform.position, this.rightHand.position) < 0.2f;
                    if(flag4) {
                        this.Pickup(this.rightHand);
                    } else {
                        bool flag5 = leftGrab && Vector3.Distance(base.transform.position, this.leftHand.position) < 0.2f;
                        if(flag5) {
                            this.Pickup(this.leftHand);
                        }
                    }
                }
            }
        }

        private void Pickup(Transform hand) {
            bool isHeld = this.IsHeld;
            if(!isHeld) {
                this.IsHeld = true;
                this.pickupHand = hand;
                this.rb.isKinematic = true;
                Action onPickup = this.OnPickup;
                if(onPickup != null) {
                    onPickup();
                }
                bool flag = this.pickupAnimCoroutine != null;
                if(flag) {
                    base.StopCoroutine(this.pickupAnimCoroutine);
                }
                this.pickupAnimCoroutine = base.StartCoroutine(this.AnimatePickup());
            }
        }

        private void Drop() {
            bool flag = !this.IsHeld;
            if(!flag) {
                this.IsHeld = false;
                this.rb.isKinematic = false;
                this.rb.velocity = GorillaTagger.Instance.bodyCollider.attachedRigidbody.velocity + this.pickupHand.forward * 2f;
                this.rb.angularVelocity = Vector3.zero;
                base.transform.SetParent(null);
                Action onDrop = this.OnDrop;
                if(onDrop != null) {
                    onDrop();
                }
                this.pickupHand = null;
            }
        }

        public void UpdateStatusText(string mode, float fov, string target) {
            bool flag = this.statusText == null || !this.statusText.gameObject.activeInHierarchy;
            if(!flag) {
                this.statusText.text = string.Format("<align=left>MODE: {0}\n<align=left>FOV: {1:F0}\n<align=left>TARGET: {2}", mode.ToUpper(), fov, target);
            }
        }

        private IEnumerator AnimatePickup() {
            base.transform.SetParent(this.pickupHand);
            float duration = 0.2f;
            float timer = 0f;
            Vector3 startPos = base.transform.localPosition;
            Quaternion startRot = base.transform.localRotation;
            while(timer < duration) {
                float progress = this.EaseOutBack(timer / duration);
                base.transform.localPosition = Vector3.Lerp(startPos, this.holdPositionOffset, progress);
                base.transform.localRotation = Quaternion.Slerp(startRot, this.holdRotationOffset, progress);
                timer += Time.deltaTime;
                yield return null;
            }
            base.transform.localPosition = this.holdPositionOffset;
            base.transform.localRotation = this.holdRotationOffset;
            yield break;
        }

        private IEnumerator IdleAnimation() {
            for(; ; )
            {
                bool flag = this.recordingLight != null;
                if(flag) {
                    this.recordingLight.intensity = 1.5f + Mathf.Sin(Time.time * 4f) * 0.5f;
                }
                yield return null;
            }
            yield break;
        }

        private void OnTriggerEnter(Collider other) {
            bool flag = other.name.Contains("HandTrigger");
            if(flag) {
                this.isGrabbable = true;
            }
            VRPhysicalButton componentInParent = other.GetComponentInParent<VRPhysicalButton>();
            bool flag2 = componentInParent != null;
            if(flag2) {
                componentInParent.TriggerPress();
            }
        }

        private void OnTriggerExit(Collider other) {
            bool flag = other.name.Contains("HandTrigger");
            if(flag) {
                this.isGrabbable = false;
            }
        }

        private float EaseOutBack(float x) {
            return 1f + 2.70158f * Mathf.Pow(x - 1f, 3f) + 1.70158f * Mathf.Pow(x - 1f, 2f);
        }

        private Rigidbody rb;

        private Transform rightHand;

        private Transform leftHand;

        private Transform pickupHand;

        private bool isGrabbable = false;

        private Vector3 holdPositionOffset = new Vector3(0.02f, -0.04f, 0.05f);

        private Quaternion holdRotationOffset = Quaternion.Euler(15f, -10f, 5f);

        private Coroutine pickupAnimCoroutine;

        private Light recordingLight;

        private TextMeshProUGUI statusText;
    }
}
