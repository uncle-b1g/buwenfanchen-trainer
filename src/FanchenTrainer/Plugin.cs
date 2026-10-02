using System.Security.Cryptography;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace FanchenTrainer;

[BepInPlugin(Id, "凡尘随心", Version)]
[BepInProcess("WorldApart.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Id = "local.worldapart.fanchen.trainer";
    public const string Version = "0.1.5";
    public const string MetadataSha256 = "BBECA25F98CFC56BFE48A5BD6DC90B9AADFEB1A6BB8F0DC03CA8DA2EF26DBDBC";
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<Key> ToggleKey = null!;
    internal static ConfigEntry<bool> ReadOnly = null!;
    private Harmony? _harmony;

    public override void Load()
    {
        Logger = Log;
        ToggleKey = Config.Bind("界面", "ToggleKey", Key.F8, "打开 / 关闭面板的按键（InputSystem Key）。");
        ReadOnly = Config.Bind("保护", "ReadOnly", false, "只浏览数据，禁用修改。");
        var metadata = Path.Combine(Paths.GameRootPath, "WorldApart_Data", "il2cpp_data", "Metadata", "global-metadata.dat");
        using var stream = File.OpenRead(metadata);
        using var sha = SHA256.Create();
        var actual = Convert.ToHexString(sha.ComputeHash(stream));
        if (!actual.Equals(MetadataSha256, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogError($"游戏版本不匹配，未加载修改器。预期 Build 25617557，metadata={actual}");
            return;
        }

        // Keep cancellation events flowing so a held movement key is released normally.
        _harmony = new Harmony(Id);
        _harmony.Patch(AccessTools.Method(typeof(Game.InputManager), "DispatchBinding"),
            prefix: new HarmonyMethod(typeof(Plugin), nameof(AllowGameBinding)));
        AddComponent<TrainerPanel>();
        Log.LogInfo($"凡尘随心 {Version} loaded; press {ToggleKey.Value}. Runtime verification pending.");
    }

    private static bool AllowGameBinding(bool isCanceled) => !TrainerPanel.IsOpen || isCanceled;

    public override bool Unload()
    {
        TrainerPanel.Close();
        _harmony?.UnpatchSelf();
        return false; // Injected Unity component remains until the process exits.
    }
}
