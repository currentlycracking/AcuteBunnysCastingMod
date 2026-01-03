using System;

namespace AcuteBunnyCastingMod.Services {
    [Serializable]
    public class ChatMessage {
        public string username;

        public string message;

        public long timestamp;
    }
}
