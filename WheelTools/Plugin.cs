using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Game.ClientState.Party;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons;
using WheelTools.Windows;

namespace WheelTools;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; set; } = null!;

    private const string CommandName = "/wheeltools";
    private const string AliasName = "/wt";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("WheelTools");
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        try
        {
            ECommonsMain.Init(PluginInterface, this, Module.All);
        }
        catch (Exception ex)
        {
            Log.Error($"ECommons init failed: {ex}");
        }
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand){HelpMessage = "Tool for wheel related tasks, use /wheeltools or /wt to open the main window."});
        CommandManager.AddHandler(AliasName, new CommandInfo(OnCommand){HelpMessage = "Tool for wheel related tasks, use /wheeltools or /wt to open the main window."});

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        
        // Empty Configuration.PartyMembers from plugin config
        if (Configuration.PartyMembers != null && Configuration.PartyMembers.Count > 0)
        {
            Log.Information("Clearing existing party members from configuration.");
            Configuration.PartyMembers.Clear();
            Configuration.Save();
        }
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }
    
    // When a player joins the party, add them to the configuration
    public void OnPartyMemberAdded(IPartyMember member)
    {
        if (member == null || string.IsNullOrWhiteSpace(member.Name.ToString()))
            return;

        var existingMember = Configuration.PartyMembers.Find(m => m.Name == member.Name.ToString());
        if (existingMember != null)
            return;

        var newMember = new Configuration.PartyMember
        {
            Name = member.Name.ToString(),
            Spins = "0",
            Preset = Configuration.DefaultPreset,
            SpinSpeed = Configuration.DefaultSpinSpeed,
            GameId = "<not created>",
            Added = DateTime.UtcNow
        };

        Configuration.PartyMembers.Add(newMember);
        Configuration.Save();
    }
    
    // When a player leaves the party, disable them in the configuration
    public void OnPartyMemberRemoved(IPartyMember member)
    {
        if (member == null || string.IsNullOrWhiteSpace(member.Name.ToString()))
            return;

        var existingMember = Configuration.PartyMembers.Find(m => m.Name == member.Name.ToString());
        if (existingMember != null)
        {
            existingMember.Enabled = false;
            Configuration.Save();
        }
    }

    private void DrawUI() => WindowSystem.Draw();
    public void ToggleMainUI() => MainWindow.Toggle();
}
