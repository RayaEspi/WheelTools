using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Party;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using Serilog;
using Action = System.Action;

namespace WheelTools.Windows;

public class MainWindow : Window, IDisposable
{
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    
    private sealed record PartyEntry(string Name);
    
    private readonly List<PartyEntry> partySnapshot = new();
    private string lastUpdated = "(never)";
    
    private readonly Configuration configuration;
    private Timer? timedSpinsTimer;

    public MainWindow(Plugin plugin)
        : base("WheelTools##Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        configuration = plugin.Configuration;
        ClientState = Plugin.ClientState;
        StartTimedSpinsTimer();
    }

    public void Dispose()
    {
        timedSpinsTimer.Dispose();
    }

    public override void Draw()
    {
        using var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true);
        if (child.Success)
        {
            ImGui.TextDisabled("Generate spins for party members.");

            if (ImGui.Button("Refresh party members"))
            {
                RefreshPartySnapshot();
            }

            ImGui.Separator();
            
            ImGui.TextDisabled("Default values.");
            
            var defaultPreset = configuration.DefaultPreset;
            ImGui.PushItemWidth(200f);
            if (ImGui.InputText($"##defaultPreset", ref defaultPreset, 512))
                UpdateConfig(() => configuration.DefaultPreset = defaultPreset);
            ImGui.SameLine();
            ImGui.TextDisabled("Preset");
            
            ImGui.SameLine();
            
            var defaultSpins = configuration.DefaultSpinAmount;
            ImGui.PushItemWidth(20f);
            if (ImGui.InputText($"##defaultSpins", ref defaultSpins, 512))
                UpdateConfig(() => configuration.DefaultSpinAmount = defaultSpins);
            ImGui.SameLine();
            ImGui.TextDisabled("Spins");
            
            ImGui.SameLine();
            
            var defaultSpinSpeed = configuration.DefaultSpinSpeed;
            ImGui.PushItemWidth(20f);
            if (ImGui.InputText($"##defaultSpinsSpeed", ref defaultSpinSpeed, 512))
                UpdateConfig(() => configuration.DefaultSpinAmount = defaultSpinSpeed);
            ImGui.SameLine();
            ImGui.TextDisabled("Spins speed");
            
            ImGui.SameLine();
            
            var testGame = configuration.TestGame;
            if (ImGui.Checkbox($"##testGame", ref testGame))
                UpdateConfig(() => configuration.TestGame = testGame);
            ImGui.SameLine();
            ImGui.TextDisabled("Test mode");
            
            ImGui.SameLine();

            if (ImGui.Button("Apply"))
            {
                foreach (var member in configuration.PartyMembers)
                {
                    member.Preset = defaultPreset;
                    member.Spins = defaultSpins;
                }
                UpdateConfig(() => { });
            }
            
            ImGui.Separator();
            
            if (partySnapshot.Count > 0)
            {

                if (ImGui.BeginTable("party_snapshot_table", 10, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
                {
                    ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 28f);
                    ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 28f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 220f);
                    ImGui.TableSetupColumn("Spins amount", ImGuiTableColumnFlags.WidthFixed, 28f);
                    ImGui.TableSetupColumn("Spins speed", ImGuiTableColumnFlags.WidthFixed, 28f);
                    ImGui.TableSetupColumn("Preset", ImGuiTableColumnFlags.WidthStretch, 220f);
                    ImGui.TableSetupColumn("Game Link", ImGuiTableColumnFlags.WidthStretch, 110f);
                    ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthStretch, 110f);
                    ImGui.TableSetupColumn("Copy", ImGuiTableColumnFlags.WidthFixed, 28f);
                    ImGui.TableSetupColumn("Send", ImGuiTableColumnFlags.WidthFixed, 38f);
                    ImGui.TableHeadersRow();

                    for (int i = 0; i < configuration.PartyMembers.Count; i++)
                    {
                        
                        //Log.Information($"{i} - {partySnapshot[i].Name} -{configuration.PartyMembers[i].Name} - {configuration.PartyMembers.Count}");
                        if (i >= configuration.PartyMembers.Count)
                        {
                            Plugin.Log.Error($"Party member index {i} exceeds configuration count {configuration.PartyMembers.Count}. Skipping.");
                            continue;
                        }
                        var partyMember = configuration.PartyMembers[i];
                        var entry = partySnapshot[i];

                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        ImGui.TextUnformatted((i + 1).ToString());

                        ImGui.TableSetColumnIndex(1);
                        var playerEnabled = partyMember.Enabled;
                        if (ImGui.Checkbox($"##partymember_enabled_{i}", ref playerEnabled))
                            UpdateConfig(() => configuration.PartyMembers[i].Enabled = playerEnabled);
                            
                        ImGui.TableSetColumnIndex(2);
                        var playerName = partyMember.Name;
                        if (ImGui.InputText($"##partymember_name_{i}", ref playerName, 512))
                            UpdateConfig(() => configuration.PartyMembers[i].Name = playerName);
                            
                        ImGui.TableSetColumnIndex(3);
                        var playerSpins = partyMember.Spins;
                        if (ImGui.InputText($"##partymember_spins_{i}", ref playerSpins, 512))
                            UpdateConfig(() => configuration.PartyMembers[i].Spins = playerSpins);
                            
                        ImGui.TableSetColumnIndex(4);
                        var spinSpeed = partyMember.SpinSpeed;
                        if (ImGui.InputText($"##partymember_spinsSpeed_{i}", ref spinSpeed, 512))
                            UpdateConfig(() => configuration.PartyMembers[i].Spins = spinSpeed);
                            
                        ImGui.TableSetColumnIndex(5);
                        var playerPreset = partyMember.Preset;
                        if (ImGui.InputText($"##partymember_preset_{i}", ref playerPreset, 512))
                            UpdateConfig(() => configuration.PartyMembers[i].Preset = playerPreset);
                        
                        ImGui.TableSetColumnIndex(6);
                        ImGui.TextUnformatted(partyMember.GameId ?? "<not created>");
                        
                        ImGui.TableSetColumnIndex(7);
                        // Difference in time between now and when the memebr was added
                        var timeSinceAdded = DateTime.UtcNow - partyMember.Added;
                        ImGui.TextUnformatted(timeSinceAdded.ToString(@"hh\:mm\:ss"));
                        
                        ImGui.TableSetColumnIndex(8);
                        if (ImGui.Button($"Copy##partymember_copy_{i}"))
                        {
                            ImGui.SetClipboardText(partyMember.GameId ?? "<not created>");
                            Plugin.Log.Information($"Copied game ID for {configuration.PartyMembers[i].Name} to clipboard.");
                        }
                        
                        ImGui.TableSetColumnIndex(9);
                        // Send the ID to the party member upon press in tells
                        if (ImGui.Button($"Send##partymember_send_{i}"))
                        {
                            if (string.IsNullOrWhiteSpace(partyMember.GameId) || partyMember.GameId == "<not created>")
                            {
                                Plugin.Log.Error($"Game ID for {configuration.PartyMembers[i].Name} is not created yet.");
                            }
                            else
                            {
                                Plugin.Log.Information($"Sending game ID {partyMember.GameId} to {configuration.PartyMembers[i].Name}");
                                var target = Plugin.PartyList.FirstOrDefault(p => p.Name.ToString().Equals(configuration.PartyMembers[i].Name, StringComparison.OrdinalIgnoreCase));
                                SendTellToPartyMember(target, $"https://wheel.gamba.pro/wheel/{partyMember.GameId}");
                            }
                        }
                        
                    }

                    ImGui.EndTable();
                }
                    
                if (ImGui.Button("Create games for everyone"))
                {
                    Plugin.Log.Information($"Creating games for party members");
                    for (int i = 0; i < partySnapshot.Count; i++)
                    {
                        if (configuration.PartyMembers[i].Enabled)
                        {
                            GenerateSpinsForPartyMember(i);
                        }
                    }
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Send game links to party"))
                {
                    Plugin.Log.Information($"Sending game links to party members");
                    _ = SendGameLinksWithDelayAsync();
                }
            }
            
            // Accordion element for further options
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Timed spins"))
            {
                ImGui.TextDisabled("Give players spins based on time in the party");
                
                ImGui.PushItemWidth(100f);
                var timedSpins = configuration.TimedSpins;
                if (ImGui.Checkbox($"##timedSpins", ref timedSpins))
                    UpdateConfig(() => configuration.TimedSpins = timedSpins);
                ImGui.SameLine();
                ImGui.TextDisabled("Enabled");
                ImGui.SameLine();
                var amountEarned = configuration.AmountEarned.ToString();
                if (ImGui.InputText($"##amountEarned", ref amountEarned, 512))
                    UpdateConfig(() => configuration.AmountEarned = int.TryParse(amountEarned, out var val) ? val : 0);
                ImGui.SameLine();
                ImGui.TextDisabled("Amount earned");
                ImGui.SameLine();
                var everyXMinutes = configuration.EveryXMinutes.ToString();
                if (ImGui.InputText($"##everyXMinutes", ref everyXMinutes, 512))
                    UpdateConfig(() => configuration.EveryXMinutes = int.TryParse(everyXMinutes, out var val) ? val : 0);
                ImGui.SameLine();
                ImGui.TextDisabled("Every X minutes");
                ImGui.SameLine();
                var maxSpins = configuration.MaxSpins.ToString();
                if (ImGui.InputText($"##maxSpins", ref maxSpins, 512))
                    // If input is more than 10, set it to 10
                    // If input is less than 0, set it to 0
                    UpdateConfig(() => configuration.MaxSpins = int.TryParse(maxSpins, out var val) ? Math.Clamp(val, 0, 10) : 0);
                ImGui.SameLine();
                ImGui.TextDisabled("Max spins per player");
            }
        }
    }
    
    // Every 1 second, set players spin amount to amtch timed spins if enabled
    private void StartTimedSpinsTimer()
    {
        timedSpinsTimer?.Dispose();
        timedSpinsTimer = new Timer(_ =>
        {
            if (configuration.TimedSpins)
            {
                foreach (var t in configuration.PartyMembers)
                {
                    var member = t;
                    var timeSinceAdded = DateTime.UtcNow - member.Added;
                    if (timeSinceAdded.TotalMinutes >= configuration.EveryXMinutes)
                    {
                        var spinsToAdd = configuration.AmountEarned;
                        
                        // Total spins player should have after adding calcualted from time
                        var totalSpins = (int)(timeSinceAdded.TotalMinutes / configuration.EveryXMinutes) * spinsToAdd;
                        
                        var newSpins = Math.Min(totalSpins, configuration.MaxSpins);
                        UpdateConfig(() => t.Spins = totalSpins.ToString());
                    }
                }
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
    public async Task SendGameLinksWithDelayAsync()
    {
        Log.Information( "Sending game links to party members with delay...");
        foreach (var member in configuration.PartyMembers)
        {
            Log.Information($"Sending game links to party member {member.GameId}");
            if (member.Enabled && !string.IsNullOrWhiteSpace(member.GameId) && member.GameId != "<not created>")
            {
                var target = Plugin.PartyList.FirstOrDefault(p => p.Name.ToString().Equals(member.Name, StringComparison.OrdinalIgnoreCase));
                Log.Information($"Sending game links to party member {member.GameId}");
                SendTellToPartyMember(target, $"https://wheel.gamba.pro/wheel/{member.GameId}");
                await Task.Delay(1000);
            }
        }
    }
    
    public static void SendTellToPartyMember(IPartyMember member, string message)
    {
        var worldRowId = member.World.RowId;
        var worldName = Plugin.DataManager.GetExcelSheet<World>().GetRow(worldRowId).Name.ToString() ?? string.Empty;

        var recipient = string.IsNullOrEmpty(worldName)
                            ? member.Name.ToString()
                            : $"{member.Name}@{worldName}";
        
        Log.Information($"Sending message to {recipient} @ {worldName}: {message}");

        try
        {
            Plugin.CommandManager.ProcessCommand($"/tell {recipient} {message}");
            Chat.SendMessage($"/tell {recipient} {message}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to send tell to {recipient}: {ex}");
        }
    }
    
    private void GenerateSpinsForPartyMember(int index)
    {
        if (index < 0 || index >= partySnapshot.Count)
        {
            Plugin.Log.Error($"Invalid party member index: {index}");
            return;
        }

        var member = configuration.PartyMembers[index];
        if (!member.Enabled)
        {
            Plugin.Log.Information($"Party member {member.Name} is disabled.");
            return;
        }

        var spins = int.TryParse(member.Spins, out var spinCount) ? spinCount : 0;
        var preset = member.Preset;

        Plugin.Log.Information($"Generating {spins} spins for {member.Name} with preset '{preset}'");

        int spinsNum = Convert.ToInt32(member.Spins);
        int spinsSpeedNum = Convert.ToInt32(member.SpinSpeed);
        var code = SimpleWheelIpc.CreateGame(
            member.Name,
            spinsNum,
            spinsSpeedNum,
            "espi",
            member.Preset,
            true
        );
        
        Plugin.Log.Information($"Game created for {member.Name} with ID: {member.GameId}");
        UpdateConfig(() => configuration.PartyMembers[index].GameId = code.Result);

    }
    private void RefreshPartySnapshot()
    {
        partySnapshot.Clear();

        Plugin.Log.Information("Refreshing party snapshot...");
        if (Plugin.ClientState == null)
        {
            Plugin.Log.Error("ClientState is null.");
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return;
        }

        if (Plugin.PartyList == null)
        {
            Plugin.Log.Error("PartyList is null.");
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return;
        }
        if (!ClientState.IsLoggedIn)
        {
            Plugin.Log.Information("Not logged in.");
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return;
        }

        if (Plugin.PartyList.Length == 0)
        {
            Plugin.Log.Information("No party members.");
            lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return;
        }

        Plugin.Log.Information($"PartyList length: {Plugin.PartyList.Length}");
        foreach (var member in Plugin.PartyList)
        {
            try
            {
                Plugin.Log.Information($"Refreshing party snapshot for member {member?.Name}");
                var name = member?.Name?.TextValue;
                if (string.IsNullOrWhiteSpace(name))
                    name = "(unknown)";

                partySnapshot.Add(new PartyEntry(name));
                // also add player to configuration if not already present
                if (!configuration.PartyMembers.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    configuration.PartyMembers.Add(new Configuration.PartyMember
                    {
                        Name = name,
                        Spins = configuration.DefaultSpinAmount,
                        Preset = configuration.DefaultPreset,
                        SpinSpeed = configuration.DefaultSpinSpeed,
                        GameId = "<not created>"
                    });
                    Plugin.Log.Information($"Added new party member {name} to configuration.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error processing party member: {ex}");
            }
        }

        lastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void UpdateConfig(Action applyChanges)
    {
        applyChanges();
        configuration.Save();
    }
}
