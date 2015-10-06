/*
* Version 1.0.0
*/

using System;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins {
  [Info("Rewards for Time PLayed on Server", "ArcaneCraeda", 1.0.0)]
  [Description("A reward system for time played on the server.")]
  public class RewardForTimePlayed : RustPlugin {

    protected override void LoadDefaultConfig() {
      PrintWarning("Creating a configuration file for RewardForTimePlayed.");
      Config.Clear();
      Config["Reward/PlayTime/AFK Check Interval"] = 30;
      Config["Cycles Until Afk"] = 4;
      Config["AFK Counts As Time PLayed?"] = false;
      Config["Max Level"] = 10;
      Config["Max Prestige"] = -1;
      Config["Time Needed For First Level Up"] = 20;
      Config["Leveling Scale"] = "linear";
      Config["Exponential Leveling Factor"] = 2;
      Config["Prestige Multiplier"] = 1.5;
      SaveConfig();
    }

    class PlayerRewardData {
      public Dictionary<string, PlayerRewardInfo> Players = new Dictionary<string, PlayerRewardInfo>();

      public PlayerRewardData() {  }
    };

    class PlayerRewardInfo {
      public string SteamID;
      public string Name;
      public int PlayTime;

      public PlayerRewardInfo() {  }

      public PlayerRewardInfo(BasePlayer player) {
        SteamID = player.userID.ToString();
        Name = player.displayName;
        PlayTime = 0;
      }
    };

    class PlayerStateData {
      public Dictionary<string, PlayerStateInfo> Players = new Dictionary<string, PlayerStateInfo>();

      public PlayerStateData() {  }
    };

    class PlayerStateInfo {
      public string SteamID;
      public int AfkCount;
      public int PlayTime;
      public double[] Position;

      public PlayerStateInfo() {  }

      public PlayerStateInfo(BasePlayer player) {
        SteamID = player.userID.ToString();
        AfkCount = 0;
        PlayTime = 0;
        Position = new double[3];
      }
    };

    PlayerRewardData playerRewardData;
    PlayerStateData playerStateData = new PlayerStateData();
    int[] levelsTimePlayed;
    int sumOfTimes;

    int allCheckInterval { get { return Config.Get<int>("Reward/PlayTime/AFK Check Interval"); } }
    int cyclesUntilAfk { get { return Config.Get<int>("Cycles Until Afk"); } }
    bool afkCounts { get { return Config.Get<bool>("AFK Counts As Time PLayed?"); } }

    int maxLevel { get { return Config.Get<int>("Max Level"); } }
    int maxPrestige { get { return Config.Get<int>("Max Prestige"); } }
    int timeToLvlUp { get { return Config.Get<int>("Time Needed For First Level Up"); } }
    string lvlScale { get { return Config.Get<string>("Leveling Scale"); } }
    float exponentLvlFactor { get { return Config.Get<string>("Exponential Leveling Factor"); } }
    float prestigeMult { get { return Config.Get<string>("Prestige Multiplier"); } }

    void Init() {
      Puts("RewardForTimePlayed Initializing...");
    }

    void OnServerInitialized() {
      playerRewardData = Interface.GetMod().DataFileSystem.ReadObject<PlayerRewardData>("RewardForTimePlayed");
      timer.Repeat(allCheckInterval, 0, () => timeCheck());
      timeToLvlUp = timeToLvlUp * 60;
      levelsTimePlayed = new int[maxLevel];
      switch (lvlScale) {
        case "stable":
          calcStableTimeArray();
          break;
        case "linear":
          calcLinearTimeArray();
          break;
        case "exponential":
          calcExpoTimeArray();
          break;
      }
      sumOfTimes = levelsTimePlayed.Sum();
    }

    void OnPlayerSleepEnded(BasePlayer player) {
      var info = new PlayerRewardInfo(player);
      var state = new PlayerStateInfo(player);

      if (!playerStateData.Players.ContainsKey(state.SteamID)) {
        playerStateData.Players.Add(state.SteamID, state);
      }
      if (!playerRewardData.Players.ContainsKey(info.SteamID)) {
        playerRewardData.Players.Add(info.SteamID, info);
      }
      playerRewardData.Players[info.SteamID].Name = player.displayName;

      playerStateData.Players[state.SteamID].PlayTime = playerRewardData.Players[info.SteamID].PlayTime;
      playerStateData.Players[state.SteamID].AfkCount = 0;

      playerStateData.Players[state.SteamID].Position[0] = Math.Round(player.transform.position.x, 2);
      playerStateData.Players[state.SteamID].Position[1] = Math.Round(player.transform.position.y, 2);
      playerStateData.Players[state.SteamID].Position[2] = Math.Round(player.transform.position.z, 2);
      
      Interface.GetMod().DataFileSystem.WriteObject("RewardForTimePlayed", playerRewardData);
    }

    void OnPlayerDisconnected(BasePlayer player) {
      var info = new PlayerRewardInfo(player);
      var state = new PlayerStateInfo(player);

      if (playerRewardData.Players.ContainsKey(info.SteamID)) {
        int playTime = playerStateData.Players[state.SteamID].PlayTime;
        playerRewardData.Players[info.SteamID].PlayTime += playTime;
        Interface.GetMod().DataFileSystem.WriteObject("RewardForTimePlayed", playerRewardData);
      }
    }

    // Master AFK/PlayTime/RewardTime checking function, iterates through all connected players.
    private void timeCheck() {
      foreach (BasePlayer player in BasePlayer.activePlayerList) {
        var state = new PlayerStateInfo(player);

        if (playerStateData.Players.ContainsKey(state.SteamID)) {
          if (!afkCheck(state, player)) {
            int playtime = playerStateData.Players[state.SteamID].PlayTime;
            
            currentPrestige = playtime / sumOfTimes;
            currentLevelTime = playtime % sumOfTimes;
            currentLevel = 0;
            foreach (int lvlTime in levelsTimePlayed){
              if (currentLevelTime > lvlTime){ currentLevel++; }
            }
            
            playtime += 30;
            newPrestige = playtime / sumOfTimes;
            newLevelTime = playtime % sumOfTimes;
            newLevel = 0;
            foreach (int lvlTime in levelsTimePlayed){
              if (newLevelTime > lvlTime){ newLevel++; }
            }

            if (newPrestige > currentPrestige || newLevel > currentLevel){
              grantReward(player, newPrestige, newLevel);
            }

            playerStateData.Players[state.SteamID].PlayTime = playtime;
          }
        }
      }
    }

    private bool afkCheck(PlayerStateInfo state, BasePlayer player){
      if (afkCounts){
        return false; 
      }
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
        return true;
      }
      return false;
    }

    private void calcLinearTimeArray(){
      levelsTimePlayed[0] = 0;
      for (int i = 1; i < levelsTimePlayed.lenght(); i++) {
        levelsTimePlayed[i] = levelsTimePlayed[i-1] + timeToLvlUp * i;
      }
    }

    private void calcStableTimeArray(){
      for (int i = 0; i < levelsTimePlayed.lenght(); i++) {
        levelsTimePlayed[i] = timeToLvlUp * i;
      }
    }

    private void calcExpoTimeArray(){
      levelsTimePlayed[0] = 0;
      for (int i = 1; i < levelsTimePlayed.lenght(); i++) {
        levelsTimePlayed[i] = levelsTimePlayed[i-1] * exponentLvlFactor + timeToLvlUp;
      }
    }

    private void grantReward(BasePlayer player, int prestige, int level){
      Puts("Prestige: " + prestige + ", Level: " + level);
    }
  };
};
