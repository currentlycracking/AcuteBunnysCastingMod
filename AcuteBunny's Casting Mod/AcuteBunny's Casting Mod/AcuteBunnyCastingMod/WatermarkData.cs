using System;
using System.Collections.Generic;
using System.Numerics;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    [Serializable]
    public class WatermarkData {
        public bool isEnabled = false;

        public List<WatermarkElement> elements = new List<WatermarkElement>();

        public Vector2 anchorPoint = new Vector2(1f, 0f);

        public Vector2 screenOffset = new Vector2(-10f, 10f);
    }
}
