using C3;
using MessagePack;
using System.Collections.Generic;
using System.Linq;
using Unfoundry;
using UnityEngine;

namespace VeinTools
{
    public class BlueprintRequest
    {
        public int positionX;
        public int positionY;
        public int positionZ;
        public int sizeX;
        public int sizeY;
        public int sizeZ;
        public Building[] buildings;
        public TrainTrack[] tracks;
        public byte[] blocks;

        public struct Building
        {
            public ulong templateId;
            public int anchorPositionX;
            public int anchorPositionY;
            public int anchorPositionZ;
            public BuildingManager.BuildOrientation orientationY;
            public float orientationUnlockedX;
            public float orientationUnlockedY;
            public float orientationUnlockedZ;
            public float orientationUnlockedW;
            public byte itemMode;
            public (string, string)[] customData;
        }

        public struct TrainTrack
        {
            public ulong templateId;
            public int anchorPositionX;
            public int anchorPositionY;
            public int anchorPositionZ;
            public int orientationY;
        }
    }

    [AddSystemToGameClient]
    public class VeinToolsSystem : SystemManager.System, IHasSystemSaveData<VeinToolsSystem.SaveData>
    {
        public event System.Action onSelectedVeinChanged;

        public int BeltCount => _beltCount;
        public int BeltTier => _beltTier;
        public int ExtraCeilingSpace => _extraCeilingSpace;
        public int ExtraBeltSpace => _extraBeltSpace;

        public int CeilingType => _ceilingType;
        public int CeilingMode => _ceilingMode;
        public ItemTemplate CeilingItemTemplate => _ceilingItemTemplate;
        public int FloorType => _floorType;
        public int FloorMode => _floorMode;
        public ItemTemplate FloorItemTemplate => _floorItemTemplate;

        public OreVein SelectedVein
        {
            get
            {
                if (_selectedVeinId == 0ul)
                    return null;

                var spawnedOreVeins = World.Systems.Get<OreVeinSpawningSystem>().GetSpawnedOreVeins();
                if (spawnedOreVeins.TryGetValue(_selectedVeinId, out var vein))
                    return vein;

                return null;
            }
        }

        private const int SAVEDATA_VERSION = 1;

        private int _beltCount = 4;
        private int _beltTier = 4;
        private int _extraCeilingSpace = 0;
        private int _extraBeltSpace = 0;

        private int _ceilingType = 0;
        private int _ceilingMode = 0;
        private ItemTemplate _ceilingItemTemplate = null;
        private int _floorType = 0;
        private int _floorMode = 0;
        private ItemTemplate _floorItemTemplate = null;

        private ulong _selectedVeinId = 0ul;
        private HashSet<HashId> _resourceTypes;
        private VeinToolsFrame _veinToolsFrame = null;

        private ulong _id_bot_miner = 0ul;
        private ulong[] _id_bot_belts = new ulong[4];
        private ulong[] _id_bot_balancers = new ulong[4];
        private ulong[] _id_bot_freightElevatorBottoms = new ulong[4];
        private ulong[] _id_bot_freightElevatorTops = new ulong[4];
        private ulong _id_bot_loader = 0ul;
        private ulong _id_bot_power_pole = 0ul;
        private ulong _id_bot_elevator = 0ul;

        public static VeinToolsSystem Instance { get; private set; }

        public override void OnAddedToWorld()
        {
            Instance = this;
            _resourceTypes = new HashSet<C3.HashId>(new C3.HashId[] { ScannableRegistrationSystem.RESOURCE_TYPE_ORE_VEIN });

            _veinToolsFrame = Object.Instantiate(AssetManager.Database.LoadAssetAtPath<VeinToolsFrame>("Assets/erkle64.VeinTools/VeinToolsFrame.prefab"), GameRoot.getDefaultCanvas().transform, false);
            _veinToolsFrame.gameObject.SetActive(false);

            _id_bot_miner = BuildableObjectTemplate.generateStringHash("_base_ore_vein_miner_i");
            _id_bot_belts[0] = BuildableObjectTemplate.generateStringHash("_base_conveyor_i");
            _id_bot_belts[1] = BuildableObjectTemplate.generateStringHash("_base_conveyor_ii");
            _id_bot_belts[2] = BuildableObjectTemplate.generateStringHash("_base_conveyor_iii");
            _id_bot_belts[3] = BuildableObjectTemplate.generateStringHash("_base_conveyor_iv");
            _id_bot_balancers[0] = BuildableObjectTemplate.generateStringHash("_base_conveyor_balancer_i");
            _id_bot_balancers[1] = BuildableObjectTemplate.generateStringHash("_base_conveyor_balancer_ii");
            _id_bot_balancers[2] = BuildableObjectTemplate.generateStringHash("_base_conveyor_balancer_iii");
            _id_bot_balancers[3] = BuildableObjectTemplate.generateStringHash("_base_conveyor_balancer_iv");
            _id_bot_freightElevatorBottoms[0] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_i_bottom");
            _id_bot_freightElevatorBottoms[1] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_ii_bottom");
            _id_bot_freightElevatorBottoms[2] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_iii_bottom");
            _id_bot_freightElevatorBottoms[3] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_iv_bottom");
            _id_bot_freightElevatorTops[0] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_i_top");
            _id_bot_freightElevatorTops[1] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_ii_top");
            _id_bot_freightElevatorTops[2] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_iii_top");
            _id_bot_freightElevatorTops[3] = BuildableObjectTemplate.generateStringHash("_base_freight_elevator_iv_top");
            _id_bot_loader = BuildableObjectTemplate.generateStringHash("_base_loader_1st_i");
            _id_bot_power_pole = BuildableObjectTemplate.generateStringHash("_base_power_pole_iii");
            _id_bot_elevator = BuildableObjectTemplate.generateStringHash("_base_elevator_i");

            if (_ceilingItemTemplate == null)
                _ceilingItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");
            if (_floorItemTemplate == null)
                _floorItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");
        }

        public override void OnRemovedFromWorld()
        {
            Instance = null;
        }

