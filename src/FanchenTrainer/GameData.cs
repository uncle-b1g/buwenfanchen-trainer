using BepInEx;
using Game.Model;
using Game.Model.Player.Components;
using LubanDatas;
using UnityEngine;

namespace FanchenTrainer;

internal sealed record Session(IntPtr Player, IntPtr World, string Storage, string Slot);
internal sealed record ItemRow(int Id, string Name, string Description);
internal sealed record NpcRow(IntPtr Pointer, int Id, string Name, int Affection);
internal sealed record AttributeRow(int Id, string Name, string Kind, float Editable, float Total);
internal sealed record InteractionRow(int Id, string Name, int Value);
internal sealed record EditCommand(Session Session, string Description, Func<PlayerModel, string> Apply);

internal static class GameData
{
    internal static Session? CurrentSession()
    {
        var player = GameStoreManager.CurrentPlayer;
        var world = GameStoreManager.CurrentGameWorld;
        if (player == null || world == null || player.combat == null || player.bag == null) return null;
        var manager = GameStoreManager.Instance;
        return new Session(player.Pointer, world.Pointer, manager.SelectedStorageId ?? "", manager.SelectedSlotId ?? "");
    }

    internal static PlayerModel Require(Session expected)
    {
        if (CurrentSession() != expected) throw new InvalidOperationException("存档或场景数据已切换，请重新选择并确认。");
        return GameStoreManager.CurrentPlayer;
    }

    internal static string Backup(Session expected)
    {
        Require(expected);
        var suppression = GameStoreManager.Instance.SuppressAutoSave();
        try
        {
            return SaveBackup.Create(Path.Combine(Application.persistentDataPath, "StorageV1"),
                Path.Combine(Paths.BepInExRootPath, "backups", "FanchenTrainer"),
                $"{expected.Storage}/{expected.Slot}");
        }
        finally { suppression?.Dispose(); }
    }

    internal static List<ItemRow> Items()
    {
        var result = new List<ItemRow>();
        var data = Tables.Current.TbItem.DataList;
        for (var i = 0; i < data.Count; i++)
        {
            var row = data[i];
            result.Add(new ItemRow(row.id.Value, Plain(row.itemName?.Value), Plain(row.itemDesc?.Value)));
        }
        return result.OrderBy(x => x.Id).ToList();
    }

    internal static List<NpcRow> Npcs()
    {
        var result = new List<NpcRow>();
        foreach (var npc in ReadNpcModels(GameStoreManager.CurrentGameWorld))
            result.Add(new NpcRow(npc.Pointer, npc.NpcCfgId.Value, Plain(npc.NpcName), npc.Intimacy));
        return result.OrderBy(x => x.Name, StringComparer.CurrentCulture).ThenBy(x => x.Id).ToList();
    }

    private static IEnumerable<NpcModel> ReadNpcModels(GameWorldModel world)
    {
        // Values exposes boxed value-type enumerators. Calling their interface
        // proxy across runtimes can pass the wrong native this pointer. The
        // game's reference-type iterator performs that traversal internally.
        var iterable = world.IterNpcModels(true).Cast<GameWorldModel._IterNpcModels_d__78>();
        var it = iterable.System_Collections_IEnumerable_GetEnumerator()
            .Cast<GameWorldModel._IterNpcModels_d__78>();
        try
        {
            while (it.MoveNext())
            {
                var npc = it.__2__current;
                if (npc != null) yield return npc;
            }
        }
        finally { it.System_IDisposable_Dispose(); }
    }

    internal static List<AttributeRow> Attributes(PlayerModel player) => Enum.GetValues<CombatAttrId>()
        .Select(id => new AttributeRow((int)id, AttributeName(id), AttributeKind(id), Editable(player.combat, id), player.combat.GetTotalAttr(id)))
        .ToList();

    internal static List<InteractionRow> Interactions(PlayerModel player)
    {
        var result = new List<InteractionRow>();
        var data = Tables.Current.TbInteractAttribute.DataList;
        for (var i = 0; i < data.Count; i++)
        {
            var row = data[i];
            result.Add(new InteractionRow(row.id.Value, Plain(row.name?.Value), player.GetInteractAttributeValue(row.id)));
        }
        return result;
    }

    internal static string SetAttribute(PlayerModel player, int number, float value)
    {
        var id = (CombatAttrId)number;
        if (!Enum.IsDefined(id)) throw new ArgumentException("未知属性。");
        var before = Editable(player.combat, id);
        switch (AttributeKind(id))
        {
            case "当前值": player.combat.SetCurrentAttr(id, value); break;
            case "基础上限": player.combat.SetMaxAttr(id, value, false); break;
            case "角色基础值":
            case "总寿元": player.combat.SetRoleAttribute(id, value); break;
            default: player.combat.SetBaseAttrSafe(id, value, true); break;
        }
        return $"{AttributeName(id)}：{Rules.Format(before)} → {Rules.Format(Editable(player.combat, id))}；最终值 {Rules.Format(player.combat.GetTotalAttr(id))}";
    }

