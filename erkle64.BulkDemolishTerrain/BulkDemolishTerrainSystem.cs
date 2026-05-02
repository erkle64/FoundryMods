using C3.ModKit;
using HarmonyLib;
using Unfoundry;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;
using C3;

namespace BulkDemolishTerrain
{
    [FoundryRPC]
    [AddSystemToGameSimulation]
    public class BulkDemolishTerrainSystem : SystemManager.System
    {
        public static LogSource log = new LogSource("BulkDemolishTerrain");

        private static readonly Queue<Vector3Int> _queuedTerrainRemovals = new Queue<Vector3Int>();
        private static float _lastTerrainRemovalUpdate = 0.0f;

        private static List<bool> shouldRemove = null;
        private static List<bool> isOre = null;
        private static int bedrockByteIdx = 0;

        private static bool _confirmationFrameOpen = false;

        private static CustomRadialMenuStateControl _radialMenuStateControl = null;

        public enum TerrainMode
        {
            Collect,
            Destroy,
            Ignore,
            CollectTerrainOnly,
            DestroyTerrainOnly,
            LiquidOnly
        }

        public override void OnAddedToWorld()
        {
            if (!Config.General.removeLiquids.value && Config.Modes.currentTerrainMode.value == TerrainMode.LiquidOnly)
            {
                Config.Modes.currentTerrainMode.value = TerrainMode.Collect;
            }

            Config.General.playerPlacedOnly.onValueChanged += OnPlayerPlacedOnlyChanged;

            _radialMenuStateControl = new(
                new CustomRadialMenuOption(
                    "Collect Terrain",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.Unfoundry/Bundled/UI/download.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.Collect
                ),
                new CustomRadialMenuOption(
                    "Destroy Terrain",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.BulkDemolishTerrain/Sprites/destroy_terrain.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.Destroy
                ),
                new CustomRadialMenuOption(
                    "Ignore Terrain",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.Unfoundry/Bundled/UI/icons8-error-100.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.Ignore
                ),
                new CustomRadialMenuOption(
                    "Collect Terrain Only",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.BulkDemolishTerrain/Sprites/collect_terrain_only.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.CollectTerrainOnly
                ),
                new CustomRadialMenuOption(
                    "Destroy Terrain Only",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.BulkDemolishTerrain/Sprites/destroy_terrain_only.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.DestroyTerrainOnly
                ),
                new CustomRadialMenuOption(
                    "Liquid Only",
                    AssetManager.Database.LoadAssetAtPath<Sprite>("Assets/erkle64.BulkDemolishTerrain/Sprites/liquid_only.png"),
                    () => Config.Modes.currentTerrainMode.value = TerrainMode.LiquidOnly
                )
            );

            Messenger.RegisterListener<BulkDemolishTerrainDestroyRequest>("BulkDemolishTerrain_Destroy", "BulkDemolishTerrain", ApplyDestroyTerrainRequest);
        }

        public override void OnRemovedFromWorld()
        {
            _radialMenuStateControl = null;

            Config.General.playerPlacedOnly.onValueChanged -= OnPlayerPlacedOnlyChanged;
        }

        bool _hasNotifiedPresence = false;
        [EventHandler]
        public void OnUpdate(OnUpdate _)
        {
            if (_hasNotifiedPresence)
                return;

            _hasNotifiedPresence = true;
            Messenger.Send("BulkDemolishTerrain_Running", true);
        }

        [Serializable]
        private struct BulkDemolishTerrainDestroyRequest
        {
            public int worldPosX;
            public int worldPosY;
            public int worldPosZ;
            public int sizeX;
            public int sizeY;
            public int sizeZ;
            public bool destroyTerrain;
            public bool destroyDecor;
        }
        private void ApplyDestroyTerrainRequest(BulkDemolishTerrainDestroyRequest request)
        {
            RunDemolition(
                new Vector3Int(request.worldPosX, request.worldPosY, request.worldPosZ),
                new Vector3Int(request.sizeX, request.sizeY, request.sizeZ),
                true,
                request.destroyTerrain,
                request.destroyDecor,
                Config.General.removeLiquids.value && request.destroyTerrain
            );
        }

        private void OnPlayerPlacedOnlyChanged(bool value)
        {
            shouldRemove = null;
        }

