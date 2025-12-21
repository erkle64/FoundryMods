using C3.ModKit;
using HarmonyLib;
using Unfoundry;
using UnityEngine;
using UnityEngine.UI;

namespace Embiggenator
{

    [ModSettingGroup, ModSettingIdentifier("Inventory")]
    [ModSettingServerSync]
    [ModSettingOrder(100)]
    public static class Config
    {
        [ModSettingTitle("Enable")]
        public static ModSetting<bool> enabled = false;

        [ModSettingTitle("Slot Count")]
        [ModSettingDescription("Total number of inventory slots players should have.", "Maximum size: 200")]
        public static ModSetting<int> slotCount = 70;

        [ModSettingTitle("Max Vertical Size")]
        [ModSettingDescription("Maximum number rows to display before adding a scrollbar.")]
        public static ModSetting<int> maxVerticalSize = 8;

        [ModSettingTitle("Scroll Speed")]
        [ModSettingDescription("Speed to scroll the inventory window when using the mouse wheel.")]
        public static ModSetting<int> scrollSpeed = 40;

        [ModSettingTitle("Disable Inventory Research")]
        [ModSettingDescription("Prevents research from increasing inventory size.")]
        public static ModSetting<bool> disableInventoryResearch = true;
    }

    [HarmonyPatch]
    public class Patch
    {
        public static LogSource log = new LogSource("Embiggenator");

        private static float _characterFrameOriginalWidth = 0f;
        private static readonly System.Collections.Generic.Dictionary<RectTransform, float> _siblingsX = new System.Collections.Generic.Dictionary<RectTransform, float>();

        [HarmonyPatch(typeof(CharacterManager), nameof(CharacterManager.joinWorld))]
        [HarmonyPostfix]
        public static void joinWorld(Character character, uint clientId)
        {
            if (!Config.enabled.value) return;

            var inventorySlots = Mathf.Clamp(Config.slotCount.value, 10, 200);
            if (inventorySlots > 0)
            {
                var currentInventorySlots = InventoryManager.inventoryManager_getInventorySlotCountByPtr(character.inventoryPtr);
                var additionalInventorySlots = inventorySlots - currentInventorySlots;
                if (additionalInventorySlots > 0)
                {
                    log.Log($"Embiggenating inventory by {additionalInventorySlots} slots for {character.username}");
                    InventoryManager.inventoryManager_enlargeInventory(character.inventoryId, (uint)additionalInventorySlots);
                }
                else
                {
                    log.Log($"Skipping {character.username}, they already have {currentInventorySlots} slots");
                }
            }
        }

        [HarmonyPatch(typeof(InventorySubFrame), nameof(InventorySubFrame.Init))]
        [HarmonyPostfix]
        public static void InventorySubFrame_Init(InventorySubFrame __instance)
        {
            if (!Config.enabled.value) return;

            if (__instance.transform.Find("ScrollBox")) return;

            log.Log($"Creating scrollbox for character inventory frame.");

            var rectTransform = __instance.transform.parent as RectTransform;
            var originalWidth = rectTransform.sizeDelta.x;

            var containerTransform = rectTransform.parent;
            _siblingsX.Clear();
            // iterate through sibling RectTransforms and store their x anchoredPositions
            foreach (RectTransform sibling in containerTransform)
            {
                if (sibling != rectTransform)
                {
                    _siblingsX[sibling] = sibling.anchoredPosition.x;
                }
            }

            var characterFrame = __instance.itemSlotContainer.GetComponentInParent<CharacterFrame>();
            var characterFrameTransform = characterFrame.transform as RectTransform;
            _characterFrameOriginalWidth = characterFrameTransform.sizeDelta.x;

            UIBuilder.BeginWith(__instance.itemSlotContainer.transform.parent.gameObject)
                .Element_ScrollBox("ScrollBox", contentBuilder =>
                {
                    __instance.itemSlotContainer.transform.SetParent(contentBuilder.GameObject.transform.parent, false);
                    Object.DestroyImmediate(contentBuilder.GameObject);
                })
                .WithComponent<ScrollRect>(scrollBox =>
                {
                    scrollBox.horizontal = false;
                    scrollBox.content = __instance.itemSlotContainer.GetComponent<RectTransform>();
                    scrollBox.movementType = ScrollRect.MovementType.Clamped;
                    scrollBox.scrollSensitivity = Config.scrollSpeed.value;
                })
                .With(scrollBox =>
                {
                    var grid = __instance.itemSlotContainer.GetComponent<GridLayoutGroup>();
                    var maxSize = grid.cellSize.y * Config.maxVerticalSize.value + grid.spacing.y * (Config.maxVerticalSize.value - 1);
                    scrollBox.AddComponent<AdaptablePreferred>()
                        .Setup(__instance.itemSlotContainer.GetComponent<RectTransform>(), true, maxSize, isOversize =>
                        {
                            rectTransform.sizeDelta = new Vector2(originalWidth + (isOversize ? 20f : 0f), rectTransform.sizeDelta.y);

                            foreach (var kvp in _siblingsX)
                            {
                                var sibling = kvp.Key;
                                var originalX = kvp.Value;
                                sibling.anchoredPosition = new Vector2(originalX - (isOversize ? 20f : 0f), sibling.anchoredPosition.y);
                            }

                            characterFrameTransform.sizeDelta = new Vector2(_characterFrameOriginalWidth + (isOversize ? 20f : 0f), characterFrameTransform.sizeDelta.y);
                        });
                    scrollBox.transform.SetAsFirstSibling();
                })
                .Done
                .End();

            UIBuilder.BeginWith(__instance.itemSlotContainer.gameObject)
                .SetRectTransform(0, 0, 0, 0, 0, 1, 0, 1, 1, 1)
                .AutoSize(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize)
                .End();
        }

        [HarmonyPatch(typeof(CharacterFrame), nameof(CharacterFrame.prepareFrameMode))]
        [HarmonyPostfix]
        public static void CharacterFrame_prepareFrameMode(CharacterFrame __instance)
        {
            if (!Config.enabled.value) return;
            _characterFrameOriginalWidth = __instance.rectTransform.sizeDelta.x;
        }

        [HarmonyPatch(typeof(CharacterManager), nameof(CharacterManager.increasePlayerInventorySizeByResearch))]
        [HarmonyPrefix]
        public static bool CharacterManager_increasePlayerInventorySizeByResearch()
        {
            if (!Config.enabled.value) return true;
            return !Config.disableInventoryResearch.value;
        }
    }

}