        [EventHandler]
        public void OnUpdate(OnUpdate _)
        {
            if (Input.GetKeyUp(Config.Input.openVeinToolsFrame.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                if (_veinToolsFrame.IsOpen)
                    _veinToolsFrame.Hide();
                else
                    _veinToolsFrame.Show();
            }
        }

        List<Scannable> _cache_scannables = new();
        public void SelectNearestVein()
        {
            var clientCharacter = GameRoot.getClientCharacter();
            if (clientCharacter == null)
                return;

            var cameraPos = World.Systems.Get<CameraFrustumSystem>().getCamera().transform.position;
            var scannableTrackingSystem = World.Systems.Get<ScannableTrackingSystem>();
            _cache_scannables.Clear();
            foreach (var chunkSection in new ChunkIterator(cameraPos, 192f))
            {
                scannableTrackingSystem.gatherScannablesForChunk(chunkSection.chunkId, _resourceTypes, _cache_scannables);
            }

            var spawnedOreVeins = World.Systems.Get<OreVeinSpawningSystem>().GetSpawnedOreVeins();
            OreVein nearestVein = null;
            float nearestVeinDistSqr = float.MaxValue;
            foreach (var scannable in _cache_scannables)
            {
                if (!spawnedOreVeins.TryGetValue(scannable.id, out var vein))
                    continue;

                var offset = new Vector2(vein.worldCellPos.x - cameraPos.x, vein.worldCellPos.z - cameraPos.z);
                var distSqr = offset.sqrMagnitude;
                if (distSqr < nearestVeinDistSqr)
                {
                    nearestVein = vein;
                    nearestVeinDistSqr = distSqr;
                }
            }

            if (nearestVein != null)
                _selectedVeinId = nearestVein.id;
            else
                _selectedVeinId = 0ul;

            onSelectedVeinChanged?.Invoke();
        }

        public int SetBeltTier(int beltTier)
        {
            return _beltTier = Mathf.Clamp(beltTier, 1, 4);
        }

        public int SetBeltCount(int beltCount)
        {
            return _beltCount = Mathf.Clamp(beltCount, 1, 100);
        }

        public int SetExtraCeilingSpace(int extraCeilingSpace)
        {
            return _extraCeilingSpace = Mathf.Clamp(extraCeilingSpace, 0, 20);
        }

        public int SetExtraBeltSpace(int extraBeltSpace)
        {
            return _extraBeltSpace = Mathf.Clamp(extraBeltSpace, 0, 20);
        }

        public int SetCeilingType(int ceilingType)
        {
            return _ceilingType = Mathf.Clamp(ceilingType, 0, 3);
        }

        public int SetCeilingMode(int ceilingMode)
        {
            return _ceilingMode = Mathf.Clamp(ceilingMode, 0, 1);
        }

        public ItemTemplate SetCeilingItemTemplate(ItemTemplate ceilingItemTemplate)
        {
            if (ceilingItemTemplate == null)
                ceilingItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");
            return _ceilingItemTemplate = ceilingItemTemplate;
        }

        public int SetFloorType(int floorType)
        {
            return _floorType = Mathf.Clamp(floorType, 0, 3);
        }

        public int SetFloorMode(int floorMode)
        {
            return _floorMode = Mathf.Clamp(floorMode, 0, 1);
        }

        public ItemTemplate SetFloorItemTemplate(ItemTemplate floorItemTemplate)
        {
            if (floorItemTemplate == null)
                floorItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");
            return _floorItemTemplate = floorItemTemplate;
        }

        public void ExcavateSelectedVein()
        {
            var vein = SelectedVein;
            if (vein == null)
                return;

            bool removeCeiling = (_ceilingMode == 1);
            bool removeFloor = (_floorMode == 1);

            Dictionary<int, Vector2Int> boundsMinByHeight = new();
            Dictionary<int, Vector2Int> boundsMaxByHeight = new();
            HashSet<int> layerHeights = new();

            void extendBoundsMin2(Vector2Int pos, int y)
            {
                if (boundsMinByHeight.TryGetValue(y, out var boundsMin))
                    boundsMinByHeight[y] = Vector2Int.Min(boundsMin, new Vector2Int(pos.x, pos.y));
                else
                    boundsMinByHeight[y] = new Vector2Int(pos.x, pos.y);
            }
            void extendBoundsMin3(Vector3Int pos, int y)
            {
                extendBoundsMin2(new Vector2Int(pos.x, pos.z), y);
            }

            void extendBoundsMax2(Vector2Int pos, int y)
            {
                if (boundsMaxByHeight.TryGetValue(y, out var boundsMax))
                    boundsMaxByHeight[y] = Vector2Int.Max(boundsMax, new Vector2Int(pos.x, pos.y));
                else
                    boundsMaxByHeight[y] = new Vector2Int(pos.x, pos.y);
            }
            void extendBoundsMax3(Vector3Int pos, int y)
            {
                extendBoundsMax2(new Vector2Int(pos.x, pos.z), y);
            }

            foreach (var drillPoint in vein.drillPoints)
            {
                GetBoundsFromDrillPoint(drillPoint, 1, out var startPos, out var endPos);
                var minPos = Vector3Int.Min(startPos, endPos);
                var maxPos = Vector3Int.Max(startPos, endPos);

                DroneDemolishArea(minPos, maxPos);

                OreVeinSpawningSystem.getCorePosAndOuterRadiusFromWorldY(vein.controlPoints, startPos.y, out var coreWorldPosBottom, out var outerRadiusBottom);
                extendBoundsMin2(new Vector2Int(Mathf.FloorToInt(coreWorldPosBottom.x - outerRadiusBottom - 1), Mathf.FloorToInt(coreWorldPosBottom.z - outerRadiusBottom - 1)), startPos.y);
                extendBoundsMax2(new Vector2Int(Mathf.CeilToInt(coreWorldPosBottom.x + outerRadiusBottom + 1), Mathf.CeilToInt(coreWorldPosBottom.z + outerRadiusBottom + 1)), startPos.y);

                OreVeinSpawningSystem.getCorePosAndOuterRadiusFromWorldY(vein.controlPoints, endPos.y, out var coreWorldPosTop, out var outerRadiusTop);
                extendBoundsMin2(new Vector2Int(Mathf.FloorToInt(coreWorldPosTop.x - outerRadiusTop - 1), Mathf.FloorToInt(coreWorldPosTop.z - outerRadiusTop - 1)), startPos.y);
                extendBoundsMax2(new Vector2Int(Mathf.CeilToInt(coreWorldPosTop.x + outerRadiusTop + 1), Mathf.CeilToInt(coreWorldPosTop.z + outerRadiusTop + 1)), startPos.y);

                extendBoundsMin3(minPos, startPos.y);
                extendBoundsMax3(maxPos, startPos.y);
            }

            foreach (var drillPoint in vein.drillPoints)
            {
                layerHeights.Add(drillPoint.position.y);

                var boundsMin = boundsMinByHeight[drillPoint.position.y];
                var boundsMax = boundsMaxByHeight[drillPoint.position.y];

                GetBoundsFromDrillPoint(drillPoint, 1, out var startPos, out var endPos);

                var orientationY = BuildingManager.BuildOrientation.xPos;
                if (drillPoint.drillPointToVeinCenterDir.z < 0) // north facing
                    orientationY = BuildingManager.BuildOrientation.zPos;
                else if (drillPoint.drillPointToVeinCenterDir.z > 0) // south facing
                    orientationY = BuildingManager.BuildOrientation.zNeg;
                else if (drillPoint.drillPointToVeinCenterDir.x < 0) // east facing
                    orientationY = BuildingManager.BuildOrientation.xPos;
                else if (drillPoint.drillPointToVeinCenterDir.x > 0) // west facing
                    orientationY = BuildingManager.BuildOrientation.xNeg;

                // extend endPos to reach bounds
                switch (orientationY)
                {
                    case BuildingManager.BuildOrientation.xPos:
                        endPos.x = Mathf.Max(endPos.x, boundsMax.x);
                        break;

                    case BuildingManager.BuildOrientation.zNeg:
                        endPos.z = Mathf.Min(endPos.z, boundsMin.y);
                        break;

                    case BuildingManager.BuildOrientation.xNeg:
                        endPos.x = Mathf.Min(endPos.x, boundsMin.x);
                        break;

                    case BuildingManager.BuildOrientation.zPos:
                        endPos.z = Mathf.Max(endPos.z, boundsMax.y);
                        break;
                }

                var minPos = Vector3Int.Min(startPos, endPos);
                var maxPos = Vector3Int.Max(startPos, endPos);

                DroneDemolishArea(minPos, maxPos);
            }

            var ceilingOffset = 3 + _extraCeilingSpace;
            var sortedLayerHeights = new List<int>(layerHeights);
            sortedLayerHeights.Sort((a, b) => b.CompareTo(a));
            var freightElevatorsPerLayer = Mathf.CeilToInt(_beltCount * 0.5f);
            var freightElevatorMinX = int.MinValue;
            var paddedWidth = _beltCount + _extraBeltSpace;
            for (int layerIndex = 0; layerIndex < sortedLayerHeights.Count; layerIndex++)
            {
                int layerHeight = sortedLayerHeights[layerIndex];
                var boundsMin = boundsMinByHeight[layerHeight];
                var boundsMax = boundsMaxByHeight[layerHeight];
                var startPositionBottom = new Vector3Int(Mathf.Max(boundsMax.x, freightElevatorMinX + freightElevatorsPerLayer * 3 - 1) + _beltCount - 2, layerHeight, boundsMin.y - paddedWidth - 6);
                var startPositionTop = Vector3Int.zero;
                int previousLayerHeight = 0;
                var previousBoundsMin = Vector2Int.zero;
                var previousBoundsMax = Vector2Int.zero;
                if (layerIndex > 0)
                {
                    previousLayerHeight = sortedLayerHeights[layerIndex - 1];
                    previousBoundsMin = boundsMinByHeight[previousLayerHeight];
                    previousBoundsMax = boundsMaxByHeight[previousLayerHeight];
                    if (startPositionBottom.x < previousBoundsMax.x - 2)
                        startPositionBottom.x = previousBoundsMax.x - 2;
                    startPositionBottom.z = Mathf.Min(boundsMin.y, previousBoundsMin.y) - paddedWidth - 6;
                    startPositionTop = new Vector3Int(startPositionBottom.x, previousLayerHeight, startPositionBottom.z);
                }

                var freightElevatorZoneBottomMin = new Vector3Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, layerHeight, startPositionBottom.z);
                var freightElevatorBottomMax = new Vector3Int(startPositionBottom.x + 2, layerHeight + ceilingOffset, startPositionBottom.z + 3);
                DroneDemolishArea(freightElevatorZoneBottomMin, freightElevatorBottomMax, false, removeFloor && _floorType >= 1);

                var freightElevatorBeltZoneBottomMin = new Vector3Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, layerHeight, startPositionBottom.z + 4);
                var freightElevatorBeltZoneBottomMax = new Vector3Int(startPositionBottom.x + 2, layerHeight + ceilingOffset, boundsMin.y - 1);
                DroneDemolishArea(freightElevatorBeltZoneBottomMin, freightElevatorBeltZoneBottomMax, removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1);

                var beltZoneBottomMin = new Vector3Int(boundsMax.x + 1, layerHeight, boundsMin.y - paddedWidth);
                var beltZoneBottomMax = new Vector3Int(startPositionBottom.x + 2, layerHeight + ceilingOffset, boundsMin.y - 1);
                DroneDemolishArea(beltZoneBottomMin, beltZoneBottomMax, removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1);

                if (layerIndex > 0)
                {
                    var freightElevatorTopMin = new Vector3Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, previousLayerHeight, startPositionBottom.z);
                    var freightElevatorTopMax = new Vector3Int(startPositionBottom.x + 2, previousLayerHeight + ceilingOffset, startPositionBottom.z + 3);
                    DroneDemolishArea(freightElevatorTopMin, freightElevatorTopMax, removeCeiling && _ceilingType >= 2, false);

                    var freightElevatorBeltZoneMin = new Vector3Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, previousLayerHeight, startPositionBottom.z + 4);
                    var freightElevatorBeltZoneMax = new Vector3Int(startPositionBottom.x + 2, previousLayerHeight + ceilingOffset, previousBoundsMin.y - 1);
                    DroneDemolishArea(freightElevatorBeltZoneMin, freightElevatorBeltZoneMax, removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1);