        [FoundryRPC]
        public static void DestroyTerrainRPC(int worldPosX, int worldPosY, int worldPosZ)
        {
            try
            {
                ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPosX, worldPosY, worldPosZ, out ulong chunkIndex, out uint blockIndex);

                byte terrainType = 0;
                ChunkManager.chunks_removeTerrainBlock(chunkIndex, blockIndex, ref terrainType);
            }
            catch(Exception ex)
            {
                log.LogWarning(ex.ToString());
            }
        }

        [FoundryRPC]
        public static void DestroyBuildingRPC(ulong entityId)
        {
            try
            {
                if (BuildingManager.buildingManager_getEntityPtr(entityId) != 0UL)
                {
                    BuildingManager.buildingManager_demolishBuildingEntityForDynamite(entityId);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex.ToString());
            }
        }

        private static void GenerateShouldRemoveArray(bool force)
        {
            var miningLevel = ResearchSystem.getUnlockedMiningHardnessLevel();
            if (force || shouldRemove == null)
            {
                var terrainTypes = ItemTemplateManager.getAllTerrainTemplates();

                shouldRemove = new List<bool>
                        {
                            false, // Air
                            false  // ???
                        };

                if (Config.General.playerPlacedOnly.value)
                {
                    foreach (var terrainType in terrainTypes)
                    {
                        shouldRemove.Add(
                            terrainType.Value.destructible
                            && terrainType.Value.yieldItemOnDig_template != null
                            && terrainType.Value.yieldItemOnDig_template.buildableObjectTemplate != null
                            && terrainType.Value.parentBOT != null
                            && (Config.General.ignoreMiningLevel.value || terrainType.Value.requiredMiningHardnessLevel <= miningLevel));
                    }
                }
                else
                {
                    foreach (var terrainType in terrainTypes)
                    {
                        shouldRemove.Add(
                            terrainType.Value.destructible
                            && (Config.General.ignoreMiningLevel.value || terrainType.Value.requiredMiningHardnessLevel <= miningLevel));
                    }
                }
            }
        }

        private static void GenerateIsOreArray()
        {
            var miningLevel = ResearchSystem.getUnlockedMiningHardnessLevel();
            if (isOre == null)
            {
                var terrainTypes = ItemTemplateManager.getAllTerrainTemplates();

                isOre = new List<bool>
                        {
                            false, // Air
                            false  // ???
                        };

                foreach (var terrainType in terrainTypes)
                {
                    isOre.Add(
                        terrainType.Value.flags.HasFlagNonAlloc(TerrainBlockType.TerrainTypeFlags.Ore)
                        || terrainType.Value.flags.HasFlagNonAlloc(TerrainBlockType.TerrainTypeFlags.OreVeinMineable)
                        || terrainType.Value.flags.HasFlagNonAlloc(TerrainBlockType.TerrainTypeFlags.OreVeinCore)
                        || terrainType.Value.flags.HasFlagNonAlloc(TerrainBlockType.TerrainTypeFlags.OreVeinExterior));
                }
            }
        }

        private static void ShowModeMenu()
        {
            CustomRadialMenuSystem.Instance.ShowMenu(_radialMenuStateControl.GetMenuOptions());
        }

