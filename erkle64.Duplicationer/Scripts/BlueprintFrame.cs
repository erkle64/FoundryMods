using System.Collections.Generic;
using TMPro;
using Unfoundry;
using UnityEngine;
using UnityEngine.UI;

namespace Duplicationer
{
    internal class BlueprintFrame : DuplicationerFrame
    {
        [Header("Blueprint Frame")]
        [SerializeField] private TextMeshProUGUI _textPositionX = null;
        [SerializeField] private TextMeshProUGUI _textPositionY = null;
        [SerializeField] private TextMeshProUGUI _textPositionZ = null;
        [SerializeField] private TextMeshProUGUI _textDemolishMode = null;
        [SerializeField] private TextMeshProUGUI _textQueuePause = null;
        [SerializeField] private TextMeshProUGUI _textCheatMode = null;
        [SerializeField] private GameObject _rowCheats;
        [SerializeField] private Button _buttonCheatMode;
        [SerializeField] private Button _buttonSave;
        [SerializeField] private Button _buttonConfirmCopy;
        [SerializeField] private Button _buttonConfirmPaste;
        [SerializeField] private GameObject _containerClearRecipes;
        [SerializeField] private GameObject _containerQueueControls;
        [SerializeField] private GameObject _containerDemolish;
        [SerializeField] private GameObject _containerDroneDemolish;
        [SerializeField] private GameObject _containerDestroy;
        [SerializeField] private GameObject _containerPosition;
        [SerializeField] private GameObject _containerMaterialReport;
        [SerializeField] private GameObject _containerMaterialReportEntries;
        [SerializeField] private GameObject _containerSelectBounds;
        [SerializeField] private MaterialReportEntry _materialReportEntryPrefab;

        private float _nextUpdateTimeCountTexts = 0.0f;

        private string QueuePauseButtonText => ActionManager.IsQueuePaused ? "Resume Queue" : "Pause Queue";
        private string CheatModeButtonText => DuplicationerSystem.IsCheatModeEnabled ? "Disable Cheat Mode" : "Enable Cheat Mode";

        public void Show()
        {
            if (IsOpen) return;

            _tool.HideSaveFrame(true);
            _tool.HideLibraryFrame(true);
            _tool.HideFolderFrame(true);

            if (!Config.Cheats.cheatModeAllowed.value)
            {
                _rowCheats.SetActive(false);
            }
            else
            {
                if (_textCheatMode != null) _textCheatMode.text = CheatModeButtonText;
            }

            Shown();

            UpdateBlueprintPositionText();
            ForceUpdateMaterialReport();
            UpdateDemolishMode();
        }

        void Update()
        {
            if (!IsOpen) return;

            var dronesAvailable = ResearchSystem.isConstructionDroneUnlocked();
            var destroyAvailable = DuplicationerSystem.isBulkDemolishRunning;

            _containerClearRecipes.gameObject.SetActive(_tool.CurrentBlueprint != null && _tool.CurrentBlueprint.HasRecipes);
            _containerQueueControls.gameObject.SetActive(!GameRoot.IsMultiplayerEnabled && ActionManager.HasQueuedEvents);
            _containerDemolish.gameObject.SetActive(_tool.boxMode != BlueprintToolCHM.BoxMode.None && _tool.CurrentMode != _tool.modeSelectArea);
            _containerDroneDemolish.gameObject.SetActive(_tool.boxMode != BlueprintToolCHM.BoxMode.None && _tool.CurrentMode != _tool.modeSelectArea && dronesAvailable);
            _containerDestroy.gameObject.SetActive(_tool.boxMode != BlueprintToolCHM.BoxMode.None && _tool.CurrentMode != _tool.modeSelectArea && destroyAvailable);
            _containerPosition.gameObject.SetActive(_tool.boxMode == BlueprintToolCHM.BoxMode.Blueprint);
            _containerSelectBounds.gameObject.SetActive(_tool.IsBlueprintLoaded && _tool.IsBlueprintActive);
            _buttonSave.interactable = _tool.IsBlueprintLoaded;
            _buttonConfirmPaste.interactable = _tool.CurrentMode != null && _tool.CurrentMode.AllowPaste(_tool);
            _buttonConfirmCopy.interactable = _tool.CurrentMode != null && _tool.CurrentMode.AllowCopy(_tool);
        }

        public void OnClick_DemolishMode()
        {
            Config.Hidden.demolishBounds.value = !Config.Hidden.demolishBounds.value;
            UpdateDemolishMode();
        }

        public void OnClick_Save()
        {
            _tool.BeginSaveBlueprint();
        }

        public void OnClick_Load()
        {
            _tool.BeginLoadBlueprint();
        }

        public void OnClick_Copy()
        {
            _tool.CopySelection();
        }

