using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    public class VRPhysicalButton : MonoBehaviour {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public event Action OnButtonPressed;

        private void Awake() {
            this.initialLocalPos = base.transform.localPosition;
            Renderer component = base.GetComponent<Renderer>();
            this.buttonMaterial = ((component != null) ? component.material : null);
            bool flag = this.buttonMaterial != null;
            if(flag) {
                this.baseColor = this.buttonMaterial.color;
            }
        }

        public void TriggerPress() {
            bool flag = Time.time < this.lastPressTime + this.pressCooldown;
            if(!flag) {
                this.lastPressTime = Time.time;
                Action onButtonPressed = this.OnButtonPressed;
                if(onButtonPressed != null) {
                    onButtonPressed();
                }
                base.StopAllCoroutines();
                base.StartCoroutine(this.AnimatePress());
            }
        }

        private IEnumerator AnimatePress() {
            AudioManager.PlayClick();
            bool flag = this.buttonMaterial != null;
            if(flag) {
                this.buttonMaterial.color = Color.Lerp(this.baseColor, Color.white, 0.5f);
            }
            Vector3 pressedPos = this.initialLocalPos - base.transform.parent.InverseTransformDirection(base.transform.forward) * 0.01f;
            float duration = 0.08f;
            float timer = 0f;
            while(timer < duration) {
                base.transform.localPosition = Vector3.Lerp(this.initialLocalPos, pressedPos, timer / duration);
                timer += Time.deltaTime;
                yield return null;
            }
            base.transform.localPosition = pressedPos;
            timer = 0f;
            while(timer < duration) {
                base.transform.localPosition = Vector3.Lerp(pressedPos, this.initialLocalPos, timer / duration);
                timer += Time.deltaTime;
                yield return null;
            }
            base.transform.localPosition = this.initialLocalPos;
            bool flag2 = this.buttonMaterial != null;
            if(flag2) {
                this.buttonMaterial.color = this.baseColor;
            }
            yield break;
        }

        private Vector3 initialLocalPos;

        private Material buttonMaterial;

        private Color baseColor;

        private float pressCooldown = 0.5f;

        private float lastPressTime = -1f;
    }
}