        private static void RunDemolition(Vector3Int pos, Vector3Int size, bool useDestroyMode, bool demolishTerrain, bool demolishDecor, bool demolishLiquids)
        {
            var clientCharacter = GameRoot.getClientCharacter();
            if (clientCharacter == null) return;

            var characterHash = clientCharacter.usernameHash;

            var removeBedrock = Config.General.allowRemoveBedrock.value;

            if (removeBedrock)
            {
                if (bedrockByteIdx == 0)
                {
                    bedrockByteIdx = GameRoot.TerrainIdxLookupTable.getKeyByValue(TerrainBlockType.generateStringHash("_base_bedrock"));
                    log.Log($"Bedrock Byte Index: {bedrockByteIdx}");
                }

                if (pos.y < 1)
                {
                    size.y += pos.y - 1;
                    pos.y = 1;
                }
            }
            else
            {
                if (pos.y < 2)
                {
                    size.y += pos.y - 2;
                    pos.y = 2;
                }
            }
            if (pos.y + size.y >= Chunk.CHUNKSIZE_Y)
            {
                size.y = Chunk.CHUNKSIZE_Y - pos.y;
            }

            if (demolishTerrain || demolishDecor)
            {
                GenerateShouldRemoveArray(false);
                GenerateIsOreArray();

                AABB3D aabb = new(pos.x, pos.y, pos.z, size.x, size.y, size.z);
                if (demolishDecor)
                {
                    using (var query = StreamingSystem.get().queryAABB3D(aabb))
                    {
                        foreach (var bogo in query)
                        {
                            if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble)
                            {
                                if (useDestroyMode)
                                {
                                    ActionManager.AddQueuedEvent(() => Rpc.Lockstep.Run(DestroyBuildingRPC, bogo.relatedEntityId));
                                }
                                else
                                {
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveWorldDecorEvent(characterHash, bogo.relatedEntityId, 0)));
                                }
                            }
                        }
                    }
                }

                if (demolishTerrain)
                {
                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x, pos.z, out var fromChunkX, out var fromChunkZ);
                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x + size.x - 1, pos.z + size.z - 1, out var toChunkX, out var toChunkZ);
                    var hasOre = false;
                    var hasNonOre = false;
                    for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                    {
                        for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                        {
                            var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                            var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                            var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                            var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                            var fromX = Mathf.Max(pos.x, chunkFromX);
                            var fromZ = Mathf.Max(pos.z, chunkFromZ);
                            var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                            var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                            for (int z = fromZ; z <= toZ; ++z)
                            {
                                for (int y = 0; y < size.y; ++y)
                                {
                                    for (int x = fromX; x <= toX; ++x)
                                    {
                                        var coords = new Vector3Int(x, pos.y + y, z);
                                        var terrainData = ChunkManager.getTerrainDataForWorldCoord(coords, out Chunk _, out uint _);
                                        if (removeBedrock && terrainData == bedrockByteIdx)
                                        {
                                            ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                        }
                                        else if (terrainData < shouldRemove.Count && shouldRemove[terrainData])
                                        {
                                            if (useDestroyMode)
                                            {
                                                ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                            }
                                            else
                                            {
                                                ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveTerrainEvent(characterHash, coords, 0, false)));
                                            }
                                        }
                                        if (terrainData < isOre.Count && isOre[terrainData])
                                        {
                                            hasOre = true;
                                        }
                                        else if (terrainData > 1)
                                        {
                                            hasNonOre = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!_confirmationFrameOpen && useDestroyMode && !hasNonOre && hasOre)
                    {
                        _confirmationFrameOpen = true;
                        GlobalStateManager.addCursorRequirement();
                        ConfirmationFrame.Show("Destroy ore blocks?", () =>
                        {
                            GlobalStateManager.removeCursorRequirement();
                            for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                            {
                                for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                                {
                                    var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                                    var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                                    var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                                    var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                                    var fromX = Mathf.Max(pos.x, chunkFromX);
                                    var fromZ = Mathf.Max(pos.z, chunkFromZ);
                                    var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                                    var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                                    for (int z = fromZ; z <= toZ; ++z)
                                    {
                                        for (int y = 0; y < size.y; ++y)
                                        {
                                            for (int x = fromX; x <= toX; ++x)
                                            {
                                                var coords = new Vector3Int(x, pos.y + y, z);
                                                var terrainData = ChunkManager.getTerrainDataForWorldCoord(coords, out Chunk _, out uint _);
                                                if (terrainData < isOre.Count && isOre[terrainData])
                                                {
                                                    ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            _confirmationFrameOpen = false;
                        }, () =>
                        {
                            GlobalStateManager.removeCursorRequirement();
                            _confirmationFrameOpen = false;
                        });
                    }
                }
            }

            if (demolishLiquids)
            {
                ActionManager.AddQueuedEvent(() =>
                {
                    var liquidSystem = GameRoot.World.Systems.Get<LiquidSystem>();
                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x, pos.z, out var fromChunkX, out var fromChunkZ);
                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x + size.x - 1, pos.z + size.z - 1, out var toChunkX, out var toChunkZ);
                    for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                    {
                        for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                        {
                            var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                            var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                            var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                            var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                            var fromX = Mathf.Max(pos.x, chunkFromX);
                            var fromZ = Mathf.Max(pos.z, chunkFromZ);
                            var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                            var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                            var chunkIndex = ChunkManager.calculateChunkIdx(chunkX, chunkZ);
                            byte[] liquidAmounts = null;
                            if (liquidSystem.tryGetLiquidChunk(chunkIndex, out var liquidChunk))
                            {
                                liquidChunk.getDecompressedArrays(out var _, out liquidAmounts);
                            }

                            for (int z = fromZ; z <= toZ; ++z)
                            {
                                for (int y = 0; y < size.y; ++y)
                                {
                                    for (int x = fromX; x <= toX; ++x)
                                    {
                                        var coords = new Vector3Int(x, pos.y + y, z);
                                        if (liquidAmounts != null)
                                        {
                                            var tx = (uint)(coords.x - chunkX * Chunk.CHUNKSIZE_XZ);
                                            var ty = (uint)coords.y;
                                            var tz = (uint)(coords.z - chunkZ * Chunk.CHUNKSIZE_XZ);
                                            var terrainIndex = Chunk.getTerrainArrayIdx(tx, ty, tz);
                                            if (liquidAmounts[terrainIndex] > 0)
                                            {
                                                GameRoot.addLockstepEvent(new SetLiquidCellEvent(coords.x, coords.y, coords.z, 0, 0));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }

        [HarmonyPatch]
        public class Patch
        {
            private static bool _ignoreNextBulkDemolishToggle = false;

            [HarmonyPatch(typeof(GameRoot), "keyHandler_toggleBulkDemolitionMode")]
            [HarmonyPrefix]
            public static bool GameRoot_keyHandler_toggleBulkDemolitionMode()
            {
                if (_ignoreNextBulkDemolishToggle)
                {
                    _ignoreNextBulkDemolishToggle = false;
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(Character.BulkDemolishBuildingEvent), MethodType.Constructor, new Type[] { typeof(ulong), typeof(Vector3Int), typeof(Vector3Int) })]
            [HarmonyPostfix]
            public static void BulkDemolishBuildingEventConstructor(Character.BulkDemolishBuildingEvent __instance, Vector3Int demolitionAreaAABB_size)
            {
                var currentTerrainMode = Config.Modes.currentTerrainMode.value;
                switch (currentTerrainMode)
                {
                    case TerrainMode.CollectTerrainOnly:
                    case TerrainMode.DestroyTerrainOnly:
                    case TerrainMode.LiquidOnly:
                        __instance.demolitionAreaAABB_size = -demolitionAreaAABB_size;
                        break;
                }
            }

            [HarmonyPatch(typeof(Character.BulkDemolishBuildingEvent), nameof(Character.BulkDemolishBuildingEvent.processEvent))]
            [HarmonyPrefix]
            public static bool processBulkDemolishBuildingEventPrefix(Character.BulkDemolishBuildingEvent __instance)
            {
                if (__instance.demolitionAreaAABB_size.x < 0)
                {
                    __instance.demolitionAreaAABB_size = new Vector3Int(Mathf.Abs(__instance.demolitionAreaAABB_size.x), Mathf.Abs(__instance.demolitionAreaAABB_size.y), Mathf.Abs(__instance.demolitionAreaAABB_size.z));
                    //processBulkDemolishBuildingEvent(__instance);
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(Character.BulkDemolishBuildingEvent), nameof(Character.BulkDemolishBuildingEvent.processEvent))]
            [HarmonyPostfix]
            public static void processBulkDemolishBuildingEvent(Character.BulkDemolishBuildingEvent __instance)
            {
                if (GlobalStateManager.isDedicatedServer) return;

                log.Log($"processBulkDemolishBuildingEvent: {__instance.demolitionAreaAABB_pos} {__instance.demolitionAreaAABB_size}");

                var clientCharacter = GameRoot.getClientCharacter();
                if (clientCharacter == null) return;

                var clientCharacterHash = clientCharacter.usernameHash;

                ulong characterHash = __instance.characterHash;
                if (characterHash != clientCharacterHash) return;

                var currentTerrainMode = Config.Modes.currentTerrainMode.value;
                var pos = __instance.demolitionAreaAABB_pos;
                var size = __instance.demolitionAreaAABB_size;
                var removeBedrock = Config.General.allowRemoveBedrock.value;
                if (removeBedrock)
                {
                    if (bedrockByteIdx == 0)
                    {
                        bedrockByteIdx = GameRoot.TerrainIdxLookupTable.getKeyByValue(TerrainBlockType.generateStringHash("_base_bedrock"));
                        log.Log($"Bedrock Byte Index: {bedrockByteIdx}");
                    }

                    if (pos.y < 1)
                    {
                        size.y += pos.y - 1;
                        pos.y = 1;
                    }
                }
                else
                {
                    if (pos.y < 2)
                    {
                        size.y += pos.y - 2;
                        pos.y = 2;
                    }
                }
                if (pos.y + size.y >= Chunk.CHUNKSIZE_Y)
                {
                    size.y = Chunk.CHUNKSIZE_Y - pos.y;
                }
                if (currentTerrainMode != TerrainMode.Ignore && currentTerrainMode != TerrainMode.LiquidOnly)
                {
                    GenerateShouldRemoveArray(false);
                    GenerateIsOreArray();

                    var useDestroyMode = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    if (currentTerrainMode == TerrainMode.Destroy || currentTerrainMode == TerrainMode.DestroyTerrainOnly) useDestroyMode = !useDestroyMode;

                    //if (GameRoot.IsMultiplayerEnabled) useDestroyMode = false;

                    AABB3D aabb = new(pos.x, pos.y, pos.z, size.x, size.y, size.z);
                    using (var query = StreamingSystem.get().queryAABB3D(aabb))
                    {
                        foreach (var bogo in query)
                        {
                            if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble)
                            {
                                if (useDestroyMode)
                                {
                                    ActionManager.AddQueuedEvent(() => Rpc.Lockstep.Run(DestroyBuildingRPC, bogo.relatedEntityId));
                                }
                                else
                                {
                                    ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveWorldDecorEvent(characterHash, bogo.relatedEntityId, 0)));
                                }
                            }
                        }
                    }

                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x, pos.z, out var fromChunkX, out var fromChunkZ);
                    ChunkManager.getChunkCoordsFromWorldCoords(pos.x + size.x - 1, pos.z + size.z - 1, out var toChunkX, out var toChunkZ);
                    var hasOre = false;
                    var hasNonOre = false;
                    for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                    {
                        for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                        {
                            var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                            var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                            var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                            var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                            var fromX = Mathf.Max(pos.x, chunkFromX);
                            var fromZ = Mathf.Max(pos.z, chunkFromZ);
                            var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                            var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                            for (int z = fromZ; z <= toZ; ++z)
                            {
                                for (int y = 0; y < size.y; ++y)
                                {
                                    for (int x = fromX; x <= toX; ++x)
                                    {
                                        var coords = new Vector3Int(x, pos.y + y, z);
                                        var terrainData = ChunkManager.getTerrainDataForWorldCoord(coords, out Chunk _, out uint _);
                                        if (removeBedrock && terrainData == bedrockByteIdx)
                                        {
                                            ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                        }
                                        else if (terrainData < shouldRemove.Count && shouldRemove[terrainData])
                                        {
                                            if (useDestroyMode)
                                            {
                                                ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                            }
                                            else
                                            {
                                                ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveTerrainEvent(characterHash, coords, 0, false)));
                                            }
                                        }
                                        if (terrainData < isOre.Count && isOre[terrainData])
                                        {
                                            hasOre = true;
                                        }
                                        else if (terrainData > 1)
                                        {
                                            hasNonOre = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (!_confirmationFrameOpen && useDestroyMode && !hasNonOre && hasOre)
                    {
                        _confirmationFrameOpen = true;
                        GlobalStateManager.addCursorRequirement();
                        ConfirmationFrame.Show("Destroy ore blocks?", () =>
                        {
                            GlobalStateManager.removeCursorRequirement();
                            for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                            {
                                for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                                {
                                    var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                                    var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                                    var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                                    var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                                    var fromX = Mathf.Max(pos.x, chunkFromX);
                                    var fromZ = Mathf.Max(pos.z, chunkFromZ);
                                    var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                                    var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                                    for (int z = fromZ; z <= toZ; ++z)
                                    {
                                        for (int y = 0; y < size.y; ++y)
                                        {
                                            for (int x = fromX; x <= toX; ++x)
                                            {
                                                var coords = new Vector3Int(x, pos.y + y, z);
                                                var terrainData = ChunkManager.getTerrainDataForWorldCoord(coords, out Chunk _, out uint _);
                                                if (terrainData < isOre.Count && isOre[terrainData])
                                                {
                                                    ActionManager.AddQueuedEvent(() => _queuedTerrainRemovals.Enqueue(coords));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            _confirmationFrameOpen = false;
                        }, () =>
                        {
                            GlobalStateManager.removeCursorRequirement();
                            _confirmationFrameOpen = false;
                        });
                    }
                }

                if (Config.General.removeLiquids.value)
                {
                    ActionManager.AddQueuedEvent(() =>
                    {
                        var liquidSystem = GameRoot.World.Systems.Get<LiquidSystem>();
                        ChunkManager.getChunkCoordsFromWorldCoords(pos.x, pos.z, out var fromChunkX, out var fromChunkZ);
                        ChunkManager.getChunkCoordsFromWorldCoords(pos.x + size.x - 1, pos.z + size.z - 1, out var toChunkX, out var toChunkZ);
                        for (var chunkZ = fromChunkZ; chunkZ <= toChunkZ; chunkZ++)
                        {
                            for (var chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
                            {
                                var chunkFromX = chunkX * Chunk.CHUNKSIZE_XZ;
                                var chunkFromZ = chunkZ * Chunk.CHUNKSIZE_XZ;
                                var chunkToX = chunkFromX + Chunk.CHUNKSIZE_XZ - 1;
                                var chunkToZ = chunkFromZ + Chunk.CHUNKSIZE_XZ - 1;
                                var fromX = Mathf.Max(pos.x, chunkFromX);
                                var fromZ = Mathf.Max(pos.z, chunkFromZ);
                                var toX = Mathf.Min(pos.x + size.x - 1, chunkToX);
                                var toZ = Mathf.Min(pos.z + size.z - 1, chunkToZ);

                                var chunkIndex = ChunkManager.calculateChunkIdx(chunkX, chunkZ);
                                byte[] liquidAmounts = null;
                                if (liquidSystem.tryGetLiquidChunk(chunkIndex, out var liquidChunk))
                                {
                                    liquidChunk.getDecompressedArrays(out var _, out liquidAmounts);
                                }

                                for (int z = fromZ; z <= toZ; ++z)
                                {
                                    for (int y = 0; y < size.y; ++y)
                                    {
                                        for (int x = fromX; x <= toX; ++x)
                                        {
                                            var coords = new Vector3Int(x, pos.y + y, z);
                                            if (liquidAmounts != null)
                                            {
                                                var tx = (uint)(coords.x - chunkX * Chunk.CHUNKSIZE_XZ);
                                                var ty = (uint)coords.y;
                                                var tz = (uint)(coords.z - chunkZ * Chunk.CHUNKSIZE_XZ);
                                                var terrainIndex = Chunk.getTerrainArrayIdx(tx, ty, tz);
                                                if (liquidAmounts[terrainIndex] > 0)
                                                {
                                                    GameRoot.addLockstepEvent(new SetLiquidCellEvent(coords.x, coords.y, coords.z, 0, 0));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });
                }
            }

            [HarmonyPatch(typeof(GameRoot), "updateInternal")]
            [HarmonyPrefix]
            private static void GameRoot_updateInternal(GameRoot __instance)
            {
                if (!GameRoot.IsGameInitDone) return;
                if (GameRoot.ExitToMainMenuCalled)
                {
                    _queuedTerrainRemovals.Clear();
                    return;
                }

                if (!_queuedTerrainRemovals.TryPeek(out var _)) return;
                if (Time.time < _lastTerrainRemovalUpdate + 0.1f) return;
                _lastTerrainRemovalUpdate = Time.time;

                var clientCharacter = GameRoot.getClientCharacter();
                if (clientCharacter == null) return;

                var characterHash = clientCharacter.usernameHash;

                while (_queuedTerrainRemovals.TryPeek(out var coords))
                {
                    _queuedTerrainRemovals.Dequeue();

                    Rpc.Lockstep.Run(DestroyTerrainRPC, coords.x, coords.y, coords.z);
                }
            }

            [HarmonyPatch(typeof(ResearchSystem), "onResearchFinished")]
            [HarmonyPostfix]
            static void ResearchSystem_onResearchFinished(ResearchTemplate rt)
            {
                if (shouldRemove != null) GenerateShouldRemoveArray(true);
            }

            private static readonly FieldInfo _HandheldTabletHH_currentlySetMode = typeof(HandheldTabletHH).GetField("currentlySetMode", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo _GameRoot_bulkDemolitionState = typeof(GameRoot).GetField("bulkDemolitionState", BindingFlags.NonPublic | BindingFlags.Instance);
            [HarmonyPatch(typeof(HandheldTabletHH), nameof(HandheldTabletHH._updateBehavoir))]
            [HarmonyPostfix]
            private static void HandheldTabletHH_updateBehavoir()
            {
                var gameRoot = GameRoot.getSingleton();
                if (gameRoot == null) return;

                var clientCharacter = GameRoot.getClientCharacter();
                if (clientCharacter == null) return;

                var player0 = GlobalStateManager.getRewiredPlayer0();

                if (clientCharacter.clientData.isBulkDemolitionModeActive() && !GlobalStateManager.checkIfCursorIsRequired())
                {
                    var bulkDemolitionState = (int)_GameRoot_bulkDemolitionState.GetValue(GameRoot.getSingleton());
                    if (player0.GetButtonDown("Alternate Action") && bulkDemolitionState == 0)
                    {
                        _ignoreNextBulkDemolishToggle = true;
                        ShowModeMenu();
                    }

                    if (Input.GetKeyDown(Config.Input.changeModeKey.value))
                    {
                        switch (Config.Modes.currentTerrainMode.value)
                        {
                            case TerrainMode.Collect:
                                Config.Modes.currentTerrainMode.value = TerrainMode.Destroy;
                                break;

                            case TerrainMode.Destroy:
                                Config.Modes.currentTerrainMode.value = TerrainMode.Ignore;
                                break;

                            case TerrainMode.Ignore:
                                Config.Modes.currentTerrainMode.value = TerrainMode.CollectTerrainOnly;
                                break;

                            case TerrainMode.CollectTerrainOnly:
                                Config.Modes.currentTerrainMode.value = TerrainMode.DestroyTerrainOnly;
                                break;

                            case TerrainMode.DestroyTerrainOnly:
                                Config.Modes.currentTerrainMode.value = Config.General.removeLiquids.value ? TerrainMode.LiquidOnly : TerrainMode.Collect;
                                break;

                            case TerrainMode.LiquidOnly:
                                Config.Modes.currentTerrainMode.value = TerrainMode.Collect;
                                break;
                        }
                    }

                    var infoText = GameRoot.getSingleton().uiText_infoText.tmp.text;
                    if (!infoText.Contains("Terrain Mode:"))
                    {
                        switch (Config.Modes.currentTerrainMode.value)
                        {
                            case TerrainMode.Collect:
                            case TerrainMode.Destroy:
                            case TerrainMode.Ignore:
                                infoText += $"\nTerrain Mode: {Config.Modes.currentTerrainMode.value}.";
                                break;

                            case TerrainMode.CollectTerrainOnly:
                                infoText += $"\nTerrain Mode: Collect Terrain. Ignore Buildings.";
                                break;

                            case TerrainMode.DestroyTerrainOnly:
                                infoText += $"\nTerrain Mode: Destroy Terrain. Ignore Buildings.";
                                break;

                            case TerrainMode.LiquidOnly:
                                infoText += $"\nTerrain Mode: Liquid Only.";
                                break;
                        }

                        if (bulkDemolitionState == 2)
                        {
                            switch (Config.Modes.currentTerrainMode.value)
                            {
                                case TerrainMode.Collect:
                                case TerrainMode.CollectTerrainOnly:
                                    infoText += " Hold [ALT] to destroy.";
                                    break;

                                case TerrainMode.Destroy:
                                case TerrainMode.DestroyTerrainOnly:
                                    infoText += " Hold [ALT] to collect.";
                                    break;
                            }
                        }
                        GameRoot.setInfoText(infoText);
                    }
                }
            }
        }
    }
}
