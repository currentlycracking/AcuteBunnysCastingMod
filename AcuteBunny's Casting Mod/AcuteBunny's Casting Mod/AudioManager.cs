using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.Mime.MediaTypeNames;

public static class AudioManager {
    public static float MusicVolume {
        get {
            return (AudioManager.musicSource != null) ? AudioManager.musicSource.volume : 0f;
        }
        set {
            bool flag = AudioManager.musicSource != null;
            if(flag) {
                AudioManager.musicSource.volume = value;
            }
        }
    }

    public static bool IsPlaying {
        get {
            return AudioManager.playerState == AudioManager.PlayState.Playing;
        }
    }

    public static bool IsInitialized {
        get {
            return AudioManager.isInitialized;
        }
    }

    public static string CurrentTrackName {
        get {
            MusicTrack currentTrack = AudioManager.GetCurrentTrack();
            return ((currentTrack != null) ? currentTrack.clip.name : null) ?? "None";
        }
    }

    public static Texture2D CurrentCoverArt {
        get {
            MusicTrack currentTrack = AudioManager.GetCurrentTrack();
            return ((currentTrack != null) ? currentTrack.coverArt : null) ?? AudioManager.defaultCoverArt;
        }
    }

    public static float PlaybackProgress {
        get {
            bool flag = AudioManager.musicSource == null || AudioManager.musicSource.clip == null || AudioManager.musicSource.clip.length <= 0f;
            float result;
            if(flag) {
                result = 0f;
            } else {
                result = AudioManager.musicSource.time / AudioManager.musicSource.clip.length;
            }
            return result;
        }
    }

    private static MusicTrack GetCurrentTrack() {
        bool flag = AudioManager.currentTrackIndex >= 0 && AudioManager.currentTrackIndex < AudioManager.musicPlaylist.Count;
        MusicTrack result;
        if(flag) {
            result = AudioManager.musicPlaylist[AudioManager.currentTrackIndex];
        } else {
            result = null;
        }
        return result;
    }

