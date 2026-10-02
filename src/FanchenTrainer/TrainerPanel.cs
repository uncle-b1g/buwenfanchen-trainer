using System.Text.Json;
using BepInEx;
using Game.Model;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace FanchenTrainer;

public sealed class TrainerPanel : MonoBehaviour
{
    private static TrainerPanel? _instance;
    public static bool IsOpen { get; private set; }
    private Session? _session;
    private Session? _backedUpSession;
    private EditCommand? _pending;
    private EditCommand? _queued;
    private GUISkin? _skin;
    private Font? _font;
    private readonly List<EventSystem> _disabledEventSystems = new();
    private readonly Dictionary<string, string> _edits = new();
    private readonly List<string> _history = new();
    private List<ItemRow> _items = new();
    private List<NpcRow> _npcs = new();
    private List<AttributeRow> _attributes = new();
    private List<InteractionRow> _interactions = new();
    private ItemRow? _item;
    private NpcRow? _npc;
    private Vector2 _scroll;
    private string _query = "", _quantity = "1", _affection = "100", _extra = "100";
    private string _pathPointAmount = "10";
    private int? _pathPoints;
    private string _spiritPointAmount = "10";
    private int? _spiritPoints;
    private string _status = "请载入存档。首次修改前自动备份磁盘存档。";
    private string _playerName = "", _details = "", _backupPath = "尚未备份";
    private int _tab, _page;
    private float _nextRefresh;
    private CursorLockMode _cursorLock;
    private bool _cursorVisible;
    private bool _uiReported;
    private bool _npcReadReported;
    private const int PageSize = 12;
    private static readonly string[] Tabs = { "主角属性", "道具物品", "人物好感", "记录与备份" };

    public TrainerPanel(IntPtr pointer) : base(pointer) { }

    public void Awake() => _instance = this;
    public static void Close() => _instance?.SetOpen(false);
    public void OnDisable() => SetOpen(false);
    public void OnDestroy()
    {
        SetOpen(false);
        if (_skin != null) Object.Destroy(_skin);
        if (_font != null) Object.Destroy(_font);
        _instance = null;
    }

