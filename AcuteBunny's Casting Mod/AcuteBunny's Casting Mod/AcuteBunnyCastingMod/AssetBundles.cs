using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AcuteBunnyCastingMod {
    public class AssetBundles {
        private void OnGameInitiated() {
            Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AcuteBunnyCastingMod");
            this.assetBundle = AssetBundle.LoadFromStream(manifestResourceStream);
            manifestResourceStream.Close();
            this.baseGO = this.LoadAsset(this.assetBundle, "????");
        }

        private GameObject LoadAsset(AssetBundle a, string n) {
            return a.LoadAsset<GameObject>(n);
        }

        private AssetBundle assetBundle;

        private GameObject baseGO;
    }
}
