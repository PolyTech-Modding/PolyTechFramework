using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Timers;
using UnityEngine;
using TMPro;
using Logger = BepInEx.Logging.Logger;
namespace PolyTechFramework
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Poly Bridge 2")]
    [BepInDependency(ConfigurationManager.ConfigurationManager.GUID, BepInDependency.DependencyFlags.HardDependency)]
    public class PolyTechMain : PolyTechMod
    {
        public new const string
            PluginGuid = "polytech.polytechframework",
            PluginName = "PolyTech Framework",
            PluginVersion = "0.9.7";
        private static BindingList<PolyTechMod>
            noncheatMods = new BindingList<PolyTechMod> { },
            cheatMods = new BindingList<PolyTechMod> { };
        private static List<PolyTechMod> modsDisabledByPTF = new List<PolyTechMod> { };
        private static string modsToggledSummary = "";
        public static ConfigDefinition
            moddedWatermarkDef = new ConfigDefinition("PolyTech Framework", "Modded Watermark"),
            vanillaWatermarkDef = new ConfigDefinition("PolyTech Framework", "Vanilla Watermark"),
            modEnabledDef = new ConfigDefinition("PolyTech Framework", "Enabled"),
            forceCheatDef = new ConfigDefinition("PolyTech Framework", "Force Cheat Flag"),
            sandboxEverywhereDef = new ConfigDefinition("PolyTech Framework", "Sandbox Everywhere"),
            globalToggleHotkeyDef = new ConfigDefinition("PolyTech Framework", "Global Toggle Hotkey"),
            leaderboardProtMinDef = new ConfigDefinition("Leaderboard Protection", "Minimum Score"),
            leaderboardCheckDef = new ConfigDefinition("Leaderboard Protection", "Confirm Before Upload"),
            leaderboardBlockDef = new ConfigDefinition("Leaderboard Protection", "Block All Scores");
        public static ConfigEntry<watermarks>
            moddedWatermark,
            vanillaWatermark;
        public static ConfigEntry<bool>
            modEnabled,
            forceCheat,
            sandboxEverywhere,
            leaderboardCheck,
            leaderboardBlock;
        public static ConfigEntry<int>
            leaderboardProtMin;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut>
            globalToggleHotkey;
        public static int
            enabledCheatTweaks;
        public static PolyTechMain
            ptfInstance;

        public BepInEx.Logging.ManualLogSource ptfLogger;
        public bool modCheated;

        GameObject
            autoDraw,
            sandbox,
            sandboxSettings,
            sandboxCreate,
            sandboxVehicles,
            sandboxWorkshop,
            sandboxResources;

        public enum watermarks
        {
            [Description("PolyTech Style Watermark")]
            polytech,
            [Description("PolyBridge Style Watermark")]
            polybridge
        }

        Harmony harmony;

        public void Awake()
        {
            moddedWatermark = Config.Bind(moddedWatermarkDef, watermarks.polytech, new ConfigDescription("Selected Watermark"));
            vanillaWatermark = Config.Bind(vanillaWatermarkDef, watermarks.polytech, new ConfigDescription("Selected Watermark"));

            modEnabled = Config.Bind(modEnabledDef, true, new ConfigDescription("Enable Mod"));
            modEnabled.SettingChanged += onEnableDisable;

            forceCheat = Config.Bind(forceCheatDef, false, new ConfigDescription("Force Cheat Flag"));
            forceCheat.SettingChanged += onForceCheatToggle;

            sandboxEverywhere = Config.Bind(sandboxEverywhereDef, false, new ConfigDescription("Allow sandbox resource editor and scene changer in any level (enables cheat flag)"));
            sandboxEverywhere.SettingChanged += sandboxEverywhereToggle;

            globalToggleHotkey = Config.Bind(globalToggleHotkeyDef, new BepInEx.Configuration.KeyboardShortcut(KeyCode.BackQuote, KeyCode.LeftAlt), new ConfigDescription("Keybind used to toggle mods without opening the config menu."));

            leaderboardProtMin = Config.Bind(leaderboardProtMinDef, 71, new ConfigDescription("Minimum value allowed to upload to leaderboard. 71 is the minimum to protect from automatic shadowbans."));
            leaderboardProtMin.SettingChanged += onLeaderboardProtChange;
            leaderboardCheck = Config.Bind(leaderboardCheckDef, false, new ConfigDescription("If checked, the game will confirm with the user before uploading scores to the leaderboard."));
            leaderboardBlock = Config.Bind(leaderboardBlockDef, false, new ConfigDescription("If checked, the game will never upload a score to the leaderboard."));

            noncheatMods.ListChanged += onCosmeticsChanged;
            cheatMods.ListChanged += onCheatsChanged;

            enabledCheatTweaks = 0 + (forceCheat.Value ? 1 : 0) + (sandboxEverywhere.Value ? 1 : 0);

            this.modCheated = false;
            this.repositoryUrl = "https://github.com/PolyTech-Modding/PolyTechFramework/";

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(PolyTechMain));
            harmony.PatchAll(typeof(PolyTechMain).Assembly);

            PolyTechUtils.setModdedSimSpeeds();
            PolyTechUtils.setReplaysModded();
            PolyTechUtils.setVersion();
            this.ptfLogger = Logger;
            Logger.LogInfo($"Loaded {PluginName} v{PluginVersion}");
            this.isCheat = false;
            this.isEnabled = modEnabled.Value;
            ptfInstance = this;
            
            this.authors = new string[] {"MoonlitJolty", "Conqu3red", "Razboy20", "Tran Fox", "nitsuga5124"};

            registerMod(this);
        }

        bool flag = false;
        private void Update()
        {
            PopupQueue.TryShowNextPopup();
            if (numEnabledCheatMods() > 0 && Bridge.IsSimulating() && !BridgeCheat.m_Cheated){
                GameStateSim.m_BudgetUsed = Mathf.RoundToInt(Budget.CalculateBridgeCost());
			    BridgeCheat.m_Cheated = BridgeCheat.CheckForCheating((float)GameStateSim.m_BudgetUsed);
            }
            if (!flag && globalToggleHotkey.Value.IsDown())
            {
                flag = true;
                if (this.isEnabled)
                {
                    this.isEnabled = modEnabled.Value = false;
                    this.disableMod();
                }
                else
                {
                    this.isEnabled = modEnabled.Value = true;
                    this.enableMod();
                }
                if (modsToggledSummary.Length > 0) PopUpWarning.Display(modsToggledSummary);
                //Logger.LogMessage(modsToggledSummary);

            }
            else if (flag & globalToggleHotkey.Value.IsUp())
            {
                flag = false;
            }

            if (autoDraw == null)
            {
                var autoDraw = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/GridStress/ButtonsHorizontalLayout/ButtonContainer_AutoDraw");
                if (autoDraw == null) return;
                autoDraw.SetActive(true);
                sandbox = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox");
                sandboxSettings = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/ButtonsHorizontalLayout/Button_SandboxSettings");
                sandboxCreate = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/ButtonsHorizontalLayout/Button_Create");
                sandboxVehicles = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/ButtonsHorizontalLayout/Button_Vehicles");
                sandboxWorkshop = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/ButtonsHorizontalLayout/Button_Workshop");
                sandboxResources = GameObject.Find("GameUI/Panel_TopBar/HorizontalLayout/CenterInfo/Sandbox/ButtonsHorizontalLayout/Button_Resources");
            }

            sandboxCreate.SetActive(GameStateManager.GetState() == GameState.SANDBOX);
            sandboxVehicles.SetActive(GameStateManager.GetState() == GameState.SANDBOX);
            sandboxWorkshop.SetActive(GameStateManager.GetState() == GameState.SANDBOX);
            sandboxSettings.SetActive(true);
            sandboxResources.SetActive(true);
            sandbox.SetActive((sandboxEverywhere.Value && PolyTechMain.modEnabled.Value) || GameStateManager.GetState() == GameState.SANDBOX);
        }

        public void onCosmeticsChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemDeleted) return;
            PolyTechMod mod = noncheatMods[noncheatMods.Count - 1];
            string nameAndVers = $"{mod.Info.Metadata.Name} v{mod.Info.Metadata.Version}";
            Logger.LogInfo("Registered cosmetic mod: " + nameAndVers);
        }
        public void onCheatsChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemDeleted) return;
            PolyTechMod mod = cheatMods[cheatMods.Count - 1];
            string nameAndVers = $"{mod.Info.Metadata.Name} v{mod.Info.Metadata.Version}";
            Logger.LogInfo("Registered cheat mod: " + nameAndVers);
        }
        public static int numEnabledCheatMods()
        {
            int result = 0;
            foreach (PolyTechMod mod in cheatMods)
            {
                if (mod.isEnabled) result++;
            }
            return result;
        }
        public static void registerMod(PolyTechMod plugin)
        {
            if (plugin.isCheat)
            {
                cheatMods.Add(plugin);
            }
            else
            {
                noncheatMods.Add(plugin);
            }

            checkForModUpdate(plugin);
        }

        public static void setCheat(PolyTechMod plugin, bool isCheat){
            if(plugin.isCheat == isCheat) return;
            if (isCheat)
            {
                noncheatMods.Remove(plugin);
                cheatMods.Add(plugin);
            }
            else
            {
                cheatMods.Remove(plugin);
                noncheatMods.Add(plugin);
            }
            plugin.isCheat = isCheat;
        }

        public override void enableMod()
        {
            string summary = "Mods Enabled: PolyTechFramework";
            foreach (PolyTechMod mod in modsDisabledByPTF)
            {
                mod.enableMod();
                summary += $" - {mod.Info.Metadata.Name}";
            }
            modsToggledSummary = summary;
            PolyTechUtils.setReplaysModded();
        }

        public override void disableMod()
        {

            string summary = "Mods Disabled: PolyTechFramework";
            modsDisabledByPTF.Clear();
            foreach (PolyTechMod mod in cheatMods)
            {
                if (!mod.isEnabled) continue;
                modsDisabledByPTF.Add(mod);
                mod.disableMod();
                summary += $" - {mod.Info.Metadata.Name}";
            }
            foreach (PolyTechMod mod in noncheatMods)
            {
                if (!mod.isEnabled) continue;
                modsDisabledByPTF.Add(mod);
                mod.disableMod();
                summary += $" - {mod.Info.Metadata.Name}";
            }
            PolyTechUtils.setReplaysVanilla();
        }

        public static void checkForModUpdate(PolyTechMod plugin)
        {
            if (plugin.repositoryUrl == null) return;
            var client = new WebClient();
            client.Headers.Add("User-Agent", "Nothing");

            // get latest release version
            string repoReleaseUri = "https://api.github.com/repos" + new Uri(plugin.repositoryUrl).AbsolutePath + "releases";
            string content;
            try
            {
                content = client.DownloadString(repoReleaseUri);
            }
            catch (Exception e)
            {
                ptfInstance.ptfLogger.LogError(e.Message);
                return;
            }

            // deserialize incoming JSON from repo api
            List<Release> releases = null;
            using (MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(content)))
            {
                DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<Release>));
                releases = (List<Release>)jsonSerializer.ReadObject(ms);
            }
            if (releases == null) return;
            if (releases.Count <= 0) return;
            Release latestRelease = releases[0];
            if (latestRelease.GetVersion().CompareTo(plugin.Info.Metadata.Version) > 0)
            {
                ModUpdate modUpdate = new ModUpdate(plugin, latestRelease);
                if (patchGameStart.game_started)
                {
                    modUpdatePopup(modUpdate);
                }
                else
                {
                    patchGameStart.modUpdates.Add(modUpdate);
                }
            }

        }
        private static void modUpdatePopup(ModUpdate modUpdate)
        {
            ptfInstance.ptfLogger.LogInfo($"\n------------------------\n{modUpdate.mod.Info.Metadata.Name} has an update available!\n{modUpdate.old_version} -> {modUpdate.new_version}\n------------------------\n");
            PopUpMessage.Display($"{modUpdate.mod.Info.Metadata.Name} has an update available!\n{modUpdate.old_version} -> {modUpdate.new_version}", () => System.Diagnostics.Process.Start(modUpdate.latest_release.html_url));
        }

        public void onForceCheatToggle(object sender, EventArgs e)
        {
            updateCheatTweaks(forceCheat.Value);
        }

        public void sandboxEverywhereToggle(object sender, EventArgs e)
        {
            updateCheatTweaks(sandboxEverywhere.Value);
        }

        public void onLeaderboardProtChange(object sender, EventArgs e)
        {
            if(leaderboardProtMin.Value < 71) leaderboardProtMin.Value = 71;
        }

        private void updateCheatTweaks(bool value)
        {
            enabledCheatTweaks += value ? 1 : -1;
            this.isCheat = enabledCheatTweaks > 0;
            if (enabledCheatTweaks > 0) this.modCheated = true;
        }

        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }

        [HarmonyPatch(typeof(GameManager), "StartManual")]
        [HarmonyPostfix]
        private static void GameStartPostfix(){
            patchGameStart.game_started = true;
            if (patchGameStart.modUpdates == null) return;
            foreach (ModUpdate modUpdate in patchGameStart.modUpdates)
            {
                modUpdatePopup(modUpdate);
            }
        }
        private class patchGameStart
        {
            public static List<ModUpdate> modUpdates = new List<ModUpdate>();
            public static bool game_started = false;


        }

        [HarmonyPatch(typeof(BridgeCheat), "CheckForCheating")]
        [HarmonyPrefix]
        private static bool PatchCheats(ref bool __result)
        {
            __result = true;
            ptfInstance.modCheated = ptfInstance.modCheated || (modEnabled.Value && numEnabledCheatMods() > 0) || (PolyTechMain.modEnabled.Value && enabledCheatTweaks > 0);
            return !ptfInstance.modCheated;
        }

        [HarmonyPatch(typeof(BridgeCheat), "GetLocalizedCheatReason")]
        [HarmonyPrefix]
        private static bool PatchCheatTooltop(ref string __result)
        {
            __result = "Cheat Mods Enabled.";
            return !ptfInstance.modCheated;
        }


        [HarmonyPatch(typeof(GameStateManager), "ChangeState")]
        [HarmonyPrefix]
        private static void PatchGetState(GameState state)
        {
            if (state == GameState.MAIN_MENU) ptfInstance.modCheated = false;
        }


        [HarmonyPatch(typeof(Panel_ShareReplay), "DoFFMpeg")]
        [HarmonyPrefix]
        private static void PatchWatermark(string inputBasePath, ref string watermarkPath, int videoKbps)
        {
            string watermarkFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "watermarks");
            string watermarkName;

            if (ptfInstance.modCheated)
            {
                switch (moddedWatermark.Value)
                {
                    case watermarks.polytech:
                        watermarkName = "polytechModdedWatermark.png";
                        break;
                    case watermarks.polybridge:
                        watermarkName = "polybridgeModdedWatermark.png";
                        break;
                    default:
                        watermarkName = "polytechModdedWatermark.png";
                        break;
                }
            }
            else
            {
                switch (vanillaWatermark.Value)
                {
                    case watermarks.polytech:
                        watermarkName = "polytechWatermark.png";
                        break;
                    case watermarks.polybridge:
                        watermarkName = "polybridgeWatermark.png";
                        break;
                    default:
                        watermarkName = "polytechWatermark.png";
                        break;
                }
            }
            watermarkPath = Path.Combine(watermarkFolder, watermarkName);

        }


        public static class PopupQueue {
            public static Queue<Popup> queue = new Queue<Popup> ();
            public static void TryShowNextPopup(){
                if (queue.Count > 0 && !PopupIsActive()){
                    var popup = queue.Dequeue();
                    popup.Display();
                }
            }
            public static bool PopupIsActive(){
                return GameUI.m_Instance.m_PopUpMessage.m_Animator.isActiveAndEnabled || PopUpMessage.IsActive() ||
                GameUI.m_Instance.m_PopUpInputField.m_Animator.isActiveAndEnabled || PopupInputField.IsActive() ||
                GameUI.m_Instance.m_PopUpTwoChoices.m_Animator.isActiveAndEnabled || PopUpTwoChoices.IsActive() ||
                GameUI.m_Instance.m_PopUpWarning.m_Animator.isActiveAndEnabled || PopUpWarning.IsActive();
            }
        }
        public interface Popup {
            void Display();
        }

        public class PopupMessageQueueItem : Popup {
            public PopupMessageQueueItem(){}
            public PopupMessageQueueItem(
                string message, 
                Panel_PopUpMessage.OnChoiceDelegate okDelegate, 
                Panel_PopUpMessage.OnChoiceDelegate cancelDelegate,
                PopUpWarningCategory warningCategory
            ){
                this.message = message;
                this.okDelegate = okDelegate;
                this.cancelDelegate = cancelDelegate;
                this.warningCategory = warningCategory;
            }
            public void Display(){
                PopUpMessage.Display(message, okDelegate, cancelDelegate, warningCategory);
                GameUI.m_Instance.m_PopUpMessage.m_NeverShowAgainToggle.transform.parent.gameObject.SetActive(false);
                if (cancelDelegate == null){
                    GameUI.m_Instance.m_PopUpMessage.m_CancelButton.gameObject.SetActive(false);
                }

            }
            public string message;
            public Panel_PopUpMessage.OnChoiceDelegate okDelegate;
            public Panel_PopUpMessage.OnChoiceDelegate cancelDelegate;
            public PopUpWarningCategory warningCategory;
        }

        [HarmonyPatch(typeof(PopUpMessage), "Display", new Type[] { 
            typeof(string), 
            typeof(Panel_PopUpMessage.OnChoiceDelegate), 
        })]
        [HarmonyPrefix]
        public static bool PopupMessageCancelButtonFix(
            string message,
            Panel_PopUpMessage.OnChoiceDelegate okDelegate
        ){
            PopUpMessage.Display(message, okDelegate, () => {}, PopUpWarningCategory.NONE);
            GameUI.m_Instance.m_PopUpMessage.m_NeverShowAgainToggle.transform.parent.gameObject.SetActive(false);
            return false;
        }

        [HarmonyPatch(typeof(PopUpMessage), "Display", new Type[] { 
            typeof(string), 
            typeof(Panel_PopUpMessage.OnChoiceDelegate), 
            typeof(Panel_PopUpMessage.OnChoiceDelegate), 
            typeof(PopUpWarningCategory)
        })]
        [HarmonyPrefix]
        public static bool PopupMessagePatch(
            string message,
            Panel_PopUpMessage.OnChoiceDelegate okDelegate,
            Panel_PopUpMessage.OnChoiceDelegate cancelDelegate,
            PopUpWarningCategory warningCategory
        ){
            if (PopupQueue.PopupIsActive()){
                ptfInstance.ptfLogger.LogInfo("popup is already active, queueing!");
                PopupMessageQueueItem QueueItem = new PopupMessageQueueItem(
                    message,
                    okDelegate,
                    cancelDelegate,
                    warningCategory
                );
                PopupQueue.queue.Enqueue(QueueItem);
                return false;
            }
            return true;
        }


        public class PopupInputFieldQueueItem : Popup {
            public PopupInputFieldQueueItem(){}

            public PopupInputFieldQueueItem(
                string title,
                string defaultText,
                Panel_PopUpInputField.OnOkDelegate okDelegate
            ){
                this.title = title;
                this.defaultText = defaultText;
                this.okDelegate = okDelegate;
            }
            public void Display(){
                PopupInputField.Display(
                    title,
                    defaultText,
                    okDelegate
                );
            }
           public string title;
           public string defaultText;
           public Panel_PopUpInputField.OnOkDelegate okDelegate;
        }
        
        [HarmonyPatch(typeof(PopupInputField), "Display", new Type[] { 
            typeof(string), 
            typeof(string), 
            typeof(Panel_PopUpInputField.OnOkDelegate)
        })]
        [HarmonyPrefix]
        public static bool PopupMessageInputFieldPatch(
            string title,
            string defaultText,
            Panel_PopUpInputField.OnOkDelegate okDelegate
        ){
            if (PopupQueue.PopupIsActive()){
                ptfInstance.ptfLogger.LogInfo("popup is already active, queueing!");
                PopupInputFieldQueueItem QueueItem = new PopupInputFieldQueueItem(
                    title,
                    defaultText,
                    okDelegate
                );
                PopupQueue.queue.Enqueue(QueueItem);
                return false;
            }
            return true;
        }




        public class PopupTwoChoicesQueueItem : Popup {
            public PopupTwoChoicesQueueItem() {}

            public PopupTwoChoicesQueueItem(
                string message,
                string choiceA,
                string choiceB,
                Panel_PopUpTwoChoices.OnChoiceDelegate callbackA,
                Panel_PopUpTwoChoices.OnChoiceDelegate callbackB
            ){
                this.message = message;
                this.choiceA = choiceA;
                this.choiceB = choiceB;
                this.callbackA = callbackA;
                this.callbackB = callbackB;
            }
            public void Display(){
                PopUpTwoChoices.Display(
                    message,
                    choiceA,
                    choiceB,
                    callbackA,
                    callbackB,
                    PopUpWarningCategory.NONE
                );
                GameUI.m_Instance.m_PopUpTwoChoices.m_NeverShowAgainToggle.transform.parent.gameObject.SetActive(false);
            }
            string message;
            string choiceA;
            string choiceB;
            Panel_PopUpTwoChoices.OnChoiceDelegate callbackA;
            Panel_PopUpTwoChoices.OnChoiceDelegate callbackB;
        }
        [HarmonyPatch(typeof(PopUpTwoChoices), "Display", new Type[] { 
            typeof(string), 
            typeof(string), 
            typeof(string), 
            typeof(Panel_PopUpTwoChoices.OnChoiceDelegate),
            typeof(Panel_PopUpTwoChoices.OnChoiceDelegate),
            typeof(PopUpWarningCategory)
        })]
        [HarmonyPrefix]
        public static bool PopupTwoChoicesPatch(
            string message,
            string choiceA,
            string choiceB,
            Panel_PopUpTwoChoices.OnChoiceDelegate callbackA,
            Panel_PopUpTwoChoices.OnChoiceDelegate callbackB,
            PopUpWarningCategory warningCategory
        ){
            if (PopupQueue.PopupIsActive()){
                ptfInstance.ptfLogger.LogInfo("popup is already active, queueing!");
                PopupTwoChoicesQueueItem QueueItem = new PopupTwoChoicesQueueItem(
                    message,
                    choiceA,
                    choiceB,
                    callbackA,
                    callbackB
                );
                PopupQueue.queue.Enqueue(QueueItem);
                return false;
            }
            return true;
        }
        public class PopupWarningQueueItem : Popup {
            public PopupWarningQueueItem() {}

            public PopupWarningQueueItem(
                string message,
                PopUpWarningCategory category
            ){
                this.message = message;
                this.category = category;
            }
            public void Display(){
                PopUpWarning.Display(
                    message,
                    category
                );
                GameUI.m_Instance.m_PopUpWarning.m_NeverShowAgainToggle.transform.parent.gameObject.SetActive(false);
            }
            public string message;
            public PopUpWarningCategory category;
        }
        [HarmonyPatch(typeof(PopUpWarning), "Display", new Type[] { 
            typeof(string), 
            typeof(PopUpWarningCategory)
        })]
        [HarmonyPrefix]
        public static bool PopupWarningPatch(
            string message,
            PopUpWarningCategory category
        ){
            if (PopupQueue.PopupIsActive()){
                ptfInstance.ptfLogger.LogInfo("popup is already active, queueing!");
                PopupWarningQueueItem QueueItem = new PopupWarningQueueItem(
                    message,
                    category
                );
                PopupQueue.queue.Enqueue(QueueItem);
                return false;
            }
            return true;
        }


        [HarmonyPatch(typeof(Replays), "CreateReplayMovieDirectory")]
        [HarmonyPrefix]
        private static bool PatchReplays()
        {
            Replays.m_Path = Path.Combine(GamePersistentPath.GetPersistentDataDirectory(), Replays.REPLAYS_DIRECTORY);
            Utils.TryToCreateDirectory(Replays.m_Path);
            Replays.m_Path = Path.Combine(Replays.m_Path, "modded");
            Utils.TryToCreateDirectory(Replays.m_Path);
            Replays.m_PathForPublicDisplay = Path.Combine(GamePersistentPath.GetCensoredPersistentDataDirectory(), Replays.REPLAYS_DIRECTORY);
            return !ptfInstance.modCheated;
        }


        [HarmonyPatch(typeof(Panel_ShareReplay), "CreateGalleryUploadBody")]
        [HarmonyPostfix]
        private static void PatchGalleryUploadBody(ref GalleryUploadBody __result)
        {
            //ptfInstance.ptfLogger.LogMessage($"Uploading video to gallery, modCheated = {ptfInstance.modCheated}");
            if (ptfInstance.modCheated)
            {
                __result.m_MaxStress = 42069;
                __result.m_BudgetUsed = int.MaxValue;
            }
        }

        public static bool isSavingAnyModData() {
            return noncheatMods.Where(x => x.isEnabled && x.shouldSaveData).Count() + cheatMods.Where(x => x.isEnabled).Count() > 0;
        }

        [HarmonyPatch(typeof(SandboxLayoutData), "SerializePreBridgeBinary")]
        [HarmonyPrefix]
        static void patchSerializerOne(SandboxLayoutData __instance, List<byte> bytes)
        {
            ptfInstance.ptfLogger.LogMessage($"Layout pre version: {__instance.m_Version}");
            if (GameStateManager.GetState() != GameState.BUILD && GameStateManager.GetState() != GameState.SANDBOX) return;
            if (!isSavingAnyModData()) return;
            __instance.m_Version *= -1;
            //PopUpMessage.Display("You have cheat mods enabled, do you want to store them?\n(This will make the layout incompatible with vanilla PB2)", yes, no);
            ptfInstance.ptfLogger.LogMessage($"Version after cheat question: {__instance.m_Version.ToString()}");
        }

        [HarmonyPatch(typeof(SandboxLayoutData), "SerializePostBridgeBinary")]
        [HarmonyPostfix]
        private static void patchSerializerTwo(SandboxLayoutData __instance, List<byte> bytes)
        {
            ptfInstance.ptfLogger.LogMessage($"Layout post version: {__instance.m_Version}");
            
            // add number of mods stored
            //bytes.AddRange(ByteSerializer.SerializeInt(noncheatMods.Where(x => x.shouldSaveData).Count() + cheatMods.Where(x => x.shouldSaveData).Count()));

            // add mod data for each mod
            
            // make sure to be backwards compatible!
            List<string> modData = cheatMods.Where(x => x.isEnabled).Select(x => $"{x.Info.Metadata.Name}\u058D{x.Info.Metadata.Version}\u058D{x.getSettings()}").ToList();
            modData.AddRange(noncheatMods.Where(x => x.shouldSaveData && x.isEnabled).Select(x => $"{x.Info.Metadata.Name}\u058D{x.Info.Metadata.Version}\u058D{x.getSettings()}").ToList());
            string[] mods = modData.ToArray();
            
            if (__instance.m_Version >= 0) return;
            bytes.AddRange(ByteSerializer.SerializeStrings(mods));
            
            // add an int indicating the number of mods that will save binary data
            int modsSavingBinary = noncheatMods.Where(x => x.shouldSaveData).Count() + cheatMods.Where(x => x.shouldSaveData).Count();
            bytes.AddRange(ByteSerializer.SerializeInt(modsSavingBinary));
            
            foreach (var mod in noncheatMods){
                if (mod.isEnabled && mod.shouldSaveData){
                    bytes.AddRange(ByteSerializer.SerializeString(
                        $"{mod.Info.Metadata.Name}\u058D{mod.Info.Metadata.Version}"
                    ));
                    bytes.AddRange(ByteSerializer.SerializeByteArray(
                        mod.saveData()
                    ));
                }
            }
            foreach (var mod in cheatMods){
                if (mod.isEnabled && mod.shouldSaveData){
                    bytes.AddRange(ByteSerializer.SerializeString(
                        $"{mod.Info.Metadata.Name}\u058D{mod.Info.Metadata.Version}"
                    ));
                    bytes.AddRange(ByteSerializer.SerializeByteArray(
                        mod.saveData()
                    ));
                }
            }
            ptfInstance.ptfLogger.LogMessage($"Serialized {mods.Length.ToString()} Mod Names");
        }

        [HarmonyPatch(typeof(SandboxLayoutData), "DeserializeBinary")]
        [HarmonyPrefix]
        public static void patchDeserializerPrefix(SandboxLayoutData __instance, byte[] bytes, ref int offset, ref bool __state)
        {
            __state = false;
            var startOffset = offset;
            __instance.m_Version = ByteSerializer.DeserializeInt(bytes, ref offset);
            offset = startOffset;
            //ptfInstance.ptfLogger.LogMessage($"Layout version pre-modcheck: {__instance.m_Version}");
            if (__instance.m_Version > 0) return;
            __instance.m_Version *= -1;
            __state = true;
            byte[] new_ver = ByteSerializer.SerializeInt(__instance.m_Version);
            for (int i = 0; i < new_ver.Length; i++)
            {
                bytes[i] = new_ver[i];
            }
            //ptfInstance.ptfLogger.LogMessage($"Layout version post-modcheck: {__instance.m_Version}");
        }

        [HarmonyPatch(typeof(SandboxLayoutData), "DeserializeBinary")]
        [HarmonyPostfix]
        public static void patchDeserializerPostfix(SandboxLayoutData __instance, byte[] bytes, ref int offset, bool __state)
        {
            //ptfInstance.ptfLogger.LogMessage($"Layout version pre-load: {__instance.m_Version}");
            if (!__state) return;
            string[] strings = ByteSerializer.DeserializeStrings(bytes, ref offset);
            ptfInstance.ptfLogger.LogInfo($"Layout created with mod{(strings.Length > 1 ? "s" : "")}: ");
            foreach (string str in strings)
            {
                string[] partsOfMod = str.Split('\u058D');
                string name = partsOfMod.Length >= 1 ? partsOfMod[0] : null;
                string version = partsOfMod.Length >= 2 ? partsOfMod[1] : null;
                string settings = partsOfMod.Length >= 3 ? partsOfMod[2] : null;

                ptfInstance.ptfLogger.LogInfo($" -- {str.Replace("\u058D", " - v")}");

                var currMod = cheatMods.Where(p => p.Info.Metadata.Name == name).FirstOrDefault();
                if (currMod == null) currMod = noncheatMods.Where(p => p.Info.Metadata.Name == name).FirstOrDefault();

                ptfInstance.checkMods(0, name, version, settings, currMod);
            }
            if (offset == bytes.Length) return;
            int extraSaveDataCount = ByteSerializer.DeserializeInt(bytes, ref offset);
            if (extraSaveDataCount == 0) return;
            
            ptfInstance.Logger.LogInfo($"Layout created with custom data from mods: ");
            
            for (int i = 0; i < extraSaveDataCount; i++){
                string modIdentifier = ByteSerializer.DeserializeString(bytes, ref offset);
                byte[] customModSaveData = ByteSerializer.DeserializeByteArray(bytes, ref offset);
                
                string[] partsOfMod = modIdentifier.Split('\u058D');
                string name = partsOfMod.Length >= 1 ? partsOfMod[0] : null;
                string version = partsOfMod.Length >= 2 ? partsOfMod[1] : null;

                ptfInstance.Logger.LogInfo($" -- {name} - v{version}");

                var currMod = cheatMods.Where(p => p.Info.Metadata.Name == name).FirstOrDefault();
                if (currMod == null) currMod = noncheatMods.Where(p => p.Info.Metadata.Name == name).FirstOrDefault();

                if (currMod == null) return;
                if (currMod.Info.Metadata.Version.ToString() != version) return;

                currMod.loadData(customModSaveData);
            }
        }
        

        void checkMods(int step, string name, string version, string settings, PolyTechMod currMod)
        {
            if (currMod == null || (step <= 0 && currMod.Info.Metadata.Name != name)) missingMod(name, version, settings, currMod);
            else if (step <= 1 && currMod.Info.Metadata.Version.ToString() != version) wrongVersion(name, version, settings, currMod);
            else if (step <= 2 && !currMod.isEnabled) notEnabled(name, version, settings, currMod);
            else if (step <= 3 && currMod.getSettings() != settings) wrongSettings(name, version, settings, currMod);
        }

        void missingMod(string name, string version, string settings, PolyTechMod currMod)
        {
            ptfInstance.ptfLogger.LogWarning("Mod in layout not present.");
            PopUpMessage.Display(
                $"Mod ({name}) in layout not present.",
                () => {}
            );
        }

        void wrongVersion(string name, string version, string settings, PolyTechMod currMod)
        {
            ptfInstance.ptfLogger.LogWarning("Mod in layout present, but not the correct version.");
            PopUpMessage.Display(
                $"Mod ({name}) in layout present, but not the correct version. (Made with {version}, Currently has {cheatMods.Where(p => p.Info.Metadata.Name == name).First().Info.Metadata.Version.ToString()})",
                () => checkMods(2, name, version, settings, currMod)
            );
        }

        void notEnabled(string name, string version, string settings, PolyTechMod currMod)
        {
            ptfInstance.ptfLogger.LogWarning("Mod in layout present but not enabled.");
            PopUpTwoChoices.Display(
                $"Mod ({name}) in layout present but not enabled.",
                "Enable Mod",
                "Ignore Warning", () =>
                {
                    currMod.enableMod();
                    checkMods(3, name, version, settings, currMod);
                },
                () =>
                {
                    ptfInstance.ptfLogger.LogWarning("Ignored the mod being disabled");
                    checkMods(3, name, version, settings, currMod);
                }
            );
        }

        void wrongSettings(string name, string version, string settings, PolyTechMod currMod)
        {
            ptfInstance.ptfLogger.LogWarning("Mod in layout but settings are not correct.");
            PopUpTwoChoices.Display(
                $"Mod ({name}) but settings are not correct.",
                "Fix Settings Automatically",
                "Ignore Warning",
                () =>
                {
                    currMod.setSettings(settings);
                },
                () =>
                {
                    ptfInstance.ptfLogger.LogWarning("Ignored the mod being disabled");
                }
            );
        }


        [HarmonyPatch(typeof(Version), "VERSION", MethodType.Getter)]
        [HarmonyPostfix]
        private static void PatchVersion(ref string __result)
        {
            __result = $"{__result} - PTF v{PolyTechMain.PluginVersion}";
        }


        [HarmonyPatch(typeof(Main), "Awake")]
        [HarmonyPostfix]
        private static void PatchMainAwake()
        {
            string cosmetics = "";
            string cheats = "";
            
            // load credit values
            TMPro.TextMeshProUGUI titleCredits = GameUI.m_Instance.m_Settings.m_CreditsPanel.transform
            .Find("Mask/Credits/Titles")
            .GetComponent<TMPro.TextMeshProUGUI>(); 
            
            TMPro.TextMeshProUGUI nameCredits = GameUI.m_Instance.m_Settings.m_CreditsPanel.transform
            .Find("Mask/Credits/Names")
            .GetComponent<TMPro.TextMeshProUGUI>();

            // aligning things properly
            titleCredits.text = titleCredits.text + "\n\n\n\n\n";
            nameCredits.text = nameCredits.text.TrimEnd('\n') + "\nArglin Kampling\n\n";

            foreach (PolyTechMod mod in PolyTechMain.noncheatMods)
            {
                cosmetics += $"\n{mod.Info.Metadata.Name} - v{mod.Info.Metadata.Version}";
                if (mod.authors != null){
                    titleCredits.text += $"{mod.Info.Metadata.Name}\n";
                    nameCredits.text += String.Join("\n", mod.authors) + "\n\n";
                    titleCredits.text += new string('\n', mod.authors.Length);
                }
            }
            foreach (PolyTechMod mod in PolyTechMain.cheatMods)
            {
                cheats += $"\n{mod.Info.Metadata.Name} - v{mod.Info.Metadata.Version}";
                if (mod.authors != null){
                    titleCredits.text += $"{mod.Info.Metadata.Name}\n";
                    nameCredits.text += String.Join("\n", mod.authors) + "\n\n";
                    titleCredits.text += new string('\n', mod.authors.Length);
                }
            }
            titleCredits.text += "\n\n";
            nameCredits.text += "\n\n";

            ptfInstance.ptfLogger.LogMessage($"Game Started with the following Cosmetic mods: {cosmetics}");
            ptfInstance.ptfLogger.LogMessage($"Game Started with the following Cheat mods: {cheats}");
        }

        [HarmonyPatch]
        static class uploadSteamScorePatch
        {
            static bool Prepare()
            {
                return TargetMethod() != null;
            }

            static MethodInfo TargetMethod()
            {
                var steamStatsType = typeof(GameStateManager).Assembly.GetType("SteamStatsAndAchievements");
                return AccessTools.Method(steamStatsType, "UploadLeaderboardScore");
            }

            static bool Prefix(int score, bool didBreak) {
                return score >= leaderboardProtMin.Value;
            }
        }

        [HarmonyPatch(typeof(LeaderBoards), "UploadScoreAsync")]
        [HarmonyPrefix]
        public static bool uploadScorePatch(
            LeaderboardsUploadBody body, 
            string levelID, 
            bool didBreak, 
            LeaderboardUploadScore.OnUploadScoreDelegate callback,
            Queue<LeaderboardUploadScore> ___m_ScoresToUpload
        )
        {
            int score = body.m_Value;
            int minScore = leaderboardProtMin.Value;
            bool allowedBudget = score >= minScore;

            if(leaderboardBlock.Value)
            {
                if(allowedBudget) PopUpWarning.Display($"Your score would be {score}, however you have blocked all scores from being uploaded in the PTF settings.");
                else PopUpWarning.Display($"Your score ({score}) was below the minimum set in the PTF settings ({minScore}).");
                GameUI.m_Instance.m_LevelComplete.m_LeaderboardPanel.ForceRefresh();
                return false;
            }

            if(!allowedBudget)
            {
                PopUpWarning.Display($"Your score {score} was below the minimum budget {minScore} and as such will not be submitted.");
                GameUI.m_Instance.m_LevelComplete.m_LeaderboardPanel.ForceRefresh();
                return false;
            }
            //PopUpMessage.Display($"Your score {score} was above or equal to the minimum budget {leaderboardProtMin.Value}.", () => {});
            
            if (leaderboardCheck.Value)
            {
                PopUpMessage.Display($"Would you like to upload your score of {score} to the leaderboard?",
                () => {
                    // On Yes
                    LeaderboardUploadScore item = new LeaderboardUploadScore(body, didBreak, levelID, callback);
	                ___m_ScoresToUpload.Enqueue(item);
                },
                () => {
                    // On No
                    GameUI.m_Instance.m_LevelComplete.m_LeaderboardPanel.ForceRefresh();
                });
                return false;
            }
            return true;
        }

        [HarmonyPatch]
        static class areModsInstalledPatch
        {
            static bool Prepare()
            {
                return TargetMethod() != null;
            }

            static MethodInfo TargetMethod()
            {
                return AccessTools.Method(typeof(GameManager), "AreModsInstalled");
            }

            static void Postfix(bool __result) {
                __result = true;
            }
        }

        [HarmonyPatch(typeof(Panel_WorkshopSubmit), "Submit")]
        static class submitPatch {
            static bool state = false;
            static bool Prefix() {
                if (state) {
                    state = false;
                    return true;
                }

                if (isSavingAnyModData()) {
                    PopUpMessage.Display(
                        "This level contains modded data, are you sure you would like to upload it to the workshop?",
                        () => {
                            state = true;
                            GameUI.m_Instance.m_WorkshopSubmit.Submit();
                        },
                        () => {}
                    );
                }
                else {
                    return true;
                }

                return false;
            }
        }

    }


}