        public void OnClick_Paste()
        {
            _tool.PlaceBlueprintMultiple(_tool.CurrentBlueprintAnchor, _tool.repeatFrom, _tool.repeatTo);
        }

        public void OnClick_ClearRecipes()
        {
            _tool.ClearBlueprintRecipes();
        }

        public void OnClick_QueuePause()
        {
            ActionManager.IsQueuePaused = !ActionManager.IsQueuePaused;
            if (_textQueuePause != null)
                _textQueuePause.text = QueuePauseButtonText;
        }

        public void OnClick_QueueClear()
        {
            ActionManager.ClearQueuedEvents();
            _tool.ClearConstructionTaskGroups();
            ActionManager.IsQueuePaused = false;
        }

        public void OnClick_MoveX(bool negative)
        {
            _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(negative ? -1 : 1, 0, 0) * _tool.NudgeX);
        }

        public void OnClick_MoveY(bool negative)
        {
            _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, negative ? -1 : 1, 0) * _tool.NudgeY);
        }

        public void OnClick_MoveZ(bool negative)
        {
            _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, 0, negative ? -1 : 1) * _tool.NudgeZ);
        }

        public void OnClick_CheatMode()
        {
            if (_buttonCheatMode == null) return;
            if (!Config.Cheats.cheatModeAllowed.value) return;
            Config.Cheats.cheatModeEnabled.value = !Config.Cheats.cheatModeEnabled.value;
            if (_textCheatMode == null) return;
            _textCheatMode.text = CheatModeButtonText;
        }

        public void OnClick_SelectBounds()
        {
            _tool.SelectBounds();
        }

        public void OnClick_DemolishDestroy(DemolishDestroyButtonMode mode)
        {
            if (mode.isDestroy)
            {
                if (isShowingConfirmation)
                    return;

                isShowingConfirmation = true;
                ConfirmationFrame.Show($"Permanently destroy {mode.label} in selection?",
                    () =>
                    {
                        isShowingConfirmation = false;
                        ApplyDemolishDestroy();
                    },
                    () => isShowingConfirmation = false);
            }
            else
            {
                ApplyDemolishDestroy();
            }

            void ApplyDemolishDestroy()
            {
                if (Config.Hidden.demolishBounds.value)
                {
                    if (mode.useDrones)
                    {
                        _tool.DroneDemolishArea();
                    }
                    else if (mode.isDestroy)
                    {
                        _tool.DestroyArea(mode.includeTerrain, mode.includeDecor);
                    }
                    else
                    {
                        _tool.DemolishArea(mode.includeBuildings, mode.includeBlocks, mode.includeTerrain, mode.includeDecor, mode.includeTracks);
                    }
                }
                else
                {
                    var blueprint = _tool.CurrentBlueprint;
                    if (blueprint == null)
                        return;

                    var ignoreSet = new HashSet<ulong>();
                    var repeatFrom = _tool.repeatFrom;
                    var repeatTo = _tool.repeatTo;
                    List<BlueprintToolCHM.BulkDemolishTerrainDestroyEntry> bulkDemolishEntries = new();
                    for (int y = repeatFrom.y; y <= repeatTo.y; ++y)
                    {
                        for (int z = repeatFrom.z; z <= repeatTo.z; ++z)
                        {
                            for (int x = repeatFrom.x; x <= repeatTo.x; ++x)
                            {
                                var position = _tool.CurrentBlueprintAnchor + new Vector3Int(x * _tool.CurrentBlueprintSize.x, y * _tool.CurrentBlueprintSize.y, z * _tool.CurrentBlueprintSize.z);
                                foreach (var aabb in blueprint.EachAABB(position))
                                {
                                    var from = new Vector3Int(aabb.x0, aabb.y0, aabb.z0);
                                    var to = new Vector3Int(aabb.x1 - 1, aabb.y1 - 1, aabb.z1 - 1);
                                    if (mode.useDrones)
                                    {
                                        _tool.DroneDemolishArea(from, to);
                                    }
                                    else if (mode.isDestroy)
                                    {
                                        bulkDemolishEntries.Add(new BlueprintToolCHM.BulkDemolishTerrainDestroyEntry()
                                        {
                                            worldPosX = from.x,
                                            worldPosY = from.y,
                                            worldPosZ = from.z,
                                            sizeX = to.x - from.x + 1,
                                            sizeY = to.y - from.y + 1,
                                            sizeZ = to.z - from.z + 1,
                                        });
                                    }
                                    else
                                    {
                                        _tool.DemolishArea(mode.includeBuildings, mode.includeBlocks, mode.includeTerrain, mode.includeDecor, mode.includeTracks, from, to, ignoreSet);
                                    }
                                }
                            }
                        }
                    }
                    if (mode.isDestroy && bulkDemolishEntries.Count > 0)
                    {
                        _tool.DestroyArea(mode.includeTerrain, mode.includeDecor, bulkDemolishEntries);
                    }
                }
            }
        }

        public void OnChange_PreviewOpacity(float value)
        {
            Config.Preview.previewAlpha.value = value;
            _tool.SetPlaceholderOpacity(value);
        }

        private static bool isShowingConfirmation = false;
        public void OnEntryClicked_MaterialReport(ItemTemplate itemTemplate)
        {
            if (isShowingConfirmation)
                return;

            isShowingConfirmation = true;
            ConfirmationFrame.Show($"Remove all '{itemTemplate.name}'?", "Remove", () => {
                _tool.RemoveItemFromBlueprint(itemTemplate);
                isShowingConfirmation = false;
            }, () => {
                isShowingConfirmation = false;
            });

            ForceUpdateMaterialReport();
        }

        internal void UpdateBlueprintPositionText()
        {
            if (!IsOpen) return;

            if (_textPositionX != null) _textPositionX.text = string.Format("Position X: {0}", _tool.CurrentBlueprintAnchor.x);
            if (_textPositionY != null) _textPositionY.text = string.Format("Position Y: {0}", _tool.CurrentBlueprintAnchor.y);
            if (_textPositionZ != null) _textPositionZ.text = string.Format("Position Z: {0}", _tool.CurrentBlueprintAnchor.z);
        }

        internal void UpdateMaterialReport()
        {
            if (!IsOpen || Time.time < _nextUpdateTimeCountTexts) return;

            ForceUpdateMaterialReport();
        }

        private List<MaterialReportEntry> materialReportEntries = new();
        internal void ForceUpdateMaterialReport()
        {
            _nextUpdateTimeCountTexts = Time.time + 0.5f;

            if (_tool.CurrentBlueprint == null)
            {
                _containerMaterialReport.SetActive(false);
                return;
            }

            int repeatCount = _tool.RepeatCount.x * _tool.RepeatCount.y * _tool.RepeatCount.z;
            ulong inventoryId = GameRoot.getClientCharacter().inventoryId;
            ulong inventoryPtr = inventoryId != 0 ? InventoryManager.inventoryManager_getInventoryPtr(inventoryId) : 0;

            int entryIndex = 0;
            MaterialReportEntry AppendEntry(string label, int inventory, int done, int total, MaterialReportEntry.MaterialReportEntryClicked onClicked = null)
            {
                MaterialReportEntry materialReportEntry = null;
                if (entryIndex >= materialReportEntries.Count)
                {
                    materialReportEntry = Instantiate(_materialReportEntryPrefab, _containerMaterialReportEntries.transform);
                    materialReportEntries.Add(materialReportEntry);
                }
                else
                {
                    materialReportEntry = materialReportEntries[entryIndex];
                }

                materialReportEntry.Setup(label, inventory, done, total, onClicked);
                materialReportEntry.gameObject.SetActive(true);
                entryIndex++;
                return materialReportEntry;
            }

            int totalItemCount = 0;
            int totalDoneCount = 0;
            foreach (var kv in _tool.CurrentBlueprint.ShoppingList)
            {
                var itemCount = kv.Value.count * repeatCount;
                if (itemCount > 0)
                {
                    var name = kv.Value.name;
                    var templateId = kv.Value.itemTemplateId;
                    if (templateId != 0)
                    {
                        totalItemCount += itemCount;

                        var itemTemplate = ItemTemplateManager.getItemTemplate(templateId);

                        var doneCount = BlueprintPlaceholder.GetStateCount(templateId, BlueprintPlaceholder.State.Done);
                        totalDoneCount += doneCount;

                        if (inventoryPtr != 0)
                        {
                            var inventoryCount = (int)InventoryManager.inventoryManager_countByItemTemplateByPtr(inventoryPtr, templateId, IOBool.iotrue);

                            AppendEntry(name, inventoryCount, doneCount, itemCount, (entry) => OnEntryClicked_MaterialReport(itemTemplate));
                        }
                        else
                        {
                            AppendEntry(name, 0, doneCount, itemCount, (entry) => OnEntryClicked_MaterialReport(itemTemplate));
                        }
                    }
                    else
                    {
                        AppendEntry(name, 0, 0, itemCount);
                    }
                }
            }

            if (totalItemCount > 0)
            {
                AppendEntry("Total:", 0, totalDoneCount, totalItemCount);
            }

            for (int i = entryIndex; i < materialReportEntries.Count; i++)
            {
                materialReportEntries[i].gameObject.SetActive(false);
            }

            _containerMaterialReport.SetActive(totalItemCount > 0);
        }

        private void UpdateDemolishMode()
        {
            _textDemolishMode.text = Config.Hidden.demolishBounds.value ? "Full Bounds" : "Minimal";
        }
    }
}
