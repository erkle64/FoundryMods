using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FarPainter
{

    [HarmonyPatch]
    public static class Patch
    {
        private static bool _bulkPaintDragging = false;
        private static float _bulkPaintStartTime = 0.0f;
        private static Vector3Int _bulkPaintStartPosition = Vector3Int.zero;
        private static BulkPaintOrientation _bulkPaintOrientation = BulkPaintOrientation.X;
        private static Plane _bulkPaintPlane = new Plane();

        private enum BulkPaintOrientation
        {
            X,
            Y,
            Z
        }

        public static readonly MethodInfo updateSelectedColorTint = typeof(ColorToolHH).GetMethod("updateSelectedColorTint", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo raycastHits = typeof(ColorToolHH).GetField("raycastHits", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo bobMultiplier = typeof(ColorToolHH).GetField("bobMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo bobTimer = typeof(ColorToolHH).GetField("bobTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo lastTintColor = typeof(ColorToolHH).GetField("lastTintColor", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo lastPlayedAudioClipIdx = typeof(ColorToolHH).GetField("lastPlayedAudioClipIdx", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo lastColorizedObject = typeof(ColorToolHH).GetField("lastColorizedObject", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo lastColorizedObject_isReset = typeof(ColorToolHH).GetField("lastColorizedObject_isReset", BindingFlags.NonPublic | BindingFlags.Instance);
        public static readonly FieldInfo lastColorizationTime = typeof(ColorToolHH).GetField("lastColorizationTime", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(ColorToolHH), nameof(ColorToolHH._updateBehavoir))]
        [HarmonyPrefix]
        public static bool ColorToolHH_updateBehavoir(ColorToolHH __instance)
        {
            updateSelectedColorTint.Invoke(__instance, null);
            if (!__instance.relatedCharacter.sessionOnly_isClientCharacter || !__instance.isClientCharacterEquip)
                return false;
            if (__instance.isClientCharacterEquip)
            {
                Vector3 localPosition = __instance.transform.localPosition;
                bobMultiplier.SetValue(__instance, (float)bobMultiplier.GetValue(__instance) * 0.97f + 0.03f);
                bobTimer.SetValue(__instance, (float)bobTimer.GetValue(__instance) + Time.deltaTime * (float)bobMultiplier.GetValue(__instance));
                localPosition.y = __instance.defaultPosition.y + (float)(((double)Mathf.PerlinNoise(0.0f, (float)bobTimer.GetValue(__instance)) * 2.0 - 1.0) * 0.00749999983236194);
                __instance.transform.localPosition = localPosition;
            }
            bool allowAction = true;
            bool allowAlternateAction = true;
            if (GlobalStateManager.checkIfCursorIsRequired())
            {
                allowAction = false;
                allowAlternateAction = false;
                _bulkPaintDragging = false;
            }
            if (GameRoot.getClientRenderCharacter().isLookingAtInteractibleObject(out GameObject _))
                allowAction = false;
            if (ScreenPanelRaycaster.isClientCharacterLookingAtScreenPanel())
                allowAction = false;
            bool isMouseDown = allowAction && GlobalStateManager.getRewiredPlayer0().GetButton("Action");
            if (allowAlternateAction && GlobalStateManager.getRewiredPlayer0().GetButtonDown("Alternate Action"))
                ColorToolFrame.showFrame();
            bool modifier1Held = GlobalStateManager.getRewiredPlayer0().GetButton("Modifier 1");
            bool modifier2Held = GlobalStateManager.getRewiredPlayer0().GetButton("Modifier 2");

            Ray ray = new Ray(GameRoot.getMainCamera().transform.position, GameRoot.getMainCamera().transform.forward);
            RaycastHit[] raycastHits = (RaycastHit[])Patch.raycastHits.GetValue(__instance);
            if (_bulkPaintDragging)
            {
                if (_bulkPaintPlane.Raycast(ray, out var distanceToPlane))
                {
                    var hitPosition = ray.GetPoint(distanceToPlane);
                    Vector3Int targetCube = _bulkPaintStartPosition;
                    switch (_bulkPaintOrientation)
                    {
                        case BulkPaintOrientation.X:
                            targetCube = new Vector3Int(_bulkPaintStartPosition.x, Mathf.FloorToInt(hitPosition.y), Mathf.FloorToInt(hitPosition.z));
                            break;

                        case BulkPaintOrientation.Y:
                            targetCube = new Vector3Int(Mathf.FloorToInt(hitPosition.x), _bulkPaintStartPosition.y, Mathf.FloorToInt(hitPosition.z));
                            break;

                        case BulkPaintOrientation.Z:
                            targetCube = new Vector3Int(Mathf.FloorToInt(hitPosition.x), Mathf.FloorToInt(hitPosition.y), _bulkPaintStartPosition.z);
                            break;
                    }

                    var diff = targetCube - _bulkPaintStartPosition;
                    var size = new Vector3Int(Mathf.Abs(diff.x) + 1, Mathf.Abs(diff.y) + 1, Mathf.Abs(diff.z) + 1);
                    var x = _bulkPaintOrientation == BulkPaintOrientation.X ? size.z : size.x;
                    var y = _bulkPaintOrientation == BulkPaintOrientation.Y ? size.z : size.y;
                    GameRoot.pushPerFrameHighlighterBox(_bulkPaintStartPosition + (Vector3)diff * 0.5f + new Vector3(0.5f, 0.5f, 0.5f), size, 1);
                    GameRoot.setInfoText(string.Format(
                        "Now point to the opposite end of the area and press {2} to confirm.\nSize: {0}x{1}\n{3} to cancel.",
                        x, y,
                        GameRoot.getHotkeyStringFromAction("Action"),
                        GameRoot.getHotkeyStringFromAction("Alternate Action")));

                    if (GlobalStateManager.getRewiredPlayer0().GetButtonUp("Action") && Time.time > _bulkPaintStartTime + 0.5f)
                    {
                        _bulkPaintDragging = false;

                        byte color_r = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.r * byte.MaxValue), 0, byte.MaxValue);
                        byte color_g = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.g * byte.MaxValue), 0, byte.MaxValue);
                        byte color_b = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.b * byte.MaxValue), 0, byte.MaxValue);

                        var pos = new Vector3Int(Mathf.Min(_bulkPaintStartPosition.x, targetCube.x), Mathf.Min(_bulkPaintStartPosition.y, targetCube.y), Mathf.Min(_bulkPaintStartPosition.z, targetCube.z));
                        AABB3D aabb = new AABB3D(pos.x, pos.y, pos.z, size.x, size.y, size.z);
                        using (var query = StreamingSystem.get().queryAABB3D(aabb))
                        {
                            foreach (var bogo in query)
                            {
                                if (bogo is IHasColorManager hasColorManager && hasColorManager.ColorManager.colorMeshRenderers.Length > 0)
                                {
                                    GameRoot.addLockstepEvent(new ColorizeObjectEvent(__instance.relatedCharacter.usernameHash, bogo.relatedEntityId, color_r, color_g, color_b, false, false));
                                }
                            }
                        }

                        lastPlayedAudioClipIdx.SetValue(__instance, (int)lastPlayedAudioClipIdx.GetValue(__instance) + 1);
                        lastPlayedAudioClipIdx.SetValue(__instance, (int)lastPlayedAudioClipIdx.GetValue(__instance) % ResourceDB.resourceLinker.audioClip_paintingStrokes.Length);
                        if (!__instance.audioSource_painting.isPlaying)
                            __instance.audioSource_painting.PlayOneShot(ResourceDB.resourceLinker.audioClip_paintingStrokes[(int)lastPlayedAudioClipIdx.GetValue(__instance)]);
                    }
                    else if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Alternate Action"))
                    {
                        _bulkPaintDragging = false;
                    }
                }
            }
            else if (allowAction && modifier1Held)
            {
                __instance.relatedCharacter.renderCharacter.getVoxelInteractionTarget(Config.paintRange.value, out var targetCube, out var faceTarget, out RaycastHit _);
                if (faceTarget != -1)
                {
                    GameRoot.pushPerFrameHighlighterBox((Vector3)targetCube + new Vector3(0.5f, 0.5f, 0.5f), Vector3.one, 1);

                    if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action"))
                    {
                        switch (faceTarget)
                        {
                            case 0:
                                _bulkPaintOrientation = BulkPaintOrientation.X;
                                _bulkPaintPlane = new Plane(Vector3.right, targetCube + new Vector3Int(1, 0, 0));
                                break;

                            case 1:
                                _bulkPaintOrientation = BulkPaintOrientation.X;
                                _bulkPaintPlane = new Plane(Vector3.left, targetCube);
                                break;

                            case 2:
                                _bulkPaintOrientation = BulkPaintOrientation.Y;
                                _bulkPaintPlane = new Plane(Vector3.up, targetCube + new Vector3Int(0, 1, 0));
                                break;

                            case 3:
                                _bulkPaintOrientation = BulkPaintOrientation.Y;
                                _bulkPaintPlane = new Plane(Vector3.down, targetCube);
                                break;

                            case 4:
                                _bulkPaintOrientation = BulkPaintOrientation.Z;
                                _bulkPaintPlane = new Plane(Vector3.forward, targetCube + new Vector3Int(0, 0, 1));
                                break;

                            case 5:
                                _bulkPaintOrientation = BulkPaintOrientation.Z;
                                _bulkPaintPlane = new Plane(Vector3.back, targetCube);
                                break;
                        }

                        _bulkPaintDragging = true;
                        _bulkPaintStartPosition = targetCube;
                        _bulkPaintStartTime = Time.time;
                    }
                }
            }
            else
            {
                // do raycast to find potential colorization target
                bool hasAnyTarget = false;
                bool hasValidTarget = false;
                Ray r = new Ray(GameRoot.getMainCamera().transform.position, GameRoot.getMainCamera().transform.forward);
                int layerMask = GlobalStaticCache.s_LayerMask_BuildableObjectFullSize | GlobalStaticCache.s_LayerMask_BuildableObjectPartialSize | GlobalStaticCache.s_LayerMask_TrainVehicle;
                int hitCount = Physics.RaycastNonAlloc(r, raycastHits, Config.paintRange.value, layerMask);
                if (hitCount > 0)
                {
                    hasAnyTarget = true;

                    int nearestHitIdx = raycastHits.findNearestHit(hitCount);
                    var hitGameObject = raycastHits[nearestHitIdx].collider.gameObject;
                    var hasColorManager = hitGameObject.GetComponentInParent<IHasColorManager>();
                    if (hasColorManager != null)
                    {
                        hasValidTarget = true;

                        // if mouse down -> colorize
                        if (isMouseDown == true)
                        {
                            // check if this should be a reset to default color
                            bool isReset = false;
                            if (GlobalStateManager.getRewiredPlayer0().GetButton("Modifier 2") == true)
                                isReset = true;

                            // we allow holding down mouse button to continuously paint, but if we don't add a timer per entity id,
                            // we will send an event each frame.
                            bool allowEvent = true;
                            if ((IHasColorManager)lastColorizedObject.GetValue(__instance) == hasColorManager && (bool)lastColorizedObject_isReset.GetValue(__instance) == isReset && (Time.realtimeSinceStartup - (float)lastColorizationTime.GetValue(__instance)) < 1f)
                                allowEvent = false;

                            // send colorization event
                            if (allowEvent == true)
                            {
                                // send event
                                byte color_r = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.r * 255f), 0, byte.MaxValue);
                                byte color_g = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.g * 255f), 0, byte.MaxValue);
                                byte color_b = (byte)Mathf.Clamp(Mathf.RoundToInt(__instance.relatedCharacter.clientData.lastSelectedColor.b * 255f), 0, byte.MaxValue);

                                var boGO = hasColorManager as BuildableObjectGO;
                                if (boGO != null)
                                {
                                    var lsEvent = new ColorizeObjectEvent(__instance.relatedCharacter.usernameHash, boGO.relatedEntityId, color_r, color_g, color_b, isReset, false);
                                    GameRoot.addLockstepEvent(lsEvent);

                                    boGO.tryGetVisualSMI(out var smi);
                                    hasColorManager.ColorManager.updateColor(smi, new Color(color_r / 255f, (byte)color_g / 255f, (byte)color_b / 255f));

                                    if (StreamingProxySystem.get().hasProxy<BuildableObjectProxy>(boGO.Id))
                                    {
                                        StreamingProxySystem.get().getProxy<BuildableObjectProxy>(boGO.Id).setColor(color_r, color_g, color_b);
                                    }
                                }
                                else
                                {
                                    var trainVehicleGO = hasColorManager as TrainTopGO;
                                    if (trainVehicleGO != null)
                                    {
                                        var lsEvent = new ColorizeObjectEvent(__instance.relatedCharacter.usernameHash, trainVehicleGO.parentTrainVehicle.id, color_r, color_g, color_b, isReset, true);
                                        GameRoot.addLockstepEvent(lsEvent);
                                        hasColorManager.ColorManager.updateColor(null, new Color(color_r / 255f, (byte)color_g / 255f, (byte)color_b / 255f));
                                    }
                                    else
                                    {
                                        C3.Dbg.LogError($"Unhandled IHasColorManager component.");
                                    }
                                }

                                // play client side sfx
                                lastPlayedAudioClipIdx.SetValue(__instance, (int)lastPlayedAudioClipIdx.GetValue(__instance) + 1);
                                lastPlayedAudioClipIdx.SetValue(__instance, (int)lastPlayedAudioClipIdx.GetValue(__instance) % ResourceDB.resourceLinker.audioClip_paintingStrokes.Length);
                                if (!__instance.audioSource_painting.isPlaying)
                                    __instance.audioSource_painting.PlayOneShot(ResourceDB.resourceLinker.audioClip_paintingStrokes[(int)lastPlayedAudioClipIdx.GetValue(__instance)]);

                                // set cache
                                lastColorizedObject.SetValue(__instance, hasColorManager);
                                lastColorizedObject_isReset.SetValue(__instance, isReset);
                                lastColorizationTime.SetValue(__instance, Time.realtimeSinceStartup);
                            }
                        }
                    }
                }

                if (hasAnyTarget == true)
                {
                    if (hasValidTarget == true)
                        // LOC: Color Tool > Info Text > Valid Target, keep the line break (0/1/2/3 = keybinds, f.i. "LMB" (for left mouse button), or "Alt+LMB")
                        GameRoot.setInfoText(
                            PoMgr._po("COLOR_TOOL_INFO_VALID", "{0} to colorize. {1} to select color.\n{2}+{3} to reset object to default color.", GameRoot.getHotkeyStringFromAction("Action"), GameRoot.getHotkeyStringFromAction("Alternate Action"), GameRoot.getHotkeyStringFromAction("Modifier 2"), GameRoot.getHotkeyStringFromAction("Action"))
                            + $"\nHold {GameRoot.getHotkeyStringFromAction("Modifier 1")} to use bulk paint mode."
                            );
                    else
                        // LOC: Color Tool > Info Text > Invalid Target
                        GameRoot.setInfoText(PoMgr._po("COLOR_TOOL_INFO_INVALID", "Target object cannot be colored."));
                }
                else
                    // LOC: Color Tool > Info Text > No Target
                    GameRoot.setInfoText(PoMgr._po("COLOR_TOOL_INFO_NO_TARGET", "Look at objects to apply color, not every object can be colorized."));
            }
            return false;
        }
    }

}




