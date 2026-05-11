using static Duplicationer.Blueprint;
using System.Collections.Generic;
using System.Text;
using TinyJSON;
using System;
using System.Linq;
using MessagePack;
using UnityEngine;

namespace Duplicationer
{
    public class CDG_Battery : TypedCustomDataGatherer<BatteryGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new BatteryDataSystemControls();
            DSF_Battery.batteryEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_BurnerGenerator : TypedCustomDataGatherer<BurnerGeneratorGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new BurnerGeneratorDataSystemControls();
            DSF_BurnerGenerator.burnerGeneratorEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Conveyor : TypedCustomDataGatherer<ConveyorGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new ConveyorDataSystemControls();
            DSF_Conveyor.conveyorEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_DataCompare : TypedCustomDataGatherer<DataCompareGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new DataCompareEntityDataSystemControls();
            DSF_DataCompare.dataCompareEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_DataProcessor : TypedCustomDataGatherer<DataProcessorGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new DataProcessingEntityDataSystemControls();
            DSF_DataProcessor.dataProcessorEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            var dcsData = JSON.Dump(dsc, EncodeOptions.NoTypeHints);
            customData.Add("dcsData", dcsData);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_DataMemory : TypedCustomDataGatherer<DataMemoryGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new DataMemoryEntityDataSystemControls();
            DSF_DataMemory.dataMemoryEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_DataSource : TypedCustomDataGatherer<DataSourceGO>
    {
        public int[] dataSource_signalIdxArray = new int[DSF_DataSource.MAX_SLOTS];
        public int[] dataSource_signalValueArray = new int[DSF_DataSource.MAX_SLOTS];
        public bool dataSource_broadcastState = false;

        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            DSF_DataSource.dataSourceEntity_ioDSC(bogo.relatedEntityId, dataSource_signalIdxArray, dataSource_signalValueArray, Mathf.Min(dataSource_signalIdxArray.Length, dataSource_signalValueArray.Length), IOBool.iotrue);

            if (bogo.template.dataSourceEntity_mode == 1)
            {
                byte ioState = 0;
                DSF_DataSource.dataSourceEntity_getBroadcastState(bogo.relatedEntityId, ref ioState);
                dataSource_broadcastState = ioState == 1;
            }
            else
            {
                dataSource_broadcastState = false;
            }

            int[] dataConfigArrays = new int[DSF_DataSource.MAX_SLOTS * 2 + 1];
            for (int i = 0; i < DSF_DataSource.MAX_SLOTS; i++)
            {
                dataConfigArrays[i] = dataSource_signalIdxArray[i];
                dataConfigArrays[DSF_DataSource.MAX_SLOTS + i] = dataSource_signalValueArray[i];
            }
            dataConfigArrays[DSF_DataSource.MAX_SLOTS * 2] = dataSource_broadcastState ? 1 : 0;
            customData.Add("dcsDataStrArray", SerializeDCSDataArray(dataConfigArrays, DSF_DataSource.MAX_SLOTS));
        }
    }

    public class CDG_Door : TypedCustomDataGatherer<DoorGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new DoorDataSystemControls();
            DSF_Door.doorEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_DroneMiner : TypedCustomDataGatherer<DroneMinerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new DroneMinerDataSystemControls();
            DSF_DroneMiner.droneEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Light : TypedCustomDataGatherer<LightGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new LightDataSystemControls();
            DSF_Light.lightEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_LvgGenerator : TypedCustomDataGatherer<BiomassBurnerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new LvgGeneratorDataSystemControls();
            DSF_LvgGenerator.lvgGeneratorEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Pump : TypedCustomDataGatherer<PumpGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new PumpDataSystemControls();
            DSF_Pump.pumpEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Pumpjack : TypedCustomDataGatherer<PumpjackGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new PumpjackDataSystemControls();
            DSF_Pumpjack.pumpjackEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_SolarPanel : TypedCustomDataGatherer<SolarPanelGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new SolarPanelDataSystemControls();
            DSF_SolarPanel.solarPanelEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Transformer : TypedCustomDataGatherer<TransformerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var dsc = new TransformerDataSystemControls();
            DSF_Transformer.transformerEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Producer : TypedCustomDataGatherer<ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var assembler = (ProducerGO)bogo;
            customData.Add("craftingRecipeId", assembler.getLastPolledRecipeId());

            var dsc = new ProducerDataSystemControls();
            DSF_Producer.producerEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_Loader : TypedCustomDataGatherer<LoaderGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var loader = (LoaderGO)bogo;
            customData.Add("isInputLoader", loader.isInputLoader() ? "true" : "false");
            if (bogo.template.loader_isFilter)
            {
                customData.Add("loaderFilterTemplateId", loader.getLastSetFilterTemplate()?.id ?? ulong.MaxValue);
            }

            var dsc = new LoaderDataSystemControls();
            DSF_Loader.loaderEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_ConveyorBalancer : TypedCustomDataGatherer<ConveyorBalancerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var balancer = (ConveyorBalancerGO)bogo;
            var filterItem = balancer.getConfiguredFilterTemplate();
            customData.Add("balancerInputPriority", balancer.getInputPriority());
            customData.Add("balancerOutputPriority", balancer.getOutputPriority());
            if (filterItem != null)
                customData.Add("filterItemTemplateId", filterItem.id);
        }
    }

    public class CDG_Sign : TypedCustomDataGatherer<SignGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var signTextLength = SignGO.signEntity_getSignTextLength(bogo.relatedEntityId);
            var signText = new byte[signTextLength];
            byte useAutoTextSize = 0;
            float textMinSize = 0;
            float textMaxSize = 0;
            SignGO.signEntity_getSignText(bogo.relatedEntityId, signText, signTextLength, ref useAutoTextSize, ref textMinSize, ref textMaxSize);

            customData.Add("signText", Encoding.Default.GetString(signText));
            customData.Add("signUseAutoTextSize", useAutoTextSize);
            customData.Add("signTextMinSize", textMinSize);
            customData.Add("signTextMaxSize", textMaxSize);
        }
    }

    public class CDG_BlastFurnaceBase : TypedCustomDataGatherer<BlastFurnaceBaseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            BlastFurnacePollingUpdateData data = default;
            if (BlastFurnaceBaseGO.blastFurnaceEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iotrue)
            {
                customData.Add("blastFurnaceModeTemplateId", data.modeTemplateId);
            }
        }
    }

    public class CDG_DroneTransport : TypedCustomDataGatherer<DroneTransportGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            DroneTransportPollingUpdateData data = default;
            if (DroneTransportGO.droneTransportEntity_queryPollingData(bogo.relatedEntityId, ref data, null, 0U) == IOBool.iotrue)
            {
                customData.Add("loadConditionFlags", data.loadConditionFlags);
                customData.Add("loadCondition_comparisonType", data.loadCondition_comparisonType);
                customData.Add("loadCondition_fillRatePercentage", data.loadCondition_fillRatePercentage);
                customData.Add("loadCondition_seconds", data.loadCondition_seconds);
            }

            byte[] stationName = new byte[128];
            uint stationNameLength = 0;
            byte stationType = (byte)(bogo.template.droneTransport_isStartStation ? 1 : 0);
            DroneTransportGO.droneTransportEntity_getStationName(bogo.relatedEntityId, stationType, stationName, (uint)stationName.Length, ref stationNameLength);
            customData.Add("stationName", Encoding.UTF8.GetString(stationName, 0, (int)stationNameLength));
            customData.Add("stationType", stationType);
        }
    }

    public class CDG_Chest : TypedCustomDataGatherer<ChestGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            ulong inventoryId = 0UL;
            if (BuildingManager.buildingManager_getInventoryAccessors(bogo.relatedEntityId, 0U, ref inventoryId) == IOBool.iotrue)
            {
                uint slotCount = 0;
                uint categoryLock = 0;
                uint firstSoftLockedSlotIdx = 0;
                InventoryManager.inventoryManager_getAuxiliaryDataById(inventoryId, ref slotCount, ref categoryLock, ref firstSoftLockedSlotIdx, IOBool.iofalse);

                customData.Add("firstSoftLockedSlotIdx", firstSoftLockedSlotIdx);
            }
        }
    }

    public class CDG_ModularEntityBase : TypedCustomDataGatherer<IHasModularEntityBaseManager>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            ModularBuildingData rootNode = null;
            uint totalModuleCount = ModularBuildingManagerFrame.modularEntityBase_getTotalModuleCount(bogo.relatedEntityId, 0U);
            for (uint id = 1; id <= totalModuleCount; ++id)
            {
                ulong botId = 0;
                uint parentId = 0;
                uint parentAttachmentPointIdx = 0;
                ModularBuildingManagerFrame.modularEntityBase_getModuleDataForModuleId(bogo.relatedEntityId, id, ref botId, ref parentId, ref parentAttachmentPointIdx, 0U);
                if (id == 1U)
                {
                    rootNode = new ModularBuildingData(bogo.template, id);
                }
                else
                {
                    var nodeById = rootNode.FindModularBuildingNodeById(parentId);
                    if (nodeById == null)
                    {
                        DuplicationerSystem.log.LogError("parent node not found!");
                        break;
                    }
                    if (nodeById.attachments[(int)parentAttachmentPointIdx] != null)
                    {
                        DuplicationerSystem.log.LogError("parent node attachment point is occupied!");
                        break;
                    }
                    var node = new ModularBuildingData(ItemTemplateManager.getBuildableObjectTemplate(botId), id);
                    nodeById.attachments[(int)parentAttachmentPointIdx] = node;
                }
            }
            if (rootNode != null)
            {
                var rootNodeJSON = JSON.Dump(rootNode, EncodeOptions.NoTypeHints);
                customData.Add("modularBuildingData", rootNodeJSON);
            }
        }
    }

    public class CDG_Powerline : CustomDataGatherer
    {
        public override bool ShouldGather(BuildableObjectGO bogo, Type bogoType)
            => bogo.template.hasPoleGridConnection;

        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            if (!powerGridBuildings.Contains(bogo))
            {
                foreach (var powerGridBuilding in powerGridBuildings)
                {
                    if (PowerLineHH.buildingManager_powerlineHandheld_checkIfAlreadyConnected(powerGridBuilding.relatedEntityId, bogo.relatedEntityId) == IOBool.iotrue)
                    {
                        customData.Add("powerline", powerGridBuilding.relatedEntityId);
                    }
                }
                powerGridBuildings.Add(bogo);
            }
        }
    }

    public class CDG_SalesCurrencyWarehouse : TypedCustomDataGatherer<SalesCurrencyWarehouseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new SalesCurrencyWarehousePollingUpdateData();
            if (SalesCurrencyWarehouseGO.salesCurrencyWarehouseEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
            }
        }
    }

    public class CDG_SalesItemWarehouse : TypedCustomDataGatherer<SalesItemWarehouseGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new SalesItemWarehousePollingUpdateData();
            if (SalesItemWarehouseGO.salesItemWarehouseEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
            }
        }
    }

    public class CDG_AL_Start : TypedCustomDataGatherer<AL_StartGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_StartPollingUpdateData();
            if (AL_StartGO.alStartEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.alotId != 0)
            {
                customData.Add("alotId", data.alotId);
            }
        }
    }

    public class CDG_AL_Producer : TypedCustomDataGatherer<AL_ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            if (AL_ProducerGO.alProducerEntity_getActionTemplateId(bogo.relatedEntityId, out var actionTemplateId) == IOBool.iofalse)
                return;

            if (actionTemplateId != 0)
            {
                customData.Add("actionTemplateId", actionTemplateId);
            }
        }
    }

    public class CDG_AL_Splitter : TypedCustomDataGatherer<AL_SplitterGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_SplitterUpdateData();
            if (AL_SplitterGO.alSplitterEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            customData.Add("priorityIdx_output01", data.priorityIdx_output01);
            customData.Add("priorityIdx_output02", data.priorityIdx_output02);
            customData.Add("priorityIdx_output03", data.priorityIdx_output03);
        }
    }

    public class CDG_AL_Merger : TypedCustomDataGatherer<AL_MergerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_MergerUpdateData();
            if (AL_MergerGO.alMergerEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            customData.Add("priorityIdx_input01", data.priorityIdx_input01);
            customData.Add("priorityIdx_input02", data.priorityIdx_input02);
            customData.Add("priorityIdx_input03", data.priorityIdx_input03);
        }
    }

    public class CDG_AL_Painter : TypedCustomDataGatherer<AL_ProducerGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new AL_ProducerPollingUpdateData();
            if (AL_ProducerGO.alProducerEntity_queryPollingData(bogo.relatedEntityId, ref data, null, 0U, null, 0U) == IOBool.iofalse || bogo.template.al_producer_machineType != BuildableObjectTemplate.AL_ProducerMachineType.Painter)
                return;

            var painterAlotId = data.painterAlotId;

            if (painterAlotId != 0)
            {
                customData.Add("painterAlotId", painterAlotId);

                var alot = ItemTemplateManager.getAssemblyLineObjectTemplate(painterAlotId);
                if (alot != null)
                {
                    var colorVariants = new Dictionary<ulong, ulong>();

                    foreach (var objectPart in alot.objectParts)
                    {
                        ulong colorVariantId = 0;
                        if (AL_ProducerGO.alProducerEntity_queryColorVariantByObjectPartId(bogo.relatedEntityId, objectPart.id, ref colorVariantId) != IOBool.iofalse)
                        {
                            colorVariants[objectPart.id] = colorVariantId;
                        }
                    }

                    customData.Add("colorVariants", string.Join("|", colorVariants.Select(x => $"{x.Key}={x.Value}")));
                }
            }
        }
    }

    public class CDG_ShippingPad : TypedCustomDataGatherer<ShippingPadGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new ShippingPadPollingUpdateData();
            if (ShippingPadGO.shippingPad_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.configuredItemTemplateId != 0)
            {
                customData.Add("configuredItemTemplateId", data.configuredItemTemplateId);
                customData.Add("buildingState", data.buildingState);
                customData.Add("minAmountToMove", data.minAmountToMove);

                List<ulong> allowedShipTypes = new();
                foreach (var shipTemplate in ItemTemplateManager.getAllSpaceShipTemplates().Values)
                {
                    if (shipTemplate.spaceShipType != SpaceShipTemplate.SpaceShipType.TransportShip)
                        continue;

                    if (ShippingPadConfigFrame.shippingPad_checkIfShipTypeIsAllowed(bogo.relatedEntityId, shipTemplate.id) == IOBool.iotrue)
                        allowedShipTypes.Add(shipTemplate.id);
                }
                customData.Add("allowedShipTypes", string.Join("|", allowedShipTypes));
            }
        }
    }

    public class CDG_Workstation : TypedCustomDataGatherer<WorkstationGO>
    {
        private static ulong[] _cache_slotContentsArray = new ulong[WorkstationGO.MAX_ROBOT_SLOTS];
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var robotSlotCount = WorkstationGO.workstationEntity_querySlotContents(bogo.relatedEntityId, _cache_slotContentsArray, (uint)_cache_slotContentsArray.Length);
            customData.Add("robotSlotContents", string.Join("|", _cache_slotContentsArray.AsSpan().Slice(0, (int)robotSlotCount).ToArray()));

            var powerCoreSlotCount = WorkstationGO.workstationEntity_querySlotContents_powerCores(bogo.relatedEntityId, _cache_slotContentsArray, (uint)_cache_slotContentsArray.Length);
            customData.Add("powerCoreSlotContents", string.Join("|", _cache_slotContentsArray.AsSpan().Slice(0, (int)powerCoreSlotCount).ToArray()));
        }
    }

    public class CDG_TrainStation : TypedCustomDataGatherer<TrainStationGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new TrainStationPollingUpdateData();
            if (TrainStationGO.trainStationEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            if (data.isTrainLimitControlledByDataSystem == IOBool.iofalse)
                customData.Add("trainStation_trainLimit", data.trainLimit);

            byte color_r = 0;
            byte color_g = 0;
            byte color_b = 0;
            BuildableEntity.buildableEntity_getObjectColor(bogo.relatedEntityId, ref color_r, ref color_g, ref color_b);
            customData.Add("color_r", color_r);
            customData.Add("color_g", color_g);
            customData.Add("color_b", color_b);

            byte[] nameBuffer = new byte[256];
            uint nameLength = 0;
            string stationName = TrainStationGO.trainStationEntity_getName(bogo.relatedEntityId, 0, nameBuffer, (uint)nameBuffer.Length, ref nameLength) == IOBool.iotrue
                ? System.Text.Encoding.UTF8.GetString(nameBuffer, 0, (int)nameLength)
                : string.Empty;
            customData.Add("trainStation_name", stationName);

            TrainStationDataSystemControls dsc = default;
            DSF_TrainStation.trainStationEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }

    public class CDG_TrainLoadingStation : TypedCustomDataGatherer<TrainLoadingStationGO>
    {
        public override void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings)
        {
            var data = new TrainLoadingStationPollingUpdateData();
            if (TrainLoadingStationGO.trainLoadingStationEntity_queryPollingData(bogo.relatedEntityId, ref data) == IOBool.iofalse)
                return;

            var buildingMode = (byte)(TrainLoadingStationGO.isUnloading(data.buildingMode) ? 1 : 0);
            customData.Add("trainLoadingStation_buildingMode", buildingMode);

            byte color_r = 0;
            byte color_g = 0;
            byte color_b = 0;
            BuildableEntity.buildableEntity_getObjectColor(bogo.relatedEntityId, ref color_r, ref color_g, ref color_b);
            customData.Add("color_r", color_r);
            customData.Add("color_g", color_g);
            customData.Add("color_b", color_b);

            TrainLoadingStationDataSystemControls dsc = default;
            DSF_TrainLoadingStation.trainLoadingStationEntity_modifyDSC(bogo.relatedEntityId, IOBool.iotrue, ref dsc);
            customData.Add("dcsDataStr", SerializeDCSData(dsc));
        }
    }
}
