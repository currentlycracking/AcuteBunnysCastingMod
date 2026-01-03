using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace AcuteBunnyCastingMod.Services {
    public static class AnnouncementService {
        public static List<Announcement> AllAnnouncements {
            get {
                return AnnouncementService.allAnnouncements;
            }
        }

        public static void StartService(MonoBehaviour context, string url) {
            AnnouncementService.announcementUrl = url;
            bool flag = AnnouncementService.checkingCoroutine != null;
            if(flag) {
                context.StopCoroutine(AnnouncementService.checkingCoroutine);
            }
            AnnouncementService.checkingCoroutine = context.StartCoroutine(AnnouncementService.CheckLoop());
        }

        private static IEnumerator CheckLoop() {
            yield return AnnouncementService.CheckForAnnouncements(true);
            for(; ; )
            {
                yield return new WaitForSeconds(300f);
                yield return AnnouncementService.CheckForAnnouncements(false);
            }
            yield break;
        }

        private static List<Announcement> ParseSimpleAnnouncementFormat(string text) {
            List<Announcement> list = new List<Announcement>();
            bool flag = string.IsNullOrWhiteSpace(text);
            List<Announcement> result;
            if(flag) {
                result = list;
            } else {
                string[] array = text.Split(new string[]
                {
                    "---"
                }, StringSplitOptions.RemoveEmptyEntries);
                foreach(string text2 in array) {
                    bool flag2 = string.IsNullOrWhiteSpace(text2);
                    if(!flag2) {
                        Announcement announcement = new Announcement();
                        string[] array3 = text2.Split(new char[]
                        {
                            '\n'
                        });
                        foreach(string text3 in array3) {
                            bool flag3 = string.IsNullOrWhiteSpace(text3);
                            if(!flag3) {
                                int num = text3.IndexOf(':');
                                bool flag4 = num == -1;
                                if(!flag4) {
                                    string text4 = text3.Substring(0, num).Trim().ToLower();
                                    string text5 = text3.Substring(num + 1).Trim();
                                    string text6 = text4;
                                    string a = text6;
                                    if(!(a == "id")) {
                                        if(!(a == "title")) {
                                            if(!(a == "message")) {
                                                if(a == "timestamp") {
                                                    long.TryParse(text5, out announcement.timestamp);
                                                }
                                            } else {
                                                announcement.message = text5;
                                            }
                                        } else {
                                            announcement.title = text5;
                                        }
                                    } else {
                                        announcement.id = text5;
                                    }
                                }
                            }
                        }
                        bool flag5 = !string.IsNullOrEmpty(announcement.id);
                        if(flag5) {
                            list.Add(announcement);
                        }
                    }
                }
                result = list;
            }
            return result;
        }

        private static IEnumerator CheckForAnnouncements(bool isFirstCheck) {
            bool flag = string.IsNullOrEmpty(AnnouncementService.announcementUrl);
            if(flag) {
                yield break;
            }
            using(UnityWebRequest www = UnityWebRequest.Get(AnnouncementService.announcementUrl)) {
                yield return www.SendWebRequest();
                bool flag2 = www.result == UnityWebRequest.Result.Success;
                if(flag2) {
                    try {
                        List<Announcement> list = AnnouncementService.ParseSimpleAnnouncementFormat(www.downloadHandler.text);
                        List<Announcement> newAnnouncements = new List<Announcement>();
                        foreach(Announcement ann in from a in list
                                                    orderby a.timestamp descending
                                                    select a) {
                            bool flag3 = !AnnouncementService.seenAnnouncementIds.Contains(ann.id);
                            if(flag3) {
                                newAnnouncements.Add(ann);
                                AnnouncementService.seenAnnouncementIds.Add(ann.id);
                            }
                            ann = null;
                        }
                        IEnumerator<Announcement> enumerator = null;
                        bool flag4 = newAnnouncements.Count > 0;
                        if(flag4) {
                            AnnouncementService.allAnnouncements.InsertRange(0, newAnnouncements);
                            bool flag5 = !isFirstCheck;
                            if(flag5) {
                                foreach(Announcement newAnn in newAnnouncements) {
                                    NotificationManager.Show("New Announcement", newAnn.title, 8f, null, false);
                                    newAnn = null;
                                }
                                List<Announcement>.Enumerator enumerator2 = default(List<Announcement>.Enumerator);
                            }
                        }
                        list = null;
                        newAnnouncements = null;
                    } catch(Exception ex) {
                        Exception e = ex;
                        CastingMod.Log.LogError("Failed to parse announcements: " + e.Message);
                    }
                } else {
                    CastingMod.Log.LogError("Failed to fetch announcements: " + www.error);
                }
            }
            UnityWebRequest www = null;
            yield break;
            yield break;
        }

        private static List<Announcement> allAnnouncements = new List<Announcement>(); 
        private static HashSet<string> seenAnnouncementIds = new HashSet<string>(); 
        private static Coroutine checkingCoroutine; 
        private static string announcementUrl = "https://pastebin.com/raw/vFXkeKTJ";
    }
}
