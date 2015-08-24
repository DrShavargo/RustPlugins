/*
* Version 1.0
*/

using System;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins {
  [Info("Playtime and AFK Tracker", "ArcaneCraeda", 1.0)]
  [Description("Logs every players' play time (minus time spent AFK).")]
  public class PlayTimeTracker : RustPlugin {

    protected override void LoadDefaultConfig() {
      PrintWarning("Creating a configuration file for PlayTimeTracker.");
      Config.Clear();
      Config["Afk Check Interval"] = 30;
      Config["Cycles Until Afk"] = 4;
      Config["Count Afk As TimePlayed?"] = false;
      SaveConfig();
    }

    class PlayTimeData {
      public Dictionary<string, PlayTimeInfo> Players = new Dictionary<string, PlayTimeInfo>();

      public PlayTimeData() {  }
    };

    class PlayTimeInfo {
      public string SteamID;
      public string Name;
      public long PlayTime;
      public long InitTimeStamp;
      public int AfkCount;
      public int AfkTime;
      public double XPosition;
      public double YPosition;
      public double ZPosition;
      public bool PauseAfk;

      public PlayTimeInfo() {  }

      public PlayTimeInfo(BasePlayer player) {
        InitTimeStamp = 0;
        SteamID = player.userID.ToString();
        Name = player.displayName;
        PlayTime = 0;
        AfkCount = 0;
        AfkTime = 0;
        XPosition = 0;
        YPosition = 0;
        ZPosition = 0;
        PauseAfk = false;
      }
    };

    PlayTimeData playTimeData;

    int afkCheckInterval { get { return Config.Get<int>("Afk Check Interval"); } }
    int cyclesUntilAfk { get { return Config.Get<int>("Cycles Until Afk"); } }
    bool afkCounts { get { return Config.Get<bool>("Count Afk As TimePlayed?"); } }

    void Init() {
      Puts("PlayTimeTracker Initializing...");
    }

    void OnServerInitialized() {
      playTimeData = Interface.GetMod().DataFileSystem.ReadObject<PlayTimeData>("PlayTimeTracker");
      timer.Repeat(afkCheckInterval, 0, () => afkCheck());
    }

    void OnPlayerSleepEnded(BasePlayer player) {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);

      if (!playTimeData.Players.ContainsKey(info.SteamID)) {
        playTimeData.Players.Add(info.SteamID, info);
        
      }
      playTimeData.Players[info.SteamID].InitTimeStamp = currentTimestamp;
      playTimeData.Players[info.SteamID].Name = player.displayName;
      playTimeData.Players[info.SteamID].XPosition = Math.Round(player.transform.position.x, 2);
      playTimeData.Players[info.SteamID].YPosition = Math.Round(player.transform.position.y, 2);
      playTimeData.Players[info.SteamID].ZPosition = Math.Round(player.transform.position.z, 2);
      playTimeData.Players[info.SteamID].AfkTime = 0;
      playTimeData.Players[info.SteamID].AfkCount = 0;
      playTimeData.Players[info.SteamID].PauseAfk = false;

      Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
    }

    void OnPlayerDisconnected(BasePlayer player) {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);

      if (playTimeData.Players.ContainsKey(info.SteamID)) {
        long initTimeStamp = playTimeData.Players[info.SteamID].InitTimeStamp;
        int afkTime = playTimeData.Players[info.SteamID].AfkTime;
        long totalPlayed = (currentTimestamp - initTimeStamp) - afkTime;

        playTimeData.Players[info.SteamID].PlayTime += totalPlayed;
        Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
      }
    }

    /* 
    * Some actions could be performed while in one position for a long time.
    * These 5 callbacks keep the player from being labelled as AFK.
    */
    void CanUpdateSign(Signage sign, BasePlayer player) {
      toggleAfkPause(player, true);
    }

    void OnSignLocked(Signage sign, BasePlayer player) {
      toggleAfkPause(player, false);
    }

    void OnSignUpdated(Signage sign, BasePlayer player) {
      toggleAfkPause(player, false);
    }

    void OnWeaponFired(BasePlayer player) {
      resetAfkCount(player);
    }

    void OnPlayerLoot(BasePlayer player) {
      resetAfkCount(player);
    }

    private void toggleAfkPause(BasePlayer player, bool pause) {
      var info = new PlayTimeInfo(player);

      if (playTimeData.Players.ContainsKey(info.SteamID))  {
        playTimeData.Players[info.SteamID].PauseAfk = pause;
      }
    }

    private void resetAfkCount(BasePlayer player) {
      var info = new PlayTimeInfo(player);

      if (playTimeData.Players.ContainsKey(info.SteamID))  {
        playTimeData.Players[info.SteamID].AfkCount = 0;
      }
    }

    private void afkCheck() {
      if (!afkCounts) {
        foreach (BasePlayer player in BasePlayer.activePlayerList) {
          var info = new PlayTimeInfo(player);

          if (playTimeData.Players.ContainsKey(info.SteamID)) {
            double currentX = Math.Round(player.transform.position.x, 2);
            double currentY = Math.Round(player.transform.position.y, 2);
            double currentZ = Math.Round(player.transform.position.z, 2);

            double storedX = playTimeData.Players[info.SteamID].XPosition;
            double storedY = playTimeData.Players[info.SteamID].YPosition;
            double storedZ = playTimeData.Players[info.SteamID].ZPosition;

            if (currentX == storedX && currentY == storedY && currentZ == storedZ) {
              playTimeData.Players[info.SteamID].AfkCount += 1;
            } else {
              playTimeData.Players[info.SteamID].AfkCount = 0;
              playTimeData.Players[info.SteamID].XPosition = currentX;
              playTimeData.Players[info.SteamID].YPosition = currentY;
              playTimeData.Players[info.SteamID].ZPosition = currentZ;
            }

            if (playTimeData.Players[info.SteamID].AfkCount >= cyclesUntilAfk && !playTimeData.Players[info.SteamID].PauseAfk) {
              playTimeData.Players[info.SteamID].AfkTime += 30;
            }

            Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
          }
        }
      }
    }

    private static long GrabCurrentTimestamp() {
      long timestamp = 0;
      long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;
      ticks /= 10000000;
      timestamp = ticks;

      return timestamp;
    }
  };
};
