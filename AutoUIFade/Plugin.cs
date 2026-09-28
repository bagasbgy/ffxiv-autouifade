using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace AutoUIFade;

public unsafe sealed class Plugin : IDalamudPlugin
{
    public string Name => "Auto UI Fade";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal Configuration Configuration { get; }
    internal UIManager UIManager { get; }
    private ConfigWindow ConfigWindow { get; }
    private readonly WindowSystem WindowSystem = new("AutoUIFade");

    private const string CommandName = "/autouifade";

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Normalize();
        Configuration.Save();

        UIManager = new UIManager(Configuration);

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Auto UI Fade settings. /autouifade"
        });

        Framework.Update += OnUpdate;

        Log.Information("Auto UI Fade loaded.");
    }

    private void OnCommand(string command, string arguments)
    {
        ConfigWindow.Toggle();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    private void OnUpdate(IFramework framework)
    {
        if (!Configuration.Enabled)
        {
            UIManager.RestoreAll();
            return;
        }

        if (GameGui.GameUiHidden)
        {
            UIManager.RestoreAll();
            return;
        }

        var inCombat = Configuration.ShowInCombat &&
                       Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

        var weaponDrawn = Configuration.ShowWhenWeaponDrawn && IsWeaponDrawn();
        var crafting = Configuration.ShowWhileCrafting &&
                   (Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Crafting] ||
                Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.PreparingToCraft] ||
                Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.ExecutingCraftingAction]);
        var gathering = Configuration.ShowWhileGathering &&
                (Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Gathering] ||
                 Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.ExecutingGatheringAction]);
        var inPvp = Configuration.ShowInPvp &&
                Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.PvPDisplayActive];
        var inDutyOrInstance = Configuration.ShowInDutyOrInstance &&
                       (Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] ||
                    Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56] ||
                    Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.DutyRecorderPlayback]);
        var npcConversationOpen = IsNpcConversationOpen();
        var showInNpcConversation = Configuration.ShowInNpcConversation && npcConversationOpen;
        var targetSelected = Configuration.ShowWhenTargetSelected && TargetManager.Target is not null;

        var hovered = Configuration.ShowOnHover &&
                      UIManager.IsMouseOverConfiguredUI();

        var showForOtherCondition = inCombat || weaponDrawn || crafting || gathering || inPvp ||
                                    inDutyOrInstance || targetSelected || hovered;
        var shouldShow = npcConversationOpen
            ? showInNpcConversation
            : showForOtherCondition;

        UIManager.Update(shouldShow);
    }

    private bool IsNpcConversationOpen()
    {
        var addon = GameGui.GetAddonByName("Talk");
        return !addon.IsNull && addon.IsReady && addon.IsVisible;
    }

    private bool IsWeaponDrawn()
    {
        var localPlayer = ObjectTable.LocalPlayer;
        if (localPlayer is null || localPlayer.Address == nint.Zero)
            return false;

        try
        {
            return ((Character*)localPlayer.Address)->IsWeaponDrawn;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unable to read the local player's weapon state.");
            return false;
        }
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;

        CommandManager.RemoveHandler(CommandName);

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;
        WindowSystem.RemoveAllWindows();

        UIManager.RestoreAll();
        Configuration.Save();

        ConfigWindow.Dispose();

        Log.Information("Auto UI Fade unloaded.");
    }
}