    public static IEnumerator Initialize(string pluginPath, ManualLogSource logger) {
        bool flag = AudioManager.isInitialized;
        if(flag) {
            yield break;
        }
        AudioManager.a_Log = logger;
        GameObject audioManagerObject = new GameObject("CastingMod_AudioManager");
        UnityEngine.Object.DontDestroyOnLoad(audioManagerObject);
        AudioManager.musicSource = audioManagerObject.AddComponent<AudioSource>();
        AudioManager.sfxSource = audioManagerObject.AddComponent<AudioSource>();
        AudioManager.musicSource.loop = false;
        AudioManager.sfxSource.playOnAwake = false;
        AudioManager.defaultCoverArt = new Texture2D(1, 1);
        AudioManager.defaultCoverArt.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f));
        AudioManager.defaultCoverArt.Apply();
        string sfxPath = Path.Combine(pluginPath, "SFX");
        bool flag2 = Directory.Exists(sfxPath);
        if(flag2) {
            yield return AudioManager.LoadAudioClip(Path.Combine(sfxPath, "click.wav"), delegate (AudioClip clip) {
                AudioManager.clickSound = clip;
            });
            yield return AudioManager.LoadAudioClip(Path.Combine(sfxPath, "hover.wav"), delegate (AudioClip clip) {
                AudioManager.hoverSound = clip;
            });
            yield return AudioManager.LoadAudioClip(Path.Combine(sfxPath, "open.wav"), delegate (AudioClip clip) {
                AudioManager.openSound = clip;
            });
            yield return AudioManager.LoadAudioClip(Path.Combine(sfxPath, "close.wav"), delegate (AudioClip clip) {
                AudioManager.closeSound = clip;
            });
            yield return AudioManager.LoadAudioClip(Path.Combine(sfxPath, "dropdown.wav"), delegate (AudioClip clip) {
                AudioManager.dropdownSound = clip;
            });
        }
        string musicPath = Path.Combine(pluginPath, "Music");
        bool flag3 = Directory.Exists(musicPath);
        if(flag3) {
            string[] musicFiles = (from f in Directory.GetFiles(musicPath)
                                   where f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".ogg")
                                   select f).ToArray<string>();
            foreach(string musicFile in musicFiles) {
                yield return AudioManager.LoadAndAddTrack(musicFile, AudioManager.a_Log, null);
                musicFile = null;
            }
            string[] array = null;
            musicFiles = null;
        }
        AudioManager.isInitialized = true;
        yield break;
    }

    private static IEnumerator LoadAudioClip(string path, Action<AudioClip> callback) {
        using(UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.UNKNOWN)) {
            yield return www.SendWebRequest();
            bool flag = www.result == UnityWebRequest.Result.Success;
            if(flag) {
                callback(DownloadHandlerAudioClip.GetContent(www));
            } else {
                AudioManager.a_Log.LogError("Failed to load audio clip at " + path + ": " + www.error);
                callback(null);
            }
        }
        UnityWebRequest www = null;
        yield break;
        yield break;
    }

    private static IEnumerator LoadTexture(string path, Action<Texture2D> callback) {
        using(UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + path)) {
            yield return www.SendWebRequest();
            bool flag = www.result == UnityWebRequest.Result.Success;
            if(flag) {
                callback(DownloadHandlerTexture.GetContent(www));
            } else {
                callback(null);
            }
        }
        UnityWebRequest www = null;
        yield break;
        yield break;
    }

    public static IEnumerator LoadAndAddTrack(string musicFile, ManualLogSource logger, Action<bool> onComplete = null) {
        MusicTrack newTrack = new MusicTrack();
        bool success = false;
        yield return AudioManager.LoadAudioClip(musicFile, delegate (AudioClip clip) {
            bool flag3 = clip != null;
            if(flag3) {
                clip.name = Path.GetFileNameWithoutExtension(musicFile);
                newTrack.clip = clip;
            }
        });
        bool flag = newTrack.clip != null;
        if(flag) {
            AudioManager.<> c__DisplayClass31_1 CS$<> 8__locals2 = new AudioManager.<> c__DisplayClass31_1();
            CS$<> 8__locals2.baseName = Path.Combine(Path.GetDirectoryName(musicFile), Path.GetFileNameWithoutExtension(musicFile));
            string[] imageExtensions = new string[]
            {
                ".jpg",
                ".png",
                ".jpeg"
            };
            string imagePath = (from ext in imageExtensions
                                select CS$<> 8__locals2.baseName + ext).FirstOrDefault(new Func<string, bool>(File.Exists));
            bool flag2 = !string.IsNullOrEmpty(imagePath);
            if(flag2) {
                yield return AudioManager.LoadTexture(imagePath, delegate (Texture2D tex) {
                    newTrack.coverArt = tex;
                });
            }
            AudioManager.musicPlaylist.Add(newTrack);
            success = true;
            CS$<> 8__locals2 = null;
            imageExtensions = null;
            imagePath = null;
        }
        if(onComplete != null) {
            onComplete(success);
        }
        yield break;
    }

    private static void PlaySfx(AudioClip clip, float volume) {
        bool flag = !AudioManager.isInitialized || AudioManager.sfxSource == null || clip == null;
        if(!flag) {
            AudioManager.sfxSource.PlayOneShot(clip, volume);
        }
    }

    public static void PlayClick() {
        AudioManager.PlaySfx(AudioManager.clickSound, 0.7f);
    }

    public static void PlayHover() {
        AudioManager.PlaySfx(AudioManager.hoverSound, 0.5f);
    }

    public static void PlayOpen() {
        AudioManager.PlaySfx(AudioManager.openSound, 0.8f);
    }

    public static void PlayClose() {
        AudioManager.PlaySfx(AudioManager.closeSound, 0.8f);
    }

    public static void PlayDropdown() {
        AudioManager.PlaySfx(AudioManager.dropdownSound, 0.6f);
    }

    public static void PlayMusic() {
        bool flag = !AudioManager.isInitialized || AudioManager.musicPlaylist.Count == 0 || AudioManager.musicSource == null || AudioManager.playerState == AudioManager.PlayState.Playing;
        if(!flag) {
            bool flag2 = AudioManager.currentTrackIndex == -1 && AudioManager.musicPlaylist.Count > 0;
            if(flag2) {
                AudioManager.currentTrackIndex = 0;
            }
            MusicTrack currentTrack = AudioManager.GetCurrentTrack();
            bool flag3 = AudioManager.musicSource.clip != ((currentTrack != null) ? currentTrack.clip : null);
            if(flag3) {
                AudioManager.musicSource.clip = ((currentTrack != null) ? currentTrack.clip : null);
            }
            bool flag4 = AudioManager.musicSource.clip != null;
            if(flag4) {
                AudioManager.musicSource.Play();
                AudioManager.playerState = AudioManager.PlayState.Playing;
            }
        }
    }

    public static void PauseMusic() {
        bool flag = !AudioManager.isInitialized || AudioManager.musicSource == null;
        if(!flag) {
            bool flag2 = AudioManager.playerState == AudioManager.PlayState.Playing;
            if(flag2) {
                AudioManager.musicSource.Pause();
                AudioManager.playerState = AudioManager.PlayState.Paused;
            }
        }
    }

    public static void TogglePlayPause() {
        bool flag = !AudioManager.isInitialized;
        if(!flag) {
            bool flag2 = AudioManager.playerState == AudioManager.PlayState.Playing;
            if(flag2) {
                AudioManager.PauseMusic();
            } else {
                AudioManager.PlayMusic();
            }
        }
    }

    private static void PlayTrackAtIndex(int index) {
        bool flag = !AudioManager.isInitialized || AudioManager.musicPlaylist.Count == 0 || AudioManager.musicSource == null;
        if(!flag) {
            AudioManager.currentTrackIndex = index;
            bool flag2 = AudioManager.currentTrackIndex < 0;
            if(flag2) {
                AudioManager.currentTrackIndex = AudioManager.musicPlaylist.Count - 1;
            }
            bool flag3 = AudioManager.currentTrackIndex >= AudioManager.musicPlaylist.Count;
            if(flag3) {
                AudioManager.currentTrackIndex = 0;
            }
            MusicTrack currentTrack = AudioManager.GetCurrentTrack();
            bool flag4 = ((currentTrack != null) ? currentTrack.clip : null) != null;
            if(flag4) {
                AudioManager.musicSource.clip = currentTrack.clip;
                AudioManager.musicSource.Play();
                AudioManager.playerState = AudioManager.PlayState.Playing;
            }
        }
    }

    public static void NextTrack() {
        bool flag = !AudioManager.isInitialized || AudioManager.musicPlaylist.Count == 0;
        if(!flag) {
            AudioManager.PlayTrackAtIndex((AudioManager.currentTrackIndex + 1) % AudioManager.musicPlaylist.Count);
        }
    }

    public static void PrevTrack() {
        bool flag = !AudioManager.isInitialized || AudioManager.musicPlaylist.Count == 0;
        if(!flag) {
            int num = AudioManager.currentTrackIndex - 1;
            bool flag2 = num < 0;
            if(flag2) {
                num = AudioManager.musicPlaylist.Count - 1;
            }
            AudioManager.PlayTrackAtIndex(num);
        }
    }

    public static void Update() {
        bool flag = !AudioManager.isInitialized || AudioManager.musicSource == null || AudioManager.musicSource.clip == null || AudioManager.musicSource.loop;
        if(!flag) {
            bool flag2 = AudioManager.playerState == AudioManager.PlayState.Playing && !AudioManager.musicSource.isPlaying;
            if(flag2) {
                AudioManager.NextTrack();
            }
        }
    }

    private static ManualLogSource a_Log;

    private static AudioSource musicSource;

    private static AudioSource sfxSource;

    private static List<MusicTrack> musicPlaylist = new List<MusicTrack>();

    private static int currentTrackIndex = -1;

    private static AudioClip clickSound;

    private static AudioClip hoverSound;

    private static AudioClip openSound;

    private static AudioClip closeSound;

    private static AudioClip dropdownSound;

    private static bool isInitialized = false;

    private static Texture2D defaultCoverArt;

    private static AudioManager.PlayState playerState = AudioManager.PlayState.Stopped;

    private enum PlayState {
        Stopped,
        Playing,
        Paused
    }
}
