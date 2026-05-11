using static Duplicationer.Blueprint;
using System.Collections.Generic;
using TinyJSON;
using Unfoundry;
using System.Linq;
using System;
using MessagePack;

namespace Duplicationer
{
    public class CDA_CraftingRecipe : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("craftingRecipeId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var craftingRecipeId = customData.GetCustomData<ulong>("craftingRecipeId");
            if (craftingRecipeId != 0)
            {
                var recipe = ItemTemplateManager.getCraftingRecipeById(craftingRecipeId);
                if (recipe != null && (Config.Cheats.allowUnresearchedRecipes.value || recipe.isResearched()))
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_01 = craftingRecipeId;
                }
            }
        }
    }

    public class CDA_Loader : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("isInputLoader");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            usePasteConfigSettings = true;
            bool isInputLoader = customData.GetCustomData<bool>("isInputLoader");
            pasteConfigSettings_01 = isInputLoader ? 1u : 0u;

            if (bot.loader_isFilter && customData.HasCustomData("loaderFilterTemplateId"))
            {
                var loaderFilterTemplateId = customData.GetCustomData<ulong>("loaderFilterTemplateId");
                if (loaderFilterTemplateId > 0)
                {
                    usePasteConfigSettings = true;
                    pasteConfigSettings_02 = loaderFilterTemplateId;
                }
            }
        }
    }

    public class CDA_DCSData : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("dcsData") || customData.HasCustomData("dcsDataStr") || customData.HasCustomData("dcsDataStrArray");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            usePasteConfigSettings = true;

            if (customData.HasCustomData("dcsDataStrArray"))
            {
                var dcsDataStr = customData.GetCustomData<string>("dcsDataStrArray");
                var parts = dcsDataStr.Split("|");
                int[] dcsDataValues = new int[parts.Length];
                var idCount = parts.Length / 2;
                bool parsingFailed = false;
                for (int i = 0; i < idCount; i++)
                {
                    if (parts[i] == "0")
                    {
                        dcsDataValues[i] = int.MaxValue;
                        continue;
                    }

                    if (ulong.TryParse(parts[i], out var id) == false)
                    {
                        UnityEngine.Debug.LogWarning("Failed to parse dcsDataStrArray, falling back to dcsData");
                        parsingFailed = true;
                        break;
                    }

                    var dst = ItemTemplateManager.getDataSignalTemplate(id);
                    var et = ItemTemplateManager.getElementTemplate(id);
                    var it = ItemTemplateManager.getItemTemplate(id);
                    if (dst != null)
                    {
                        dcsDataValues[i] = dst.getRunningIdx();
                    }
                    else if (et != null)
                    {
                        dcsDataValues[i] = et.getRunningIdx();
                    }
                    else if (it != null)
                    {
                        dcsDataValues[i] = it.getRunningIdx();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("Failed to find template for id in dcsDataStrArray, falling back to dcsData");
                        parsingFailed = true;
                        break;
                    }
                }
                for (int i = idCount; i < parts.Length; i++)
                {
                    if (int.TryParse(parts[i], out var value) == false)
                    {
                        UnityEngine.Debug.LogWarning("Failed to parse dcsDataStrArray, falling back to dcsData");
                        parsingFailed = true;
                        break;
                    }
                    dcsDataValues[i] = value;
                }
                if (!parsingFailed)
                {
                    dcsData = MessagePackSerializer.Serialize(dcsDataValues, GlobalStateManager.msgp_options_fast);
                    return;
                }
            }

            if (customData.HasCustomData("dcsDataStr"))
            {
                dcsData = TranslateDCSData(customData.GetCustomData<string>("dcsDataStr"));
                if (dcsData != null)
                    return;

                UnityEngine.Debug.LogWarning("Failed to translate dcsDataStr, falling back to dcsData");
            }

            var loader_dsc = customData.GetCustomData<string>("dcsData");
            if (loader_dsc != null)
            {
                dcsData = Convert.FromBase64String(loader_dsc);
            }
        }
    }

    public class CDA_ObjectColor : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("objectColor");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var color = customData.GetCustomData<int>("objectColor");
            var r = (byte)(color & 0xff);
            var g = (byte)((color >> 8) & 0xff);
            var b = (byte)((color >> 16) & 0xff);

            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new ColorizeObjectEvent(usernameHash, task.entityId, r, g, b, false, false));
                }
            });
        }
    }

    public class CDA_ConveyorBalancer : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.ConveyorBalancer;

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var balancerInputPriority = customData.GetCustomData<int>("balancerInputPriority");
            var balancerOutputPriority = customData.GetCustomData<int>("balancerOutputPriority");
            var filterItemTemplateId = customData.HasCustomData("filterItemTemplateId") ? customData.GetCustomData<ulong>("filterItemTemplateId") : 0UL;
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new SetConveyorBalancerConfig(usernameHash, task.entityId, balancerInputPriority, balancerOutputPriority, filterItemTemplateId));
                }
            });
        }
    }

    public class CDA_Sign : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.Sign;

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var signText = customData.GetCustomData<string>("signText");
            var signUseAutoTextSize = customData.GetCustomData<byte>("signUseAutoTextSize");
            var signTextMinSize = customData.GetCustomData<float>("signTextMinSize");
            var signTextMaxSize = customData.GetCustomData<float>("signTextMaxSize");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new SignSetTextEvent(usernameHash, task.entityId, signText, signUseAutoTextSize != 0, signTextMaxSize, signTextMinSize));
                }
            });
        }
    }

    public class CDA_BlastFurnaceMode : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("blastFurnaceModeTemplateId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var modeTemplateId = customData.GetCustomData<ulong>("blastFurnaceModeTemplateId");
            if (modeTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new BlastFurnaceSetModeEvent(usernameHash, task.entityId, modeTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_StorageLockedSlots : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.Storage && customData.HasCustomData("firstSoftLockedSlotIdx");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var firstSoftLockedSlotIdx = customData.GetCustomData<uint>("firstSoftLockedSlotIdx");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    ulong inventoryId = 0UL;
                    if (BuildingManager.buildingManager_getInventoryAccessors(task.entityId, 0U, ref inventoryId) == IOBool.iotrue)
                    {
                        GameRoot.addLockstepEvent(new SetSoftLockForInventory(usernameHash, inventoryId, firstSoftLockedSlotIdx));
                    }
                    else
                    {
                        DuplicationerSystem.log.LogWarning("Failed to get inventory accessor for storage");
                    }
                }
                else
                {
                    DuplicationerSystem.log.LogWarning("Failed to get entity id for storage");
                }
            });
        }
    }

    public class CDA_DroneTransportConditions : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.DroneTransport && customData.HasCustomData("loadConditionFlags");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var loadConditionFlags = customData.GetCustomData<byte>("loadConditionFlags");
            var loadCondition_comparisonType = customData.GetCustomData<byte>("loadCondition_comparisonType");
            var loadCondition_fillRatePercentage = customData.GetCustomData<byte>("loadCondition_fillRatePercentage");
            var loadCondition_seconds = customData.GetCustomData<uint>("loadCondition_seconds");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new DroneTransportLoadConditionEvent(usernameHash, task.entityId, loadConditionFlags, loadCondition_fillRatePercentage, loadCondition_seconds, loadCondition_comparisonType));
                }
                else
                {
                    DuplicationerSystem.log.LogWarning("Failed to get entity id for drone transport");
                }
            });
        }
    }

    public class CDA_DroneTransportName : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => bot.type == BuildableObjectTemplate.BuildableObjectType.DroneTransport && customData.HasCustomData("stationName");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var stationName = customData.GetCustomData<string>("stationName");
            var stationType = customData.GetCustomData<byte>("stationType");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new DroneTransportSetNameEvent(usernameHash, stationName, task.entityId, stationType));
                }
                else
                {
                    DuplicationerSystem.log.LogWarning("Failed to get entity id for drone transport");
                }
            });
        }
    }

    public class CDA_ModularBuildingData : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("modularBuildingData");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var modularBuildingDataJSON = customData.GetCustomData<string>("modularBuildingData");
            var modularBuildingData = JSON.Load(modularBuildingDataJSON).Make<ModularBuildingData>();
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    byte out_mbState = 0;
                    byte out_isEnabled = 0;
                    byte out_canBeEnabled = 0;
                    byte out_constructionIsDismantle = 0;
                    uint out_assignedConstructionDronePorts = 0;
                    ModularBuildingManagerFrame.modularEntityBase_getGenericData(task.entityId, ref out_mbState, ref out_isEnabled, ref out_canBeEnabled, ref out_constructionIsDismantle, ref out_assignedConstructionDronePorts);
                    if (out_mbState == (byte)ModularBuildingManagerFrame.MBState.ConstructionSiteInactive)
                    {
                        var moduleCount = ModularBuildingManagerFrame.modularEntityBase_getTotalModuleCount(task.entityId, 0);
                        if (moduleCount <= 1)
                        {
                            var mbmfData = modularBuildingData.BuildMBMFData();
                            GameRoot.addLockstepEvent(new SetModularEntityConstructionStateDataEvent(usernameHash, 0U, task.entityId, mbmfData));
                        }
                    }
                }
            });
        }
    }

    public class CDA_PowerLines : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => true;

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var powerlineEntityIds = new List<ulong>();
            customData.GetCustomDataList("powerline", powerlineEntityIds);
            foreach (var powerlineEntityId in powerlineEntityIds)
            {
                var powerlineIndex = blueprintData.FindEntityIndex(powerlineEntityId);
                if (powerlineIndex >= 0)
                {
                    var toBuildableObjectData = blueprintData.buildableObjects[powerlineIndex];
                    postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                    {
                        if (entityIdMap.TryGetValue(toBuildableObjectData.originalEntityId, out var toEntityId))
                        {
                            if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(task.entityId, toEntityId) == IOBool.iofalse)
                            {
                                GameRoot.addLockstepEvent(new PoleConnectionEvent(usernameHash, PowerlineItemTemplate.id, task.entityId, toEntityId, 0));
                            }
                        }
                    });
                }
            }
        }
    }

    public class CDA_ConfiguredItemTemplateId : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("configuredItemTemplateId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var itemTemplateId = customData.GetCustomData<ulong>("configuredItemTemplateId");
            if (itemTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new SalesWarehouseConfigEvent(task.entityId, usernameHash, itemTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Start : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("alotId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var alotId = customData.GetCustomData<ulong>("alotId");
            if (alotId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new AL_StartSetAlotEvent(usernameHash, task.entityId, alotId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Producer : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("actionTemplateId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var actionTemplateId = customData.GetCustomData<ulong>("actionTemplateId");
            if (actionTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        GameRoot.addLockstepEvent(new AL_ProducerSetActionEvent(usernameHash, task.entityId, actionTemplateId));
                    }
                });
            }
        }
    }

    public class CDA_AL_Painter : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("painterAlotId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var painterAlotId = customData.GetCustomData<ulong>("painterAlotId");
            if (painterAlotId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        var evt = new AL_ProducerSetPaintingActionEvent(usernameHash, task.entityId, painterAlotId);
                        if (customData.HasCustomData("colorVariants"))
                        {
                            var colorVariants = customData.GetCustomData<string>("colorVariants").Split("|")
                                .Select(x => x.Split("=").Select(y => Convert.ToUInt64(y)).ToArray());
                            foreach (var colorVariant in colorVariants)
                            {
                                var objectPartId = colorVariant[0];
                                var colorVariantId = colorVariant[1];
                                evt.addColorVariant(objectPartId, colorVariantId);
                            }
                        }
                        GameRoot.addLockstepEvent(evt);
                    }
                });
            }
        }
    }

    public class CDA_AL_Splitter : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("priorityIdx_output01")
                && customData.HasCustomData("priorityIdx_output02")
                && customData.HasCustomData("priorityIdx_output03");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var priorityIdx_output01 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output01"));
            var priorityIdx_output02 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output02"));
            var priorityIdx_output03 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_output03"));
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    for (int i = 0; i < priorityIdx_output01; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 0));
                    }
                    for (int i = 0; i < priorityIdx_output02; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 1));
                    }
                    for (int i = 0; i < priorityIdx_output03; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_SplitterTogglePriorityEvent(usernameHash, task.entityId, 2));
                    }
                }
            });
        }

        private int GetToggleCount(byte priorityIdx) => (priorityIdx + 2) % 3;
    }

    public class CDA_AL_Merger : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("priorityIdx_input01")
                && customData.HasCustomData("priorityIdx_input02")
                && customData.HasCustomData("priorityIdx_input03");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var priorityIdx_input01 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input01"));
            var priorityIdx_input02 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input02"));
            var priorityIdx_input03 = GetToggleCount(customData.GetCustomData<byte>("priorityIdx_input03"));
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    for (int i = 0; i < priorityIdx_input01; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 0));
                    }
                    for (int i = 0; i < priorityIdx_input02; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 1));
                    }
                    for (int i = 0; i < priorityIdx_input03; i++)
                    {
                        GameRoot.addLockstepEvent(new AL_MergerTogglePriorityEvent(usernameHash, task.entityId, 2));
                    }
                }
            });
        }

        private int GetToggleCount(byte priorityIdx) => (priorityIdx + 2) % 3;
    }

    public class CDA_ShippingPad : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("configuredItemTemplateId");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var configuredItemTemplateId = customData.GetCustomData<ulong>("configuredItemTemplateId");
            if (configuredItemTemplateId > 0)
            {
                postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
                {
                    if (task.entityId > 0)
                    {
                        var direction = customData.GetCustomData<byte>("buildingState");
                        var minAmountToMove = customData.GetCustomData<uint>("minAmountToMove");
                        var allowedShipTypesString = customData.GetCustomData<string>("allowedShipTypes");

                        var allowedShipTypes = new List<ulong>();
                        foreach (var entry in allowedShipTypesString.Split("|", StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (ulong.TryParse(entry, out var shipTypeId))
                            {
                                allowedShipTypes.Add(shipTypeId);
                            }
                        }

                        GameRoot.addLockstepEvent(new ShippingPadResetEvent(usernameHash, task.entityId));
                        GameRoot.addLockstepEvent(new ShippingPadConfigureEvent(usernameHash, task.entityId, direction, configuredItemTemplateId, (int)minAmountToMove, allowedShipTypes));
                    }
                });
            }
        }
    }

    public class CDA_Workstation : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("robotSlotContents");

        public override void GetRequiredItemCountsById(BuildableObjectTemplate bot, CustomDataWrapper customData, AddToShoppingListDelegate addToShoppingList)
        {
            var robotSlotContentsString = customData.GetCustomData<string>("robotSlotContents");
            var robotSlotContentsParts = robotSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < robotSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
            {
                if (ulong.TryParse(robotSlotContentsParts[i], out var slotContentId) && slotContentId > 0)
                {
                    addToShoppingList(slotContentId, 1);
                }
            }
        }

        public override void RemoveItems(BuildableObjectTemplate bot, ItemTemplate itemTemplate, ref CustomDataWrapper customData)
        {
            if (!customData.HasCustomData("robotSlotContents"))
                return;

            var robotSlotContentsString = customData.GetCustomData<string>("robotSlotContents");
            var robotSlotContentsParts = robotSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            var newRobotSlotContentsParts = new List<string>();
            for (int i = 0; i < robotSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
            {
                if (ulong.TryParse(robotSlotContentsParts[i], out var slotContentId) && slotContentId != itemTemplate.id)
                {
                    newRobotSlotContentsParts.Add(robotSlotContentsParts[i]);
                }
            }
            var newRobotSlotContentsString = string.Join("|", newRobotSlotContentsParts);
            customData.Remove("robotSlotContents");
            customData.Add("robotSlotContents", newRobotSlotContentsString);
        }

        private static CubeInterOp.ItemBufferPollingUpdateData[] _cache_itemBufferPollingData = new CubeInterOp.ItemBufferPollingUpdateData[64];

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var robotSlotContentsString = customData.GetCustomData<string>("robotSlotContents");
            var robotSlotContentsParts = robotSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            var robotSlotContents = new ulong[WorkstationGO.MAX_ROBOT_SLOTS];

            for (int i = 0; i < robotSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
                if (ulong.TryParse(robotSlotContentsParts[i], out var slotContentId))
                    robotSlotContents[i] = slotContentId;

            var clientCharacter = GameRoot.getClientCharacter();
            var inventorySlotCount = InventoryManager.inventoryManager_getInventorySlotCountByPtr(clientCharacter.inventoryPtr);

            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    InventoryManager.buildableEntity_populateItemBufferData(task.entityId, _cache_itemBufferPollingData, (uint)_cache_itemBufferPollingData.Length);

                    for (uint slotIdx = 0; slotIdx < robotSlotContents.Length; slotIdx++)
                    {
                        var robotItemTemplateId = robotSlotContents[slotIdx];
                        var robotItemTemplate = ItemTemplateManager.getItemTemplate(robotItemTemplateId);
                        if (robotItemTemplate == null)
                            continue;

                        // check if client character has the robot item
                        if (InventoryManager.inventoryManager_hasItem(clientCharacter.inventoryId, robotItemTemplateId, 1U, IOBool.iotrue) == IOBool.iofalse)
                            continue;

                        // search inventory for robot item
                        uint foundSlotIdx = uint.MaxValue;
                        for (uint invSlotIdx = 0; invSlotIdx < inventorySlotCount; invSlotIdx++)
                        {
                            ushort itemTemplateRunningIdx = 0;
                            uint itemCount = 0;
                            ushort lockedTemplateRunningIdx = 0;
                            IOBool isLocked = IOBool.iofalse;
                            InventoryManager.inventoryManager_getSingleSlotDataByPtr(clientCharacter.inventoryPtr, invSlotIdx, ref itemTemplateRunningIdx, ref itemCount, ref lockedTemplateRunningIdx, ref isLocked, IOBool.iotrue);
                            if (itemTemplateRunningIdx == ushort.MaxValue)
                                continue;

                            if (itemTemplateRunningIdx == robotItemTemplate._runningTypeIdx_all && itemCount > 0)
                            {
                                foundSlotIdx = invSlotIdx;
                                break;
                            }
                        }
                        if (foundSlotIdx == uint.MaxValue)
                            continue;

                        // transfer robot item to workstation item buffer
                        GameRoot.addLockstepEvent(new ItemMoveItemBufferEvent(
                            clientCharacter,
                            clientCharacter.inventoryId,
                            foundSlotIdx,
                            task.entityId,
                            slotIdx,
                            0U, // INV->BUFFER
                            1U));
                    }
                }
            });
        }
    }

    public class CDA_Workstation_PowerCores : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("powerCoreSlotContents");

        public override void GetRequiredItemCountsById(BuildableObjectTemplate bot, CustomDataWrapper customData, AddToShoppingListDelegate addToShoppingList)
        {
            var powerCoreSlotContentsString = customData.GetCustomData<string>("powerCoreSlotContents");
            var powerCoreSlotContentsParts = powerCoreSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < powerCoreSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
            {
                if (ulong.TryParse(powerCoreSlotContentsParts[i], out var slotContentId) && slotContentId > 0)
                {
                    addToShoppingList(slotContentId, 1);
                }
            }
        }

        public override void RemoveItems(BuildableObjectTemplate bot, ItemTemplate itemTemplate, ref CustomDataWrapper customData)
        {
            if (!customData.HasCustomData("powerCoreSlotContents"))
                return;

            var powerCoreSlotContentsString = customData.GetCustomData<string>("powerCoreSlotContents");
            var powerCoreSlotContentsParts = powerCoreSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            var newPowerCoreSlotContentsParts = new List<string>();
            for (int i = 0; i < powerCoreSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
            {
                if (ulong.TryParse(powerCoreSlotContentsParts[i], out var slotContentId) && slotContentId != itemTemplate.id)
                {
                    newPowerCoreSlotContentsParts.Add(powerCoreSlotContentsParts[i]);
                }
            }
            var newPowerCoreSlotContentsString = string.Join("|", newPowerCoreSlotContentsParts);
            customData.Remove("powerCoreSlotContents");
            customData.Add("powerCoreSlotContents", newPowerCoreSlotContentsString);
        }

        private static CubeInterOp.ItemBufferPollingUpdateData[] _cache_itemBufferPollingData = new CubeInterOp.ItemBufferPollingUpdateData[64];

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var powerCoreSlotContentsString = customData.GetCustomData<string>("powerCoreSlotContents");
            var powerCoreSlotContentsParts = powerCoreSlotContentsString.Split("|", StringSplitOptions.RemoveEmptyEntries);
            var powerCoreSlotContents = new ulong[WorkstationGO.MAX_ROBOT_SLOTS];

            for (int i = 0; i < powerCoreSlotContentsParts.Length && i < WorkstationGO.MAX_ROBOT_SLOTS; i++)
                if (ulong.TryParse(powerCoreSlotContentsParts[i], out var slotContentId))
                    powerCoreSlotContents[i] = slotContentId;

            var clientCharacter = GameRoot.getClientCharacter();
            var inventorySlotCount = InventoryManager.inventoryManager_getInventorySlotCountByPtr(clientCharacter.inventoryPtr);

            var robotSlotCount = bot.workstation_robotSlotCount;

            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    InventoryManager.buildableEntity_populateItemBufferData(task.entityId, _cache_itemBufferPollingData, (uint)_cache_itemBufferPollingData.Length);

                    for (uint slotIdx = 0; slotIdx < powerCoreSlotContents.Length; slotIdx++)
                    {
                        var powerCoreItemTemplateId = powerCoreSlotContents[slotIdx];
                        var powerCoreItemTemplate = ItemTemplateManager.getItemTemplate(powerCoreItemTemplateId);
                        if (powerCoreItemTemplate == null)
                            continue;

                        // check if client character has the power core item
                        if (InventoryManager.inventoryManager_hasItem(clientCharacter.inventoryId, powerCoreItemTemplateId, 1U, IOBool.iotrue) == IOBool.iofalse)
                            continue;

                        // search inventory for robot item
                        uint foundSlotIdx = uint.MaxValue;
                        for (uint invSlotIdx = 0; invSlotIdx < inventorySlotCount; invSlotIdx++)
                        {
                            ushort itemTemplateRunningIdx = 0;
                            uint itemCount = 0;
                            ushort lockedTemplateRunningIdx = 0;
                            IOBool isLocked = IOBool.iofalse;
                            InventoryManager.inventoryManager_getSingleSlotDataByPtr(clientCharacter.inventoryPtr, invSlotIdx, ref itemTemplateRunningIdx, ref itemCount, ref lockedTemplateRunningIdx, ref isLocked, IOBool.iotrue);
                            if (itemTemplateRunningIdx == ushort.MaxValue)
                                continue;

                            if (itemTemplateRunningIdx == powerCoreItemTemplate._runningTypeIdx_all && itemCount > 0)
                            {
                                foundSlotIdx = invSlotIdx;
                                break;
                            }
                        }
                        if (foundSlotIdx == uint.MaxValue)
                            continue;

                        // transfer power core item to workstation item buffer
                        GameRoot.addLockstepEvent(new ItemMoveItemBufferEvent(
                            clientCharacter,
                            clientCharacter.inventoryId,
                            foundSlotIdx,
                            task.entityId,
                            (uint)(slotIdx + robotSlotCount),
                            0U, // INV->BUFFER
                            1U));
                    }
                }
            });
        }
    }

    public class CDA_TrainStation : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("trainStation_name");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var trainStationName = customData.GetCustomData<string>("trainStation_name");
            var trainStationHasLimit = customData.HasCustomData("trainStation_trainLimit");
            var trainStationTrainLimit = trainStationHasLimit ? customData.GetCustomData<int>("trainStation_trainLimit") : 0;
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new TrainStationSetNameEvent(usernameHash, trainStationName, task.entityId));
                    if (trainStationHasLimit)
                        GameRoot.addLockstepEvent(new TrainSystem.TrainStationSetTrainLimitEvent(usernameHash, task.entityId, trainStationTrainLimit));
                }
            });
        }
    }

    public class CDA_TrainLoadingStation : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("trainLoadingStation_buildingMode");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var buildingMode = customData.GetCustomData<byte>("trainLoadingStation_buildingMode");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new TrainLoadingStationGO.SetModeEvent(usernameHash, task.entityId, buildingMode != 0));
                }
            });
        }
    }

    public class CDA_Color : CustomDataApplier
    {
        public override bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData)
            => customData.HasCustomData("color_r")
                && customData.HasCustomData("color_g")
                && customData.HasCustomData("color_b");

        public override void Apply(
            BuildableObjectTemplate bot,
            CustomDataWrapper customData,
            List<PostBuildAction> postBuildActions,
            ulong usernameHash,
            ref bool usePasteConfigSettings,
            ref ulong pasteConfigSettings_01,
            ref ulong pasteConfigSettings_02,
            ref ulong additionalData_ulong_01,
            ref ulong additionalData_ulong_02,
            ref byte[] dcsData,
            ref BlueprintData blueprintData,
            Dictionary<ulong, ulong> entityIdMap)
        {
            var colorR = customData.GetCustomData<byte>("color_r");
            var colorG = customData.GetCustomData<byte>("color_g");
            var colorB = customData.GetCustomData<byte>("color_b");
            postBuildActions.Add((ConstructionTaskGroup taskGroup, ConstructionTaskGroup.ConstructionTask task) =>
            {
                if (task.entityId > 0)
                {
                    GameRoot.addLockstepEvent(new ColorizeObjectEvent(usernameHash, task.entityId, colorR, colorG, colorB, false, false));
                }
            });
        }
    }
}
