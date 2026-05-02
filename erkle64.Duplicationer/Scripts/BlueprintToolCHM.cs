using System.Collections.Generic;
using UnityEngine;
using Unfoundry;
using System.Linq;
using System.IO;
using C3;
using UnityEngine.Pool;

namespace Duplicationer
{
    internal class BlueprintToolCHM : CustomHandheldMode
    {
        public bool IsBlueprintLoaded => CurrentBlueprint != null;
        public Blueprint CurrentBlueprint { get; private set; } = null;
        public string CurrentBlueprintStatusText { get; private set; } = "";
        public Vector3Int CurrentBlueprintSize => CurrentBlueprint == null ? Vector3Int.zero : CurrentBlueprint.Size;
        public Vector3Int CurrentBlueprintAnchor { get; private set; } = Vector3Int.zero;

        public bool IsBlueprintActive { get; private set; } = false;
        public bool IsPlaceholdersHidden { get; private set; } = false;
        private BatchRenderingGroup placeholderRenderGroup = new BatchRenderingGroup();
        private List<BlueprintPlaceholder> buildingPlaceholders = new List<BlueprintPlaceholder>();
        private List<BlueprintPlaceholder> trainTrackPlaceholders = new List<BlueprintPlaceholder>();
        private List<BlueprintPlaceholder> terrainPlaceholders = new List<BlueprintPlaceholder>();
        private int buildingPlaceholderUpdateIndex = 0;
        private int trainTrackPlaceholderUpdateIndex = 0;
        private int terrainPlaceholderUpdateIndex = 0;

        private CustomRadialMenuStateControl menuStateControl = null;
        public BlueprintToolMode CurrentMode { get; private set; } = null;
        public readonly BlueprintToolModePlace modePlace;
        public readonly BlueprintToolModeSelectArea modeSelectArea;
        public readonly BlueprintToolModeResize modeResize;
        public readonly BlueprintToolModeResizeVertical modeResizeVertical;
        public readonly BlueprintToolModeMove modeMove;
        public readonly BlueprintToolModeMoveSideways modeMoveSideways;
        public readonly BlueprintToolModeMoveVertical modeMoveVertical;
        public readonly BlueprintToolModeRepeat modeRepeat;
        private BlueprintToolMode[] _blueprintToolModes;

        public bool BlueprintHasTrainTracks => CurrentBlueprint != null && CurrentBlueprint.HasTrainTracks;

        internal BoxMode boxMode = BoxMode.None;
        public Vector3Int BlueprintMin => CurrentBlueprintAnchor;
        public Vector3Int BlueprintMax => CurrentBlueprintAnchor + CurrentBlueprintSize - Vector3Int.one;

        internal Vector3Int repeatFrom = Vector3Int.zero;
        internal Vector3Int repeatTo = Vector3Int.zero;
        public Vector3Int RepeatCount => repeatTo - repeatFrom + Vector3Int.one;
        public Vector3Int RepeatBlueprintMin => BlueprintMin + new Vector3Int(CurrentBlueprintSize.x * repeatFrom.x, CurrentBlueprintSize.y * repeatFrom.y, CurrentBlueprintSize.z * repeatFrom.z);
        public Vector3Int RepeatBlueprintMax => BlueprintMax + new Vector3Int(CurrentBlueprintSize.x * repeatTo.x, CurrentBlueprintSize.y * repeatTo.y, CurrentBlueprintSize.z * repeatTo.z);

        internal Plane dragPlane = default;
        internal Vector3Int selectionFrom = Vector3Int.zero;
        internal Vector3Int selectionTo = Vector3Int.zero;

        public Vector3Int DragMin => Vector3Int.Min(selectionFrom, selectionTo);
        public Vector3Int DragMax => Vector3Int.Max(selectionFrom, selectionTo);
        public Vector3Int DragSize => DragMax - DragMin + Vector3Int.one;

        internal bool isDragArrowVisible = false;
        internal Ray dragFaceRay = default;
        internal Material dragArrowMaterial = null;
        internal float dragArrowScale = 1.0f;
        internal float dragArrowOffset = 0.5f;
        internal bool isDragArrowDouble = false;

        private BlueprintFrame _blueprintFrame;
        private SaveFrame _saveFrame;
        private LibraryFrame _libraryFrame;
        private FolderFrame _folderFrame;
        private List<DuplicationerFrame> _frames = new List<DuplicationerFrame>();

        public bool IsBlueprintFrameOpen => _blueprintFrame.IsOpen;
        public bool IsSaveFrameOpen => _saveFrame.IsOpen;
        public bool IsLibraryFrameOpen => _libraryFrame.IsOpen;
        public bool IsFolderFrameOpen => _folderFrame.IsOpen;
        public bool IsAnyFrameOpen => _frames.Any(x => x.IsOpen);

        internal int NudgeX => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeX : 1;
        internal int NudgeY => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeY : 1;
        internal int NudgeZ => CurrentBlueprint != null && InputHelpers.IsAltHeld ? CurrentBlueprint.SizeZ : 1;

        private static List<ConstructionTaskGroup> _activeConstructionTaskGroups = new List<ConstructionTaskGroup>();

        private static List<bool> _terrainTypeRemovalMask = null;

        internal LazyPrefab prefabGridScrollView = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/GridScrollView.prefab");
        internal LazyPrefab prefabBlueprintNameInputField = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintNameInputField.prefab");
        internal LazyPrefab prefabBlueprintButtonDefaultIcon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButtonDefaultIcon.prefab");
        internal LazyPrefab prefabBlueprintButtonIcon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButtonIcon.prefab");
        internal LazyPrefab prefabBlueprintButton1Icon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButton1Icon.prefab");
        internal LazyPrefab prefabBlueprintButton2Icon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButton2Icon.prefab");
        internal LazyPrefab prefabBlueprintButton3Icon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButton3Icon.prefab");
        internal LazyPrefab prefabBlueprintButton4Icon = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButton4Icon.prefab");
        internal LazyPrefab prefabBlueprintButtonFolder = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButtonFolder.prefab");
        internal LazyPrefab prefabBlueprintButtonFolderNew = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButtonFolderNew.prefab");
        internal LazyPrefab prefabBlueprintButtonFolderBack = new LazyPrefab("Assets/erkle64.Duplicationer/prefabs/BlueprintButtonFolderBack.prefab");

        private static Material _placeholderMaterial = null;
        private static Material _placeholderPrepassMaterial = null;
        private static Material _placeholderSolidMaterial = null;