                    var beltZoneTopMin = new Vector3Int(previousBoundsMax.x + 1, previousLayerHeight, previousBoundsMin.y);
                    var beltZoneTopMax = new Vector3Int(startPositionBottom.x + 2, previousLayerHeight + ceilingOffset, previousBoundsMin.y + paddedWidth - 1);
                    DroneDemolishArea(beltZoneTopMin, beltZoneTopMax, removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1);
                }

                freightElevatorMinX = startPositionBottom.x + 3;
            }

            var boundsMaxMaxX = int.MinValue;
            foreach (var kvp in boundsMaxByHeight)
            {
                if (kvp.Value.x > boundsMaxMaxX)
                    boundsMaxMaxX = kvp.Value.x;
            }
            var boundsMinMaxZ = int.MinValue;
            foreach (var kvp in boundsMinByHeight)
            {
                if (kvp.Value.y > boundsMinMaxZ)
                    boundsMinMaxZ = kvp.Value.y;
            }
            var elevatorPosition = new Vector2Int(boundsMaxMaxX + paddedWidth + 1, boundsMinMaxZ + paddedWidth);
            var elevatorCenter = elevatorPosition + new Vector2Int(2, 2);
            var elevatorHeight = 2;
            for (int y = 255; y > 2; y--)
            {
                // check for non air block in the center of the elevator shaft
                var worldPos = new Vector3Int(elevatorCenter.x, y, elevatorCenter.y);
                ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out ulong chunkIndex, out uint blockIndex);
                var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                if (terrainData != 0)
                {
                    elevatorHeight = y + 1;
                    break;
                }
            }
            DroneDemolishArea(
                new Vector3Int(elevatorPosition.x, elevatorHeight, elevatorPosition.y),
                new Vector3Int(elevatorPosition.x + 4, elevatorHeight + ceilingOffset, elevatorPosition.y + 5)
                );
            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];
                DroneDemolishArea(
                    new Vector3Int(boundsMax.x + paddedWidth, y, elevatorPosition.y),
                    new Vector3Int(elevatorPosition.x - 1, y + ceilingOffset, elevatorPosition.y + 5),
                    removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1
                );
            }

            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];

                // south strip
                DroneDemolishArea(
                    new Vector3Int(boundsMin.x - paddedWidth, y, boundsMin.y - paddedWidth),
                    new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset, boundsMin.y - 1),
                    removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1
                );
                // north strip
                DroneDemolishArea(
                    new Vector3Int(boundsMin.x - paddedWidth, y, boundsMax.y + 1),
                    new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset, boundsMax.y + paddedWidth),
                    removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1
                );
                // west strip
                DroneDemolishArea(
                    new Vector3Int(boundsMin.x - paddedWidth, y, boundsMin.y),
                    new Vector3Int(boundsMin.x - 1, y + ceilingOffset, boundsMax.y),
                    removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1
                );
                // east strip
                DroneDemolishArea(
                    new Vector3Int(boundsMax.x + 1, y, boundsMin.y),
                    new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset, boundsMax.y),
                    removeCeiling && _ceilingType >= 2, removeFloor && _floorType >= 1
                );
            }

            if (removeCeiling && _ceilingType == 1)
            {
                foreach (var kvp in boundsMinByHeight)
                {
                    var y = kvp.Key;
                    var boundsMin = kvp.Value;
                    var boundsMax = boundsMaxByHeight[y];
                    // south strip
                    DroneDemolishArea(
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount),
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount)
                    );
                    // north strip
                    DroneDemolishArea(
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount),
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount)
                    );
                    // west strip
                    DroneDemolishArea(
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount + 1),
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount - 1)
                    );
                    // east strip
                    DroneDemolishArea(
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount + 1),
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount - 1)
                    );
                }
            }

            if (removeCeiling && _ceilingType >= 3)
            {
                foreach (var kvp in boundsMinByHeight)
                {
                    var y = kvp.Key;
                    var boundsMin = kvp.Value;
                    var boundsMax = boundsMaxByHeight[y];
                    DroneDemolishArea(
                        new Vector3Int(boundsMin.x, y + ceilingOffset + 1, boundsMax.y),
                        new Vector3Int(boundsMax.x, y + ceilingOffset + 1, boundsMax.y + paddedWidth)
                        );
                }
            }

            if (removeFloor && _floorType >= 2)
            {
                foreach (var kvp in boundsMinByHeight)
                {
                    var y = kvp.Key;
                    var boundsMin = kvp.Value;
                    var boundsMax = boundsMaxByHeight[y];
                    DroneDemolishArea(
                        new Vector3Int(boundsMin.x, y - 1, boundsMin.y),
                        new Vector3Int(boundsMax.x, y - 1, boundsMax.y)
                        );
                }
            }
        }

        public void PlaceBlueprintForSelectedVein()
        {
            var vein = SelectedVein;
            if (vein == null)
                return;

            BuildableObjectTemplate ceilingBOT = null;
            if (_ceilingType > 0 && _ceilingItemTemplate != null)
                ceilingBOT = _ceilingItemTemplate.buildableObjectTemplate;

            BuildableObjectTemplate floorBOT = null;
            if (_floorType > 0 && _floorItemTemplate != null)
                floorBOT = _floorItemTemplate.buildableObjectTemplate;

            Dictionary<int, Vector2Int> boundsMinByHeight = new();
            Dictionary<int, Vector2Int> boundsMaxByHeight = new();
            HashSet<int> layerHeights = new();
            Dictionary<Vector3Int, byte> blocksToPlace = new();

            void extendBoundsMin2(Vector2Int pos, int y)
            {
                if (boundsMinByHeight.TryGetValue(y, out var boundsMin))
                    boundsMinByHeight[y] = Vector2Int.Min(boundsMin, new Vector2Int(pos.x, pos.y));
                else
                    boundsMinByHeight[y] = new Vector2Int(pos.x, pos.y);
            }
            void extendBoundsMin3(Vector3Int pos, int y)
            {
                extendBoundsMin2(new Vector2Int(pos.x, pos.z), y);
            }

            void extendBoundsMax2(Vector2Int pos, int y)
            {
                if (boundsMaxByHeight.TryGetValue(y, out var boundsMax))
                    boundsMaxByHeight[y] = Vector2Int.Max(boundsMax, new Vector2Int(pos.x, pos.y));
                else
                    boundsMaxByHeight[y] = new Vector2Int(pos.x, pos.y);
            }
            void extendBoundsMax3(Vector3Int pos, int y)
            {
                extendBoundsMax2(new Vector2Int(pos.x, pos.z), y);
            }

            foreach (var drillPoint in vein.drillPoints)
            {
                layerHeights.Add(drillPoint.position.y);

                GetBoundsFromDrillPoint(drillPoint, 1, out var startPos, out var endPos);
                var minPos = Vector3Int.Min(startPos, endPos);
                var maxPos = Vector3Int.Max(startPos, endPos);

                OreVeinSpawningSystem.getCorePosAndOuterRadiusFromWorldY(vein.controlPoints, startPos.y, out var coreWorldPosBottom, out var outerRadiusBottom);
                extendBoundsMin2(new Vector2Int(Mathf.FloorToInt(coreWorldPosBottom.x - outerRadiusBottom - 1), Mathf.FloorToInt(coreWorldPosBottom.z - outerRadiusBottom - 1)), drillPoint.position.y);
                extendBoundsMax2(new Vector2Int(Mathf.CeilToInt(coreWorldPosBottom.x + outerRadiusBottom + 1), Mathf.CeilToInt(coreWorldPosBottom.z + outerRadiusBottom + 1)), drillPoint.position.y);

                OreVeinSpawningSystem.getCorePosAndOuterRadiusFromWorldY(vein.controlPoints, endPos.y, out var coreWorldPosTop, out var outerRadiusTop);
                extendBoundsMin2(new Vector2Int(Mathf.FloorToInt(coreWorldPosTop.x - outerRadiusTop - 1), Mathf.FloorToInt(coreWorldPosTop.z - outerRadiusTop - 1)), drillPoint.position.y);
                extendBoundsMax2(new Vector2Int(Mathf.CeilToInt(coreWorldPosTop.x + outerRadiusTop + 1), Mathf.CeilToInt(coreWorldPosTop.z + outerRadiusTop + 1)), drillPoint.position.y);

                extendBoundsMin3(minPos, drillPoint.position.y);
                extendBoundsMax3(maxPos, drillPoint.position.y);
            }

            var ceilingOffset = 3 + _extraCeilingSpace;
            var paddedWidth = _beltCount + _extraBeltSpace;
            Vector3Int overallBoundsMin = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            Vector3Int overallBoundsMax = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];
                overallBoundsMin = Vector3Int.Min(overallBoundsMin, new Vector3Int(boundsMin.x - paddedWidth, y, boundsMin.y - paddedWidth));
                overallBoundsMax = Vector3Int.Max(overallBoundsMax, new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset, boundsMax.y + paddedWidth));
            }
            var actualBoundsMin = overallBoundsMin;
            var actualBoundsMax = overallBoundsMax;
            void expandActualBounds3(Vector3Int pos)
            {
                actualBoundsMin = Vector3Int.Min(actualBoundsMin, pos);
                actualBoundsMax = Vector3Int.Max(actualBoundsMax, pos);
            }
            void expandActualBounds2(Vector2Int pos, int y)
            {
                expandActualBounds3(new Vector3Int(pos.x, y, pos.y));
            }

            HashSet<Vector3Int> invalidatedPositions = new();
            List<BlueprintRequest.Building> buildings = new();
            List<int> drillPointBuildingIndices = new();
            foreach (var drillPoint in vein.drillPoints)
            {
                var boundsMin = boundsMinByHeight[drillPoint.position.y];
                var boundsMax = boundsMaxByHeight[drillPoint.position.y];
                
                GetBoundsFromDrillPoint(drillPoint, 0, out var startPos, out var endPos);
                expandActualBounds3(startPos);
                expandActualBounds3(endPos);

                var orientationY = BuildingManager.BuildOrientation.xPos;
                if (drillPoint.drillPointToVeinCenterDir.z < 0) // north facing
                    orientationY = BuildingManager.BuildOrientation.zNeg;
                else if (drillPoint.drillPointToVeinCenterDir.z > 0) // south facing
                    orientationY = BuildingManager.BuildOrientation.zPos;
                else if (drillPoint.drillPointToVeinCenterDir.x < 0) // east facing
                    orientationY = BuildingManager.BuildOrientation.xNeg;
                else if (drillPoint.drillPointToVeinCenterDir.x > 0) // west facing
                    orientationY = BuildingManager.BuildOrientation.xPos;

                var minPos = Vector3Int.Min(startPos, endPos);
                drillPointBuildingIndices.Add(buildings.Count);
                AddMiner(buildings, invalidatedPositions, minPos - overallBoundsMin, orientationY);
            }

            void handleCeilingAndFloor(Vector2Int from, Vector2Int to, int layerHeight, int minCeilingType, int minFloorType)
            {
                if (from.x > to.x || from.y > to.y)
                    return;

                if (_ceilingType >= minCeilingType && ceilingBOT != null)
                {
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(from.x, layerHeight + ceilingOffset + 1, from.y) - overallBoundsMin,
                        new Vector3Int(to.x, layerHeight + ceilingOffset + 1, to.y) - overallBoundsMin,
                        ceilingBOT);
                    expandActualBounds3(new Vector3Int(from.x, layerHeight + ceilingOffset + 1, from.y));
                    expandActualBounds3(new Vector3Int(to.x, layerHeight + ceilingOffset + 1, to.y));
                }
                if (_floorType >= minFloorType && floorBOT != null)
                {
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(from.x, layerHeight - 1, from.y) - overallBoundsMin,
                        new Vector3Int(to.x, layerHeight - 1, to.y) - overallBoundsMin,
                        floorBOT);
                    expandActualBounds3(new Vector3Int(from.x, layerHeight - 1, from.y));
                    expandActualBounds3(new Vector3Int(to.x, layerHeight - 1, to.y));
                }
            }

            var sortedLayerHeights = new List<int>(layerHeights);
            sortedLayerHeights.Sort((a, b) => b.CompareTo(a));
            var freightElevatorsPerLayer = Mathf.CeilToInt(_beltCount * 0.5f);
            var freightElevatorMinX = int.MinValue;
            for (int layerIndex = 0; layerIndex < sortedLayerHeights.Count; layerIndex++)
            {
                int layerHeight = sortedLayerHeights[layerIndex];
                var boundsMin = boundsMinByHeight[layerHeight];
                var boundsMax = boundsMaxByHeight[layerHeight];
                var startPositionBottom = new Vector3Int(Mathf.Max(boundsMax.x, freightElevatorMinX + freightElevatorsPerLayer * 3 - 1) + _beltCount - 2, layerHeight, boundsMin.y - paddedWidth - 6);
                var startPositionTop = Vector3Int.zero;
                int previousLayerHeight = 0;
                var previousBoundsMin = Vector2Int.zero;
                var previousBoundsMax = Vector2Int.zero;
                if (layerIndex > 0)
                {
                    previousLayerHeight = sortedLayerHeights[layerIndex - 1];
                    previousBoundsMin = boundsMinByHeight[previousLayerHeight];
                    previousBoundsMax = boundsMaxByHeight[previousLayerHeight];
                    if (startPositionBottom.x < previousBoundsMax.x - 2)
                        startPositionBottom.x = previousBoundsMax.x - 2;
                    startPositionBottom.z = Mathf.Min(boundsMin.y, previousBoundsMin.y) - paddedWidth - 6;
                    startPositionTop = new Vector3Int(startPositionBottom.x, previousLayerHeight, startPositionBottom.z);
                }
                for (int elevatorIndex = 0; elevatorIndex < freightElevatorsPerLayer; elevatorIndex++)
                {
                    if (layerIndex > 0)
                    {
                        AddFreightElevatorTop(buildings, invalidatedPositions, startPositionTop + Vector3Int.left * elevatorIndex * 3 - overallBoundsMin, BuildingManager.BuildOrientation.zPos);
                        expandActualBounds3(startPositionTop + Vector3Int.left * elevatorIndex * 3);
                        expandActualBounds3(startPositionTop + Vector3Int.left * elevatorIndex * 3 + new Vector3Int(2, ceilingOffset, 3));
                    }

                    AddFreightElevatorBottom(buildings, invalidatedPositions, startPositionBottom + Vector3Int.left * elevatorIndex * 3 - overallBoundsMin, BuildingManager.BuildOrientation.zPos);
                    expandActualBounds3(startPositionBottom + Vector3Int.left * elevatorIndex * 3);
                    expandActualBounds3(startPositionBottom + Vector3Int.left * elevatorIndex * 3 + new Vector3Int(2, ceilingOffset, 3));
                }

                var freightElevatorZoneBottomMin = new Vector2Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, startPositionBottom.z);
                var freightElevatorZoneBottomMax = new Vector2Int(startPositionBottom.x + 2, startPositionBottom.z + 3);
                handleCeilingAndFloor(freightElevatorZoneBottomMin, freightElevatorZoneBottomMax, layerHeight, int.MaxValue, 1);

                var freightElevatorBeltZoneBottomMin = new Vector2Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, startPositionBottom.z + 4);
                var freightElevatorBeltZoneBottomMax = new Vector2Int(startPositionBottom.x + 2, boundsMin.y - paddedWidth - 1);
                handleCeilingAndFloor(freightElevatorBeltZoneBottomMin, freightElevatorBeltZoneBottomMax, layerHeight, 2, 1);

                var beltZoneBottomMin = new Vector2Int(boundsMax.x + 1, boundsMin.y - paddedWidth);
                var beltZoneBottomMax = new Vector2Int(startPositionBottom.x + 2, boundsMin.y - 1);
                handleCeilingAndFloor(beltZoneBottomMin, beltZoneBottomMax, layerHeight, 2, 1);

                for (int beltIndex = 0; beltIndex < _beltCount; beltIndex++)
                {
                    var elevatorIndex = beltIndex / 2;
                    var loaderIndex = beltIndex % 2;
                    var startPosition = new Vector3Int(beltZoneBottomMin.x, layerHeight, beltZoneBottomMax.y - beltIndex);
                    var endPosition = startPositionBottom + new Vector3Int(1 - loaderIndex - elevatorIndex * 3, 0, 5);
                    for (int x = startPosition.x; x < endPosition.x; x++)
                        AddBelt(buildings, invalidatedPositions, new Vector3Int(x, layerHeight, startPosition.z) - overallBoundsMin, BuildingManager.BuildOrientation.xPos);
                    for (int z = startPosition.z; z >= endPosition.z; z--)
                        AddBelt(buildings, invalidatedPositions, new Vector3Int(endPosition.x, layerHeight, z) - overallBoundsMin, BuildingManager.BuildOrientation.zNeg);
                    AddLoader(buildings, invalidatedPositions, endPosition + Vector3Int.back - overallBoundsMin, BuildingManager.BuildOrientation.zPos, false);
                }
                InvalidateRectangle(invalidatedPositions, beltZoneBottomMin - overallBoundsMin.flatten(), beltZoneBottomMax - overallBoundsMin.flatten(), layerHeight - overallBoundsMin.y);

                if (layerIndex > 0)
                {
                    var freightElevatorTopMin = new Vector2Int(startPositionTop.x - freightElevatorsPerLayer * 3 + 3, startPositionTop.z);
                    var freightElevatorTopMax = new Vector2Int(startPositionTop.x + 2, startPositionTop.z + 3);
                    handleCeilingAndFloor(freightElevatorTopMin, freightElevatorTopMax, previousLayerHeight, 2, int.MaxValue);

                    var freightElevatorBeltZoneMin = new Vector2Int(startPositionBottom.x - freightElevatorsPerLayer * 3 + 3, startPositionTop.z + 4);
                    var freightElevatorBeltZoneMax = new Vector2Int(startPositionBottom.x + 2, previousBoundsMin.y - 1);
                    handleCeilingAndFloor(freightElevatorBeltZoneMin, freightElevatorBeltZoneMax, previousLayerHeight, 2, 1);

                    var beltZoneTopMin = new Vector2Int(previousBoundsMax.x + 1, previousBoundsMin.y);
                    var beltZoneTopMax = new Vector2Int(startPositionBottom.x + 2, previousBoundsMin.y + paddedWidth - 1);
                    handleCeilingAndFloor(beltZoneTopMin, beltZoneTopMax, previousLayerHeight, 2, 1);

                    for (int beltIndex = 0; beltIndex < _beltCount; beltIndex++)
                    {
                        var elevatorIndex = freightElevatorsPerLayer - beltIndex / 2 - 1;
                        var loaderIndex = 1 - (beltIndex % 2);
                        var startPosition = new Vector3Int(beltZoneTopMin.x + beltIndex + 1, previousLayerHeight, beltZoneTopMax.y - paddedWidth + beltIndex + 1);
                        var endPosition = startPositionTop + new Vector3Int(2 - loaderIndex - elevatorIndex * 3, 0, 5);
                        for (int x = startPosition.x; x <= endPosition.x; x++)
                            AddBelt(buildings, invalidatedPositions, new Vector3Int(x, previousLayerHeight, startPosition.z) - overallBoundsMin, BuildingManager.BuildOrientation.xNeg);
                        for (int z = startPosition.z - 1; z >= endPosition.z; z--)
                            AddBelt(buildings, invalidatedPositions, new Vector3Int(endPosition.x, previousLayerHeight, z) - overallBoundsMin, BuildingManager.BuildOrientation.zPos);
                        for (int z = startPosition.z; z <= beltZoneTopMax.y; z++)
                            AddBelt(buildings, invalidatedPositions, new Vector3Int(startPosition.x - 1, previousLayerHeight, z) - overallBoundsMin, BuildingManager.BuildOrientation.zPos);
                        AddLoader(buildings, invalidatedPositions, endPosition + Vector3Int.back - overallBoundsMin, BuildingManager.BuildOrientation.zPos, true);
                    }
                    InvalidateRectangle(invalidatedPositions, beltZoneTopMin - overallBoundsMin.flatten(), beltZoneTopMax - overallBoundsMin.flatten(), previousLayerHeight - overallBoundsMin.y);
                }

                freightElevatorMinX = startPositionBottom.x + 3;
            }

            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key - overallBoundsMin.y;
                var boundsMin = kvp.Value - overallBoundsMin.flatten();
                var boundsMax = boundsMaxByHeight[kvp.Key] - overallBoundsMin.flatten();

                // south strip
                AddBeltBus(
                    buildings, invalidatedPositions,
                    new Vector3Int(boundsMin.x - _beltCount, y, boundsMin.y - _beltCount), Vector3Int.right, BuildingManager.BuildOrientation.xPos,
                    boundsMax.x - boundsMin.x + _beltCount * 2, _beltCount, new Vector3Int(1, 0, 1), -2
                    );

                // north strip
                AddBeltBus(
                    buildings, invalidatedPositions,
                    new Vector3Int(boundsMax.x + _beltCount, y, boundsMax.y + _beltCount), Vector3Int.left, BuildingManager.BuildOrientation.xNeg,
                    boundsMax.x - boundsMin.x + _beltCount * 2, _beltCount, new Vector3Int(-1, 0, -1), -2
                    );

                // west strip
                AddBeltBus(
                    buildings, invalidatedPositions,
                    new Vector3Int(boundsMin.x - _beltCount, y, boundsMax.y + _beltCount), Vector3Int.back, BuildingManager.BuildOrientation.zNeg,
                    boundsMax.y - boundsMin.y + _beltCount * 2, _beltCount, new Vector3Int(1, 0, -1), -2
                    );

                // east strip
                AddBeltBus(
                    buildings, invalidatedPositions,
                    new Vector3Int(boundsMax.x + _beltCount, y, boundsMin.y - _beltCount), Vector3Int.forward, BuildingManager.BuildOrientation.zPos,
                    boundsMax.y - boundsMin.y + _beltCount * 2, _beltCount, new Vector3Int(-1, 0, 1), -2
                    );
            }

            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];

                // south strip
                handleCeilingAndFloor(
                    new Vector2Int(boundsMin.x - paddedWidth, boundsMin.y - paddedWidth),
                    new Vector2Int(boundsMax.x + paddedWidth, boundsMin.y - 1),
                    y, 2, 1
                    );

                // north strip
                handleCeilingAndFloor(
                    new Vector2Int(boundsMin.x - paddedWidth, boundsMax.y + 1),
                    new Vector2Int(boundsMax.x + paddedWidth, boundsMax.y + paddedWidth),
                    y, 2, 1
                    );

                // west strip
                handleCeilingAndFloor(
                    new Vector2Int(boundsMin.x - paddedWidth, boundsMin.y),
                    new Vector2Int(boundsMin.x - 1, boundsMax.y),
                    y, 2, 1
                    );

                // east strip
                handleCeilingAndFloor(
                    new Vector2Int(boundsMax.x + 1, boundsMin.y),
                    new Vector2Int(boundsMax.x + paddedWidth, boundsMax.y),
                    y, 2, 1
                    );
            }

            Dictionary<int, List<Vector3Int>> powerPolePositionsByHeight = new();
            Dictionary<int, List<int>> powerPoleDrillPointIndicesByHeight = new();
            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];

                List<Vector3Int> powerPolePositions = new();

                // south strip
                AddPowerPolePositions(powerPolePositions,
                    new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset, boundsMin.y - _beltCount),
                    new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset, boundsMin.y - _beltCount)
                    );

                // east strip
                AddPowerPolePositions(powerPolePositions,
                    new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset, boundsMin.y - _beltCount),
                    new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset, boundsMax.y + _beltCount)
                    );

                // north strip
                AddPowerPolePositions(powerPolePositions,
                    new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset, boundsMax.y + _beltCount),
                    new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset, boundsMax.y + _beltCount)
                    );

                // west strip
                AddPowerPolePositions(powerPolePositions,
                    new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset, boundsMax.y + _beltCount),
                    new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset, boundsMin.y - _beltCount)
                    );

                if (ceilingBOT != null && _ceilingType == 1)
                {
                    // ceiling strip south
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount) - overallBoundsMin,
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount) - overallBoundsMin,
                        ceilingBOT);
                    // ceiling strip north
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount) - overallBoundsMin,
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount) - overallBoundsMin,
                        ceilingBOT);
                    // ceiling strip west
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount + 1) - overallBoundsMin,
                        new Vector3Int(boundsMin.x - _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount - 1) - overallBoundsMin,
                        ceilingBOT);
                    // ceiling strip east
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMin.y - _beltCount + 1) - overallBoundsMin,
                        new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset + 1, boundsMax.y + _beltCount - 1) - overallBoundsMin,
                        ceilingBOT);
                }

                powerPolePositionsByHeight[y] = powerPolePositions;
                powerPoleDrillPointIndicesByHeight[y] = new List<int>(Enumerable.Repeat(-1, powerPolePositions.Count));
            }

            if (ceilingBOT != null && _ceilingType >= 3)
            {
                foreach (var kvp in boundsMinByHeight)
                {
                    var y = kvp.Key;
                    var boundsMin = kvp.Value;
                    var boundsMax = boundsMaxByHeight[y];
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMin.x, y + ceilingOffset + 1, boundsMax.y) - overallBoundsMin,
                        new Vector3Int(boundsMax.x, y + ceilingOffset + 1, boundsMax.y) - overallBoundsMin,
                        ceilingBOT);
                }
            }

            if (floorBOT != null && _floorType >= 2)
            {
                foreach (var kvp in boundsMinByHeight)
                {
                    var y = kvp.Key;
                    var boundsMin = kvp.Value;
                    var boundsMax = boundsMaxByHeight[y];
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMin.x, y - 1, boundsMin.y) - overallBoundsMin,
                        new Vector3Int(boundsMax.x, y - 1, boundsMax.y) - overallBoundsMin,
                        floorBOT);
                }
            }

            var boundsMaxMaxX = int.MinValue;
            foreach (var kvp in boundsMaxByHeight)
            {
                if (kvp.Value.x > boundsMaxMaxX)
                    boundsMaxMaxX = kvp.Value.x;
            }
            var boundsMinMaxZ = int.MinValue;
            foreach (var kvp in boundsMinByHeight)
            {
                if (kvp.Value.y > boundsMinMaxZ)
                    boundsMinMaxZ = kvp.Value.y;
            }
            var elevatorPosition = new Vector2Int(boundsMaxMaxX + paddedWidth + 1, boundsMinMaxZ + paddedWidth);
            var elevatorCenter = elevatorPosition + new Vector2Int(2, 2);
            var elevatorHeight = 2;
            for (int y = 255; y > 2; y--)
            {
                // check for non air block in the center of the elevator shaft
                var worldPos = new Vector3Int(elevatorCenter.x, y, elevatorCenter.y);
                ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out ulong chunkIndex, out uint blockIndex);
                var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                if (terrainData != 0)
                {
                    elevatorHeight = y + 1;
                    break;
                }
            }
            if (elevatorHeight < 255)
            {
                AABB3D elevatorSearchBounds = new AABB3D
                {
                    x0 = elevatorPosition.x,
                    y0 = elevatorHeight,
                    z0 = elevatorPosition.y,
                    wx = 5,
                    wy = 256 - elevatorHeight,
                    wz = 6
                };
                unsafe
                {
                    var results = StreamingSystem.get().queryAABB3D(elevatorSearchBounds);
                    foreach (var result in results)
                    {
                        if (result is ElevatorStationGO elevatorStation)
                        {
                            var stationPosition = elevatorStation.aabb.anchorToV3I();
                            if (stationPosition.y > elevatorHeight)
                                elevatorHeight = stationPosition.y;
                        }
                    }
                }
            }
            var stationHeights = new int[sortedLayerHeights.Count];
            for (int i = 0; i < sortedLayerHeights.Count; i++)
                stationHeights[i] = sortedLayerHeights[i] - overallBoundsMin.y;
            AddElevator(buildings, invalidatedPositions, new Vector3Int(elevatorPosition.x, elevatorHeight, elevatorPosition.y) - overallBoundsMin, BuildingManager.BuildOrientation.xNeg, stationHeights);
            foreach (var kvp in boundsMinByHeight)
            {
                var y = kvp.Key;
                var boundsMin = kvp.Value;
                var boundsMax = boundsMaxByHeight[y];
                InsertPowerPole(-1, powerPolePositionsByHeight[y], powerPoleDrillPointIndicesByHeight[y],
                    new Vector3Int(boundsMax.x + _beltCount, y + ceilingOffset, elevatorPosition.y + 5));

                if (ceilingBOT != null && _ceilingType >= 2)
                {
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset + 1, elevatorPosition.y) - overallBoundsMin,
                        new Vector3Int(elevatorPosition.x - 1, y + ceilingOffset + 1, elevatorPosition.y + 5) - overallBoundsMin,
                        ceilingBOT);
                    expandActualBounds3(new Vector3Int(boundsMax.x + paddedWidth, y + ceilingOffset + 1, elevatorPosition.y));
                    expandActualBounds3(new Vector3Int(elevatorPosition.x - 1, y + ceilingOffset + 1, elevatorPosition.y + 5));
                }

                if (floorBOT != null && _floorType >= 1)
                {
                    AddBlockGrid(buildings, blocksToPlace, invalidatedPositions,
                        new Vector3Int(boundsMax.x + paddedWidth, y - 1, elevatorPosition.y) - overallBoundsMin,
                        new Vector3Int(elevatorPosition.x - 1, y - 1, elevatorPosition.y + 5) - overallBoundsMin,
                        floorBOT);
                    expandActualBounds3(new Vector3Int(boundsMax.x + paddedWidth, y - 1, elevatorPosition.y));
                    expandActualBounds3(new Vector3Int(elevatorPosition.x - 1, y - 1, elevatorPosition.y + 5));
                }
            }

            for (int drillPointIndex = 0; drillPointIndex < vein.drillPoints.Length; drillPointIndex++)
            {
                OreVeinDrillPoint drillPoint = vein.drillPoints[drillPointIndex];
                var y = drillPoint.position.y;
                GetBoundsFromDrillPoint(drillPoint, 0, out var startPos, out var endPos);
                var minPos = Vector3Int.Min(startPos, endPos);
                var maxPos = Vector3Int.Max(startPos, endPos);

                var powerPolePositions = powerPolePositionsByHeight[y];
                var powerPoleDrillPointIndices = powerPoleDrillPointIndicesByHeight[y];

                Vector3Int polePosition = Vector3Int.zero;
                if (drillPoint.drillPointToVeinCenterDir.z < 0) // north facing
                    polePosition = new Vector3Int(minPos.x + 1, y + ceilingOffset, maxPos.z + 1 + _beltCount);
                else if (drillPoint.drillPointToVeinCenterDir.z > 0) // south facing
                    polePosition = new Vector3Int(minPos.x + 3, y + ceilingOffset, minPos.z - 1 - _beltCount);
                else if (drillPoint.drillPointToVeinCenterDir.x < 0) // east facing
                    polePosition = new Vector3Int(maxPos.x + 1 + _beltCount, y + ceilingOffset, minPos.z + 3);
                else if (drillPoint.drillPointToVeinCenterDir.x > 0) // west facing
                    polePosition = new Vector3Int(minPos.x - 1 - _beltCount, y + ceilingOffset, minPos.z + 1);

                InsertPowerPole(drillPointIndex, powerPolePositions, powerPoleDrillPointIndices, polePosition);
            }

            foreach (var kvp in powerPolePositionsByHeight)
            {
                var y = kvp.Key;
                var powerPolePositions = kvp.Value;
                var powerPoleDrillPointIndices = powerPoleDrillPointIndicesByHeight[y];
                var previousIndex = -1;
                var baseIndex = buildings.Count;
                var firstNonDrillPointPoleIndex = -1;
                for (int i = 0; i < powerPoleDrillPointIndices.Count; i++)
                {
                    if (powerPoleDrillPointIndices[i] < 0)
                    {
                        firstNonDrillPointPoleIndex = i;
                        break;
                    }
                }
                var lastNonDrillPointPoleIndex = -1;
                for (int i = powerPoleDrillPointIndices.Count - 1; i >= 0; i--)
                {
                    if (powerPoleDrillPointIndices[i] < 0)
                    {
                        lastNonDrillPointPoleIndex = i;
                        break;
                    }
                }
                for (int i = 0; i < powerPolePositions.Count; i++)
                {
                    var position = powerPolePositions[i];
                    var drillPointIndex = powerPoleDrillPointIndices[i];
                    var drillPointBuildingIndex = drillPointIndex >= 0 ? drillPointBuildingIndices[drillPointIndex] : -1;

                    if (i == lastNonDrillPointPoleIndex)
                        drillPointBuildingIndex = baseIndex + firstNonDrillPointPoleIndex;
                    AddPowerPole(buildings, position - overallBoundsMin, previousIndex, drillPointBuildingIndex);
                    previousIndex = i + baseIndex;
                }
            }

            actualBoundsMin -= Vector3Int.one;
            actualBoundsMax += Vector3Int.one;
            var actualSize = actualBoundsMax - actualBoundsMin + Vector3Int.one;
            var blocks = new byte[actualSize.x * actualSize.y * actualSize.z];
            var offset = overallBoundsMin - actualBoundsMin;
            foreach (var kvp in blocksToPlace)
            {
                var pos = kvp.Key + offset;
                if (pos.x < 0 || pos.x >= actualSize.x || pos.y < 0 || pos.y >= actualSize.y || pos.z < 0 || pos.z >= actualSize.z)
                {
                    Debug.LogWarning($"Block at {kvp.Key} is out of bounds and will be skipped");
                    continue;
                }

                blocks[(pos.z * actualSize.y + pos.y) * actualSize.x + pos.x] = kvp.Value;
            }

            for (int buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
            {
                BlueprintRequest.Building building = buildings[buildingIndex];
                building.anchorPositionX += offset.x;
                building.anchorPositionY += offset.y;
                building.anchorPositionZ += offset.z;
                if (building.templateId == _id_bot_elevator)
                {
                    for (int i = 0; i < building.customData.Length; i++)
                    {
                        var parts = building.customData[i].Item2.Split('|');
                        parts[1] = (int.Parse(parts[1]) + offset.y).ToString();
                        building.customData[i].Item2 = string.Join("|", parts);
                    }
                }
                buildings[buildingIndex] = building;
            }

            var blueprintRequest = new BlueprintRequest
            {
                positionX = actualBoundsMin.x,
                positionY = actualBoundsMin.y,
                positionZ = actualBoundsMin.z,
                sizeX = actualSize.x,
                sizeY = actualSize.y,
                sizeZ = actualSize.z,
                buildings = buildings.ToArray(),
                tracks = new BlueprintRequest.TrainTrack[0],
                blocks = blocks,
            };
            Messenger.Send("Duplicationer.PlaceBlueprint", blueprintRequest);
        }

        private void AddBlockGrid(List<BlueprintRequest.Building> buildings, Dictionary<Vector3Int, byte> blocksToPlace, HashSet<Vector3Int> invalidatedPositions, Vector3Int from, Vector3Int to, BuildableObjectTemplate bot)
        {
            if (bot.type == BuildableObjectTemplate.BuildableObjectType.TerrainBlock || bot.type == BuildableObjectTemplate.BuildableObjectType.BuildingPart)
            {
                byte blockId = 0;
                if (bot.type == BuildableObjectTemplate.BuildableObjectType.BuildingPart)
                {
                    blockId = (byte)GameRoot.BuildingPartIdxLookupTable.getKeyByValue(bot.id);
                }
                else if (bot.terrainBlock_tbt != null)
                {
                    blockId = (byte)bot.terrainBlock_tbt.tbtByteIdx;
                }
                else
                {
                    Debug.LogWarning($"BOT {bot.name} does not have a terrain block TBT and cannot be placed as a block grid");
                    return;
                }

                for (int x = from.x; x <= to.x; x++)
                {
                    for (int y = from.y; y <= to.y; y++)
                    {
                        for (int z = from.z; z <= to.z; z++)
                        {
                            if (invalidatedPositions.Contains(new Vector3Int(x, y, z)))
                                continue;
                            invalidatedPositions.Add(new Vector3Int(x, y, z));
                            blocksToPlace[new Vector3Int(x, y, z)] = blockId;
                        }
                    }
                }
                return;
            }

            for (int x = from.x; x <= to.x; x++)
            {
                for (int y = from.y; y <= to.y; y++)
                {
                    for (int z = from.z; z <= to.z; z++)
                    {
                        if (invalidatedPositions.Contains(new Vector3Int(x, y, z)))
                            continue;

                        invalidatedPositions.Add(new Vector3Int(x, y, z));

                        buildings.Add(new BlueprintRequest.Building
                        {
                            templateId = bot.id,
                            anchorPositionX = x,
                            anchorPositionY = y,
                            anchorPositionZ = z,
                            orientationY = BuildingManager.BuildOrientation.xPos,
                            orientationUnlockedX = Quaternion.identity.x,
                            orientationUnlockedY = Quaternion.identity.y,
                            orientationUnlockedZ = Quaternion.identity.z,
                            orientationUnlockedW = Quaternion.identity.w,
                            itemMode = 0,
                            customData = new (string, string)[0]
                        });
                    }
                }
            }
        }

        private static void InsertPowerPole(int drillPointIndex, List<Vector3Int> powerPolePositions, List<int> powerPoleDrillPointIndices, Vector3Int polePosition)
        {
            // if the pole position is already in the list of positions to place poles, don't add it again
            // otherwise, find the two closest positions in the list and insert the pole in between them in the list
            if (powerPolePositions.Contains(polePosition))
            {
                var index = powerPolePositions.IndexOf(polePosition);
                if (powerPoleDrillPointIndices[index] == -1)
                    powerPoleDrillPointIndices[index] = drillPointIndex;
                return;
            }

            int closestIndex1 = -1;
            int closestIndex2 = -1;
            float closestDistSqr1 = float.MaxValue;
            float closestDistSqr2 = float.MaxValue;
            for (int powerPoleIndex = 0; powerPoleIndex < powerPolePositions.Count; powerPoleIndex++)
            {
                Vector3Int pos = powerPolePositions[powerPoleIndex];
                var distSqr = (pos - polePosition).sqrMagnitude;
                if (distSqr < closestDistSqr1)
                {
                    closestIndex2 = closestIndex1;
                    closestDistSqr2 = closestDistSqr1;
                    closestIndex1 = powerPoleIndex;
                    closestDistSqr1 = distSqr;
                }
                else if (distSqr < closestDistSqr2)
                {
                    closestIndex2 = powerPoleIndex;
                    closestDistSqr2 = distSqr;
                }
            }

            if (closestIndex1 != -1 && closestIndex2 != -1)
            {
                if (closestIndex1 < closestIndex2)
                {
                    powerPolePositions.Insert(closestIndex2, polePosition);
                    powerPoleDrillPointIndices.Insert(closestIndex2, drillPointIndex);
                }
                else
                {
                    powerPolePositions.Insert(closestIndex1, polePosition);
                    powerPoleDrillPointIndices.Insert(closestIndex1, drillPointIndex);
                }
            }
        }

        private void AddElevator(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY, int[] stationHeights)
        {
            var customData = new (string, string)[stationHeights.Length + 1];
            customData[stationHeights.Length] = ("elevatorStation", $"Surface|{position.y}");
            for (int i = 0; i < stationHeights.Length; i++)
                customData[stationHeights.Length - i - 1] = ("elevatorStation", $"L{i + 1}|{stationHeights[i]}");

            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_elevator,
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = customData
            });
        }

        private void AddPowerPole(List<BlueprintRequest.Building> buildings, Vector3Int position, int connectionIndex1, int connectionIndex2)
        {
            var connectionCount = (connectionIndex1 >= 0 ? 1 : 0) + (connectionIndex2 >= 0 ? 1 : 0);
            (string, string)[] customData = new (string, string)[connectionCount];
            int customDataIndex = 0;
            if (connectionIndex1 >= 0)
            {
                customData[customDataIndex] = ("powerline", (connectionIndex1 + 1).ToString());
                customDataIndex++;
            }
            if (connectionIndex2 >= 0)
            {
                customData[customDataIndex] = ("powerline", (connectionIndex2 + 1).ToString());
                customDataIndex++;
            }

            var orientation = Quaternion.Euler(0f, 0f, 90f);

            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_power_pole,
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = BuildingManager.BuildOrientation.xPos,
                orientationUnlockedX = orientation.x,
                orientationUnlockedY = orientation.y,
                orientationUnlockedZ = orientation.z,
                orientationUnlockedW = orientation.w,
                itemMode = 0,
                customData = customData
            });
        }

        private void AddPowerPolePositions(List<Vector3Int> powerPolePositions, Vector3Int from, Vector3Int to)
        {
            static int sign(int value)
            {
                if (value < 0) return -1;
                else if (value > 0) return 1;
                else return 0;
            }

            var distance = Vector3Int.Distance(from, to);
            var poleCount = Mathf.CeilToInt(distance / 23f) + 1;
            var spacing = distance / (poleCount - 1);
            var direction = to - from;
            direction = new Vector3Int(sign(direction.x), sign(direction.y), sign(direction.z));

            float accumulator = spacing;
            for (int i = 1; i < poleCount; i++)
            {
                var position = from + direction * Mathf.FloorToInt(accumulator);
                powerPolePositions.Add(position);
                accumulator += spacing;
            }
        }

        private void AddBeltBus(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int startPosition, Vector3Int direction, BuildingManager.BuildOrientation orientationY, int startLength, int beltCount, Vector3Int positionChange, int lengthChange)
        {
            var length = startLength;
            for (int i = 0; i < beltCount; i++)
            {
                var position = startPosition;
                for (int j = 0; j < length; j++)
                {
                    AddBelt(buildings, invalidatedPositions, position, orientationY);
                    position += direction;
                }
                startPosition += positionChange;
                length += lengthChange;
            }
        }

        private void AddMiner(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY)
        {
            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_miner,
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[0]
            });

            switch (orientationY)
            {
                case BuildingManager.BuildOrientation.xPos: AddBalancersWest(buildings, invalidatedPositions, position); break;
                case BuildingManager.BuildOrientation.zNeg: AddBalancersNorth(buildings, invalidatedPositions, position); break;
                case BuildingManager.BuildOrientation.xNeg: AddBalancersEast(buildings, invalidatedPositions, position); break;
                case BuildingManager.BuildOrientation.zPos: AddBalancersSouth(buildings, invalidatedPositions, position); break;
            }
        }

        private void AddBalancersWest(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position)
        {
            AddBelt(buildings, invalidatedPositions, position + new Vector3Int(-1, 0, 2), BuildingManager.BuildOrientation.zNeg);
            var balancerPosition = position + new Vector3Int(-2, 0, 0);
            for (int i = 0; i < _beltCount; i++)
            {
                AddBalancer(buildings, invalidatedPositions, balancerPosition, BuildingManager.BuildOrientation.zNeg);
                balancerPosition += new Vector3Int(-1, 0, -2);
            }
        }

        private void AddBalancersNorth(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position)
        {
            AddBelt(buildings, invalidatedPositions, position + new Vector3Int(2, 0, 10), BuildingManager.BuildOrientation.xNeg);
            var balancerPosition = position + new Vector3Int(0, 0, 10);
            for (int i = 0; i < _beltCount; i++)
            {
                AddBalancer(buildings, invalidatedPositions, balancerPosition, BuildingManager.BuildOrientation.xNeg);
                balancerPosition += new Vector3Int(-2, 0, 1);
            }
        }

        private void AddBalancersEast(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position)
        {
            AddBelt(buildings, invalidatedPositions, position + new Vector3Int(10, 0, 2), BuildingManager.BuildOrientation.zPos);
            var balancerPosition = position + new Vector3Int(10, 0, 3);
            for (int i = 0; i < _beltCount; i++)
            {
                AddBalancer(buildings, invalidatedPositions, balancerPosition, BuildingManager.BuildOrientation.zPos);
                balancerPosition += new Vector3Int(1, 0, 2);
            }
        }

        private void AddBalancersSouth(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position)
        {
            AddBelt(buildings, invalidatedPositions, position + new Vector3Int(2, 0, -1), BuildingManager.BuildOrientation.xPos);
            var balancerPosition = position + new Vector3Int(3, 0, -2);
            for (int i = 0; i < _beltCount; i++)
            {
                AddBalancer(buildings, invalidatedPositions, balancerPosition, BuildingManager.BuildOrientation.xPos);
                balancerPosition += new Vector3Int(2, 0, -1);
            }
        }

        private void AddBelt(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY)
        {
            if (invalidatedPositions.Contains(position))
                return;

            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_belts[_beltTier - 1],
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[0]
            });
            invalidatedPositions.Add(position);
        }

        private void AddBalancer(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY)
        {
            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_balancers[_beltTier - 1],
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[]
                {
                    ("balancerInputPriority", "0"),
                    ("balancerOutputPriority", "1")
                }
            });

            invalidatedPositions.Add(position);
            invalidatedPositions.Add(position + new Vector3Int(1, 0, 0));
            invalidatedPositions.Add(position + new Vector3Int(0, 0, 1));
            invalidatedPositions.Add(position + new Vector3Int(1, 0, 1));
        }

        private void AddLoader(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY, bool isOutput)
        {
            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_loader,
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[]
                {
                    ("isInputLoader", isOutput ? "false" : "true")
                }
            });
        }

        private void AddFreightElevatorBottom(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY)
        {
            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_freightElevatorBottoms[_beltTier - 1],
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[0]
            });
            invalidatedPositions.Add(position);
        }

        private void AddFreightElevatorTop(List<BlueprintRequest.Building> buildings, HashSet<Vector3Int> invalidatedPositions, Vector3Int position, BuildingManager.BuildOrientation orientationY)
        {
            buildings.Add(new BlueprintRequest.Building
            {
                templateId = _id_bot_freightElevatorTops[_beltTier - 1],
                anchorPositionX = position.x,
                anchorPositionY = position.y,
                anchorPositionZ = position.z,
                orientationY = orientationY,
                orientationUnlockedX = Quaternion.identity.x,
                orientationUnlockedY = Quaternion.identity.y,
                orientationUnlockedZ = Quaternion.identity.z,
                orientationUnlockedW = Quaternion.identity.w,
                itemMode = 0,
                customData = new (string, string)[0]
            });
            invalidatedPositions.Add(position);
        }

        private static void InvalidateRectangle(HashSet<Vector3Int> invalidatedPositions, Vector2Int from, Vector2Int to, int y)
        {
            var min = Vector2Int.Min(from, to);
            var max = Vector2Int.Max(from, to);
            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.y; z <= max.y; z++)
                {
                    invalidatedPositions.Add(new Vector3Int(x, y, z));
                }
            }
        }

        private static void GetBoundsFromDrillPoint(OreVeinDrillPoint drillPoint, int extraLength, out Vector3Int startPos, out Vector3Int endPos)
        {
            startPos = drillPoint.position - drillPoint.drillPointToVeinCenterDir - drillPoint.drillPointTangentToVeinDir * 2;
            endPos = startPos - drillPoint.drillPointToVeinCenterDir * (9 + extraLength) + drillPoint.drillPointTangentToVeinDir * 4 + Vector3Int.up * 3;
        }

        public void ShowFrame()
        {
            _veinToolsFrame.Show();
        }

        public void HideFrame()
        {
            _veinToolsFrame.Hide();
        }

        private void DroneDemolishArea(Vector3Int from, Vector3Int to, bool demolishCeiling, bool demolishFloor)
        {
            if (demolishCeiling)
                to.y += 1;
            if (demolishFloor)
                from.y -= 1;
            DroneDemolishArea(from, to);
        }

        private void DroneDemolishArea(Vector3Int from, Vector3Int to)
        {
            GameRoot.addLockstepEvent(new GameRoot.SetMiningAreaEvent(
                new AABB3D(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1),
                true
            ));
        }

        //
        // SAVE DATA
        //
        [MessagePackObject(true)]
        [SystemSaveData(SAVEDATA_VERSION)]
        public class SaveData
        {
            public int beltCount;
            public int beltTier;
            public int extraCeilingSpace;
            public int extraBeltSpace;
            public int ceilingType;
            public int ceilingMode;
            public ulong ceilingBlockItemId;
            public int floorType;
            public int floorMode;
            public ulong floorBlockItemId;
        }

        public SaveData save()
        {
            var saveData = new SaveData
            {
                beltCount = _beltCount,
                beltTier = _beltTier,
                extraCeilingSpace = _extraCeilingSpace,
                extraBeltSpace = _extraBeltSpace,
                ceilingType = _ceilingType,
                ceilingMode = _ceilingMode,
                ceilingBlockItemId = (_ceilingItemTemplate == null) ? 0 : _ceilingItemTemplate.id,
                floorType = _floorType,
                floorMode = _floorMode,
                floorBlockItemId = (_floorItemTemplate == null) ? 0 : _floorItemTemplate.id
            };
            return saveData;
        }

        public void load(SaveData saveData, int version)
        {
            if (saveData == null)
                return;

            _beltCount = saveData.beltCount;
            _beltTier = saveData.beltTier;
            _extraCeilingSpace = saveData.extraCeilingSpace;
            _extraBeltSpace = saveData.extraBeltSpace;
            _ceilingType = saveData.ceilingType;
            _ceilingMode = saveData.ceilingMode;
            _ceilingItemTemplate = null;
            if (saveData.ceilingBlockItemId != 0)
                _ceilingItemTemplate = ItemTemplateManager.getItemTemplate(saveData.ceilingBlockItemId);
            _floorType = saveData.floorType;
            _floorMode = saveData.floorMode;
            _floorItemTemplate = null;
            if (saveData.floorBlockItemId != 0)
                _floorItemTemplate = ItemTemplateManager.getItemTemplate(saveData.floorBlockItemId);

            if (_beltCount < 1 || _beltCount > 100)
                _beltCount = 4;

            if (_beltTier < 1 || _beltTier > 4)
                _beltTier = 4;

            if (_extraCeilingSpace < 0 || _extraCeilingSpace > 20)
                _extraCeilingSpace = 0;

            if (_extraBeltSpace < 0 || _extraBeltSpace > 20)
                _extraBeltSpace = 0;

            if (_ceilingType < 0 || _ceilingType > 3)
                _ceilingType = 0;
            if (_ceilingMode < 0 || _ceilingMode > 1)
                _ceilingMode = 0;
            if (_ceilingItemTemplate == null)
                _ceilingItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");

            if (_floorType < 0 || _floorType > 3)
                _floorType = 0;
            if (_floorMode < 0 || _floorMode > 1)
                _floorMode = 0;
            if (_floorItemTemplate == null)
                _floorItemTemplate = ItemTemplateManager.getItemTemplate("_base_dirt");
        }

        public void finalizeAfterLoad(SaveData saveData, int version)
        {
        }
    }

}
