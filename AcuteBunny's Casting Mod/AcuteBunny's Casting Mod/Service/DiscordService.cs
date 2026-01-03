using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;

namespace AcuteBunnyCastingMod.Services {
    public static class DiscordService {
        public static void SendMessage(string webhookUrl, string content) {
            bool flag = string.IsNullOrEmpty(webhookUrl) || string.IsNullOrEmpty(content);
            if(!flag) {
                Task.Run(delegate () {
                    try {
                        string data = JsonUtility.ToJson(new DiscordService.DiscordPayload
                        {
                            content = content
                        });
                        using(WebClient webClient = new WebClient()) {
                            webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                            webClient.UploadString(webhookUrl, "POST", data);
                        }
                    } catch(Exception ex) {
                        CastingMod.Log.LogError("Discord webhook failed: " + ex.Message);
                    }
                });
            }
        }

        [Serializable]
        private class DiscordPayload {
            public string content = null;
        }
    }
}
