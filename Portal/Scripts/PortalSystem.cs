using C3;
using MessagePack;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Portal
{
    [FoundryRPC]
    [AddSystemToGameSimulation]
    public class PortalSystem : SystemManager.System, IHasSystemSaveData<PortalSystem.SaveData>
    {
        private const int SAVEDATA_VERSION = 1;

        static PortalSystem singleton;

        public override void OnAddedToWorld()
        {
            singleton = this;
        }

        public override void OnRemovedFromWorld()
        {
            singleton = null;
        }

        [EventHandler(filterOnType = typeof(PortalGO))]
        public void EntityRemoved(OnEntitiesDestroyed evt)
        {
            foreach (var entityId in evt.entities)
            {
                RemovePortal(entityId);
                portalDestinationsByEntityId.Remove(entityId);
            }
        }

        [EventHandler(filterOnType = typeof(PortalGO))]
        public void EntityStreamedIn(OnEntityStreamedIn evt)
        {
            if (!portalEffectTimesByEntityId.TryGetValue(evt.entityId, out float time))
                return;

            portalEffectTimesByEntityId.Remove(evt.entityId);

            if (Time.time > time + 3f)
                return;

            (evt.bgo as PortalGO).PlayTeleportEffects(time);
        }

        public static string GetPortalName(ulong entityId)
        {
            return singleton.portalNamesByEntityId.TryGetValue(entityId, out string name) ? name : null;
        }

        public static IEnumerable<string> GetAllPortalNames()
        {
            return singleton.portalEntityIdsByName.Keys;
        }

        public static void SetPortalName(ulong entityId, string name)
        {
            Rpc.Lockstep.Run(SetPortalNameRPC, entityId, name);
        }

        public static string GetPortalDestination(ulong entityId)
        {
            return singleton.portalDestinationsByEntityId.TryGetValue(entityId, out string name) ? name : null;
        }

        public static void SetPortalDestination(ulong entityId, string name)
        {
            Rpc.Lockstep.Run(SetPortalDestinationRPC, entityId, name);
        }

        public static HashSet<ulong> GetPortalEntityIdsByName(string destinationName)
        {
            return singleton.portalEntityIdsByName.TryGetValue(destinationName, out HashSet<ulong> entityIds) ? entityIds : null;
        }

        public static bool HasValidDestination(string destinationName, ulong sourceEntityId)
        {
            var entityIds = GetPortalEntityIdsByName(destinationName);
            if (entityIds == null || entityIds.Count == 0)
                return false;

            if (entityIds.Count > 1)
                return true;

            if (entityIds.First() != sourceEntityId)
                return true;

            return false;
        }

        public static void TeleportPlayer(ulong portalEntityId, ulong playerId)
        {
            singleton.DoTeleportPlayer(portalEntityId, playerId);
        }

        [FoundryRPC]
        public static void SetPortalNameRPC(ulong entityId, string name)
        {
            if (singleton == null)
                return;

            singleton.RemovePortal(entityId);

            if (!string.IsNullOrEmpty(name))
                singleton.AddPortal(entityId, name);
        }

        [FoundryRPC]
        public static void SetPortalDestinationRPC(ulong entityId, string name)
        {
            if (singleton == null)
                return;

            singleton.portalDestinationsByEntityId[entityId] = name;
        }

        [FoundryRPC]
        public static void PlayTeleportEffectsRPC(ulong entityId)
        {
            if (singleton == null)
                return;

            var bogo = StreamingSystem.getBuildableObjectGOByEntityId(entityId);
            if (bogo == null)
            {
                singleton.portalEffectTimesByEntityId[entityId] = Time.time;
                return;
            }

            var portalGO = bogo as PortalGO;

            if (portalGO != null)
                portalGO.PlayTeleportEffects(Time.time);
        }

        [MessagePackObject(true)]
        [SystemSaveData(SAVEDATA_VERSION)]
        public class SaveData
        {
            [Save]
            public Dictionary<ulong, string> portalNames = new();

            [Save]
            public Dictionary<ulong, string> portalDestinations = new();
        }

        public SaveData save()
        {
            var saveData = new SaveData
            {
                portalNames = portalNamesByEntityId,
                portalDestinations = portalDestinationsByEntityId
            };
            return saveData;
        }

        public void load(SaveData saveData, int version)
        {
            portalEntityIdsByName.Clear();
            portalDestinationsByEntityId.Clear();

            if (saveData == null)
                return;

            if (saveData.portalNames != null)
            {
                foreach (var kvp in saveData.portalNames)
                {
                    AddPortal(kvp.Key, kvp.Value);
                }
            }

            if (saveData.portalDestinations != null)
            {
                foreach (var kvp in saveData.portalDestinations)
                {
                    portalDestinationsByEntityId[kvp.Key] = kvp.Value;
                }
            }
        }

        public void finalizeAfterLoad(SaveData saveData, int version)
        {
        }

        private void AddPortal(ulong entityId, string name)
        {
            portalNamesByEntityId[entityId] = name;

            if (!portalEntityIdsByName.TryGetValue(name, out HashSet<ulong> entityIds))
            {
                entityIds = new();
                portalEntityIdsByName[name] = entityIds;
            }
            entityIds.Add(entityId);
        }

        private void RemovePortal(ulong entityId)
        {
            if (!portalNamesByEntityId.TryGetValue(entityId, out string name))
                return;

            portalNamesByEntityId.Remove(entityId);

            if (!portalEntityIdsByName.TryGetValue(name, out HashSet<ulong> entityIds))
                return;

            entityIds.Remove(entityId);
            if (entityIds.Count == 0)
                portalEntityIdsByName.Remove(name);
        }

        private bool DoTeleportPlayer(ulong portalEntityId, ulong playerId)
        {
            var destinationName = GetPortalDestination(portalEntityId);
            if (string.IsNullOrEmpty(destinationName))
                return false;

            var destinationEntityIds = GetPortalEntityIdsByName(destinationName);

            if (destinationEntityIds == null)
                return false;

            var destinationCount = destinationEntityIds.Count;
            if (destinationCount == 0)
                return false;

            var destinationEntityId = GetRandomDestination(destinationEntityIds);
            if (destinationEntityId == 0ul)
                return false;

            if (destinationEntityId == portalEntityId)
            {
                if (destinationCount == 1)
                    return false;

                var limit = 1000;
                while (destinationEntityId == portalEntityId && limit-- > 0)
                {
                    destinationEntityId = GetRandomDestination(destinationEntityIds);
                    if (destinationEntityId == 0ul)
                        return false;
                }

                if (limit == 0)
                {
                    Debug.LogWarning("Portal could not find a different destination.");
                    return false;
                }
            }

            BuildableEntity.BuildableEntityGeneralData generalData = default;
            if (BuildingManager.buildingManager_getBuildableEntityGeneralData(destinationEntityId, ref generalData) == IOBool.iofalse)
                return false;

            var bot = ItemTemplateManager.getBuildableObjectTemplate(generalData.buildableObjectTemplateId);
            if (bot == null)
                return false;

            var portalGO = bot.prefabOnDisk.GetComponent<PortalGO>();
            if (portalGO == null)
                return false;

            BuildingManager.getWidthFromOrientation(bot, (BuildingManager.BuildOrientation)generalData.orientationY, out var wx, out var wy, out var wz);

            var localOffset = portalGO.teleportOffset;
            var target = (Vector3)generalData.pos;
            target.y += localOffset.y;
            switch (generalData.orientationY)
            {
                case 0: // 0
                    target.x += localOffset.x;
                    target.z += localOffset.z;
                    break;
                case 1: // 90
                    target.x += localOffset.z;
                    target.z += wz - localOffset.x;
                    break;
                case 2: // 180
                    target.x += wx - localOffset.x;
                    target.z += wz - localOffset.z;
                    break;
                case 3: // 270
                    target.x += wx - localOffset.z;
                    target.z += localOffset.x;
                    break;
            }

            Rpc.Lockstep.Run(PlayTeleportEffectsRPC, portalEntityId);
            Rpc.Lockstep.Run(PlayTeleportEffectsRPC, destinationEntityId);
            GameRoot.addLockstepEvent(new Character.CharacterRelocateEvent(playerId, target.x, target.y, target.z, (long)(target.x * FixedPointMath.FPM_BASE), (long)(target.y * FixedPointMath.FPM_BASE), (long)(target.z * FixedPointMath.FPM_BASE)));

            return true;
        }

        private ulong GetRandomDestination(HashSet<ulong> destinationEntityIds)
        {
            var randomIndex = UnityEngine.Random.Range(0, destinationEntityIds.Count);
            foreach (var entityId in destinationEntityIds)
            {
                if (randomIndex-- <= 0)
                    return entityId;
            }

            return 0ul;
        }

        Dictionary<ulong, string> portalNamesByEntityId = new();
        Dictionary<string, HashSet<ulong>> portalEntityIdsByName = new();

        Dictionary<ulong, string> portalDestinationsByEntityId = new();

        Dictionary<ulong, float> portalEffectTimesByEntityId = new();
    }

    /*[HarmonyPatch]
    public static class Patch
    {
        private const int SAVEDATA_VERSION = 1;
        private static readonly System.Type systemType = typeof(PortalSystem);
        private static readonly System.Type saveDataType = typeof(PortalSystem.SaveData);

        static MethodBase TargetMethod()
        {
            return typeof(SystemSaveLoadManager).Assembly.GetType("SystemSerializer`1", true).MakeGenericType(saveDataType).GetConstructor(new System.Type[] { typeof(SystemManager.System) });
        }

        public static bool Prefix(object __instance, SystemManager.System system)
        {
            if (system.GetType() != systemType)
                return true;

            var serializerType = typeof(SystemSaveLoadManager).Assembly.GetType("SystemSerializer`1", true).MakeGenericType(saveDataType);

            serializerType.GetField("system", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, system);
            serializerType.GetField("hasSaveData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, system);
            serializerType.GetProperty("systemTypeName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, system.GetType().Name);
            serializerType.GetProperty("saveDataVersion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, SAVEDATA_VERSION);

            return false;
        }
    }*/

}