        internal LazySprite iconBlack = null;
        internal LazySprite iconEmpty = null;
        internal LazySprite iconCopy = null;
        internal LazySprite iconCopyInPlace = null;
        internal LazySprite iconMoveVertical = null;
        internal LazySprite iconMove = null;
        internal LazySprite iconMoveSideways = null;
        internal LazySprite iconPanel = null;
        internal LazySprite iconPaste = null;
        internal LazySprite iconPlace = null;
        internal LazySprite iconRepeat = null;
        internal LazySprite iconResizeVertical = null;
        internal LazySprite iconResize = null;
        internal LazySprite iconSelectArea = null;
        internal LazySprite iconMirror = null;

        private LazyMaterial materialDragBox = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_placeholder_green);
            material.renderQueue = 3001;
            material.SetFloat("_Opacity", 0.25f);
            material.SetColor("_Color", new Color(0.0f, 0.0f, 1.0f, 0.25f));
            return material;
        });

        private LazyMaterial materialDragBoxEdge = new LazyMaterial(() =>
        {
            var material = new Material(ResourceDB.material_glow_blue);
            material.SetColor("_Color", new Color(0.0f, 0.0f, 0.8f, 1.0f));
            return material;
        });

        public BlueprintToolCHM()
        {
            CreateFrame(ref _blueprintFrame, "Assets/erkle64.Duplicationer/UI/BlueprintFrame.prefab");
            CreateFrame(ref _saveFrame, "Assets/erkle64.Duplicationer/UI/SaveFrame.prefab");
            CreateFrame(ref _libraryFrame, "Assets/erkle64.Duplicationer/UI/LibraryFrame.prefab");
            CreateFrame(ref _folderFrame, "Assets/erkle64.Duplicationer/UI/FolderFrame.prefab");

            _saveFrame.FillSaveGrid();

            dragArrowScale = 1.0f;
            dragArrowOffset = 0.5f;

            _blueprintToolModes = new BlueprintToolMode[]
            {
                modeSelectArea = new BlueprintToolModeSelectArea(),
                modeResize = new BlueprintToolModeResize(),
                modeResizeVertical = new BlueprintToolModeResizeVertical(),
                modePlace = new BlueprintToolModePlace(),
                modeMove = new BlueprintToolModeMove(),
                modeMoveSideways = new BlueprintToolModeMoveSideways(),
                modeMoveVertical = new BlueprintToolModeMoveVertical(),
                modeRepeat = new BlueprintToolModeRepeat()
            };

            modePlace.Connect(modeSelectArea, modeMove);
            modeSelectArea.Connect(modeResize);

            SetPlaceholderOpacity(Config.Preview.previewAlpha.value);
        }

        public override void Registered()
        {
            Messenger.RegisterListener<BlueprintRequest>("Duplicationer.CreateBlueprint", nameof(BlueprintToolCHM), ReceiveBlueprintRequest);
        }

        public override void Deregistered()
        {
            Messenger.DeregisterListener("Duplicationer.CreateBlueprint", nameof(BlueprintToolCHM));
        }

        private void CreateFrame<T>(ref T frame, string prefabPath) where T : DuplicationerFrame
        {
            frame = Object.Instantiate(AssetManager.Database.LoadAssetAtPath<T>(prefabPath), GameRoot.getDefaultCanvas().transform, false);
            frame.Setup(this);
            frame.gameObject.SetActive(false);
            _frames.Add(frame);
        }

        public static bool IsActive { get; private set; } = false;

        public override void Enter()
        {
            if (CurrentMode == null) SelectMode(modePlace);

            CurrentMode?.Enter(this, null);

            IsActive = true;
        }

        public override void Exit()
        {
            CurrentMode?.Exit(this);

            IsActive = false;
        }

        public override void ShowMenu()
        {
            if (IsAnyFrameOpen) return;

            if (menuStateControl == null)
            {
                menuStateControl = new CustomRadialMenuStateControl(
                    new CustomRadialMenuOption(
                        "Confirm Copy", iconCopy.Sprite, "",
                        CopySelection,
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Confirm Copy In Place", iconCopyInPlace.Sprite, "",
                        CopySelectionInPlace,
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Confirm Paste", iconPaste.Sprite, "",
                        () =>
                        {
                            isDragArrowVisible = false;
                            PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_paste);
                        },
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Place", iconPlace.Sprite, "",
                        () => SelectMode(modePlace),
                        () => IsBlueprintLoaded && CurrentMode != modePlace),

                    new CustomRadialMenuOption(
                        "Select Area", iconSelectArea.Sprite, "Clears current blueprint",
                        () => SelectMode(modeSelectArea)),

                    new CustomRadialMenuOption(
                        "Move", iconMove.Sprite, "",
                        () => SelectMode(modeMove),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Move Sideways", iconMoveSideways.Sprite, "",
                        () => SelectMode(modeMoveSideways),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Move Vertical", iconMoveVertical.Sprite, "",
                        () => SelectMode(modeMoveVertical),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Resize", iconResize.Sprite, "",
                        () => SelectMode(modeResize),
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Resize Vertical", iconResizeVertical.Sprite, "",
                        () => SelectMode(modeResizeVertical),
                        () => CurrentMode != null && CurrentMode.AllowCopy(this)),

                    new CustomRadialMenuOption(
                        "Repeat", iconRepeat.Sprite, "",
                        () => SelectMode(modeRepeat),
                        () => CurrentMode != null && CurrentMode.AllowPaste(this)),

                    new CustomRadialMenuOption(
                        "Mirror", iconMirror.Sprite, "",
                        () => MirrorBlueprint(),
                        () => CurrentMode != null && CurrentMode.AllowMirror(this) && CurrentBlueprint != null && CurrentBlueprint.IsMirrorable),

                    new CustomRadialMenuOption(
                        "Open Panel", iconPanel.Sprite, "",
                        ShowBlueprintFrame)
                );
            }

            if (menuStateControl != null)
            {
                CustomRadialMenuSystem.Instance.ShowMenu(menuStateControl.GetMenuOptions());
            }
        }

        internal void SelectMode(BlueprintToolMode mode)
        {
            var fromMode = CurrentMode;
            CurrentMode?.Exit(this);
            CurrentMode = mode;
            CurrentMode?.Enter(this, fromMode);
        }

        internal void CopySelection()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(DragMin, DragSize);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        internal void CopySelectionInPlace()
        {
            repeatFrom = repeatTo = Vector3Int.zero;
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(DragMin, DragSize);
            isDragArrowVisible = false;
            SelectMode(modeRepeat);
            boxMode = BoxMode.Blueprint;
            ShowBlueprint(DragMin);
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        public void CopyCustomSelection(Vector3Int from, Vector3Int size, IEnumerable<BuildableObjectGO> buildings, IEnumerable<ulong> trainTrackIds, byte[] blocks)
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(from, size, buildings, trainTrackIds, blocks);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        public void ReceiveBlueprintRequest(BlueprintRequest blueprintRequest)
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = Blueprint.Create(blueprintRequest);
            TabletHelper.SetTabletTextQuickActions($"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint");
            isDragArrowVisible = false;
            SelectMode(modePlace);
            boxMode = BoxMode.None;
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_copy);
        }

        private BlueprintPlaceholder FindPlaceholderForBuilding(int index)
        {
            for (int i = 0; i < buildingPlaceholders.Count; i++)
            {
                if (buildingPlaceholders[i].Index == index && buildingPlaceholders[i].Template != null)
                {
                    return buildingPlaceholders[i];
                }
            }

            return null;
        }

        private BlueprintPlaceholder FindPlaceholderForTrainTracks(int index)
        {
            for (int i = 0; i < trainTrackPlaceholders.Count; i++)
            {
                if (trainTrackPlaceholders[i].Index == index && trainTrackPlaceholders[i].TrainTrackTemplate != null)
                {
                    return trainTrackPlaceholders[i];
                }
            }
            return null;
        }

        private BlueprintPlaceholder FindPlaceholderByOriginalIdForBuilding(ulong entityId)
        {
            for (int i = 0; i < buildingPlaceholders.Count; i++)
                if (buildingPlaceholders[i].OriginalEntityId == entityId && buildingPlaceholders[i].Template != null)
                    return buildingPlaceholders[i];

            return null;
        }

        private BlueprintPlaceholder FindPlaceholderByOriginalIdForTrainTracks(ulong entityId)
        {
            for (int i = 0; i < trainTrackPlaceholders.Count; i++)
                if (trainTrackPlaceholders[i].OriginalEntityId == entityId && trainTrackPlaceholders[i].TrainTrackTemplate != null)
                    return trainTrackPlaceholders[i];

            return null;
        }

        private ulong FindExistingEntityIdForBuilding(ulong originalEntityId)
        {
            var placeholder = FindPlaceholderByOriginalIdForBuilding(originalEntityId);
            if (placeholder == null || placeholder.Template == null)
                return 0uL;

            var template = placeholder.Template;
            var buildableObjectData = CurrentBlueprint.GetBuildableObjectData(placeholder.Index);

            int wx, wy, wz;
            if (template.canBeRotatedAroundXAxis)
                BuildingManager.getWidthFromUnlockedOrientation(template.size, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
            else
                BuildingManager.getWidthFromOrientation(template, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

            var repeatOffset = new Vector3Int(placeholder.RepeatIndex.x * CurrentBlueprintSize.x, placeholder.RepeatIndex.y * CurrentBlueprintSize.y, placeholder.RepeatIndex.z * CurrentBlueprintSize.z);
            var worldPos = new Vector3Int(buildableObjectData.worldX + CurrentBlueprintAnchor.x + repeatOffset.x, buildableObjectData.worldY + CurrentBlueprintAnchor.y + repeatOffset.y, buildableObjectData.worldZ + CurrentBlueprintAnchor.z + repeatOffset.z);

            AABB3D aabb = new(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
            return Blueprint.CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
        }

        private ulong FindExistingEntityIdForTrainTracks(ulong originalEntityId)
        {
            var placeholder = FindPlaceholderByOriginalIdForTrainTracks(originalEntityId);
            if (placeholder == null || placeholder.TrainTrackTemplate == null)
                return 0uL;

            var template = placeholder.TrainTrackTemplate;
            var trainTrackData = CurrentBlueprint.GetTrainTrackData(placeholder.Index);

            var repeatOffset = new Vector3Int(placeholder.RepeatIndex.x * CurrentBlueprintSize.x, placeholder.RepeatIndex.y * CurrentBlueprintSize.y, placeholder.RepeatIndex.z * CurrentBlueprintSize.z);
            var worldPos = new Vector3Int(trainTrackData.worldX + CurrentBlueprintAnchor.x + repeatOffset.x, trainTrackData.worldY + CurrentBlueprintAnchor.y + repeatOffset.y, trainTrackData.worldZ + CurrentBlueprintAnchor.z + repeatOffset.z);

            return Blueprint.CheckIfTrainTrackExists(worldPos, trainTrackData);
        }

        public static List<AABB3D> debugAABBs = new();
        public override void UpdateBehavoir()
        {
            foreach (var debugAABB in debugAABBs)
            {
                var size = new Vector3(debugAABB.wx, debugAABB.wy, debugAABB.wz);
                var center = new Vector3(debugAABB.x0, debugAABB.y0, debugAABB.z0) + size * 0.5f;
                GameRoot.pushPerFrameHighlighterBox(center, size, 0);
            }

            if (IsBlueprintActive)
            {
                int count = Mathf.Min(Config.Events.maxBuildingValidationsPerFrame.value, buildingPlaceholders.Count);
                if (buildingPlaceholderUpdateIndex >= buildingPlaceholders.Count) buildingPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = buildingPlaceholders[buildingPlaceholderUpdateIndex];
                    var buildableObjectData = CurrentBlueprint.GetBuildableObjectData(placeholder.Index);
                    var buildableObjectPlaceholder = placeholder;
                    if (placeholder.Template == null)
                        buildableObjectPlaceholder = FindPlaceholderForBuilding(placeholder.Index);

                    var buildableObjectTemplate = ItemTemplateManager.getBuildableObjectTemplate(buildableObjectData.templateId);

                    var repeatOffset = new Vector3Int(buildableObjectPlaceholder.RepeatIndex.x * CurrentBlueprintSize.x, buildableObjectPlaceholder.RepeatIndex.y * CurrentBlueprintSize.y, buildableObjectPlaceholder.RepeatIndex.z * CurrentBlueprintSize.z);
                    var worldPos = new Vector3Int(buildableObjectData.worldX + CurrentBlueprintAnchor.x + repeatOffset.x, buildableObjectData.worldY + CurrentBlueprintAnchor.y + repeatOffset.y, buildableObjectData.worldZ + CurrentBlueprintAnchor.z + repeatOffset.z);
                    int wx, wy, wz;
                    if (buildableObjectTemplate.canBeRotatedAroundXAxis)
                        BuildingManager.getWidthFromUnlockedOrientation(buildableObjectTemplate.size, buildableObjectData.orientationUnlocked, out wx, out wy, out wz);
                    else
                        BuildingManager.getWidthFromOrientation(buildableObjectTemplate, (BuildingManager.BuildOrientation)buildableObjectData.orientationY, out wx, out wy, out wz);

                    byte errorCodeRaw = 0;
                    v3i[] powerLineCollisionArray = new v3i[0];
                    BuildingManager.buildingManager_validateConstruction_buildableEntityWrapper(new v3i(worldPos.x, worldPos.y, worldPos.z), buildableObjectData.orientationY, buildableObjectData.orientationUnlocked, buildableObjectData.templateId, ref errorCodeRaw, IOBool.iofalse, powerLineCollisionArray, 0);
                    var errorCode = (BuildingManager.CheckBuildableErrorCode)errorCodeRaw;

                    if (errorCode == BuildingManager.CheckBuildableErrorCode.RequiresTrainTracks)
                    {
                        // for buildings that require train tracks, ignore the error about missing train tracks and just check if the building itself can be placed
                        // this is because the train tracks might be part of the same blueprint and not yet recognized as existing in the world
                        errorCode = BuildingManager.CheckBuildableErrorCode.Success;
                    }

                    if (buildableObjectPlaceholder.ExtraBoundingBoxes != null)
                    {
                        foreach (var extraBox in buildableObjectPlaceholder.ExtraBoundingBoxes)
                        {
                            AABB3D aabb = new(
                                extraBox.position.x + CurrentBlueprintAnchor.x,
                                extraBox.position.y + CurrentBlueprintAnchor.y,
                                extraBox.position.z + CurrentBlueprintAnchor.z,
                                extraBox.size.x,
                                extraBox.size.y,
                                extraBox.size.z);

                            if (aabb.y0 < 0 || aabb.y1 >= 256)
                            {
                                errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                break;
                            }

                            using (var query = StreamingSystem.get().queryAABB3D(aabb))
                            {
                                foreach (var bogo in query)
                                {
                                    if (aabb.hasXYZIntersection(bogo.aabb))
                                    {
                                        errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                        break;
                                    }
                                }
                            }

                            var from = new Vector3Int(aabb.x0, aabb.y0, aabb.z0);
                            var to = new Vector3Int(aabb.x0 + aabb.wx - 1, aabb.y0 + aabb.wy - 1, aabb.z0 + aabb.wz - 1);
                            for (int bz = from.z; bz < to.z; ++bz)
                            {
                                for (int by = from.y; by < to.y; ++by)
                                {
                                    for (int bx = from.x; bx < to.x; ++bx)
                                    {
                                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(bx, by, bz, out ulong chunkIndex, out uint blockIndex);
                                        var blockId = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                                        if (blockId > 0)
                                        {
                                            errorCode = BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea;
                                            break;
                                        }
                                    }
                                    if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                                }
                                if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                            }

                            if (errorCode == BuildingManager.CheckBuildableErrorCode.BlockedByReservedArea) break;
                        }
                    }

                    bool positionClear = false;
                    ulong positionFilledByEntityId = 0uL;

                    switch (errorCode)
                    {
                        case BuildingManager.CheckBuildableErrorCode.Success:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_MissingModuleAttachmentPoint:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_ModuleValidationFailed:
                        case BuildingManager.CheckBuildableErrorCode.ModularBuilding_MissingFoundation:
                            positionClear = true;
                            break;

                        case BuildingManager.CheckBuildableErrorCode.BlockedByBuildableObject_Building:
                            AABB3D aabb = new(worldPos.x, worldPos.y, worldPos.z, wx, wy, wz);
                            positionFilledByEntityId = Blueprint.CheckIfBuildingExists(aabb, worldPos, buildableObjectData);
                            break;
                    }

                    if (placeholder.Template == null)
                    {
                        var entityData = new BuildableEntity.BuildableEntityGeneralData();
                        var targetEntityId = FindExistingEntityIdForBuilding(placeholder.OriginalEntityId);
                        if (targetEntityId != 0uL && BuildingManager.buildingManager_getBuildableEntityGeneralData(targetEntityId, ref entityData) == IOBool.iotrue)
                        {
                            if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(targetEntityId, positionFilledByEntityId) == IOBool.iotrue)
                            {
                                placeholder.SetState(BlueprintPlaceholder.State.Done);
                            }
                            else
                            {
                                placeholder.SetState(BlueprintPlaceholder.State.Clear);
                            }
                        }
                        else
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Clear);
                        }
                    }
                    else if (positionFilledByEntityId != 0uL)
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Done);
                    }
                    else if (positionClear)
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Clear);
                    }
                    else
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Blocked);
                    }

                    buildingPlaceholders[buildingPlaceholderUpdateIndex] = placeholder;

                    if (++buildingPlaceholderUpdateIndex >= buildingPlaceholders.Count) buildingPlaceholderUpdateIndex = 0;
                }

                count = Mathf.Min(Config.Events.maxTrainTrackValidationsPerFrame.value, trainTrackPlaceholders.Count);
                if (trainTrackPlaceholderUpdateIndex >= trainTrackPlaceholders.Count) trainTrackPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = trainTrackPlaceholders[trainTrackPlaceholderUpdateIndex];
                    var trainTrackData = CurrentBlueprint.GetTrainTrackData(placeholder.Index);
                    var trainTrackPlaceholder = placeholder;
                    if (placeholder.TrainTrackTemplate == null)
                        trainTrackPlaceholder = FindPlaceholderForTrainTracks(placeholder.Index);

                    var trainTrackTemplate = ItemTemplateManager.getTrainTrackTemplate(trainTrackData.templateId);
                    var repeatOffset = new Vector3Int(trainTrackPlaceholder.RepeatIndex.x * CurrentBlueprintSize.x, trainTrackPlaceholder.RepeatIndex.y * CurrentBlueprintSize.y, trainTrackPlaceholder.RepeatIndex.z * CurrentBlueprintSize.z);
                    var worldPos = new Vector3Int(trainTrackData.worldX + CurrentBlueprintAnchor.x + repeatOffset.x, trainTrackData.worldY + CurrentBlueprintAnchor.y + repeatOffset.y, trainTrackData.worldZ + CurrentBlueprintAnchor.z + repeatOffset.z);
                    bool positionClear = true;
                    bool positionFilled = false;
                    uint terrainToClear = 0u;
                    uint decorToClear = 0u;
                    byte errorCode = 0;
                    var validationResult = TrainSystem.trainSystemManager_validateConstructTracks(trainTrackTemplate.id, new v3i(worldPos), trainTrackData.orientationY, ref errorCode, ref terrainToClear, ref decorToClear);
                    if (validationResult == IOBool.iofalse)
                    {
                        if (errorCode == (byte)TrainSystem.CheckTrainTracksErrorCode.AlreadyPlaced)
                            positionFilled = true;
                        else if (terrainToClear == 0 && decorToClear == 0)
                            positionClear = false;
                    }

                    if (positionClear)
                    {
                        if (positionFilled)
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Done);
                        }
                        else
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Clear);
                        }
                    }
                    else
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Blocked);
                    }

                    trainTrackPlaceholders[trainTrackPlaceholderUpdateIndex] = placeholder;

                    if (++trainTrackPlaceholderUpdateIndex >= trainTrackPlaceholders.Count) trainTrackPlaceholderUpdateIndex = 0;
                }

                count = Mathf.Min(Config.Events.maxTerrainValidationsPerFrame.value, terrainPlaceholders.Count);
                if (terrainPlaceholderUpdateIndex >= terrainPlaceholders.Count) terrainPlaceholderUpdateIndex = 0;
                for (int i = 0; i < count; i++)
                {
                    var placeholder = terrainPlaceholders[terrainPlaceholderUpdateIndex];
                    var worldPos = new Vector3Int(Mathf.FloorToInt(placeholder.Position.x), Mathf.FloorToInt(placeholder.Position.y), Mathf.FloorToInt(placeholder.Position.z));

                    bool positionClear = true;
                    bool positionFilled = false;

                    var queryResult = StreamingSystem.get().queryPointXYZ(worldPos);
                    if (queryResult != null)
                    {
                        positionClear = false;
                    }
                    else
                    {
                        ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(worldPos.x, worldPos.y, worldPos.z, out ulong chunkIndex, out uint blockIndex);

                        var blockId = CurrentBlueprint.GetBlockId(placeholder.Index);
                        var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);
                        if (terrainData == blockId)
                        {
                            positionFilled = true;
                        }
                        else if (terrainData > 0)
                        {
                            positionClear = false;
                        }
                    }

                    if (positionClear)
                    {
                        if (positionFilled)
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Done);
                        }
                        else
                        {
                            placeholder.SetState(BlueprintPlaceholder.State.Clear);
                        }
                    }
                    else
                    {
                        placeholder.SetState(BlueprintPlaceholder.State.Blocked);
                    }

                    terrainPlaceholders[terrainPlaceholderUpdateIndex] = placeholder;

                    if (++terrainPlaceholderUpdateIndex >= terrainPlaceholders.Count) terrainPlaceholderUpdateIndex = 0;
                }

                string text = "";

                int countUntested = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Untested);
                if (countUntested > 0) text += $"<color=#CCCCCC>Untested:</color> {countUntested}\n";

                int countReady;
                int countMissing;

                if (DuplicationerSystem.IsCheatModeEnabled)
                {
                    countReady = 0;
                    countMissing = 0;

                    foreach (var kv in BlueprintPlaceholder.GetStateCounts(BlueprintPlaceholder.State.Clear))
                    {
                        if (kv.Key != 0)
                        {
                            var clear = kv.Value;
                            if (clear > 0) countReady += clear;
                        }
                    }
                }
                else
                {
                    ulong inventoryId = GameRoot.getClientCharacter().inventoryId;
                    ulong inventoryPtr = inventoryId != 0 ? InventoryManager.inventoryManager_getInventoryPtr(inventoryId) : 0;
                    if (inventoryPtr != 0)
                    {
                        countReady = 0;
                        countMissing = 0;

                        foreach (var kv in BlueprintPlaceholder.GetStateCounts(BlueprintPlaceholder.State.Clear))
                        {
                            if (kv.Key != 0)
                            {
                                var clear = kv.Value;
                                if (clear > 0)
                                {
                                    var inventoryCount = InventoryManager.inventoryManager_countByItemTemplateByPtr(inventoryPtr, kv.Key, IOBool.iotrue);
                                    if (inventoryCount >= clear)
                                    {
                                        countReady += clear;
                                    }
                                    else
                                    {
                                        countReady += (int)inventoryCount;
                                        countMissing += clear - (int)inventoryCount;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        countMissing = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Clear);
                        countReady = 0;
                    }
                }
                if (countMissing > 0) text += $"Missing: {countMissing}\n";
                if (countReady > 0) text += $"Ready: {countReady}\n";

                int countBlocked = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked);
                if (countBlocked > 0) text += $"<color=\"red\">Blocked:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Blocked)}\n";

                int countDone = BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done);
                if (countDone > 0) text += $"<color=#AACCFF>Done:</color> {BlueprintPlaceholder.GetStateCount(BlueprintPlaceholder.State.Done)}\n";

                if (countUntested > 0 || countBlocked > 0 || countMissing > 0 || countReady > 0 || countDone > 0)
                {
                    CurrentBlueprintStatusText = text + $"Total: {BlueprintPlaceholder.GetStateCountTotal()}";
                }
                else
                {
                    CurrentBlueprintStatusText = "";
                }
                _blueprintFrame.UpdateMaterialReport();

                int remainingTasks = 0;
                foreach (var group in _activeConstructionTaskGroups) remainingTasks += group.Remaining;
                if (remainingTasks > 0) CurrentBlueprintStatusText = $"ToDo: {remainingTasks}{(CurrentBlueprintStatusText.Length > 0 ? "\n" : "")}{CurrentBlueprintStatusText}";

                if (!IsPlaceholdersHidden)
                {
                    if (_placeholderMaterial == null)
                    {
                        _placeholderMaterial = AssetManager.Database.LoadAssetAtPath<Material>("Assets/erkle64.Duplicationer/Materials/PlaceholderMaterial.mat");
                        Object.DontDestroyOnLoad(_placeholderMaterial);
                    }

                    if (_placeholderPrepassMaterial == null)
                    {
                        _placeholderPrepassMaterial = AssetManager.Database.LoadAssetAtPath<Material>("Assets/erkle64.Duplicationer/Materials/PlaceholderPrepassMaterial.mat");
                        Object.DontDestroyOnLoad(_placeholderPrepassMaterial);
                    }

                    if (_placeholderSolidMaterial == null)
                    {
                        _placeholderSolidMaterial = AssetManager.Database.LoadAssetAtPath<Material>("Assets/erkle64.Duplicationer/Materials/PlaceholderSolidMaterial.mat");
                        Object.DontDestroyOnLoad(_placeholderSolidMaterial);
                    }

                    float alpha = Config.Preview.previewAlpha.value;
                    if (alpha > 0.0f)
                    {
                        if (alpha < 1.0f)
                        {
                            if (_placeholderMaterial != null && _placeholderPrepassMaterial != null)
                            {
                                placeholderRenderGroup.Render(_placeholderPrepassMaterial, _placeholderMaterial);
                            }
                        }
                        else
                        {
                            if (_placeholderSolidMaterial != null)
                            {
                                placeholderRenderGroup.Render(null, _placeholderSolidMaterial);
                            }
                        }
                    }
                }
            }

            if (IsBlueprintLoaded && IsBlueprintActive && Input.GetKeyDown(Config.Input.pasteBlueprintKey.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                PlaceBlueprintMultiple(CurrentBlueprintAnchor, repeatFrom, repeatTo);
                AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_recipeCopyTool_paste);
            }

            if (Input.GetKeyDown(Config.Input.togglePanelKey.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                if (IsBlueprintFrameOpen) HideBlueprintFrame();
                else ShowBlueprintFrame();
            }

            if (Input.GetKeyDown(Config.Input.saveBlueprintKey.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                if (IsBlueprintLoaded) BeginSaveBlueprint();
            }

            if (Input.GetKeyDown(Config.Input.loadBlueprintKey.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                BeginLoadBlueprint();
            }

            CurrentMode?.Update(this);

            TabletHelper.SetTabletTextAnalyzer(GetTabletTitle());
            TabletHelper.SetTabletTextLastCopiedConfig(CurrentBlueprintStatusText.Replace('\n', ' '));
            TabletHelper.ClearDataSignals();

            switch (boxMode)
            {
                case BoxMode.None:
                    GameRoot.setHighVisibilityInfoText(ActionManager.StatusText);
                    break;

                case BoxMode.Blueprint:
                    DrawBoxWithEdges(RepeatBlueprintMin, RepeatBlueprintMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    var repeatCount = RepeatCount;
                    GameRoot.setHighVisibilityInfoText((repeatCount != Vector3Int.one) ? $"{ActionManager.StatusText} Repeat: {repeatCount.x}x{repeatCount.y}x{repeatCount.z}\n{CurrentBlueprintStatusText}" : $"{ActionManager.StatusText} {CurrentBlueprintStatusText}");
                    break;

                case BoxMode.Selection:
                    DrawBoxWithEdges(DragMin, DragMax + Vector3.one, 0.015f, 0.04f, materialDragBox.Material, materialDragBoxEdge.Material);
                    GameRoot.setHighVisibilityInfoText(DragSize == Vector3Int.one ? ActionManager.StatusText : $"{ActionManager.StatusText} {DragSize.x}x{DragSize.y}x{DragSize.z}");
                    break;
            }

            if (isDragArrowVisible)
            {
                DrawArrow(dragFaceRay.origin, dragFaceRay.direction, dragArrowMaterial, dragArrowScale, dragArrowOffset);
                if (isDragArrowDouble) DrawArrow(dragFaceRay.origin, -dragFaceRay.direction, dragArrowMaterial, dragArrowScale, dragArrowOffset);
            }
        }

        public override bool OnRotateY()
        {
            if (!IsBlueprintLoaded || !IsBlueprintActive) return true;
            if (CurrentMode == null || !CurrentMode.AllowRotate(this)) return false;

            RotateBlueprint();
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);

            return false;
        }

        internal void ClearBlueprintPlaceholders()
        {
            if (IsBlueprintActive)
            {
                IsBlueprintActive = false;

                foreach (var placeholder in buildingPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                }
                buildingPlaceholders.Clear();

                foreach (var placeholder in trainTrackPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                }
                trainTrackPlaceholders.Clear();

                foreach (var placeholder in terrainPlaceholders)
                {
                    placeholder.SetState(BlueprintPlaceholder.State.Invalid);
                }
                terrainPlaceholders.Clear();

                placeholderRenderGroup.Clear();
            }
        }

        internal void MoveBlueprint(Vector3Int newPosition)
        {
            if (BlueprintHasTrainTracks)
            {
                // align to 4x4 grid if blueprint has train tracks
                // note: must handle negative positions correctly, so that for example -1 becomes -4 instead of 0
                newPosition.x = newPosition.x - (((newPosition.x % 4) + 4) % 4);
                newPosition.z = newPosition.z - (((newPosition.z % 4) + 4) % 4);
            }

            ShowBlueprint(newPosition);

            if (IsBlueprintActive) OnBlueprintMoved();
        }

        internal void PlaceBlueprintMultiple(Vector3Int targetPosition, Vector3Int repeatFrom, Vector3Int repeatTo)
        {
            for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
            {
                for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                {
                    for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                    {
                        PlaceBlueprint(targetPosition + new Vector3Int(x * CurrentBlueprintSize.x, y * CurrentBlueprintSize.y, z * CurrentBlueprintSize.z));
                    }
                }
            }
        }

        internal void ClearConstructionTaskGroups()
        {
            foreach (var taskGroup in _activeConstructionTaskGroups)
                taskGroup.ClearTasks();

            _activeConstructionTaskGroups.Clear();
        }

        internal void PlaceBlueprint(Vector3Int targetPosition)
        {
            if (CurrentBlueprint == null) throw new System.ArgumentNullException(nameof(CurrentBlueprint));

            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            DuplicationerSystem.log.Log(string.Format("Placing blueprint at {0}", targetPosition.ToString()));
            var modularBaseCoords = new Dictionary<ulong, Vector3Int>();
            var constructionTaskGroup = new ConstructionTaskGroup((ConstructionTaskGroup taskGroup) => { _activeConstructionTaskGroups.Remove(taskGroup); });
            _activeConstructionTaskGroups.Add(constructionTaskGroup);

            CurrentBlueprint.Place(targetPosition, constructionTaskGroup);

            constructionTaskGroup.SortTasks();
            ActionManager.AddQueuedEvent(() => constructionTaskGroup.InvokeNextTask());
        }

        internal void RotateBlueprint()
        {
            CurrentBlueprint?.Rotate();
        }

        internal void MirrorBlueprint()
        {
            if (!IsBlueprintLoaded || !IsBlueprintActive) return;
            if (CurrentMode == null || !CurrentMode.AllowMirror(this)) return;

            CurrentBlueprint?.Mirror();
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_construction);
        }

        internal void HideBlueprint()
        {
            IsPlaceholdersHidden = true;
        }

        internal void ShowBlueprint()
        {
            ShowBlueprint(CurrentBlueprintAnchor);
        }

        internal void ShowBlueprint(Vector3Int targetPosition)
        {
            if (!IsBlueprintLoaded) return;

            if (BlueprintHasTrainTracks)
            {
                // align to 4x4 grid if blueprint has train tracks
                // note: must handle negative positions correctly, so that for example -1 becomes -4 instead of 0
                targetPosition.x = targetPosition.x - (((targetPosition.x % 4) + 4) % 4);
                targetPosition.z = targetPosition.z - (((targetPosition.z % 4) + 4) % 4);
            }

            IsPlaceholdersHidden = false;

            if (!IsBlueprintActive)
            {
                IsBlueprintActive = true;
                placeholderRenderGroup.Clear();

                CurrentBlueprint?.Show(targetPosition, repeatFrom, repeatTo, CurrentBlueprintSize, placeholderRenderGroup, buildingPlaceholders, trainTrackPlaceholders, terrainPlaceholders);
            }
            else if (targetPosition != CurrentBlueprintAnchor)
            {
                var offset = targetPosition - CurrentBlueprintAnchor;
                placeholderRenderGroup.Move(offset);
                foreach (var placeholder in buildingPlaceholders) placeholder.Moved(offset);
                foreach (var placeholder in trainTrackPlaceholders) placeholder.Moved(offset);
                foreach (var placeholder in terrainPlaceholders) placeholder.Moved(offset);
            }

            CurrentBlueprintAnchor = targetPosition;
        }

        internal void RefreshBlueprint()
        {
            ClearBlueprintPlaceholders();
            ShowBlueprint();
        }

        private void OnBlueprintMoved()
        {
            _blueprintFrame.UpdateBlueprintPositionText();
        }

        private string GetTabletTitle()
        {
            return CurrentMode?.TabletTitle(this) ?? "";
        }

        internal void BeginSaveBlueprint()
        {
            HideBlueprintFrame(true);
            ShowSaveFrame();
        }

        internal void FinishSaveBlueprint()
        {
            if (CurrentBlueprint == null) throw new System.ArgumentNullException(nameof(CurrentBlueprint));

            string name = _saveFrame.BlueprintName;
            if (string.IsNullOrWhiteSpace(name)) return;

            string filenameBase = Path.Combine(Path.GetDirectoryName(name), PathHelpers.MakeValidFileName(Path.GetFileName(name)));
            string path = Path.Combine(DuplicationerSystem.BlueprintFolder, $"{filenameBase}.{DuplicationerSystem.BLUEPRINT_EXTENSION}");
            if (File.Exists(path))
            {
                ConfirmationFrame.Show($"Overwrite '{name}'?", "Overwrite", () =>
                {
                    DuplicationerSystem.log.Log($"Saving blueprint '{name}' to '{path}'");
                    if (CurrentBlueprint.Save(path, Path.GetFileName(name), _saveFrame.IconItemTemplates.Take(_saveFrame.IconCount).ToArray()))
                        HideSaveFrame();
                });
            }
            else
            {
                DuplicationerSystem.log.Log($"Saving blueprint '{name}' to '{path}'");
                if (CurrentBlueprint.Save(path, Path.GetFileName(name), _saveFrame.IconItemTemplates.Take(_saveFrame.IconCount).ToArray()))
                    HideSaveFrame();
            }
        }

        internal void BeginLoadBlueprint()
        {
            HideBlueprintFrame(true);
            ShowLibraryFrame();
        }

        internal void HideBlueprintFrame(bool silent = false)
        {
            _blueprintFrame.Hide(silent);
        }

        internal void ShowBlueprintFrame()
        {
            _blueprintFrame.Show();
        }

        internal void HideSaveFrame(bool silent = false)
        {
            _saveFrame.Hide(silent);
        }

        internal void ShowSaveFrame()
        {
            _saveFrame.Show();
        }

        internal void HideLibraryFrame(bool silent = false)
        {
            _libraryFrame.Hide(silent);
        }

        internal void ShowLibraryFrame(SaveFrame saveFrame = null)
        {
            _libraryFrame.Show(saveFrame);
        }

        internal void FillLibraryGrid(string relativePath, SaveFrame saveFrame = null)
        {
            _libraryFrame.FillLibraryGrid(relativePath, saveFrame);
        }

        internal void HideFolderFrame(bool silent = false)
        {
            _folderFrame.Hide(silent);
        }

        internal void ShowFolderFrame(string relativePath, string folderName)
        {
            _folderFrame.Show(relativePath, folderName);
        }

        private static List<bool> GetTerrainTypeRemovalMask()
        {
            if (_terrainTypeRemovalMask == null)
            {
                var terrainTypes = ItemTemplateManager.getAllTerrainTemplates();

                _terrainTypeRemovalMask = new List<bool>
                {
                    false, // Air
                    false // ???
                };

                foreach (var terrainType in terrainTypes)
                {
                    _terrainTypeRemovalMask.Add(terrainType.Value.destructible);
                }
            }

            return _terrainTypeRemovalMask;
        }

        [System.Serializable]
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
        internal void DestroyArea(bool doTerrain, bool doDecor, Vector3Int from, Vector3Int to)
        {
            Messenger.Send("BulkDemolishTerrain_Destroy", new BulkDemolishTerrainDestroyRequest
            {
                worldPosX = from.x,
                worldPosY = from.y,
                worldPosZ = from.z,
                sizeX = to.x - from.x + 1,
                sizeY = to.y - from.y + 1,
                sizeZ = to.z - from.z + 1,
                destroyTerrain = doTerrain,
                destroyDecor = doDecor
            });
        }

        internal void DestroyArea(bool doTerrain, bool doDecor)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                DestroyArea(doTerrain, doDecor, from, to);
            }
        }

        internal void DemolishArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor, bool doTracks, Vector3Int from, Vector3Int to, HashSet<ulong> ignoreSet)
        {
            ulong characterHash = GameRoot.getClientCharacter().usernameHash;

            if (doBuildings || doDecor)
            {
                AABB3D aabb = new(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                using (var query = StreamingSystem.get().queryAABB3D(aabb))
                {
                    foreach (var bogo in query)
                    {
                        if (ignoreSet != null && ignoreSet.Contains(bogo.relatedEntityId))
                            continue;

                        if (bogo.template.type == BuildableObjectTemplate.BuildableObjectType.WorldDecorMineAble)
                        {
                            if (doDecor)
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    GameRoot.addLockstepEvent(new Character.RemoveWorldDecorEvent(characterHash, bogo.relatedEntityId, 0));
                                });

                                if (ignoreSet != null)
                                    ignoreSet.Add(bogo.relatedEntityId);
                            }
                        }
                        else if (doBuildings)
                        {
                            ActionManager.AddQueuedEvent(() =>
                            {
                                GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, bogo.relatedEntityId, bogo.placeholderId, 0));
                            });

                            if (ignoreSet != null)
                                ignoreSet.Add(bogo.relatedEntityId);
                        }
                    }
                }
            }

            if (doTracks)
            {
                AABB3D aabb = new(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1);
                var trackEntityIds = ListPool<ulong>.Get();
                TrainRenderSystem.get().queryTrainTracksByVoxelAABB3D(aabb, true, trackEntityIds);
                foreach ( var trackEntityId in trackEntityIds)
                {
                    GameRoot.addLockstepEvent(
                        new Character.DemolishTrainTrackEvent(characterHash, trackEntityId, 0)
                        );
                }
                ListPool<ulong>.Release(trackEntityIds);
            }

            if (doBlocks || doTerrain)
            {
                var shouldRemove = GetTerrainTypeRemovalMask();

                int blocksRemoved = 0;
                for (int wz = from.z; wz <= to.z; ++wz)
                {
                    for (int wy = from.y; wy <= to.y; ++wy)
                    {
                        for (int wx = from.x; wx <= to.x; ++wx)
                        {
                            ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(wx, wy, wz, out ulong chunkIndex, out uint blockIndex);
                            var terrainData = ChunkManager.chunks_getTerrainData(chunkIndex, blockIndex);

                            if (terrainData >= GameRoot.BUILDING_PART_ARRAY_IDX_START && doBlocks)
                            {
                                var worldPos = new Vector3Int(wx, wy, wz);
                                ulong entityId = 0;
                                ChunkManager.chunks_getBuildingPartBlock(chunkIndex, blockIndex, ref entityId);
                                ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.DemolishBuildingEvent(characterHash, entityId, 0, 0)));
                                ++blocksRemoved;
                            }
                            else if (doTerrain && terrainData > 0 && terrainData < GameRoot.BUILDING_PART_ARRAY_IDX_START && terrainData < shouldRemove.Count && shouldRemove[terrainData])
                            {
                                var worldPos = new Vector3Int(wx, wy, wz);
                                ActionManager.AddQueuedEvent(() => GameRoot.addLockstepEvent(new Character.RemoveTerrainEvent(characterHash, worldPos, 0, false)));
                                ++blocksRemoved;
                            }
                        }
                    }
                }
            }
        }

        internal void DemolishArea(bool doBuildings, bool doBlocks, bool doTerrain, bool doDecor, bool doTracks, HashSet<ulong> ignoreSet = null)
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                DemolishArea(doBuildings, doBlocks, doTerrain, doDecor, doTracks, from, to, ignoreSet);
            }
        }

        internal void DroneDemolishArea(Vector3Int from, Vector3Int to)
        {
            GameRoot.addLockstepEvent(new GameRoot.SetMiningAreaEvent(
                new AABB3D(from.x, from.y, from.z, to.x - from.x + 1, to.y - from.y + 1, to.z - from.z + 1),
                true
            ));
        }

        internal void DroneDemolishArea()
        {
            if (TryGetSelectedArea(out Vector3Int from, out Vector3Int to))
            {
                DroneDemolishArea(from, to);
            }
        }

        // clears blueprint and selects the area of the current blueprint
        internal void SelectBounds()
        {
            if (!IsBlueprintLoaded || !IsBlueprintActive) return;

            if (!TryGetSelectedArea(out var from, out var to))
                return;

            boxMode = BoxMode.Selection;
            selectionFrom = from;
            selectionTo = to;
            SelectMode(modeResize);
        }

        private bool TryGetSelectedArea(out Vector3Int from, out Vector3Int to)
        {
            switch (boxMode)
            {
                case BoxMode.Selection:
                    from = DragMin;
                    to = DragMax;
                    return true;

                case BoxMode.Blueprint:
                    from = RepeatBlueprintMin;
                    to = RepeatBlueprintMax;
                    return true;
            }

            from = Vector3Int.zero;
            to = Vector3Int.zero;
            return false;
        }

        internal void LoadIconSprites()
        {
            iconBlack = new LazySprite("Assets/erkle64.Duplicationer/icons/black.png");
            iconEmpty = new LazySprite("Assets/erkle64.Duplicationer/icons/empty.png");
            iconCopy = new LazySprite("Assets/erkle64.Duplicationer/icons/copy.png");
            iconCopyInPlace = new LazySprite("Assets/erkle64.Duplicationer/icons/copy-in-place.png");
            iconMove = new LazySprite("Assets/erkle64.Duplicationer/icons/move.png");
            iconMoveSideways = new LazySprite("Assets/erkle64.Duplicationer/icons/move-sideways.png");
            iconMoveVertical = new LazySprite("Assets/erkle64.Duplicationer/icons/move-vertical.png");
            iconPanel = new LazySprite("Assets/erkle64.Duplicationer/icons/panel.png");
            iconPaste = new LazySprite("Assets/erkle64.Duplicationer/icons/paste.png");
            iconPlace = new LazySprite("Assets/erkle64.Duplicationer/icons/place.png");
            iconRepeat = new LazySprite("Assets/erkle64.Duplicationer/icons/repeat.png");
            iconResizeVertical = new LazySprite("Assets/erkle64.Duplicationer/icons/resize-vertical.png");
            iconResize = new LazySprite("Assets/erkle64.Duplicationer/icons/resize.png");
            iconSelectArea = new LazySprite("Assets/erkle64.Duplicationer/icons/select-area.png");
            iconMirror = new LazySprite("Assets/erkle64.Duplicationer/icons/mirror.png");
        }

        private void OnGameInitializationDone()
        {
            CurrentBlueprintStatusText = "";

            IsBlueprintActive = false;
            CurrentBlueprintAnchor = Vector3Int.zero;
            buildingPlaceholders.Clear();
            terrainPlaceholders.Clear();
            buildingPlaceholderUpdateIndex = 0;
            terrainPlaceholderUpdateIndex = 0;

            _activeConstructionTaskGroups.Clear();
        }

        public void SetPlaceholderOpacity(float alpha)
        {
            placeholderRenderGroup.SetAlpha(alpha);

            for (int i = 0; i < BlueprintPlaceholder.stateColours.Length; i++)
            {
                BlueprintPlaceholder.stateColours[i].a = alpha;
            }
        }

        internal void ClearBlueprint()
        {
            ClearBlueprintPlaceholders();
            CurrentBlueprint = null;
            IsBlueprintActive = false;
        }

        internal void LoadBlueprintFromFile(string path)
        {
            CurrentBlueprint = Blueprint.LoadFromFile(path);
        }

        internal void ClearBlueprintRecipes()
        {
            if (CurrentBlueprint == null) return;

            CurrentBlueprint.ClearRecipes();
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        internal void RemoveItemFromBlueprint(ItemTemplate template)
        {
            if (CurrentBlueprint == null) return;

            CurrentBlueprint.RemoveItem(template);
            ClearBlueprintPlaceholders();
            ShowBlueprint(CurrentBlueprintAnchor);
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
        }

        internal enum BoxMode
        {
            None,
            Selection,
            Blueprint
        }
    }
}
