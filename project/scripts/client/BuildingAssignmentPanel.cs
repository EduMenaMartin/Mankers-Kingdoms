using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MankersKingdoms.Shared;

namespace MankersKingdoms.Client;

/// <summary>
/// Building Assignment Panel — opened by pressing E near any Shelter (founder only).
/// Shows all recruited settlement NPCs on the left and all workable stations on the right.
/// The founder clicks an NPC row and a station row, then presses Assign.
/// Unassign button next to each currently-assigned NPC removes them from their station.
///
/// NPC data comes from LocalState.VillageRosterJson (pushed by VillageSystem on every change).
/// Station list is scanned live from the SettlementSystem node's children.
///
/// Layer 31 — above stockpile panel (29) and recruitment dialogue (28).
/// Editor task: add as CanvasLayer child of GameWorld root, attach this script.
/// </summary>
public partial class BuildingAssignmentPanel : CanvasLayer
{
    private const string VILLAGE_SYSTEM_PATH    = "/root/GameWorld/VillageSystem";
    private const string SETTLEMENT_SYSTEM_PATH = "/root/GameWorld/SettlementSystem";

    // Workable station prefixes — only these building types can have NPC workers.
    // Godot 4 normalises dots in node names to underscores, so we match against
    // the normalised form (building_woodcutters_post_x_z, not building.woodcutters_post_x_z).
    private static readonly string[] WORKABLE_PREFIXES =
    {
        "building_woodcutters_post",
        "building_herbalists_hut"
    };

    private VBoxContainer _npcList      = null!;
    private VBoxContainer _stationList  = null!;
    private Button        _assignBtn    = null!;
    private Label         _statusLabel  = null!;

    private string _selectedNpcId      = "";
    private string _selectedStation    = "";

