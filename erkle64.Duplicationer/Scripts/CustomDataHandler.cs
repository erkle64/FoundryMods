using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using static Duplicationer.Blueprint;

namespace Duplicationer
{
    public struct CustomDataWrapper
    {
        public readonly List<BlueprintData.BuildableObjectData.CustomData> customData;

        public CustomDataWrapper(List<BlueprintData.BuildableObjectData.CustomData> customData)
        {
            this.customData = customData;
        }

        public CustomDataWrapper(BlueprintData.BuildableObjectData.CustomData[] customData)
        {
            this.customData = new List<BlueprintData.BuildableObjectData.CustomData>(customData);
        }

        public void Add(in string identifier, in object value)
        {
            customData.Add(new BlueprintData.BuildableObjectData.CustomData(identifier, value));
        }

        public void Remove(in string identifier)
        {
            for (int i = customData.Count - 1; i >= 0; i--)
                if (customData[i].identifier == identifier) customData.RemoveAt(i);
        }

        public bool HasCustomData(in string identifier)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) return true;
            return false;
        }

        public T GetCustomData<T>(in string identifier)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) return (T)System.Convert.ChangeType(customDataEntry.value, typeof(T));
            return default;
        }

        public void GetCustomDataList<T>(in string identifier, List<T> list)
        {
            foreach (var customDataEntry in customData) if (customDataEntry.identifier == identifier) list.Add((T)System.Convert.ChangeType(customDataEntry.value, typeof(T)));
        }
    }

    public abstract class CustomDataGatherer
    {
        public abstract bool ShouldGather(BuildableObjectGO bogo, Type bogoType);
        public abstract void Gather(BuildableObjectGO bogo, CustomDataWrapper customData, HashSet<BuildableObjectGO> powerGridBuildings);

        private static CustomDataGatherer[] _gatherers = null;
        public static CustomDataGatherer[] All
        {
            get
            {
                if (_gatherers == null) {
                    _gatherers = System.Reflection.Assembly
                        .GetAssembly(typeof(CustomDataGatherer))
                        .GetTypes()
                        .Where(t => typeof(CustomDataGatherer).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => (CustomDataGatherer)Activator.CreateInstance(t))
                        .ToArray();
                }
                return _gatherers;
            }
        }

        protected static string SerializeDCSDataArray(int[] idxArray, int idSlots)
        {
            // returns format: "id1|id2|id3|..." where ids are from getDataSignalId
            string[] strings = new string[idxArray.Length];
            for (int i = 0; i < idSlots; i++)
            {
                int intValue = idxArray[i];
                if (intValue != int.MaxValue && intValue >= 0)
                {
                    IDataSignal dataSignal = GameRoot.RunningIdxTable_dataSignals_all.getDataByRunningIdx(intValue);
                    if (dataSignal == null)
                        strings[i] = $"0";
                    else
                        strings[i] = $"{dataSignal.getDataSignalId()}";
                }
                else
                {
                    strings[i] = $"0";
                }
            }
            // for the remaining slots, we just store the int value directly, as they are not used for running indices
            for (int i = idSlots; i < idxArray.Length; i++)
            {
                strings[i] = $"{idxArray[i]}";
            }

            return string.Join("|", strings);
        }

        protected static string SerializeDCSData<D>(D data)
        {
            // returns format: "Namespace.TypeName|field1name=field1value|field2name=field2value|..."
            // treats fields of type int specially, as they can contain a running index
            // for those fields we store it as "fieldName=#id", where the id is from getDataSignalId
            var fieldInfos = typeof(D).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldCount = fieldInfos.Length;
            string[] strings = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                var fieldInfo = fieldInfos[i];
                if (fieldInfo == null)
                {
                    UnityEngine.Debug.LogWarning($"No field info found for field index {i} of type {typeof(D).FullName}");
                    continue;
                }
                var value = fieldInfo.GetValue(data);
                if (fieldInfo.FieldType == typeof(int))
                {
                    int intValue = (int)value;
                    if (UnityEngine.Mathf.Abs(intValue) > DataSystemSlot.VALID_RANGE && intValue != int.MaxValue)
                    {
                        IDataSignal dataSignal = GameRoot.RunningIdxTable_dataSignals_all.getDataByRunningIdx(intValue - (DataSystemSlot.VALID_RANGE + 1));
                        if (dataSignal != null)
                            strings[i] = $"{fieldInfo.Name}=#{dataSignal.getDataSignalId()}";
                        else
                            strings[i] = $"{fieldInfo.Name}={intValue}";
                    }
                    else
                    {
                        strings[i] = $"{fieldInfo.Name}={intValue}";
                    }
                }
                else
                {
                    strings[i] = $"{fieldInfo.Name}={value}";
                }
            }
            return $"{typeof(D).AssemblyQualifiedName}|{string.Join("|", strings)}";
        }
    }

    public abstract class TypedCustomDataGatherer<T> : CustomDataGatherer
    {
        public override bool ShouldGather(BuildableObjectGO bogo, Type bogoType)
            => typeof(T).IsAssignableFrom(bogoType);
    }

    public abstract class CustomDataApplier
    {
        public delegate void AddToShoppingListDelegate(ulong itemId, int count);

        public abstract bool ShouldApply(BuildableObjectTemplate bot, CustomDataWrapper customData);
        public virtual void GetRequiredItemCountsById(BuildableObjectTemplate bot, CustomDataWrapper customData, AddToShoppingListDelegate addToShoppingList) { }
        public virtual void RemoveItems(BuildableObjectTemplate bot, ItemTemplate itemTemplate, ref CustomDataWrapper customData) { }
        public abstract void Apply(
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
            Dictionary<ulong, ulong> entityIdMap, UnityEngine.Vector3Int blueprintAnchorPosition);

        private static CustomDataApplier[] _appliers = null;
        public static CustomDataApplier[] All
        {
            get
            {
                if (_appliers == null) {
                    _appliers = System.Reflection.Assembly
                        .GetAssembly(typeof(CustomDataApplier))
                        .GetTypes()
                        .Where(t => typeof(CustomDataApplier).IsAssignableFrom(t) && !t.IsAbstract)
                        .Select(t => (CustomDataApplier)Activator.CreateInstance(t))
                        .ToArray();
                }
                return _appliers;
            }
        }

        public static byte[] TranslateDCSData(string dcsDataStr)
        {
            if (dcsDataStr == null)
            {
                UnityEngine.Debug.LogWarning("DCS data string is null");
                return null;
            }

            string[] parts = dcsDataStr.Split('|');
            if (parts.Length < 1)
            {
                UnityEngine.Debug.LogWarning($"Invalid DCS data string: {dcsDataStr}");
                return null;
            }

            // first part is the type name, we can use it to determine how to parse the rest of the string
            string typeName = parts[0];
            Type dataType = Type.GetType(typeName, false, false);
            if (dataType == null)
            {
                UnityEngine.Debug.LogWarning($"Invalid DCS data type: {typeName} in DCS data string: {dcsDataStr}");
                return null;
            }

            // create an instance of the data type, and set its fields according to the rest of the string
            object data = Activator.CreateInstance(dataType);
            if (data == null)
            {
                UnityEngine.Debug.LogWarning($"Failed to create instance of type: {typeName} for DCS data string: {dcsDataStr}");
                return null;
            }

            var fieldInfos = dataType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int partIndex = 1; partIndex < parts.Length; partIndex++)
            {
                string part = parts[partIndex];
                var keyValue = part.Split('=');
                if (keyValue.Length != 2)
                {
                    UnityEngine.Debug.LogWarning($"Invalid DCS data part: {part} in DCS data string: {dcsDataStr}");
                    continue;
                }

                // find field index by name, and set the value accordingly
                var fieldName = keyValue[0];
                int fieldIndex = Array.FindIndex(fieldInfos, f => f.Name == fieldName);
                if (fieldIndex < 0)
                    continue;

                string fieldValueStr = keyValue[1];
                var fieldType = fieldInfos[fieldIndex].FieldType;
                if (fieldType == typeof(int))
                {
                    int fieldValueInt = int.MaxValue;
                    if (fieldValueStr.StartsWith("#"))
                    {
                        if (!ulong.TryParse(fieldValueStr.Substring(1), out ulong dataSignalId))
                        {
                            UnityEngine.Debug.LogWarning($"Invalid data signal id: {fieldValueStr} in DCS data string: {dcsDataStr}");
                            continue;
                        }

                        var dst = ItemTemplateManager.getDataSignalTemplate(dataSignalId);
                        var et = ItemTemplateManager.getElementTemplate(dataSignalId);
                        var it = ItemTemplateManager.getItemTemplate(dataSignalId);
                        if (dst != null)
                        {
                            fieldValueInt = dst.getRunningIdx() + (DataSystemSlot.VALID_RANGE + 1);
                        }
                        else if (et != null)
                        {
                            fieldValueInt = et.getRunningIdx() + (DataSystemSlot.VALID_RANGE + 1);
                        }
                        else if (it != null)
                        {
                            fieldValueInt = it.getRunningIdx() + (DataSystemSlot.VALID_RANGE + 1);
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"No data signal, element template, or item template found for data signal id: {fieldValueStr} in DCS data string: {dcsDataStr}");
                            continue;
                        }
                    }
                    else
                    {
                        if (!int.TryParse(fieldValueStr, out fieldValueInt))
                        {
                            UnityEngine.Debug.LogWarning($"Invalid int value: {fieldValueStr} in DCS data string: {dcsDataStr}");
                            continue;
                        }
                    }

                    fieldInfos[fieldIndex].SetValue(data, fieldValueInt);
                }
                else if (fieldType == typeof(IOBool))
                {
                    var fieldValue = (fieldValueStr == "iotrue" || fieldValueStr == "IOBool.iotrue") ? IOBool.iotrue : IOBool.iofalse;
                    fieldInfos[fieldIndex].SetValue(data, fieldValue);
                }
                else
                {
                    try
                    {
                        object fieldValue = Convert.ChangeType(fieldValueStr, fieldType);
                        fieldInfos[fieldIndex].SetValue(data, fieldValue);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"Failed to convert value: {fieldValueStr} to type: {fieldType.FullName} for field: {fieldName} in DCS data string: {dcsDataStr}. Exception: {ex}");
                        continue;
                    }
                }
            }

            return MessagePackSerializer.Serialize(data, GlobalStateManager.msgp_options_fast);
        }
    }
}
