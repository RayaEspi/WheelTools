using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace WheelTools;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;


    public class PartyMember
    {
        public bool   Enabled  { get; set; } = true;
        public string Name     { get; set; } = string.Empty;
        public string Spins { get; set; } = "0";
        public string Preset { get; set; } = string.Empty;
        public string SpinSpeed { get; set; } = "8";
        public string GameId { get; set; } = "<not created>";
        public DateTime Added { get; set; } = DateTime.UtcNow;
    }
    
    public string DefaultPreset { get; set; } = string.Empty;
    public string DefaultSpinAmount { get; set; } = "0";
    public string DefaultSpinSpeed { get; set; } = "8";
    public bool TestGame { get; set; } = false;
    
    
    public bool TimedSpins { get; set; } = false;
    public int AmountEarned { get; set; } = 1;
    public int EveryXMinutes { get; set; } = 10;
    public int MaxSpins { get; set; } = 10;
    

    public List<PartyMember> PartyMembers { get; set; } = new List<PartyMember>();
    
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
