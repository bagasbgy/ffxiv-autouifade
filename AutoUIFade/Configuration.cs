using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Dalamud.Configuration;

namespace AutoUIFade;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    [JsonInclude] public int Version { get; set; } = 11;

    [JsonInclude] public bool Enabled { get; set; } = true;
    [JsonInclude] public bool ShowInCombat { get; set; } = true;
    [JsonInclude] public bool ShowWhenWeaponDrawn { get; set; } = true;
    [JsonInclude] public bool ShowOnHover { get; set; } = true;
    [JsonInclude] public bool ShowWhileCrafting { get; set; } = true;
    [JsonInclude] public bool ShowWhileGathering { get; set; } = true;
    [JsonInclude] public bool ShowInPvp { get; set; } = true;
    [JsonInclude] public bool ShowInDutyOrInstance { get; set; } = true;
    [JsonInclude] public bool ShowInNpcConversation { get; set; } = false;
    [JsonInclude] public bool ShowWhenTargetSelected { get; set; } = true;

    [JsonInclude] public float InactiveOpacityPercent { get; set; } = 0f;

    [JsonInclude] public float FadeDurationSeconds { get; set; } = 0.15f;

    // Delay before hiding, in seconds.
    [JsonInclude] public float HideDelaySeconds { get; set; } = 0.50f;

    [JsonInclude] public bool? FadeHotbars { get; set; }

    [JsonInclude] public bool? FadeJobGauges { get; set; }

    [JsonInclude] public bool? FadeParameterBar { get; set; }

    [JsonInclude] public bool? FadeExperienceBar { get; set; }

    [JsonInclude] public bool? FadePlayerStatus { get; set; }

    [JsonInclude] public List<string> CustomAddonNames { get; set; } = new();

    // Native FFXIV addon names. The first bar is _ActionBar.
    // The numbered bars are _ActionBar01.._ActionBar09.
    [JsonInclude] public List<string> HotbarAddonNames { get; set; } =
        new()
        {
            "_ActionBar",
            "_ActionBar01",
            "_ActionBar02",
            "_ActionBar03",
            "_ActionBar04",
            "_ActionBar05",
            "_ActionBar06",
            "_ActionBar07",
            "_ActionBar08",
            "_ActionBar09",
        };

    [JsonInclude] public List<string> JobGaugeAddonNames { get; set; } =
        CreateDefaultJobGaugeAddonNames();

    [JsonInclude] public List<string> ParameterBarAddonNames { get; set; } =
        new()
        {
            "_ParameterWidget",
        };

    [JsonInclude] public List<string> ExperienceBarAddonNames { get; set; } =
        new()
        {
            "_Exp",
        };

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void Normalize()
    {
        if (Version < 11)
        {
            ShowInNpcConversation = false;
            Version = 11;
        }

        HotbarAddonNames ??= new List<string>();
        JobGaugeAddonNames ??= new List<string>();
        ParameterBarAddonNames ??= new List<string>();
        ExperienceBarAddonNames ??= new List<string>();
        CustomAddonNames ??= new List<string>();

        if (JobGaugeAddonNames.Count == 0)
            JobGaugeAddonNames = CreateDefaultJobGaugeAddonNames();

        HotbarAddonNames = HotbarAddonNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Select(MigrateLegacyAddonName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var gaugeNames = JobGaugeAddonNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        gaugeNames.RemoveAll(IsLegacyGenericGaugeName);
        if (gaugeNames.Count == 0 || JobGaugeAddonNames.Any(IsLegacyGenericGaugeName))
            gaugeNames.AddRange(CreateDefaultJobGaugeAddonNames());

        JobGaugeAddonNames = gaugeNames
            .Distinct(StringComparer.Ordinal)
            .ToList();

        ParameterBarAddonNames = ParameterBarAddonNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ParameterBarAddonNames.Count == 0)
            ParameterBarAddonNames.Add("_ParameterWidget");

        ExperienceBarAddonNames = ExperienceBarAddonNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Select(MigrateExperienceBarName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ExperienceBarAddonNames.Count == 0)
            ExperienceBarAddonNames.Add("_Exp");

        CustomAddonNames = NormalizeAddonNames(CustomAddonNames)
            .Select(MigrateLegacyAddonName)
            .Select(MigrateExperienceBarName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var fadeHotbars = FadeHotbars;
        MigrateSegmentSelection(ref fadeHotbars, HotbarAddonNames, CreateDefaultHotbarAddonNames());
        FadeHotbars = fadeHotbars;

        var fadeJobGauges = FadeJobGauges;
        MigrateSegmentSelection(ref fadeJobGauges, JobGaugeAddonNames, CreateDefaultJobGaugeAddonNames());
        FadeJobGauges = fadeJobGauges;

        var fadeParameterBar = FadeParameterBar;
        MigrateSegmentSelection(ref fadeParameterBar, ParameterBarAddonNames, CreateDefaultParameterBarAddonNames());
        FadeParameterBar = fadeParameterBar;

        var fadeExperienceBar = FadeExperienceBar;
        MigrateSegmentSelection(ref fadeExperienceBar, ExperienceBarAddonNames, CreateDefaultExperienceBarAddonNames());
        FadeExperienceBar = fadeExperienceBar;

        FadePlayerStatus ??= false;

        var enabledStandardNames = new HashSet<string>(StringComparer.Ordinal);
        if (FadeHotbars == true)
            enabledStandardNames.UnionWith(CreateDefaultHotbarAddonNames());
        if (FadeJobGauges == true)
            enabledStandardNames.UnionWith(CreateDefaultJobGaugeAddonNames());
        if (FadeParameterBar == true)
            enabledStandardNames.UnionWith(CreateDefaultParameterBarAddonNames());
        if (FadeExperienceBar == true)
            enabledStandardNames.UnionWith(CreateDefaultExperienceBarAddonNames());
        if (FadePlayerStatus == true)
            enabledStandardNames.UnionWith(CreateDefaultPlayerStatusAddonNames());

        CustomAddonNames = CustomAddonNames
            .Where(name => !enabledStandardNames.Contains(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        HideDelaySeconds = Math.Clamp(HideDelaySeconds, 0f, 10f);
        InactiveOpacityPercent = Math.Clamp(InactiveOpacityPercent, 0f, 100f);
        FadeDurationSeconds = Math.Clamp(FadeDurationSeconds, 0f, 3f);
    }

    internal IEnumerable<string> GetConfiguredAddonNames()
    {
        if (FadeHotbars == true)
        {
            foreach (var name in CreateDefaultHotbarAddonNames())
                yield return name;
        }

        if (FadeJobGauges == true)
        {
            foreach (var name in CreateDefaultJobGaugeAddonNames())
                yield return name;
        }

        if (FadeParameterBar == true)
        {
            foreach (var name in CreateDefaultParameterBarAddonNames())
                yield return name;
        }

        if (FadeExperienceBar == true)
        {
            foreach (var name in CreateDefaultExperienceBarAddonNames())
                yield return name;
        }

        if (FadePlayerStatus == true)
        {
            foreach (var name in CreateDefaultPlayerStatusAddonNames())
                yield return name;
        }

        foreach (var name in CustomAddonNames)
            yield return name;
    }

    private static string MigrateLegacyAddonName(string name)
    {
        return name switch
        {
            "ActionBar" => "_ActionBar",
            "ActionBar01" => "_ActionBar01",
            "ActionBar02" => "_ActionBar02",
            "ActionBar03" => "_ActionBar03",
            "ActionBar04" => "_ActionBar04",
            "ActionBar05" => "_ActionBar05",
            "ActionBar06" => "_ActionBar06",
            "ActionBar07" => "_ActionBar07",
            "ActionBar08" => "_ActionBar08",
            "ActionBar09" => "_ActionBar09",
            _ => name,
        };
    }

    private static string MigrateExperienceBarName(string name)
    {
        return name == "ExpBar" ? "_Exp" : name;
    }

    private void MigrateSegmentSelection(
        ref bool? selection,
        List<string> legacyNames,
        List<string> standardNames)
    {
        if (selection.HasValue)
            return;

        var selectedNames = new HashSet<string>(legacyNames, StringComparer.Ordinal);
        var standardNameSet = new HashSet<string>(standardNames, StringComparer.Ordinal);
        var hasAllStandardNames = standardNames.All(selectedNames.Contains);

        selection = hasAllStandardNames;

        if (!hasAllStandardNames)
        {
            CustomAddonNames.AddRange(legacyNames);
            return;
        }

        CustomAddonNames.AddRange(legacyNames.Where(name => !standardNameSet.Contains(name)));
    }

    private static List<string> NormalizeAddonNames(IEnumerable<string> names)
    {
        return names
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static List<string> CreateDefaultHotbarAddonNames()
    {
        return new List<string>
        {
            "_ActionBar",
            "_ActionBar01",
            "_ActionBar02",
            "_ActionBar03",
            "_ActionBar04",
            "_ActionBar05",
            "_ActionBar06",
            "_ActionBar07",
            "_ActionBar08",
            "_ActionBar09",
        };
    }

    internal static List<string> CreateDefaultParameterBarAddonNames()
    {
        return new List<string> { "_ParameterWidget" };
    }

    internal static List<string> CreateDefaultExperienceBarAddonNames()
    {
        return new List<string> { "_Exp" };
    }

    internal static List<string> CreateDefaultPlayerStatusAddonNames()
    {
        return new List<string>
        {
            "_Status",
            "_StatusCustom0",
            "_StatusCustom1",
            "_StatusCustom2",
            "_StatusCustom3",
        };
    }

    internal static List<string> CreateDefaultJobGaugeAddonNames()
    {
        return new List<string>
        {
            "JobHudACN0",
            "JobHudPLD0",
            "JobHudWAR0",
            "JobHudDRK0",
            "JobHudDRK1",
            "JobHudGNB0",
            "JobHudWHM0",
            "JobHudSCH0",
            "JobHudAST0",
            "JobHudGFF0",
            "JobHudGFF1",
            "JobHudMNK0",
            "JobHudMNK1",
            "JobHudDRG0",
            "JobHudNIN0",
            "JobHudNIN1v70",
            "JobHudSAM0",
            "JobHudSAM1",
            "JobHudRRP0",
            "JobHudRRP1",
            "JobHudRDB0",
            "JobHudRDB1",
            "JobHudBRD0",
            "JobHudMCH0",
            "JobHudDNC0",
            "JobHudDNC1",
            "JobHudBLM0",
            "JobHudBLM1",
            "JobHudSMN0",
            "JobHudSMN1",
            "JobHudRDM0",
            "JobHudRPM0",
            "JobHudRPM1",
        };
    }

    private static bool IsLegacyGenericGaugeName(string name)
    {
        return name is "_JobHud" or "_JobHudSimple" or "JobHud" or "JobHudSimple";
    }
}
