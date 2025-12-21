using System.Reflection;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using Expressive;
using Expressive.Exceptions;
using C3;
using CubeInterOp;

namespace FoundryCommands
{

    [AddSystemToGameClient]
    public class FoundryCommandsSystem : SystemManager.System
    {
        public static FoundryCommandsSystem Instance { get; private set; }

        public override void OnAddedToWorld()
        {
            Instance = this;

            _dumpFolder = Path.Combine(Application.persistentDataPath, "FoundryCommands");

            SetupCommands();
        }

        public override void OnRemovedFromWorld()
        {
            Instance = null;
        }

        [EventHandler]
        private void Update(OnUpdate evt)
        {
            if (_monitorActive && Time.time > _monitorTime)
            {
                _monitorTime += _monitorInterval;

                var delta = 0.0f;
                var deltaTotal = 0.0f;
                var unit = "L";
                switch (_monitorType)
                {
                    case BuildableObjectTemplate.BuildableObjectType.Storage:
                        {
                            ulong inventoryId = 0UL;
                            if (BuildingManager.buildingManager_getInventoryAccessors(_monitorEntityId, 0U, ref inventoryId) == IOBool.iofalse) return;

                            var inventoryPtr = InventoryManager.inventoryManager_getInventoryPtr(inventoryId);

                            _monitorActive = true;
                            _monitorType = BuildableObjectTemplate.BuildableObjectType.Storage;

                            var slotCount = InventoryManager.inventoryManager_getInventorySlotCountByPtr(inventoryPtr);
                            uint totalItemCount = 0;
                            ushort itemTemplateRunningIdx = 0;
                            ushort lockedTemplateRunningIdx = 0;
                            uint itemCount = 0;
                            IOBool isLocked = IOBool.iofalse;
                            for (uint i = 0; i < slotCount; i++)
                            {
                                InventoryManager.inventoryManager_getSingleSlotDataByPtr(inventoryPtr, i, ref itemTemplateRunningIdx, ref itemCount, ref lockedTemplateRunningIdx, ref isLocked, IOBool.iofalse);
                                totalItemCount += itemCount;
                            }
                            delta = totalItemCount - _monitorContent;
                            _monitorContent = totalItemCount;
                            deltaTotal = totalItemCount - _monitorContentStart;
                            unit = string.Empty;
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.Tank:
                        {
                            var data = new TankPollingUpdateData();
                            TankGO.tankEntity_queryPollingData(_monitorEntityId, ref data);
                            delta = data.content_l_fpm - _monitorContent;
                            _monitorContent = data.content_l_fpm / (float)FixedPointMath.FPM_BASE;
                            deltaTotal = data.content_l_fpm / (float)FixedPointMath.FPM_BASE - _monitorContentStart;
                        }
                        break;

                    case BuildableObjectTemplate.BuildableObjectType.ModularFluidTank:
                        {
                            var data = new ModularFluidTankPollingUpdateData();
                            ModularFluidTankBaseGO.modularFluidTankEntity_queryPollingData(_monitorEntityId, ref data);
                            delta = data.fbData.content_l_fpm / (float)FixedPointMath.FPM_BASE - _monitorContent;
                            _monitorContent = data.fbData.content_l_fpm / (float)FixedPointMath.FPM_BASE;
                            deltaTotal = data.fbData.content_l_fpm / (float)FixedPointMath.FPM_BASE - _monitorContentStart;
                        }
                        break;
                }

                var timeTotal = Time.time - _monitorTimeStart;

                ChatFrame.addMessage($"Monitor: {delta / _monitorInterval:+0.##;-0.##;0}{unit}/s  [{60.0f * deltaTotal / timeTotal:+0.##;-0.##;0}{unit}/m]  ({Mathf.RoundToInt(_monitorContent)}{unit})", 0);
            }
        }

        public bool TryProcessCommand(string message, Character clientCharacter)
        {
            foreach (var handler in _commandHandlers)
            {
                if (handler.TryProcessCommand(message))
                {
                    ChatFrame.hideMessageBox();
                    return true;
                }
            }

            return false;
        }

        private Vector3 _lastPositionAtTeleport = Vector3.zero;
        private bool _hasTeleported = false;

        private CommandHandler[] _commandHandlers;

        private Dictionary<string, object> _calculatorVariables = new Dictionary<string, object>()
        {
            { "result", 0 }
        };

        private ulong _monitorEntityId = 0;
        private bool _monitorActive = false;
        private float _monitorTime = 0.0f;
        private float _monitorTimeStart = 0.0f;
        private float _monitorInterval = 1.0f;
        private float _monitorContent = 0.0f;
        private float _monitorContentStart = 0.0f;
        private BuildableObjectTemplate.BuildableObjectType _monitorType = BuildableObjectTemplate.BuildableObjectType.Tank;
        private ItemBufferPollingUpdateData[] _monitorItemBufferPollingUpdateData = new ItemBufferPollingUpdateData[64];

        private string _dumpFolder;

        private static readonly FieldInfo _timeInTicks = typeof(GameRoot).GetField("timeInTicks", BindingFlags.NonPublic | BindingFlags.Instance);

        private const ulong TICKS_PER_DAY = GameRoot.TIME_SYSTEM_TICKS_PER_DAY;
        private const ulong TICKS_PER_HOUR = GameRoot.TIME_SYSTEM_TICKS_PER_DAY / 24UL;
        private const ulong TICKS_PER_MINUTE = TICKS_PER_HOUR / 60UL;
        private Vector2Int TicksToTime(ulong ticks)
        {
            var hours = ticks / TICKS_PER_HOUR % 24UL;
            var minutes = ticks / TICKS_PER_MINUTE % 60UL;
            return new Vector2Int((int)hours, (int)minutes);
        }

        private ulong TimeToTicks(int hours, int minutes)
        {
            return (ulong)hours * TICKS_PER_HOUR + (ulong)minutes * TICKS_PER_MINUTE;
        }

        private void SetupCommands()
        {
            _commandHandlers = new CommandHandler[]
            {
                // help
                new CommandHandler(@"^\/help(?:(?:\s+)(.+))?$", (string[] arguments) => {

                    if (arguments.Length > 0)
                    {
                        var commandToFind = arguments[0].Trim();
                        foreach (var command in _commandHandlers)
                        {
                            if (command.helpNames.Contains(commandToFind))
                            {
                                ChatFrame.addMessage(command.helpText, 0);
                                return;
                            }
                        }

                        ChatFrame.addMessage($"Unknown command: {commandToFind}", 0);
                        return;
                    }
                    else
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var command in _commandHandlers)
                        {
                            if (command.helpNames.Length == 0) continue;
                            if (sb.Length > 0) sb.Append(", ");
                            sb.Append(string.Join("|", command.helpNames));
                        }
                        ChatFrame.addMessage("Available commands: "+sb.ToString(), 0);
                        ChatFrame.addMessage("use <b>/help</b> <i>command</i> for more info", 0);
                    }
                }),

                // drag
                new CommandHandler(@"^\/drag\s*?(?:\s+(\d+(?:\.\d*)?))?$", (string[] arguments) => {
                    switch(arguments.Length)
                    {
                        case 1:
                            var range = float.Parse(arguments[0]);
                            if(range < 38.0f) range = 38.0f;
                            Config.dragRange.value = range;
                            ChatFrame.addMessage($"Drag scale set to {range}.", 0);
                            break;

                        default:
                            ChatFrame.addMessage("Usage: <b>/drag</b> <i>range</i>", 0);
                            return;
                    }
                })
                .SetHelpNames("drag")
                .SetHelpText("<b>/drag</b> <i>range</i>\nSet the maximum range for drag building.\nDefault: 38.0"),

                // teleport
                new CommandHandler(@"^\/(?:(?:tp)|(?:teleport))(?:\s+(.*?)\s*)?$", (string[] arguments) => {
                    if (arguments.Length == 0 || arguments[0].Length == 0)
                    {
                        ChatFrame.addMessage("Usage: <b>/tp</b> <i>waypoint-name</i>", 0);
                        return;
                    }

                    var wpName = arguments[0];

                    var character = GameRoot.getClientCharacter();
                    if (character == null)
                    {
                        ChatFrame.addMessage("Client character not found.", 0);
                        return;
                    }

                    foreach (var wp in character.getWaypointDict().Values)
                    {
                        if (wp.description.ToLower() == wpName.ToLower())
                        {
                            ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wp.waypointPosition.x,wp.waypointPosition.y,wp.waypointPosition.z,out ulong cidx,out uint tidx);
                            var chunk = ChunkManager.getChunkByWorldCoords(wp.waypointPosition.x, wp.waypointPosition.z);
                            if(chunk != null)
                            {
                                _lastPositionAtTeleport = character.position;
                                _hasTeleported = true;
                                GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, string.Format("Teleporting to '{0}' at {1}, {2}, {3}", wp.description, wp.waypointPosition.x.ToString(), wp.waypointPosition.y.ToString(), wp.waypointPosition.z.ToString()), 0, false));
                                GameRoot.addLockstepEvent(new Character.CharacterRelocateEvent(character.usernameHash, wp.waypointPosition.x, wp.waypointPosition.y + 0.5f, wp.waypointPosition.z, wp.waypointPosition.x * FixedPointMath.FPM_BASE, (long)((wp.waypointPosition.y + 0.5f) * FixedPointMath.FPM_BASE), wp.waypointPosition.z * FixedPointMath.FPM_BASE));
                            }
                            else
                            {
                                GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, "Ungenerated chunk.", 0, false));
                                ChunkManager.generateNewChunksBasedOnPosition(wp.waypointPosition, ChunkManager._getChunkLoadDistance());
                            }
                            return;
                        }
                    }

                    ChatFrame.addMessage("Waypoint not found.", 0);
                })
                .SetHelpNames("tp", "teleport")
                .SetHelpText("<b>/tp</b> <i>waypoint-name</i>\n<b>/teleport</b> <i>waypoint-name</i>\nTeleport to a waypoint."),

                // return
                new CommandHandler(@"^\/(?:(?:tpr)|(?:ret)|(?:return))$", (string[] arguments) => {
                    if (!_hasTeleported)
                    {
                        ChatFrame.addMessage("No return point found.", 0);
                        return;
                    }

                    var character = GameRoot.getClientCharacter();
                    if (character == null)
                    {
                        ChatFrame.addMessage("Client character not found.", 0);
                        return;
                    }

                    var targetCube = new Vector3Int(
                        Mathf.FloorToInt(_lastPositionAtTeleport.x),
                        Mathf.FloorToInt(_lastPositionAtTeleport.y),
                        Mathf.FloorToInt(_lastPositionAtTeleport.z)
                        );

                    ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(targetCube.x,targetCube.y,targetCube.z,out ulong cidx,out uint tidx);
                    var chunk = ChunkManager.getChunkByWorldCoords(targetCube.x, targetCube.z);
                    if(chunk != null)
                    {
                        GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, $"Returning to {targetCube.x}, {targetCube.y}, {targetCube.z}", 0, false));
                        GameRoot.addLockstepEvent(new Character.CharacterRelocateEvent(character.usernameHash, _lastPositionAtTeleport.x, _lastPositionAtTeleport.y, _lastPositionAtTeleport.z, (long)(_lastPositionAtTeleport.x * FixedPointMath.FPM_BASE), (long)(_lastPositionAtTeleport.y * FixedPointMath.FPM_BASE), (long)(_lastPositionAtTeleport.z * FixedPointMath.FPM_BASE)));
                        _lastPositionAtTeleport = character.position;
                    }
                    else
                    {
                        ChatFrame.addMessage("Ungenerated chunk.", 0);
                        ChunkManager.generateNewChunksBasedOnPosition(_lastPositionAtTeleport, ChunkManager._getChunkLoadDistance());
                    }
                })
                .SetHelpNames("tpr", "ret", "return")
                .SetHelpText("<b>/tpr</b>\n<b>/ret</b>\n<b>/return</b>\nReturn to position at last telport."),

                // monitor
                new CommandHandler(@"^\/(?:(?:monitor)|(?:mon))\s*?(?:\s+(\d+(?:\.\d*)?))?$", (string[] arguments) => {
                    var renderCharacter = GameRoot.getClientRenderCharacter();
                    if (renderCharacter == null) return;

                    _monitorActive = false;

                    var hit = renderCharacter.getDemolitionInteractionTarget(
                        100.0f,
                        out var bogo,
                        out var entityId,
                        out var bpIdx,
                        out var hitDecor,
                        out var hitTerrain,
                        out var bot,
                        out var isPlaceholder,
                        out var isTrainPlaceholder,
                        out var placeholderId,
                        out var powerlineGO,
                        out var drillBotGO);
                    if (!hit || bogo == null || isPlaceholder) return;

                    var interval = 1.0f;
                    try {
                        if (arguments.Length > 0) interval = Convert.ToSingle(arguments[0], System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch { }

                    _monitorInterval = interval;
                    _monitorEntityId = entityId;
                    _monitorTimeStart = Time.time;
                    _monitorTime = _monitorTimeStart + _monitorInterval;

                    if (bogo is TankGO)
                    {
                        var data = new TankPollingUpdateData();
                        if (TankGO.tankEntity_queryPollingData(_monitorEntityId, ref data) == IOBool.iofalse) return;

                        _monitorActive = true;
                        _monitorType = BuildableObjectTemplate.BuildableObjectType.Tank;
                        _monitorContentStart = _monitorContent = data.content_l_fpm /(float) FixedPointMath.FPM_BASE;
                    }
                    else if (bogo is ChestGO)
                    {
                        ulong inventoryId = 0UL;
                        if (BuildingManager.buildingManager_getInventoryAccessors(_monitorEntityId, 0U, ref inventoryId) == IOBool.iofalse) return;

                        var inventoryPtr = InventoryManager.inventoryManager_getInventoryPtr(inventoryId);

                        _monitorActive = true;
                        _monitorType = BuildableObjectTemplate.BuildableObjectType.Storage;

                        var slotCount = InventoryManager.inventoryManager_getInventorySlotCountByPtr(inventoryPtr);
                        uint totalItemCount = 0;
                        ushort itemTemplateRunningIdx = 0;
                        ushort lockedTemplateRunningIdx = 0;
                        uint itemCount = 0;
                        IOBool isLocked = IOBool.iofalse;
                        for (uint i = 0; i < slotCount; i++)
                        {
                            InventoryManager.inventoryManager_getSingleSlotDataByPtr(inventoryPtr, i, ref itemTemplateRunningIdx, ref itemCount, ref lockedTemplateRunningIdx, ref isLocked, IOBool.iofalse);
                            totalItemCount += itemCount;
                        }
                        _monitorContentStart = _monitorContent = totalItemCount;
                    }
                    else if (bogo is ModularFluidTankBaseGO)
                    {
                        var data = new ModularFluidTankPollingUpdateData();
                        if (ModularFluidTankBaseGO.modularFluidTankEntity_queryPollingData(_monitorEntityId, ref data) == IOBool.iofalse) return;

                        _monitorActive = true;
                        _monitorType = BuildableObjectTemplate.BuildableObjectType.ModularFluidTank;
                        _monitorContentStart = _monitorContent = data.fbData.content_l_fpm /(float) FixedPointMath.FPM_BASE;
                    }
                })
                .SetHelpNames("monitor", "mon")
                .SetHelpText("<b>/monitor</b> <i>interval</i>\n<b>/mon</b> <i>interval</i>\nMonitor contents of targetted container/tank."),

                // skyPlatform
                new CommandHandler(@"^\/(?:(?:skyPlatform)|(?:sp))$", (string[] arguments) => {
                    SkyPlatformRoot.showFrame(0);
                })
                .SetHelpNames("skyPlatform", "sp")
                .SetHelpText("<b>/skyPlatform</b>\n<b>/sp</b>\nShow sky platform frame."),

                // time
                new CommandHandler(@"^\/time$", (string[] arguments) => {
                    var gameRoot = GameRoot.getSingleton();
                    if (gameRoot == null) return;

                    var time = TicksToTime((ulong)_timeInTicks.GetValue(gameRoot));

                    ChatFrame.addMessage($"Current time is {time.x}:{time.y:00}.", 0);
                }),
                new CommandHandler(@"^\/time\s+([012]?\d)(?:\:(\d\d))?$", (string[] arguments) => {
                    var gameRoot = GameRoot.getSingleton();
                    if (gameRoot == null) return;

                    var hours = Convert.ToInt32(arguments[0]);
                    var minutes = arguments.Length > 1 && !string.IsNullOrWhiteSpace(arguments[1]) ? Convert.ToInt32(arguments[1]) : 0;
                    var targetTicks = TimeToTicks(hours, minutes);
                    var currentTicks = (ulong)_timeInTicks.GetValue(gameRoot);
                    var deltaTicks = (targetTicks + TICKS_PER_DAY - (currentTicks % TICKS_PER_DAY)) % TICKS_PER_DAY;
                    GameRoot.addLockstepEvent(new GameRoot.DebugAdvanceTimeEvent(deltaTicks));


                    var message = $"Setting time to {hours}:{minutes:00}.";
                    var character = GameRoot.getClientCharacter();
                    if (character == null)
                    {
                        ChatFrame.addMessage(message, 0);
                    }
                    else
                    {
                        GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, message, 0, false));
                    }
                })
                .SetHelpNames("time")
                .SetHelpText("<b>/time</b>\n<b>/time</b> <i>HH</i>\n<b>/time</b> <i>HH:MM</i>\nSet or show the current time."),

                // calc
                new CommandHandler(@"^\/(?:(?:c)|(?:calc)|(?:calculate))\s+(.+)$", (string[] arguments) => {
                    try {

                        var regex = new Regex(@"(?!<\w)(?:(?:result)|(?:res)|(?:r))(?!\w)", RegexOptions.IgnoreCase);
                        var expression = new Expression(
                            regex.Replace(arguments[0].Trim(), @"[result]"),
                            ExpressiveOptions.IgnoreCaseForParsing | ExpressiveOptions.NoCache);
                        var result = expression.Evaluate(_calculatorVariables);
                        _calculatorVariables["result"] = result;
                        ChatFrame.addMessage($"{arguments[0]} = {result}", 0);
                    }
                    catch(ExpressiveException e)
                    {
                        ChatFrame.addMessage($"Error: {e.Message}", 0);
                    }
                })
                .SetHelpNames("calc", "calculate")
                .SetHelpText("<b>/calc</b> <i>expression</i>\n<b>/c</b> <i>expression</i>\nEvaluate a mathematical expression."),

                // count
                new CommandHandler(@"^\/count$", (string[] arguments) => {
                    if (!Directory.Exists(_dumpFolder)) Directory.CreateDirectory(_dumpFolder);

                    var buildings = StreamingSystem.getBuildableObjectTable();
                    var counts = new Dictionary<ItemTemplate, int>();
                    foreach(var building in buildings)
                    {
                        if (building.Value != null
                            && building.Value.template != null
                            && building.Value.template.type != BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble
                            && building.Value.template.parentItemTemplate != null)
                        {
                            var template = building.Value.template.parentItemTemplate;
                            counts.TryGetValue(template, out var currentCount);
                            counts[template] = currentCount + 1;
                        }
                    }

                    var f = new StreamWriter(Path.Combine(_dumpFolder, "count.txt"), false);
                    foreach (var kv in counts.OrderBy(x => x.Key.name))
                    {
                        f.WriteLine($"{kv.Key.name}: {kv.Value}");
                    }
                    f.Close();

                    ChatFrame.addMessage($"Counts saved to {_dumpFolder}\\count.txt", 0);
                    ChatFrame.addMessage($"Total: {buildings.Count}", 0);
                }),

                // give
                new CommandHandler(@"^\/give(?:\s+(.+?)(?:\s+(\d+))?)?$", (string[] arguments) => {
                    void GiveItem(ItemTemplate item, uint amount)
                    {
                        if (amount == 0) amount = item.stackSize;
                        else if(GameRoot.IsMultiplayerEnabled)
                        {
                            ChatFrame.addMessage("<b>WARNING:</b> Count parameter not supported in multiplayer. Giving 1 stack.", 0);
                        }

                        var character = GameRoot.getClientCharacter();
                        if(character == null)
                        {
                            ChatFrame.addMessage("<b>ERROR:</b> Client character not found!", 0);
                            return;
                        }
                        if (GameRoot.IsMultiplayerEnabled)
                        {
                            GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, string.Format("Spawning {0} of {1}", item.stackSize, item.name), 0, false));
                            GameRoot.addLockstepEvent(new DebugItemSpawnEvent(character.usernameHash, item.id));
                        }
                        else
                        {
                            GameRoot.addLockstepEvent(new GameRoot.ChatMessageEvent(character.usernameHash, string.Format("Spawning {0} of {1}", amount, item.name), 0, false));
                            InventoryManager.inventoryManager_tryAddItemAtAnyPosition(character.inventoryId, item.id, amount, IOBool.iofalse);
                        }
                    }

                    uint count = 0;
                    switch(arguments.Length)
                    {
                        case 1:
                            var name = arguments[0].ToLower();
                            List<ItemTemplate> foundItems = new List<ItemTemplate>();
                            bool foundExact = false;
                            foreach(var item in ItemTemplateManager.getAllItemTemplates().Values)
                            {
                                if(item.identifier.ToLower() == name || item.name.ToLower() == name)
                                {
                                    GiveItem(item, count);
                                    foundExact = true;
                                    break;
                                }
                                else if(item.identifier.ToLower().Contains(name) || item.name.ToLower().Contains(name))
                                {
                                    foundItems.Add(item);
                                }
                            }
                            if(foundExact) break;
                            switch(foundItems.Count)
                            {
                                case 0: ChatFrame.addMessage("Found no matching item template", 0); break;
                                case 1: GiveItem(foundItems[0], count); break;
                                default:
                                    ChatFrame.addMessage("Found multiple matches:", 0);
                                    foreach(var item in foundItems)
                                    {
                                        ChatFrame.addMessage($"name: {item.name}    ident: {item.identifier}", 0);
                                    }
                                    break;
                            }
                            break;

                        case 2:
                            count = uint.Parse(arguments[1]);
                            goto case 1;

                        default:
                            ChatFrame.addMessage("Usage: <b>/give</b> <i>name</i> <i>amount</i>", 0);
                            break;
                    }
                })
                .SetHelpNames("give")
                .SetHelpText("<b>/give</b> <i>name</i>\n<b>/give</b> <i>name</i> <i>amount</i>\nAdd one stack or a specific number of items to player inventory.")
            };
        }
    }

}


