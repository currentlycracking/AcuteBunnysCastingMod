using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace AcuteBunnyCastingMod.Services {
    public static class ChatService {
        public static bool IsConnected { get; private set; }

        public static void Connect(MonoBehaviour context, Action<List<ChatMessage>> onMessagesReceived, Action<string> onError) {
            bool isConnected = ChatService.IsConnected;
            if(!isConnected) {
                ChatService.pollingCoroutine = context.StartCoroutine(ChatService.PollForMessages(onMessagesReceived, onError));
            }
        }

        public static void Disconnect(MonoBehaviour context) {
            bool flag = !ChatService.IsConnected;
            if(!flag) {
                ChatService.IsConnected = false;
                bool flag2 = ChatService.pollingCoroutine != null;
                if(flag2) {
                    context.StopCoroutine(ChatService.pollingCoroutine);
                }
                ChatService.pollingCoroutine = null;
            }
        }

        private static IEnumerator PollForMessages(Action<List<ChatMessage>> onMessagesReceived, Action<string> onError) {
            using(UnityWebRequest www = UnityWebRequest.Get(ChatService.serverUrl + "/messages")) {
                yield return www.SendWebRequest();
                bool flag = www.result == UnityWebRequest.Result.Success;
                if(!flag) {
                    CastingMod.Log.LogError("Chat server connection failed: " + www.error);
                    if(onError != null) {
                        onError("Connection Failed: " + www.error);
                    }
                    yield break;
                }
                ChatService.IsConnected = true;
                CastingMod.Log.LogInfo("Successfully connected to chat server.");
            }
            UnityWebRequest www = null;
            while(ChatService.IsConnected) {
                using(UnityWebRequest www2 = UnityWebRequest.Get(ChatService.serverUrl + "/messages")) {
                    yield return www2.SendWebRequest();
                    bool flag2 = www2.result == UnityWebRequest.Result.Success;
                    if(!flag2) {
                        CastingMod.Log.LogError("Chat poll error: " + www2.error);
                        if(onError != null) {
                            onError("Polling Error: " + www2.error);
                        }
                        ChatService.IsConnected = false;
                        break;
                    }
                    try {
                        ChatMessageList messageList = JsonUtility.FromJson<ChatMessageList>(www2.downloadHandler.text);
                        bool flag3 = messageList != null;
                        if(flag3) {
                            if(onMessagesReceived != null) {
                                onMessagesReceived(messageList.messages);
                            }
                        }
                        messageList = null;
                    } catch(Exception ex) {
                        Exception e = ex;
                        CastingMod.Log.LogError("Chat message parse error: " + e.Message + "\nReceived Text: " + www2.downloadHandler.text);
                    }
                }
                UnityWebRequest www2 = null;
                yield return new WaitForSeconds(2f);
                continue;
                break;
            }
            yield break;
            yield break;
        }

        public static void SendMessage(MonoBehaviour context, ChatMessage message) {
            context.StartCoroutine(ChatService.SendMessageCoroutine(message));
        }

        private static IEnumerator SendMessageCoroutine(ChatMessage message) {
            string json = JsonUtility.ToJson(message);
            using(UnityWebRequest www = new UnityWebRequest(ChatService.serverUrl + "/send", "POST")) {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                yield return www.SendWebRequest();
                bool flag = www.result != UnityWebRequest.Result.Success;
                if(flag) {
                    CastingMod.Log.LogError("Failed to send chat message: " + www.error);
                }
                bodyRaw = null;
            }
            UnityWebRequest www = null;
            yield break;
            yield break;
        }

        private static string serverUrl = "acutebunny.pythonanywhere.com";

        private static Coroutine pollingCoroutine;
    }
}
