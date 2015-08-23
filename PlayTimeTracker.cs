/******************************************************************************
* Version 1.0
******************************************************************************/

using System;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
  [Info("Playtime and Action Tracker", "ArcaneCraeda", 1.0)]
  [Description("Logs players play time and action count, based on Waizujin's PlayTime plugin.")]
  public class PlayTimeTracker : RustPlugin
  {
    class PlayTimeData
    {
      public Dictionary<string, PlayTimeInfo> Players = new Dictionary<string, PlayTimeInfo>();

      public PlayTimeData() {  }
    };

    class PlayTimeInfo
    {
      public string SteamID;
      public string Name;
      public long PlayTime;
      public long ActionCount;
      public long initTimestamp;
      public int AfkCount;
      public int AfkTime;
      public double XPosition;
      public double YPosition;
      public double ZPosition;

      public PlayTimeInfo() {  }

      public PlayTimeInfo(BasePlayer player)
      {
        initTimestamp = 0;
        SteamID = player.userID.ToString();
        Name = player.displayName;
        PlayTime = 0;
        AfkCount = 0;
        AfkTime = 0;
        XPosition = 0;
        YPosition = 0;
        ZPosition = 0;
      }
    };

    PlayTimeData playTimeData;

    int afkCheckInterval = 30;
    int cyclesUntilAfk = 4;

    void Init()
    {
      Puts("PlayTimeTracker Initializing...");
    }

    void OnServerInitialized()
    {
      playTimeData = Interface.GetMod().DataFileSystem.ReadObject<PlayTimeData>("PlayTimeTracker");
      timer.Repeat(afkCheckInterval, 0, () => afkCheck());
    }

    void OnPlayerSleepEnded(BasePlayer player)
    {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);

      if (!playTimeData.Players.ContainsKey(info.SteamID))
      {
        playTimeData.Players.Add(info.SteamID, info);
        
      }
      playTimeData.Players[info.SteamID].initTimestamp = currentTimestamp;
      playTimeData.Players[info.SteamID].Name = player.displayName;
      playTimeData.Players[info.SteamID].XPosition = Math.Round(player.transform.position.x, 2);
      playTimeData.Players[info.SteamID].YPosition = Math.Round(player.transform.position.y, 2);
      playTimeData.Players[info.SteamID].ZPosition = Math.Round(player.transform.position.z, 2);
      playTimeData.Players[info.SteamID].AfkTime = 0;
      playTimeData.Players[info.SteamID].AfkCount = 0;

      Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
    }

    void OnPlayerDisconnected(BasePlayer player)
    {
      long currentTimestamp = GrabCurrentTimestamp();
      var info = new PlayTimeInfo(player);

      if (playTimeData.Players.ContainsKey(info.SteamID)) {
        long initTimestamp = playTimeData.Players[info.SteamID].initTimestamp;
        int afkTime = playTimeData.Players[info.SteamID].AfkTime;
        long totalPlayed = (currentTimestamp - initTimestamp) - afkTime;

        playTimeData.Players[info.SteamID].PlayTime += totalPlayed;
        Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
      }
    }

    private void afkCheck()
    {
      foreach (BasePlayer player in BasePlayer.activePlayerList) 
      {
        var info = new PlayTimeInfo(player);

        if (playTimeData.Players.ContainsKey(info.SteamID)) 
        {
          double currentX = Math.Round(player.transform.position.x, 2);
          double currentY = Math.Round(player.transform.position.y, 2);
          double currentZ = Math.Round(player.transform.position.z, 2);

          double storedX = playTimeData.Players[info.SteamID].XPosition;
          double storedY = playTimeData.Players[info.SteamID].YPosition;
          double storedZ = playTimeData.Players[info.SteamID].ZPosition;

          if (currentX == storedX && currentY == storedY && currentZ == storedZ)
          {
            playTimeData.Players[info.SteamID].AfkCount += 1;
          }else
          {
            playTimeData.Players[info.SteamID].AfkCount = 0;
            playTimeData.Players[info.SteamID].XPosition = currentX;
            playTimeData.Players[info.SteamID].YPosition = currentY;
            playTimeData.Players[info.SteamID].ZPosition = currentZ;
          }

          if (playTimeData.Players[info.SteamID].AfkCount >= cyclesUntilAfk) 
          {
            playTimeData.Players[info.SteamID].AfkTime += 30;
          }

          Interface.GetMod().DataFileSystem.WriteObject("PlayTimeTracker", playTimeData);
        }
      }
    }

    private static long GrabCurrentTimestamp()
    {
      long timestamp = 0;
      long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;
      ticks /= 10000000;
      timestamp = ticks;

      return timestamp;
    }
  };
};
