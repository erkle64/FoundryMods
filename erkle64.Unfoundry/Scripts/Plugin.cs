﻿using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using C3.ModKit;
using C3;

namespace Unfoundry
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class UnfoundryModAttribute : System.Attribute
    {
        public string modIdentifier;

        public UnfoundryModAttribute(string modIdentifier)
        {
            this.modIdentifier = modIdentifier;
        }
    }

    [ModSettingGroup, ModSettingIdentifier("Action Manager")]
    public static class UnfoundryConfig
    {
        [ModSettingIdentifier("Max Queued Events Per Frame")]
        public static ModSetting<int> maxQueuedEventsPerFrame = 40;
    }

    [NoStrip]
    public class Plugin : AssemblyProcessor
    {
        public const string
            MODNAME = "Unfoundry",
            AUTHOR = "erkle64",
            GUID = AUTHOR + "." + MODNAME,
            VERSION = "0.3.14";

        private static readonly List<KeyValuePair<string, System.Type>> _unfoundryPluginTypes = new();
        private static readonly List<KeyValuePair<string, UnfoundryPlugin>> _unfoundryPlugins = new();

        public override void ProcessAssembly(Assembly assembly, System.Type[] types)
        {
            foreach (System.Type type in assembly.GetTypes())
            {
                var attributes = (UnfoundryModAttribute[])type.GetCustomAttributes(typeof(UnfoundryModAttribute), true);
                if (typeof(UnfoundryPlugin).IsAssignableFrom(type) && attributes.Length > 0)
                {
                    _unfoundryPluginTypes.Add(new(attributes[0].modIdentifier, type));
                }
            }
        }

        public struct HandheldData
        {
            public int CurrentlySetMode { get; internal set; }

            public HandheldData(int currentlySetMode)
            {
                CurrentlySetMode = currentlySetMode;
            }
        }
        private static readonly Dictionary<ulong, HandheldData> handheldData = new Dictionary<ulong, HandheldData>();

        private static ulong lastSpawnedBuildableWrapperEntityId = 0;
        
        public static Vector3 SnappedToNearestAxis(Vector3 direction)
        {
            float num1 = Mathf.Abs(direction.x);
            float num2 = Mathf.Abs(direction.y);
            float num3 = Mathf.Abs(direction.z);
            if ((double)num1 > (double)num2 && (double)num1 > (double)num3)
                return new Vector3(Mathf.Sign(direction.x), 0.0f, 0.0f);
            return (double)num2 > (double)num1 && (double)num2 > (double)num3 ? new Vector3(0.0f, Mathf.Sign(direction.y), 0.0f) : new Vector3(0.0f, 0.0f, Mathf.Sign(direction.z));
        }

        public static T GetBundledAsset<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetManager.Database.LoadAssetAtPath<T>($"Assets/erkle64.Unfoundry/Bundled/{name}");
            if (asset == null)
            {
                Debug.Log($"Missing asset '{name}'");
                return null;
            }

            return asset;
        }

        [HarmonyPatch]
        public static class Patch
        {
            [HarmonyPatch(typeof(ObjectPoolManager), nameof(ObjectPoolManager.InitOnApplicationStart))]
            [HarmonyPrefix]
            private static void LoadPlugin()
            {
                foreach (var kvp in _unfoundryPluginTypes)
                {
                    var plugin = (UnfoundryPlugin)System.Activator.CreateInstance(kvp.Value);
                    if (plugin != null)
                    {
                        Debug.Log($"Unfoundry instantiating plugin for {kvp.Value.FullName}");
                        _unfoundryPlugins.Add(new(kvp.Key, plugin));
                    }
                }

                foreach (var kvp in _unfoundryPlugins)
                {
                    if (ModManager.tryGetMod(kvp.Key, out Mod mod))
                    {
                        Debug.Log($"Unfoundry loading mod '{kvp.Key}'");
                        kvp.Value.Load(mod);
                        UnfoundrySystem.RegisterPlugin(kvp.Value);
                    }
                    else
                    {
                        Debug.Log($"Unfoundry failed to find mod '{kvp.Key}'");
                    }
                }
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPrefix]
            private static void BuildEntityEvent_processEvent_prefix(BuildEntityEvent __instance)
            {
                if (__instance == null) return;
                if (!(GameRoot.getClientCharacter() is Character character)) return;
                if (__instance.characterHash != character.usernameHash) return;
                lastSpawnedBuildableWrapperEntityId = 0;
            }

            [HarmonyPatch(typeof(BuildEntityEvent), nameof(BuildEntityEvent.processEvent))]
            [HarmonyPostfix]
            private static void BuildEntityEvent_processEvent_postfix(BuildEntityEvent __instance)
            {
                if (__instance == null) return;
                if (!(GameRoot.getClientCharacter() is Character character)) return;
                if (__instance.characterHash != character.usernameHash) return;
                ActionManager.InvokeAndRemoveBuildEvent(__instance, lastSpawnedBuildableWrapperEntityId);
            }

            [HarmonyPatch(typeof(BuildingManager), nameof(BuildingManager.buildingManager_constructBuildableWrapper))]
            [HarmonyPostfix]
            private static void BuildingManager_buildingManager_constructBuildableWrapper(ulong __result)
            {
                lastSpawnedBuildableWrapperEntityId = __result;
            }

            [HarmonyPatch(typeof(Character.DemolishBuildingEvent), nameof(Character.DemolishBuildingEvent.processEvent))]
            [HarmonyPrefix]
            private static bool DemolishBuildingEvent_processEvent(Character.DemolishBuildingEvent __instance)
            {
                if (__instance.clientPlaceholderId == -2)
                {
                    __instance.clientPlaceholderId = 0;
                    BuildingManager.buildingManager_demolishBuildingEntityForDynamite(__instance.entityId);
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(Character.RemoveTerrainEvent), nameof(Character.RemoveTerrainEvent.processEvent))]
            [HarmonyPrefix]
            private static bool RemoveTerrainEvent_processEvent(Character.RemoveTerrainEvent __instance)
            {
                if (__instance.terrainRemovalPlaceholderId == ulong.MaxValue)
                {
                    __instance.terrainRemovalPlaceholderId = 0ul;

                    ChunkManager.getChunkIdxAndTerrainArrayIdxFromWorldCoords(__instance.worldPos.x, __instance.worldPos.y, __instance.worldPos.z, out ulong chunkIndex, out uint blockIndex);

                    byte terrainType = 0;
                    ChunkManager.chunks_removeTerrainBlock(chunkIndex, blockIndex, ref terrainType);
                    ChunkManager.flagChunkVisualsAsDirty(chunkIndex, true, true);
                    return false;
                }

                return true;
            }
        }
    }
}
