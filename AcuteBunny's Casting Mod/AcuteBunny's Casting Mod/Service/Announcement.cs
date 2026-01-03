using System;

namespace AcuteBunnyCastingMod.Services {
    [Serializable]
    public class Announcement {
        public string id;

        public string title;

        public string message;

        public long timestamp;
    }
}