    // Parsed roster entry.
    private record NpcEntry(string Id, string Name, string ArchetypeKey, string Station);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Layer   = 31;
        Visible = false;
        BuildUI();
        LocalState.VillageRosterChanged += OnRosterChanged;
    }

    public override void _ExitTree()
    {
        LocalState.VillageRosterChanged -= OnRosterChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Open()
    {
        _selectedNpcId  = "";
        _selectedStation = "";
        Refresh();
        Visible = true;
    }

    public void Close() => Visible = false;

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Semi-transparent backdrop.
        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.45f) };
        backdrop.AnchorRight  = 1f;
        backdrop.AnchorBottom = 1f;
        AddChild(backdrop);

        // Centred panel.
        var panel = new Panel();
        panel.AnchorLeft   = 0.5f;
        panel.AnchorRight  = 0.5f;
        panel.AnchorTop    = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.OffsetLeft   = -280f;
        panel.OffsetRight  =  280f;
        panel.OffsetTop    = -200f;
        panel.OffsetBottom =  200f;
        AddChild(panel);

        var root = new VBoxContainer();
        root.AnchorRight  = 1f;
        root.AnchorBottom = 1f;
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        // Title.
        var title = new Label
        {
            Text                = Loc.T("assignment.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        root.AddChild(title);
        root.AddChild(new HSeparator());

        // Two-column content row.
        var cols = new HBoxContainer();
        cols.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        cols.AddThemeConstantOverride("separation", 12);
        root.AddChild(cols);

        // Left — NPC list.
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(220f, 0f) };
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cols.AddChild(leftCol);

        leftCol.AddChild(new Label { Text = Loc.T("assignment.col_villagers") });
        var npcScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 260f) };
        npcScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        leftCol.AddChild(npcScroll);
        _npcList = new VBoxContainer();
        _npcList.AddThemeConstantOverride("separation", 4);
        npcScroll.AddChild(_npcList);

        // Right — station list.
        var rightCol = new VBoxContainer { CustomMinimumSize = new Vector2(180f, 0f) };
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cols.AddChild(rightCol);

        rightCol.AddChild(new Label { Text = Loc.T("assignment.col_stations") });
        var stationScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 260f) };
        stationScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightCol.AddChild(stationScroll);
        _stationList = new VBoxContainer();
        _stationList.AddThemeConstantOverride("separation", 4);
        stationScroll.AddChild(_stationList);

        root.AddChild(new HSeparator());

        // Status + Assign row.
        var bottomRow = new HBoxContainer();
        root.AddChild(bottomRow);

        _statusLabel = new Label
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode        = TextServer.AutowrapMode.WordSmart
        };
        bottomRow.AddChild(_statusLabel);

        _assignBtn = new Button { Text = Loc.T("assignment.assign"), Disabled = true };
        _assignBtn.Pressed += OnAssignPressed;
        bottomRow.AddChild(_assignBtn);

        var closeBtn = new Button { Text = Loc.T("assignment.close_hint") };
        closeBtn.Pressed += Close;
        root.AddChild(closeBtn);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void OnRosterChanged()
    {
        if (Visible) Refresh();
    }

    private void Refresh()
    {
        RefreshNpcList();
        RefreshStationList();
        RefreshAssignButton();
    }

    private void RefreshNpcList()
    {
        foreach (Node child in _npcList.GetChildren())
            child.QueueFree();

        var npcs = ParseRoster();
        if (npcs.Count == 0)
        {
            _npcList.AddChild(new Label
            {
                Text     = Loc.T("assignment.no_villagers"),
                Modulate = new Color(0.7f, 0.7f, 0.7f)
            });
            return;
        }

        foreach (var npc in npcs)
        {
            var row = new HBoxContainer();
            var captured = npc; // capture for lambdas

            bool isSelected = _selectedNpcId == npc.Id;
            var nameLabel = new Label
            {
                Text                = npc.Name,
                CustomMinimumSize   = new Vector2(90f, 0f),
                Modulate            = isSelected ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 1f, 1f)
            };
            row.AddChild(nameLabel);

            string archetypeName = Loc.T(npc.ArchetypeKey);
            string stationShort  = string.IsNullOrEmpty(npc.Station)
                ? Loc.T("assignment.idle")
                : StationDisplayName(npc.Station);
            row.AddChild(new Label
            {
                Text              = $"{archetypeName} — {stationShort}",
                CustomMinimumSize = new Vector2(90f, 0f),
                Modulate          = new Color(0.75f, 0.75f, 0.75f)
            });

            var selectBtn = new Button { Text = isSelected ? Loc.T("assignment.selected") : Loc.T("assignment.select") };
            selectBtn.Pressed += () =>
            {
                _selectedNpcId = captured.Id;
                RefreshNpcList();
                RefreshAssignButton();
            };
            row.AddChild(selectBtn);

            // Unassign button — only shown when NPC is currently working.
            if (!string.IsNullOrEmpty(npc.Station))
            {
                var unassignBtn = new Button { Text = Loc.T("assignment.unassign") };
                unassignBtn.Pressed += () => SendUnassign(captured.Id);
                row.AddChild(unassignBtn);
            }

            _npcList.AddChild(row);
        }
    }

    private void RefreshStationList()
    {
        foreach (Node child in _stationList.GetChildren())
            child.QueueFree();

        var stations = GetWorkableStations();
        if (stations.Count == 0)
        {
            _stationList.AddChild(new Label
            {
                Text     = Loc.T("assignment.no_stations"),
                Modulate = new Color(0.7f, 0.7f, 0.7f)
            });
            return;
        }

        // Build a set of assigned stations from the roster so we can show current occupant.
        var assignedTo = new Dictionary<string, string>(); // stationNodeName → npcName
        foreach (var npc in ParseRoster())
            if (!string.IsNullOrEmpty(npc.Station))
                assignedTo[npc.Station] = npc.Name;

        foreach (var stationNode in stations)
        {
            bool isSelected = _selectedStation == stationNode;
            var row = new HBoxContainer();
            var captured = stationNode;

            var nameLabel = new Label
            {
                Text              = StationDisplayName(stationNode),
                CustomMinimumSize = new Vector2(110f, 0f),
                Modulate          = isSelected ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 1f, 1f)
            };
            row.AddChild(nameLabel);

            string occupant = assignedTo.TryGetValue(stationNode, out var who) ? who : "—";
            row.AddChild(new Label
            {
                Text    = occupant,
                Modulate = new Color(0.7f, 0.7f, 0.7f)
            });

            var selectBtn = new Button { Text = isSelected ? Loc.T("assignment.selected") : Loc.T("assignment.select") };
            selectBtn.Pressed += () =>
            {
                _selectedStation = captured;
                RefreshStationList();
                RefreshAssignButton();
            };
            row.AddChild(selectBtn);
            _stationList.AddChild(row);
        }
    }

    private void RefreshAssignButton()
    {
        bool ready = !string.IsNullOrEmpty(_selectedNpcId) &&
                     !string.IsNullOrEmpty(_selectedStation);
        _assignBtn.Disabled = !ready;
        _statusLabel.Text   = ready
            ? $"{NpcName(_selectedNpcId)} → {StationDisplayName(_selectedStation)}"
            : Loc.T("assignment.status_hint");
    }

    // ── RPC helpers ───────────────────────────────────────────────────────────

    private void OnAssignPressed()
    {
        if (string.IsNullOrEmpty(_selectedNpcId) || string.IsNullOrEmpty(_selectedStation)) return;

        var vs = GetNodeOrNull(VILLAGE_SYSTEM_PATH);
        if (vs == null) return;

        if (Multiplayer.IsServer())
            vs.Call("RequestAssignNpcToStation", _selectedNpcId, _selectedStation);
        else
            vs.RpcId(1, "RequestAssignNpcToStation", _selectedNpcId, _selectedStation);

        _selectedNpcId  = "";
        _selectedStation = "";
    }

    private void SendUnassign(string npcId)
    {
        var vs = GetNodeOrNull(VILLAGE_SYSTEM_PATH);
        if (vs == null) return;

        if (Multiplayer.IsServer())
            vs.Call("RequestUnassignNpc", npcId);
        else
            vs.RpcId(1, "RequestUnassignNpc", npcId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<NpcEntry> ParseRoster()
    {
        var list = new List<NpcEntry>();
        try
        {
            using var doc = JsonDocument.Parse(LocalState.VillageRosterJson);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new NpcEntry(
                    Id:          el.GetProperty("id").GetString() ?? "",
                    Name:        el.GetProperty("name").GetString() ?? "",
                    ArchetypeKey: el.GetProperty("archetypeKey").GetString() ?? "",
                    Station:     el.GetProperty("station").GetString() ?? ""
                ));
            }
        }
        catch { /* malformed json — return empty */ }
        return list;
    }

    private List<string> GetWorkableStations()
    {
        var result = new List<string>();
        var settlement = GetNodeOrNull(SETTLEMENT_SYSTEM_PATH);
        if (settlement == null) return result;

        foreach (Node child in settlement.GetChildren())
        {
            var name = child.Name.ToString();
            foreach (var prefix in WORKABLE_PREFIXES)
            {
                if (name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    result.Add(name);
                    break;
                }
            }
        }
        result.Sort(System.StringComparer.Ordinal);
        return result;
    }

    private static string StationDisplayName(string nodeName)
    {
        // "building_herbalists_hut_10_20" → "Herbalist's Hut"
        // "building_woodcutters_post_5_5" → "Woodcutter's Post"
        // Note: Godot normalises dots to underscores in node names.
        if (nodeName.StartsWith("building_herbalists_hut",  System.StringComparison.Ordinal)) return "Herbalist's Hut";
        if (nodeName.StartsWith("building_woodcutters_post", System.StringComparison.Ordinal)) return "Woodcutter's Post";
        return nodeName;
    }

    private string NpcName(string npcId)
    {
        foreach (var npc in ParseRoster())
            if (npc.Id == npcId) return npc.Name;
        return npcId;
    }
}