    internal static string AddItem(PlayerModel player, ItemRow row, int count)
    {
        var id = new TbItemId(row.Id);
        if (Tables.Current.TbItem.GetOrDefault(id) == null) throw new InvalidOperationException("物品已不在游戏表中。");
        var before = player.bag.GetItemCount(id);
        // IL2CPP Nullable<T> is a boxed value type. A managed null cannot be unboxed.
        var context = new Il2CppSystem.Nullable<Game.Model.Components.AddItemContext>();
        player.bag.AddItem(id, count, ItemSourceType.GMCommand, context, null);
        var after = player.bag.GetItemCount(id);
        return $"{row.Name} [{row.Id}]：请求 +{count}，背包 {before} → {after}。若游戏弹出结算窗口，请完成结算。";
    }

    internal static string SetAffection(NpcRow row, int target)
    {
        foreach (var npc in ReadNpcModels(GameStoreManager.CurrentGameWorld))
        {
            if (npc.Pointer != row.Pointer || npc.NpcCfgId.Value != row.Id) continue;
            var before = npc.Intimacy;
            var delta = checked(target - before);
            npc.AddIntimacy(delta, 0);
            return $"{row.Name} [{row.Id}] 好感度：{before} → {npc.Intimacy}（目标 {target}，遵循游戏关系限制）";
        }
        throw new InvalidOperationException("该 NPC 已离开当前世界数据，请刷新列表。");
    }

    internal static string SetInteraction(PlayerModel player, InteractionRow row, int value)
    {
        var id = new TbInteractAttributeId(row.Id);
        var before = player.GetInteractAttributeValue(id);
        player.SetInteractAttributeValue(id, value);
        return $"{row.Name}：{before} → {player.GetInteractAttributeValue(id)}";
    }

    internal static string AddPathPoints(PlayerModel player, int amount)
    {
        if (amount < 1 || amount > 1_000_000)
            throw new ArgumentException("每次请输入 1 到 1000000 之间的道途点数量。");
        var path = player.talentPath ?? throw new InvalidOperationException("当前主角的道途数据尚未加载。");
        var before = path.PathPoints;
        if (before < 0 || (long)before + amount > int.MaxValue)
            throw new ArgumentException("增加后道途点超出可用整数范围，请减少数量。");
        path.AddPathPoints(amount);
        return $"剩余道途点：{before} → {path.PathPoints}（请求增加 {amount}）";
    }

    internal static string AddSpiritPoints(PlayerModel player, int amount)
    {
        if (amount < 1 || amount > 1_000_000)
            throw new ArgumentException("每次请输入 1 到 1000000 之间的灵根属性点数量。");
        var path = player.talentPath ?? throw new InvalidOperationException("当前主角的灵根数据尚未加载。");
        var before = path.SpiritPointRemain;
        if (before < 0 || (long)before + amount > int.MaxValue)
            throw new ArgumentException("增加后灵根属性点超出可用整数范围，请减少数量。");
        path.AddSpiritPoints(amount);
        return $"剩余灵根属性点：{before} → {path.SpiritPointRemain}（请求增加 {amount}）";
    }


    private static float Editable(CombatModel model, CombatAttrId id) => AttributeKind(id) switch
    {
        "总寿元" => model.GetRoleAttribute(id),
        _ => model.BaseAttrs.TryGetValue(id, out var value) ? value : 0
    };

    internal static string AttributeKind(CombatAttrId id) => (int)id switch
    {
        1 or 23 or 25 or 26 or 28 or 200 => "当前值",
        4 or 22 or 24 or 27 or 201 => "基础上限",
        5 or 6 or 7 => "角色基础值",
        8 => "总寿元",
        _ => "基础值"
    };

    private static string AttributeName(CombatAttrId id) => (int)id switch
    {
        1 => "当前生命", 2 => "攻击", 3 => "防御", 4 => "生命上限", 5 => "气运", 6 => "魅力", 7 => "移动速度", 8 => "寿元",
        11 => "生命百分比", 12 => "攻击百分比", 13 => "防御百分比", 21 => "速度", 22 => "法力上限", 23 => "当前韧性",
        24 => "韧性上限", 25 => "当前法力", 26 => "当前共鸣", 27 => "共鸣上限", 28 => "护盾",
        51 => "金系伤害加成", 52 => "木系伤害加成", 53 => "水系伤害加成", 54 => "火系伤害加成", 55 => "土系伤害加成",
        101 => "伤害加成", 102 => "伤害减免", 103 => "吸血效率", 104 => "治疗效率", 108 => "破韧效率",
        111 => "暴击率", 112 => "暴击伤害", 113 => "暴击抵抗", 114 => "暴伤减免", 115 => "命中率", 116 => "闪避率",
        117 => "无视防御", 118 => "无视防御抵抗", 119 => "追击概率", 120 => "反击概率", 121 => "反击伤害加成", 122 => "反击伤害抗性",
        200 => "当前体力", 201 => "体力上限", _ => id.ToString()
    };

    private static string Plain(string? text) => System.Text.RegularExpressions.Regex.Replace(text ?? "（未命名）", "<[^>]*>", "");
}
