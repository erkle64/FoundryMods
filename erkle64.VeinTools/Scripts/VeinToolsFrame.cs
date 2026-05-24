using System;
using UnityEngine;

namespace VeinTools
{

    public class VeinToolsFrame : UIFrame
    {
        public bool IsOpen => gameObject.activeSelf;

        public TMPro.TextMeshProUGUI textVeinInfo;

        public TMPro.TMP_Dropdown dropdownBeltTier;
        public TMPro.TMP_InputField inputBeltCount;
        public TMPro.TextMeshProUGUI textBeltCountTip;
        public TMPro.TMP_InputField inputExtraCeilingSpace;
        public TMPro.TMP_InputField inputExtraBeltSpace;

        public TMPro.TMP_Dropdown dropdownCeilingType;
        public TMPro.TextMeshProUGUI textCeilingMode;
        public IconButton buttonCeilingBlock;
        public TMPro.TMP_Dropdown dropdownFloorType;
        public TMPro.TextMeshProUGUI textFloorMode;
        public IconButton buttonFloorBlock;

        public ItemSelectFrame itemSelectFramePrefab;

        private ItemSelectFrame _itemSelectFrame;

        private static readonly string[] _fillModeNames = new[] { "Add", "Replace" };

        public void Show()
        {
            gameObject.SetActive(true);
            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIOpen);
            GlobalStateManager.addCursorRequirement();

            var veinTools = VeinToolsSystem.Instance;
            veinTools.onSelectedVeinChanged += RefreshVeinInfo;
            RefreshVeinInfo();

            dropdownBeltTier.SetValueWithoutNotify(veinTools.BeltTier - 1);
            inputBeltCount.SetTextWithoutNotify(veinTools.BeltCount.ToString());
            inputExtraCeilingSpace.SetTextWithoutNotify(veinTools.ExtraCeilingSpace.ToString());
            inputExtraBeltSpace.SetTextWithoutNotify(veinTools.ExtraBeltSpace.ToString());

            dropdownCeilingType.SetValueWithoutNotify(veinTools.CeilingType);
            textCeilingMode.text = _fillModeNames[veinTools.CeilingMode];
            buttonCeilingBlock.Setup(veinTools.CeilingItemTemplate.icon, veinTools.CeilingItemTemplate.name);
            buttonCeilingBlock.onClick += OnClickCeilingBlock;
            dropdownFloorType.SetValueWithoutNotify(veinTools.FloorType);
            textFloorMode.text = _fillModeNames[veinTools.FloorMode];
            buttonFloorBlock.Setup(veinTools.FloorItemTemplate.icon, veinTools.FloorItemTemplate.name);
            buttonFloorBlock.onClick += OnClickFloorBlock;

            RefreshBeltCountTip();

            if (_itemSelectFrame == null)
            {
                _itemSelectFrame = Instantiate(itemSelectFramePrefab, transform.parent);
                _itemSelectFrame.BuildContent();
                _itemSelectFrame.gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            if (_itemSelectFrame != null && _itemSelectFrame.IsOpen)
                _itemSelectFrame.Hide(false);

            if (IsOpen)
            {
                gameObject.SetActive(false);
                AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);
                GlobalStateManager.removeCursorRequirement();
                VeinToolsSystem.Instance.onSelectedVeinChanged -= RefreshVeinInfo;
                buttonCeilingBlock.onClick -= OnClickCeilingBlock;
                buttonFloorBlock.onClick -= OnClickFloorBlock;
            }
        }

        void OnDestroy()
        {
            if (_itemSelectFrame != null)
            {
                Destroy(_itemSelectFrame.gameObject);
                _itemSelectFrame = null;
            }
        }

        public override void iec_triggerFrameClose()
        {
            Hide();
        }

        public override bool IsModal()
        {
            return false;
        }

        public void RefreshBeltCountTip()
        {
            var selectedVein = VeinToolsSystem.Instance.SelectedVein;
            if (selectedVein == null)
            {
                textBeltCountTip.text = "Recommended Belt Count: N/A";
                return;
            }

            var beltTier = VeinToolsSystem.Instance.BeltTier;
            var beltSpeed = 1 << (beltTier - 1);
            var drillPointCount = selectedVein.drillPoints.Length;
            float recommendedBeltCountF = drillPointCount / (float)beltSpeed;
            var recommendedBeltCount = Mathf.CeilToInt(recommendedBeltCountF);
            textBeltCountTip.text = $"Recommended Belt Count: {recommendedBeltCount} ({recommendedBeltCountF:0.##})";
        }

        public void OnBeltTierChanged(int selectedValue)
        {
            var beltTier = selectedValue + 1;
            var actualBeltTier = VeinToolsSystem.Instance.SetBeltTier(beltTier);
            if (actualBeltTier != beltTier)
                dropdownBeltTier.SetValueWithoutNotify(actualBeltTier - 1);
            RefreshBeltCountTip();
        }

        public void OnBeltCountChanged()
        {
            if (int.TryParse(inputBeltCount.text, out int beltCount))
            {
                var actualBeltCount = VeinToolsSystem.Instance.SetBeltCount(beltCount);
                if (actualBeltCount != beltCount)
                {
                    inputBeltCount.SetTextWithoutNotify(actualBeltCount.ToString());
                }
            }
        }

        public void OnBeltCountIncrement()
        {
            var beltCount = VeinToolsSystem.Instance.BeltCount + 1;
            var actualBeltCount = VeinToolsSystem.Instance.SetBeltCount(beltCount);
            inputBeltCount.SetTextWithoutNotify(actualBeltCount.ToString());
        }