    public void Update()
    {
        try
        {
            var keyboard = Keyboard.current;
            if (Application.isFocused && keyboard != null && keyboard[Plugin.ToggleKey.Value].wasPressedThisFrame)
                SetOpen(!IsOpen);
            else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetOpen(false);
            if (!IsOpen) return;
            BlockUiInput();
            if (_queued != null)
            {
                var command = _queued;
                _queued = null;
                Execute(command);
                _nextRefresh = 0;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                Refresh();
                _nextRefresh = Time.unscaledTime + 1;
            }
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void SetOpen(bool open)
    {
        if (open == IsOpen) return;
        IsOpen = open;
        if (open)
        {
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            _nextRefresh = 0;
            BlockUiInput();
        }
        else
        {
            _pending = null;
            _queued = null;
            foreach (var events in _disabledEventSystems)
                if (events != null) events.enabled = true;
            _disabledEventSystems.Clear();
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }
    }

    private void BlockUiInput()
    {
        // Track only systems we disable; do not enable a system disabled by the game.
        var current = EventSystem.current;
        if (current != null && current.enabled)
        {
            _disabledEventSystems.Add(current);
            current.enabled = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Refresh()
    {
        var session = GameData.CurrentSession();
        if (session != _session)
        {
            _session = session;
            _npcReadReported = false;
            _pathPoints = null;
            _spiritPoints = null;
            _pending = null;
            _queued = null;
            _backedUpSession = null;
            _backupPath = "尚未备份";
            _items.Clear(); _npcs.Clear(); _attributes.Clear(); _interactions.Clear(); _edits.Clear();
            _item = null; _npc = null; _page = 0;
            _status = session == null ? "请先进入游戏并载入存档。" : "存档已连接。首次修改前自动备份磁盘存档。";
        }
        if (session == null) return;
        var player = GameData.Require(session);
        _pathPoints = player.talentPath?.PathPoints;
        _spiritPoints = player.talentPath?.SpiritPointRemain;
        _playerName = player.playerName;
        _details = $"年龄 {player.CurrentAge}  |  修为 {player.combat.CultivateExp}  |  储备修为 {player.combat.CultivateReserveExp}  |  恶名 {player.playerPropEvil}  |  宗门贡献 {player.sectContributionTotal}";
        _attributes = GameData.Attributes(player);
        _interactions = GameData.Interactions(player);
        if (_tab == 1 && _items.Count == 0) _items = GameData.Items();
        if (_tab == 2)
        {
            if (!_npcReadReported) Plugin.Logger.LogInfo("Reading NPC list through the game's native iterator.");
            _npcs = GameData.Npcs();
            if (!_npcReadReported)
            {
                _npcReadReported = true;
                Plugin.Logger.LogInfo($"NPC list read successfully: {_npcs.Count} characters.");
            }
        }
    }

    private void Execute(EditCommand command)
    {
        try
        {
            if (Plugin.ReadOnly.Value) throw new InvalidOperationException("当前为只读模式，请在配置中关闭 ReadOnly 后重启游戏。");
            var player = GameData.Require(command.Session);
            if (_backedUpSession != command.Session)
            {
                _backupPath = GameData.Backup(command.Session);
                _backedUpSession = command.Session;
                Record("已备份：" + _backupPath);
            }
            GameData.Require(command.Session);
            var result = command.Apply(player);
            _edits.Clear();
            Record(result);
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void Record(string message)
    {
        _status = message;
        _history.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        if (_history.Count > 100) _history.RemoveAt(_history.Count - 1);
        Plugin.Logger.LogInfo(message);
        try
        {
            var path = Path.Combine(Paths.BepInExRootPath, "FanchenTrainer-operations.jsonl");
            File.AppendAllText(path, JsonSerializer.Serialize(new { at = DateTime.UtcNow, message }) + Environment.NewLine);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("操作日志写入失败：" + ex.Message); }
    }

    private void ReportError(Exception ex)
    {
        _status = "未完成：" + ex.Message;
        Plugin.Logger.LogError(ex);
        _nextRefresh = Time.unscaledTime + 5;
    }

    private void Ask(string description, Func<PlayerModel, string> apply)
    {
        if (_session == null || Plugin.ReadOnly.Value) return;
        _pending = new EditCommand(_session, description, apply);
        GUI.FocusControl(null);
    }

    public void OnGUI()
    {
        if (!IsOpen) return;
        var oldSkin = GUI.skin;
        var oldMatrix = GUI.matrix;
        var oldColor = GUI.color;
        var oldDepth = GUI.depth;
        var oldEnabled = GUI.enabled;
        try
        {
            EnsureSkin();
            GUI.skin = _skin;
            GUI.depth = -10000;
            var scale = Math.Min(1.35f, Math.Min(Screen.width / 1040f, Screen.height / 760f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(new Rect((width - 1000) / 2, (height - 710) / 2, 1000, 710), _skin!.window);
            try
            {
                DrawPanel();
                if (!_uiReported && Event.current.type == EventType.Repaint)
                {
                    _uiReported = true;
                    Plugin.Logger.LogInfo("F8 panel rendered successfully.");
                }
            }
            finally { GUILayout.EndArea(); }
        }
        catch (Exception ex) { ReportError(ex); SetOpen(false); }
        finally
        {
            GUI.skin = oldSkin; GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; GUI.enabled = oldEnabled;
        }
    }

    private void EnsureSkin()
    {
        if (_skin != null) return;
        _skin = Object.Instantiate(GUI.skin).Cast<GUISkin>();
        try
        {
            // The public OS-font factory was stripped from this game. Allocate
            // the IL2CPP object and use the available native font binding.
            _font = new Font(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Font>.NativeClassPtr));
            Font.Internal_CreateDynamicFont(_font,
                new Il2CppStringArray(new[] { "Microsoft YaHei", "SimHei", "Arial" }), 18);
            _skin.font = _font;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("中文字体创建失败，使用默认字体：" + ex.Message);
            if (_font != null) Object.Destroy(_font);
            _font = null;
        }
        foreach (var style in new[] { _skin.label, _skin.button, _skin.textField, _skin.box, _skin.toggle })
        {
            style.fontSize = 17;
            style.richText = false;
            style.padding = new RectOffset(10, 10, 6, 6);
        }
        _skin.label.wordWrap = true;
        _skin.button.fixedHeight = 34;
        _skin.textField.fixedHeight = 34;
        _skin.window.padding = new RectOffset(18, 18, 18, 16);
    }

    private void DrawPanel()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"凡尘随心   v{Plugin.Version}   ·   HB叔叔制做   ·   {_playerName}", GUILayout.ExpandWidth(true));
        if (GUILayout.Button($"关闭 [{Plugin.ToggleKey.Value}]", GUILayout.Width(140))) SetOpen(false);
        GUILayout.EndHorizontal();
        GUILayout.Label(_session == null ? "等待游戏存档" : _details);
        // The Toolbar string-array helper was stripped; individual buttons
        // use the native Button entry point that this build retains.
        var tab = _tab;
        GUILayout.BeginHorizontal();
        for (var i = 0; i < Tabs.Length; i++)
        {
            var oldColor = GUI.color;
            if (i == _tab) GUI.color = new Color(0.65f, 0.9f, 1);
            if (GUILayout.Button(Tabs[i], GUILayout.Height(38))) tab = i;
            GUI.color = oldColor;
        }
        GUILayout.EndHorizontal();
        if (tab != _tab) { _tab = tab; _query = ""; _page = 0; _scroll = Vector2.zero; _nextRefresh = 0; }
        GUILayout.Space(8);
        var editable = _session != null && !Plugin.ReadOnly.Value && _pending == null && _queued == null;
        GUI.enabled = _pending == null && _queued == null;
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(434));
        try
        {
            if (_session == null && _tab != 3) GUILayout.Label("进入一个已保存的游戏存档后，这里会显示可修改的数据。");
            else switch (_tab)
            {
                case 0: DrawAttributes(editable); break;
                case 1: DrawItems(editable); break;
                case 2: DrawNpcs(editable); break;
                default: DrawHistory(); break;
            }
        }
        finally { GUILayout.EndScrollView(); }
        GUI.enabled = true;
        GUILayout.Space(5);
        if (_pending != null)
        {
            GUILayout.Label("确认修改：" + _pending.Description);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("确认执行（自动备份）")) { _queued = _pending; _pending = null; }
            if (GUILayout.Button("取消")) _pending = null;
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label(_status, GUILayout.Height(74));
        GUILayout.Label(Plugin.ReadOnly.Value ? "只读模式" : "修改由游戏正常存档流程保存。首次修改前备份现有磁盘存档；退出游戏后可手动恢复。", GUILayout.Height(30));
    }

    private void Search()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("搜索名称 / ID", GUILayout.Width(150));
        var query = GUILayout.TextField(_query, 80);
        if (query != _query) { _query = query; _page = 0; }
        if (GUILayout.Button("清空", GUILayout.Width(65))) { _query = ""; _page = 0; }
        GUILayout.EndHorizontal();
    }

    private void DrawAttributes(bool editable)
    {
        Search();
        GUILayout.Label("道途点");
        GUILayout.BeginHorizontal();
        GUILayout.Label("剩余道途点：" + (_pathPoints?.ToString() ?? "尚未加载"), GUILayout.Width(280));
        GUILayout.Label("增加数量", GUILayout.Width(110));
        _pathPointAmount = GUILayout.TextField(_pathPointAmount, 7, GUILayout.Width(160));
        EnabledButton("增加道途点", editable && _pathPoints.HasValue, () =>
        {
            var amount = Rules.Integer(_pathPointAmount, 1, 1_000_000);
            Ask($"增加道途点 {amount}", p => GameData.AddPathPoints(p, amount));
        }, 170);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        GUILayout.Label("灵根属性点");
        GUILayout.BeginHorizontal();
        GUILayout.Label("剩余灵根属性点：" + (_spiritPoints?.ToString() ?? "尚未加载"), GUILayout.Width(280));
        GUILayout.Label("增加数量", GUILayout.Width(110));
        _spiritPointAmount = GUILayout.TextField(_spiritPointAmount, 7, GUILayout.Width(160));
        EnabledButton("增加灵根属性点", editable && _spiritPoints.HasValue, () =>
        {
            var amount = Rules.Integer(_spiritPointAmount, 1, 1_000_000);
            Ask($"增加灵根属性点 {amount}（增加后可在游戏灵根界面分配）", p => GameData.AddSpiritPoints(p, amount));
        }, 190);
        GUILayout.EndHorizontal();
        GUILayout.Label("增加未分配的灵根属性点后，可在游戏灵根界面给各系灵根加点。");
        GUILayout.Space(8);
        GUILayout.Label("填写目标基础值 / 当前值。最终值可能含装备、功法加成。比例属性保持游戏原始单位。");
        foreach (var row in _attributes.Where(x => Rules.Matches(x.Name, x.Id, _query)))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{row.Name}  [{row.Id}]", GUILayout.Width(225));
            GUILayout.Label($"{row.Kind} {Rules.Format(row.Editable)}  /  最终 {Rules.Format(row.Total)}", GUILayout.Width(355));
            var input = Edit("stat" + row.Id, Rules.Format(row.Editable));
            EnabledButton("应用", editable, () =>
            {
                var number = Rules.Number(input);
                Ask($"{row.Name}（{row.Kind}）设为 {number}", p => GameData.SetAttribute(p, row.Id, number));
            });
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(10);
        GUILayout.Label("交互属性");
        foreach (var row in _interactions.Where(x => Rules.Matches(x.Name, x.Id, _query)))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{row.Name} [{row.Id}]  ·  当前 {row.Value}", GUILayout.Width(580));
            var input = Edit("interaction" + row.Id, row.Value.ToString());
            EnabledButton("应用", editable, () =>
            {
                var number = Rules.Integer(input, 0, 1_000_000);
                Ask($"{row.Name}设为 {number}", p => GameData.SetInteraction(p, row, number));
            });
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        EnabledButton("生命 / 法力 / 体力回满", editable, () => Ask("恢复全部当前属性至上限", p =>
        {
            p.combat.RestoreAllCurrentAttributesToMax();
            return "已调用游戏恢复全部当前属性接口。";
        }), 290);
        GUILayout.EndHorizontal();
        GUILayout.Label("修为与角色数值（输入整数）");
        _extra = GUILayout.TextField(_extra, 12, GUILayout.Width(200));
        GUILayout.BeginHorizontal();
        EnabledButton("增加修为", editable, () =>
        {
            var number = Rules.Integer(_extra, 1, 10_000_000);
            Ask($"增加修为 {number}", p => { var before = p.combat.CultivateExp; p.combat.AddCultivateExp(number, false, 0); return $"修为 {before} → {p.combat.CultivateExp}"; });
        }, 170);
        EnabledButton("增加储备修为", editable, () =>
        {
            var number = Rules.Integer(_extra, 1, 10_000_000);
            Ask($"增加储备修为 {number}", p => { var before = p.combat.CultivateReserveExp; p.combat.AddCultivateReserveExp(number); return $"储备修为 {before} → {p.combat.CultivateReserveExp}"; });
        }, 170);
        EnabledButton("设置恶名", editable, () =>
        {
            var number = Rules.Integer(_extra, 0, 1_000_000);
            Ask($"恶名设为 {number}", p => { var before = p.playerPropEvil; p.SetPropEvil(number); return $"恶名 {before} → {p.playerPropEvil}"; });
        }, 170);
        EnabledButton("增加宗门贡献", editable, () =>
        {
            var number = Rules.Integer(_extra, 1, 1_000_000);
            Ask($"增加宗门贡献 {number}", p => { var before = p.sectContributionTotal; p.AddSectContribution(number, ItemSourceType.GMCommand); return $"宗门贡献 {before} → {p.sectContributionTotal}"; });
        }, 170);
        GUILayout.EndHorizontal();
    }

    private void DrawItems(bool editable)
    {
        Search();
        if (_item != null)
        {
            GUILayout.Label($"已选：{_item.Name} [{_item.Id}]\n{_item.Description}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("添加数量（1—9999）", GUILayout.Width(235));
            _quantity = GUILayout.TextField(_quantity, 5, GUILayout.Width(120));
            EnabledButton("加入背包", editable, () =>
            {
                var row = _item;
                var count = Rules.Integer(_quantity, 1, 9999);
                Ask($"添加 {row.Name} [{row.Id}] × {count}", p => GameData.AddItem(p, row, count));
            }, 150);
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("选择物品，然后填写数量。列表来自当前游戏物品表。");
        var rows = _items.Where(x => Rules.Matches(x.Name, x.Id, _query)).ToList();
        Pager(rows.Count);
        foreach (var row in rows.Skip(_page * PageSize).Take(PageSize))
            if (GUILayout.Button($"{row.Id}    {row.Name}")) _item = row;
    }

    private void DrawNpcs(bool editable)
    {
        Search();
        if (_npc != null)
        {
            var current = _npcs.FirstOrDefault(x => x.Pointer == _npc.Pointer);
            GUILayout.Label($"已选：{_npc.Name} [{_npc.Id}]  ·  当前好感 {current?.Affection.ToString() ?? "已离开世界"}");
            GUILayout.BeginHorizontal();
            GUILayout.Label("目标好感（可填负数）", GUILayout.Width(235));
            _affection = GUILayout.TextField(_affection, 7, GUILayout.Width(120));
            EnabledButton("设置好感", editable && current != null, () =>
            {
                var row = _npc;
                var value = Rules.Integer(_affection, -10000, 10000);
                Ask($"{row.Name}的好感度设为 {value}", _ => GameData.SetAffection(row, value));
            }, 150);
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("选择当前世界中已生成的人物。实际好感变化遵循游戏的关系阶段限制。");
        var rows = _npcs.Where(x => Rules.Matches(x.Name, x.Id, _query)).ToList();
        Pager(rows.Count);
        foreach (var row in rows.Skip(_page * PageSize).Take(PageSize))
            if (GUILayout.Button($"{row.Name}    [{row.Id}]    好感 {row.Affection}")) { _npc = row; _affection = row.Affection.ToString(); }
    }

    private void Pager(int count)
    {
        var last = Math.Max(0, (count - 1) / PageSize);
        _page = Math.Clamp(_page, 0, last);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("上一页", GUILayout.Width(110))) _page = Math.Max(0, _page - 1);
        GUILayout.Label($"共 {count} 项  ·  第 {_page + 1} / {last + 1} 页", GUILayout.Width(380));
        if (GUILayout.Button("下一页", GUILayout.Width(110))) _page = Math.Min(last, _page + 1);
        GUILayout.EndHorizontal();
    }

    private void DrawHistory()
    {
        GUILayout.Label("最近的修改前备份：\n" + _backupPath);
        GUILayout.Label("恢复方法：退出游戏，另存当前 StorageV1，再将备份 ZIP 内的 StorageV1 文件夹恢复到游戏存档目录。备份仅包含磁盘上已保存的数据。");
        GUILayout.Label($"测试版 {Plugin.Version} · 适配 Steam Build 25617557。游戏更新后需重新适配。");
        GUILayout.Space(10);
        foreach (var line in _history) GUILayout.Label(line);
    }

    private string Edit(string key, string initial)
    {
        if (!_edits.TryGetValue(key, out var text)) text = initial;
        var result = GUILayout.TextField(text, 20, GUILayout.Width(190));
        _edits[key] = result;
        return result;
    }

    private void EnabledButton(string title, bool enabled, Action action, float width = 100)
    {
        var old = GUI.enabled;
        GUI.enabled = old && enabled;
        if (GUILayout.Button(title, GUILayout.Width(width)))
        {
            try { action(); }
            catch (Exception ex) { ReportError(ex); }
        }
        GUI.enabled = old;
    }
}
