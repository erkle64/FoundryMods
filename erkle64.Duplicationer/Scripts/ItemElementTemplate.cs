using System;
using System.Collections.Generic;
using UnityEngine;

namespace Duplicationer
{
    public readonly struct ItemElementTemplate : IEquatable<ItemElementTemplate>
    {
        private readonly ItemTemplate _itemTemplate;
        private readonly ElementTemplate _elementTemplate;
        private readonly DataSignalTemplate _dataSignalTemplate;

        public bool isItem => _itemTemplate != null;
        public bool isElement => _elementTemplate != null;
        public bool isDataSignal => _dataSignalTemplate != null;
        public bool isValid => isItem || isElement || isDataSignal;
        public string name => _itemTemplate?.name ?? _elementTemplate?.name ?? _dataSignalTemplate.name ?? string.Empty;
        public string identifier => _itemTemplate?.identifier ?? _elementTemplate?.identifier ?? _dataSignalTemplate?.identifier ?? string.Empty;
        public Sprite icon => _itemTemplate?.icon ?? _elementTemplate?.icon ?? _dataSignalTemplate?.icon ?? null;
        public ulong id => _itemTemplate?.id ?? _elementTemplate?.id ?? _dataSignalTemplate?.id ??  0UL;
        public ItemTemplate itemTemplate => _itemTemplate;
        public ElementTemplate elementTemplate => _elementTemplate;
        public DataSignalTemplate dataSignalTemplate => _dataSignalTemplate;
        public string fullIdentifier => _itemTemplate != null
            ? $"item:{_itemTemplate.identifier}"
            : (_elementTemplate != null
            ? $"element:{_elementTemplate.identifier}"
            : (_dataSignalTemplate != null
            ? $"signal:{_dataSignalTemplate.identifier}"
            : string.Empty));

        public static readonly ItemElementTemplate Empty = new ItemElementTemplate((ItemTemplate)null);

        private static List<ItemElementTemplate> _allItemElements = null;

        public bool isHiddenItem => _itemTemplate?.isHiddenItem ?? false;

        public static List<ItemElementTemplate> GatherAll()
        {
            if (_allItemElements == null)
            {
                _allItemElements = new List<ItemElementTemplate>();
                foreach (var itemTemplate in ItemTemplateManager.getAllItemTemplates())
                {
                    _allItemElements.Add(new ItemElementTemplate(itemTemplate.Value));
                }
                foreach (var elementTemplate in ItemTemplateManager.getAllElementTemplates())
                {
                    _allItemElements.Add(new ItemElementTemplate(elementTemplate.Value));
                }
                foreach (var dataSignalTemplate in ItemTemplateManager.getAllDataSignalTemplates())
                {
                    _allItemElements.Add(new ItemElementTemplate(dataSignalTemplate.Value));
                }
            }

            return _allItemElements;
        }

        public ItemElementTemplate(ItemTemplate itemTemplate)
        {
            _itemTemplate = itemTemplate;
            _elementTemplate = null;
            _dataSignalTemplate = null;
        }

        public ItemElementTemplate(ElementTemplate elementTemplate)
        {
            _itemTemplate = null;
            _elementTemplate = elementTemplate;
            _dataSignalTemplate = null;
        }

        public ItemElementTemplate(DataSignalTemplate dataSignalTemplate)
        {
            _itemTemplate = null;
            _elementTemplate = null;
            _dataSignalTemplate = dataSignalTemplate;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ItemElementTemplate other)) return false;
            if (isItem != other.isItem) return false;
            if (isElement != other.isElement) return false;
            if (isDataSignal != other.isDataSignal) return false;
            if (isItem && _itemTemplate.id != other._itemTemplate.id) return false;
            if (isElement && _elementTemplate.id != other._elementTemplate.id) return false;
            if (isDataSignal && _dataSignalTemplate.id != other._dataSignalTemplate.id) return false;
            return true;
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public bool Equals(ItemElementTemplate other)
        {
            if (isItem != other.isItem) return false;
            if (isElement != other.isElement) return false;
            if (isDataSignal != other.isDataSignal) return false;
            if (isItem && _itemTemplate.id != other._itemTemplate.id) return false;
            if (isElement && _elementTemplate.id != other._elementTemplate.id) return false;
            if (isDataSignal && _dataSignalTemplate.id != other._dataSignalTemplate.id) return false;
            return true;
        }

        public static ItemElementTemplate Get(string fullIdentifier)
        {
            if (fullIdentifier.StartsWith("item:"))
            {
                var hash = ItemTemplate.generateStringHash(fullIdentifier.Substring(5));
                var item = ItemTemplateManager.getItemTemplate(hash);
                if (item != null) return new ItemElementTemplate(item);
            }
            else if (fullIdentifier.StartsWith("element:"))
            {
                var hash = ElementTemplate.generateStringHash(fullIdentifier.Substring(8));
                var element = ItemTemplateManager.getElementTemplate(hash);
                if (element != null) return new ItemElementTemplate(element);
            }
            else if (fullIdentifier.StartsWith("signal:"))
            {
                var hash = DataSignalTemplate.generateStringHash(fullIdentifier.Substring(7));
                var signal = ItemTemplateManager.getDataSignalTemplate(hash);
                if (signal != null) return new ItemElementTemplate(signal);
            }

            return Empty;
        }

        public static bool operator==(ItemElementTemplate a, ItemElementTemplate b) => a.Equals(b);
        public static bool operator!=(ItemElementTemplate a, ItemElementTemplate b) => !a.Equals(b);
    }
}
