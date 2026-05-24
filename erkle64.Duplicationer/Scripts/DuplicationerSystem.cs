using UnityEngine;
using System.IO;
using Unfoundry;
using C3;

namespace Duplicationer
{

    [AddSystemToGameClient]
    public class DuplicationerSystem : SystemManager.System
    {
        public static LogSource log = new LogSource("Duplicationer");

        public static string BlueprintFolder { get; private set; }

        public const string BLUEPRINT_EXTENSION = "ebp";

        private BlueprintToolCHM blueprintTool;
        private int blueprintToolModeIndex;
        public static bool isBulkDemolishRunning => Messenger.HasListener("BulkDemolishTerrain.Destroy");

        public override void OnAddedToWorld()
        {
            BlueprintFolder = Path.Combine(Application.persistentDataPath, "duplicationer");

            log.Log("Loading Duplicationer");
            log.Log($"BlueprintFolder: {BlueprintFolder}");

            if (!Directory.Exists(BlueprintFolder)) Directory.CreateDirectory(BlueprintFolder);

            blueprintTool = new BlueprintToolCHM();
            blueprintTool.LoadIconSprites();
            CommonEvents.OnDeselectTool += CustomHandheldModeManager.ExitCurrentMode2;

            blueprintToolModeIndex = CustomHandheldModeManager.RegisterMode(blueprintTool);
        }

        public override void OnRemovedFromWorld()
        {
            CommonEvents.OnDeselectTool -= CustomHandheldModeManager.ExitCurrentMode2;

            CustomHandheldModeManager.DeregisterMode(blueprintTool);

            blueprintTool = null;
        }

        [EventHandler]
        public void Update(OnUpdate evt)
        {
            var clientCharacter = GameRoot.getClientCharacter();
            if (clientCharacter == null) return;

            if (Input.GetKeyDown(Config.Input.toggleBlueprintToolKey.value) && InputHelpers.IsKeyboardInputAllowed)
            {
                CustomHandheldModeManager.ToggleMode(clientCharacter, blueprintToolModeIndex);
            }
        }

        public static bool IsCheatModeEnabled => Config.Cheats.cheatModeAllowed.value && Config.Cheats.cheatModeEnabled.value;
    }

}
