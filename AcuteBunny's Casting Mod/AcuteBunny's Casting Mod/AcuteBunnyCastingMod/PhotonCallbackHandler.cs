using ExitGames.Client.Photon;
using GorillaLocomotion;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using UnityEngine;
using static System.Net.Mime.MediaTypeNames;

namespace AcuteBunnyCastingMod {
    public class PhotonCallbackHandler : MonoBehaviourPunCallbacks, IOnEventCallback {
        public void Initialize(CastingMod mod) {
            this.mainMod = mod;
        }

        public override void OnEnable() {
            base.OnEnable();
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable() {
            base.OnDisable();
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public override void OnJoinedRoom() {
            this.mainMod.OnRoomJoined();
            this.mainMod.castingModUsers.Clear();
            this.mainMod.customAdminTags.Clear();
            PhotonNetwork.RaiseEvent(199, null, new RaiseEventOptions {
                Receivers = ReceiverGroup.Others
            }, SendOptions.SendReliable);
            bool flag = this.mainMod.IsLocalPlayerAdmin();
            if(flag) {
                this.mainMod.BroadcastAdminTag(-1);
            }
            this.mainMod.LogAllPlayerInfo();
            this.mainMod.DiscordLogRoomInfo();
            this.mainMod.RefreshSpectatablePlayers();
        }

        public override void OnJoinRoomFailed(short returnCode, string message) {
            this.mainMod.OnRoomJoinFailed();
        }

        public override void OnLeftRoom() {
            this.mainMod.castingModUsers.Clear();
            this.mainMod.customAdminTags.Clear();
            this.mainMod.RefreshSpectatablePlayers();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer) {
            bool flag = this.mainMod.castingModUsers.Contains(otherPlayer.ActorNumber);
            if(flag) {
                this.mainMod.castingModUsers.Remove(otherPlayer.ActorNumber);
            }
            bool flag2 = this.mainMod.customAdminTags.ContainsKey(otherPlayer.ActorNumber);
            if(flag2) {
                this.mainMod.customAdminTags.Remove(otherPlayer.ActorNumber);
            }
            this.mainMod.RefreshSpectatablePlayers();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) {
            PhotonNetwork.RaiseEvent(199, null, new RaiseEventOptions {
                TargetActors = new int[]
                {
                    newPlayer.ActorNumber
                }
            }, SendOptions.SendReliable);
            bool flag = this.mainMod.IsLocalPlayerAdmin();
            if(flag) {
                this.mainMod.BroadcastAdminTag(newPlayer.ActorNumber);
            }
            this.mainMod.RefreshSpectatablePlayers();
        }

        public void RequestUserAnnouncements() {
            bool inRoom = PhotonNetwork.InRoom;
            if(inRoom) {
                PhotonNetwork.RaiseEvent(198, null, new RaiseEventOptions {
                    Receivers = ReceiverGroup.Others
                }, SendOptions.SendReliable);
            }
        }

        public void OnEvent(EventData photonEvent) {
            switch(photonEvent.Code) {
                case 195: {
                    Player player = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender, false);
                    bool flag = player != null && this.mainMod.IsPlayerAdmin(player);
                    if(flag) {
                        Application.Quit();
                    }
                    break;
                }
                case 196: {
                    object[] array = photonEvent.CustomData as object[];
                    bool flag2 = array != null && array.Length == 1;
                    if(flag2) {
                        Player player2 = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender, false);
                        bool flag3 = player2 != null && this.mainMod.IsPlayerAdmin(player2);
                        if(flag3) {
                            Vector3 position = (Vector3)array[0];
                            GTPlayer.Instance.transform.position = position;
                        }
                    }
                    break;
                }
                case 197: {
                    string text = photonEvent.CustomData as string;
                    bool flag4 = text != null;
                    if(flag4) {
                        this.mainMod.customAdminTags[photonEvent.Sender] = text;
                    }
                    break;
                }
                case 198:
                PhotonNetwork.RaiseEvent(199, null, new RaiseEventOptions {
                    TargetActors = new int[]
                    {
                        photonEvent.Sender
                    }
                }, SendOptions.SendReliable);
                break;
                case 199: {
                    this.mainMod.castingModUsers.Add(photonEvent.Sender);
                    Player player3 = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender, false);
                    bool flag5 = player3 != null;
                    if(flag5) {
                        this.mainMod.DiscordLogUserAuth(player3);
                    }
                    break;
                }
            }
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged) {
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) {
        }

        public override void OnMasterClientSwitched(Player newMasterClient) {
        }

        public override void OnConnected() {
        }

        public override void OnConnectedToMaster() {
        }

        public override void OnDisconnected(DisconnectCause cause) {
        }

        public override void OnRegionListReceived(RegionHandler regionHandler) {
        }

        public override void OnCustomAuthenticationResponse(Dictionary<string, object> data) {
        }

        public override void OnCustomAuthenticationFailed(string debugMessage) {
        }

        public override void OnFriendListUpdate(List<FriendInfo> friendList) {
        }

        public override void OnCreatedRoom() {
        }

        private CastingMod mainMod;
    }
}