        public void OnBeltCountDecrement()
        {
            var beltCount = VeinToolsSystem.Instance.BeltCount - 1;
            var actualBeltCount = VeinToolsSystem.Instance.SetBeltCount(beltCount);
            inputBeltCount.SetTextWithoutNotify(actualBeltCount.ToString());
        }

        public void OnExtraCeilingSpaceChanged()
        {
            if (int.TryParse(inputExtraCeilingSpace.text, out int extraCeilingSpace))
            {
                var actualExtraCeilingSpace = VeinToolsSystem.Instance.SetExtraCeilingSpace(extraCeilingSpace);
                if (actualExtraCeilingSpace != extraCeilingSpace)
                {
                    inputExtraCeilingSpace.SetTextWithoutNotify(actualExtraCeilingSpace.ToString());
                }
            }
        }

        public void OnExtraCeilingSpaceIncrement()
        {
            var extraCeilingSpace = VeinToolsSystem.Instance.ExtraCeilingSpace + 1;
            var actualExtraCeilingSpace = VeinToolsSystem.Instance.SetExtraCeilingSpace(extraCeilingSpace);
            inputExtraCeilingSpace.SetTextWithoutNotify(actualExtraCeilingSpace.ToString());
        }

        public void OnExtraCeilingSpaceDecrement()
        {
            var extraCeilingSpace = VeinToolsSystem.Instance.ExtraCeilingSpace - 1;
            var actualExtraCeilingSpace = VeinToolsSystem.Instance.SetExtraCeilingSpace(extraCeilingSpace);
            inputExtraCeilingSpace.SetTextWithoutNotify(actualExtraCeilingSpace.ToString());
        }

        public void OnExtraBeltSpaceChanged()
        {
            if (int.TryParse(inputExtraBeltSpace.text, out int extraBeltSpace))
            {
                var actualExtraBeltSpace = VeinToolsSystem.Instance.SetExtraBeltSpace(extraBeltSpace);
                if (actualExtraBeltSpace != extraBeltSpace)
                {
                    inputExtraBeltSpace.SetTextWithoutNotify(actualExtraBeltSpace.ToString());
                }
            }
        }

        public void OnExtraBeltSpaceIncrement()
        {
            var extraBeltSpace = VeinToolsSystem.Instance.ExtraBeltSpace + 1;
            var actualExtraBeltSpace = VeinToolsSystem.Instance.SetExtraBeltSpace(extraBeltSpace);
            inputExtraBeltSpace.SetTextWithoutNotify(actualExtraBeltSpace.ToString());
        }

        public void OnExtraBeltSpaceDecrement()
        {
            var extraBeltSpace = VeinToolsSystem.Instance.ExtraBeltSpace - 1;
            var actualExtraBeltSpace = VeinToolsSystem.Instance.SetExtraBeltSpace(extraBeltSpace);
            inputExtraBeltSpace.SetTextWithoutNotify(actualExtraBeltSpace.ToString());
        }

        public void OnCeilingTypeChanged(int selectedValue)
        {
            var ceilingType = selectedValue;
            var actualCeilingType = VeinToolsSystem.Instance.SetCeilingType(ceilingType);
            if (actualCeilingType != ceilingType)
                dropdownCeilingType.SetValueWithoutNotify(actualCeilingType);
        }

        public void OnClickCeilingMode()
        {
            var ceilingMode = VeinToolsSystem.Instance.CeilingMode == 0 ? 1 : 0;
            VeinToolsSystem.Instance.SetCeilingMode(ceilingMode);
            textCeilingMode.text = _fillModeNames[ceilingMode];
        }

        private void OnClickCeilingBlock()
        {
            _itemSelectFrame.Show(OnCeilingBlockSelected);
        }

        private void OnCeilingBlockSelected(ItemTemplate result)
        {
            result = VeinToolsSystem.Instance.SetCeilingItemTemplate(result);
            buttonCeilingBlock.Setup(result.icon, result.name);
        }

        public void OnFloorTypeChanged(int selectedValue)
        {
            var floorType = selectedValue;
            var actualFloorType = VeinToolsSystem.Instance.SetFloorType(floorType);
            if (actualFloorType != floorType)
                dropdownFloorType.SetValueWithoutNotify(actualFloorType);
        }

        public void OnClickFloorMode()
        {
            var floorMode = VeinToolsSystem.Instance.FloorMode == 0 ? 1 : 0;
            VeinToolsSystem.Instance.SetFloorMode(floorMode);
            textFloorMode.text = _fillModeNames[floorMode];
        }

        private void OnClickFloorBlock()
        {
            _itemSelectFrame.Show(OnFloorBlockSelected);
        }

        private void OnFloorBlockSelected(ItemTemplate result)
        {
            result = VeinToolsSystem.Instance.SetFloorItemTemplate(result);
            buttonFloorBlock.Setup(result.icon, result.name);
        }

        public void OnClickExcavate()
        {
            VeinToolsSystem.Instance.ExcavateSelectedVein();
        }

        public void OnClickSelectNearestVein()
        {
            VeinToolsSystem.Instance.SelectNearestVein();
        }

        public void OnClickPlaceBlueprint()
        {
            VeinToolsSystem.Instance.PlaceBlueprintForSelectedVein();
        }

        private void RefreshVeinInfo()
        {
            var vein = VeinToolsSystem.Instance.SelectedVein;
            if (vein != null)
            {
                var resourceTypeName = vein.template.mineableBlockType.name;
                var position = vein.worldCellPos;
                var drillCount = vein.drillPoints.Length;

                textVeinInfo.text = $@"<b>Vein Info
Resource Type: {resourceTypeName}
Position: {position.x}, {position.y}, {position.z}
Drill Count: {drillCount}";
            }
            else
            {
                textVeinInfo.text = "No vein selected";
            }

            RefreshBeltCountTip();
        }
    }

}
