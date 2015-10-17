/*
* Version 1.1.0
*/

using System;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins {
  [Info("Playtime and AFK Tracker", "ArcaneCraeda", 1.0)]
  [Description("Logs every players' play and afk time, separately.")]
  public class PlayTimeTracker : RustPlugin {

    protected override void LoadDefaultConfig() {
      PrintWarning("Creating a configuration file for PlayTimeTracker.");
      Config.Clear();
      Config["Afk Check Interval"] = 30;
      Config["Cycles Until Afk"] = 4;
      Config["Track AFK Time?"] = true;
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
      public long AfkTime;
      public string HumanPlayTime;
      public string HumanAfkTime;

      public PlayTimeInfo() {  }

      public PlayTimeInfo(BasePlayer player) {
        SteamID = player.userID.ToString();
        Name = player.displayName;
        PlayTime = 0;
        AfkTime = 0;
        HumanPlayTime = "00:00:00";
        HumanAfkTime = "00:00:00";
      }
    };

    class PlayerStateData {
      public Dictionary<string, PlayerStateInfo> Players = new Dictionary<string, PlayerStateInfo>();

      public PlayerStateData() {  }
    };

    class PlayerStateInfo {
      public string SteamID;
      public long InitTimeStamp;
      public int AfkCount;
      public int AfkTime;
      public double[] Position;

      public PlayerStateInfo() {  }

      public PlayerStateInfo(BasePlayer player) {
        InitTimeStamp = 0;
        SteamID = player.userID.ToString();
        AfkCount = 0;
        AfkTime = 0;
        Position = new double[3];
      }
    };

    PlayTimeData playTimeData;
    PlayerStateData playerStateData = new PlayerStateData();

    int afkCheckInterval { get { return Config.Get<int>("Afk Check Interval"); } }
    int cyclesUntilAfk { get { return Config.Get<int>("Cycles Until Afk"); } }
    bool afkCounts { get { return Config.Get<bool>("Track AFK Time?"); } }

    void Init() {
      Puts("PlayTimeTracker Initializing...");
    }

    void OnServerInitialized() {
      playTimeData = Interface.GetMod().DataFileSystem.ReadObject<PlayTimeData>("PlayTimeTracker");
      if (afkCounts) {
        timer.Repeat(afkCheckInterval, 0, () => afkCheck());
      }
    }

    void OnPlayerSleepEnded(BasePlayer player) {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);
      var state = new PlayerStateInfo(player);

      if (!playerStateData.Players.ContainsKey(state.SteamID)) {
        playerStateData.Players.Add(state.SteamID, state);
      }
      if (!playTimeData.Players.ContainsKey(info.SteamID)) {
        playTimeData.Players.Add(info.SteamID, info);
      }
      playTimeData.Players[info.SteamID].Name = player.displayName;

      playerStateData.Players[state.SteamID].InitTimeStamp = currentTimestamp;
      playerStateData.Players[state.SteamID].AfkTime = 0;
      playerStateData.Players[state.SteamID].AfkCount = 0;

      playerStateData.Players[state.SteamID].Position[0] = Math.Round(player.transform.position.x, 2);
      playerStateData.Players[state.SteamID].Position[1] = Math.Round(player.transform.position.y, 2);
      playerStateData.Players[state.SteamID].Position[2] = Math.Round(player.transform.position.z, 2);
      
      Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
    }

    void OnPlayerDisconnected(BasePlayer player) {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);
      var state = new PlayerStateInfo(player);

      if (playTimeData.Players.ContainsKey(info.SteamID)) {
        long initTimeStamp = playerStateData.Players[state.SteamID].InitTimeStamp;
        int afkTime = playerStateData.Players[state.SteamID].AfkTime;
        long totalPlayed = currentTimestamp - initTimeStamp;

        playTimeData.Players[info.SteamID].AfkTime += afkTime;
        TimeSpan humanAfkTime = TimeSpan.FromSeconds(playTimeData.Players[info.SteamID].AfkTime);
        playTimeData.Players[info.SteamID].HumanAfkTime = string.Format("{0:c}", humanAfkTime);

        playTimeData.Players[info.SteamID].PlayTime += totalPlayed;
        TimeSpan humanPlayTime = TimeSpan.FromSeconds(playTimeData.Players[info.SteamID].PlayTime);
        playTimeData.Players[info.SteamID].HumanPlayTime = string.Format("{0:c}", humanPlayTime);

        Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
      }
    }

    // Master AFK checking function, iterates through all connected players.
    private void afkCheck() {
      foreach (BasePlayer player in BasePlayer.activePlayerList) {
        var state = new PlayerStateInfo(player);

        if (playerStateData.Players.ContainsKey(state.SteamID)) {
          double currentX = Math.Round(player.transform.position.x, 2);
          double currentY = Math.Round(player.transform.position.y, 2);
          double currentZ = Math.Round(player.transform.position.z, 2);

          double[] storedPos = playerStateData.Players[state.SteamID].Position;

          if (currentX == storedPos[0] && currentY == storedPos[1] && currentZ == storedPos[2]) {
            playerStateData.Players[state.SteamID].AfkCount += 1;
          } else {
            playerStateData.Players[state.SteamID].AfkCount = 0;
            playerStateData.Players[state.SteamID].Position[0] = currentX;
            playerStateData.Players[state.SteamID].Position[1] = currentY;
            playerStateData.Players[state.SteamID].Position[2] = currentZ;
          }

          if (playerStateData.Players[state.SteamID].AfkCount > cyclesUntilAfk) {
            playerStateData.Players[state.SteamID].AfkTime += 30;
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
