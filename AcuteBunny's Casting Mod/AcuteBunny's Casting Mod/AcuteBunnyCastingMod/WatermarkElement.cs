using System;
using System.Drawing;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    [Serializable]
    public class WatermarkElement {
        public WatermarkElement.ElementType type;

        public Vector2 position;

        public Vector2 size;

        public float rotation;

        public Color color;

        public string textContent;

        public int fontSize;

        public TextAnchor alignment;

        public float cornerRadius;

        public string elementName;

        public enum ElementType {
            Text,
            Rectangle,
            RoundedRectangle,
            Image
        }
    }
}
