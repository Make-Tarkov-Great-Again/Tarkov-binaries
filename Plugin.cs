﻿using Aki.Custom.Airdrops.Patches;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.AssetsManager;
using MTGA.Core.AI;
using MTGA.Core.Bundles;
using MTGA.Core.Hideout;
using MTGA.Core.Menus;
using MTGA.Core.Misc;
using MTGA.Core.PlayerPatches;
using MTGA.Core.PlayerPatches.Health;
using MTGA.Core.Raid;
using MTGA.Core.SP;
using MTGA.Core.SP.ScavMode;
using MTGA.SP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTGA.Core
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        // Headlamp Fix by SamSwat
        private bool EnabledHeadLamps { get; set; }
        private static GameObject _flashlight;
        private static GameObject[] _modes;
        private static int _currentMode = 1;
        internal static ConfigEntry<KeyboardShortcut> HeadlightToggleKey;
        internal static ConfigEntry<KeyboardShortcut> HeadlightModeKey;

        // AI Limit by Props
        private bool EnabledAILimit { get; set; }
        public static ConfigEntry<int> BotLimit;
        public static ConfigEntry<float> BotDistance;
        public static ConfigEntry<float> TimeAfterSpawn;
        public static Dictionary<int, Player> playerMapping = new();
        public static Dictionary<int, BotPlayer> botMapping = new();
        public static List<BotPlayer> botList = new();
        public static Player player { get; set; }
        public static BotPlayer bot { get; set; }

        // Bush ESP by Props
        public static bool BossesStillSee { get; set; }
        public static bool FollowersStillSee { get; set; }
        public static bool PMCsStillSee { get; set; }
        public static bool ScavsStillSee { get; set; }


        void Awake()
        {
            PatchConstants.GetBackendUrl();


            // - TURN OFF FileChecker and BattlEye -----
            new ConsistencySinglePatch().Enable();
            new ConsistencyMultiPatch().Enable();
            new BattlEyePatch().Enable();
            new SslCertificatePatch().Enable();
            new UnityWebRequestPatch().Enable();
            new WebSocketPatch().Enable();

            // - Loading Bundles from Server. Working Aki version with some tweaks by me -----
            var enableBundles = Config.Bind("Bundles", "Enable", true);
            if (enableBundles != null && enableBundles.Value == true)
            {
                BundleSetup.Init();
                BundleManager.GetBundles(); // Crash happens here
                new EasyAssetsPatch().Enable();
                new EasyBundlePatch().Enable();
            }

            // --------- Container Id Debug ------------
            var enableLootableContainerDebug = Config.Bind("Debug", "Lootable Container Debug", false, "Description: Print Lootable Container information").Value;
            if (enableLootableContainerDebug)
                new LootableContainerInteractPatch().Enable();

            // --------- PMC Dogtags -------------------
            new UpdateDogtagPatch().Enable();

            // --------- On Dead -----------------------
            new OnDeadPatch(Config).Enable();

            // --------- Player Init -------------------
            new PlayerInitPatch().Enable();

            // --------- SCAV MODE ---------------------
            new DisableScavModePatch().Enable();

            // --------- Airdrop (THANKS TO AKI) -----------------------
            new AirdropPatch().Enable();
            new AirdropFlarePatch().Enable();

            // --------- AI -----------------------
            var enabledMTGAAISystem = Config.Bind("AI", "AI System", true, "Description: Enable MTGA AI???????").Value;
            if (enabledMTGAAISystem)
            {
                //new IsEnemyPatch().Enable();
                new IsPlayerEnemyPatch().Enable();
                new IsPlayerEnemyByRolePatch().Enable();
                new BotBrainActivatePatch().Enable();
                new BotSelfEnemyPatch().Enable();
            }

            EnabledAILimit = Config.Bind("EXPERIEMENTAL AI Limit by Props", "Enable", false, "Description: Disable AI (temporarily) based on distance to the player and user defined bot limit within that distance. Only the closest bots (distance wise) are enabled based on a max value set").Value;
            BotDistance = Config.Bind("EXPERIEMENTAL AI Limit by Props", "Bot Distance", 200f, "Set Max Distance to activate bots");
            BotLimit = Config.Bind("EXPERIEMENTAL AI Limit by Props", "Bot Limit (At Distance)", 10, "Based on your distance selected, limits up to this many # of bots moving at one time");
            TimeAfterSpawn = Config.Bind("EXPERIEMENTAL AI Limit by Props", "Time After Spawn", 10f, "Time (sec) to wait before disabling");
            if (EnabledAILimit)
            {
                PatchConstants.Logger.LogInfo("Enabling AI Limit");
                PatchConstants.Logger.LogInfo("AI Limit Enabled");
            }

            // --------- Matchmaker ----------------
            new AutoSetOfflineMatch().Enable();
            //new BringBackInsuranceScreen().Enable();
            new DisableReadyButtonOnFirstScreen().Enable();

            // -------------------------------------
            // Progression
            new OfflineSaveProfile().Enable();
            new ExperienceGainFix().Enable();
            new OfflineDisplayProgressPatch().Enable();

            // -------------------------------------
            // Quests
            new ItemDroppedAtPlace_Beacon().Enable();

            // -------------------------------------
            // Raid
            new LoadBotDifficultyFromServer().Enable();

            var enabledCultistsDuringDay = Config.Bind("EXPERIEMENTAL Cultists During Day by Lua", "Enable", true, "Description: Cultists Spawning During Day").Value;
            if (enabledCultistsDuringDay)
            {
                PatchConstants.Logger.LogInfo("Enabling Cultists During Day");
                new CultistsSpawnDuringDay().Enable();
                PatchConstants.Logger.LogInfo("Cultists During Day Enabled");
            }

            var EnabledNoBushESP = Config.Bind("EXPERIEMENTAL No Bush ESP by dvize", "Enable", true, "Description: Tired of AI Looking at your bush and destroying you through it? Now they no longer can.").Value;
            BossesStillSee = Config.Bind("EXPERIEMENTAL No Bush ESP by dvize", "BossesStillSee", false, "Allow bosses to see through bushes").Value;
            FollowersStillSee = Config.Bind("EXPERIEMENTAL No Bush ESP by dvize", "FollowersStillSee", false, "Allow boss followers to see through bushes").Value;
            PMCsStillSee = Config.Bind("EXPERIEMENTAL No Bush ESP by dvize", "PMCsStillSee", false, "Allow PMCs to see through bushes").Value;
            ScavsStillSee = Config.Bind("EXPERIEMENTAL No Bush ESP by dvize", "ScavssStillSee", false, "Allow Scavs to see through bushes").Value;
            if (EnabledNoBushESP)
            {
                PatchConstants.Logger.LogInfo("Enabling No Bush ESP");
                new NoBushESP().Enable();
                PatchConstants.Logger.LogInfo("Enabled No Bush ESP");
            }
            //new SpawnPointPatch().Enable();
            //new BossSpawnChancePatch().Enable();

            // --------------------------------------
            // Health stuff
            new ReplaceInPlayer().Enable();

            new ChangeHealthPatch().Enable();
            new ChangeEnergyPatch().Enable();
            new ChangeHydrationPatch().Enable();


            var EnabledAdrenaline = Config.Bind("EXPERIEMENTAL Adrenaline by Kobrakon", "Enable", false, "Description: Adrenaline effect when Damaged").Value;
            if (EnabledAdrenaline)
            {
                PatchConstants.Logger.LogInfo("Enabling Adrenaline");
                new Adrenaline().Enable();
                PatchConstants.Logger.LogInfo("Adrenaline Enabled");
            };

            EnabledHeadLamps = Config.Bind("EXPERIEMENTAL Headlamps by SamSwat", "Enable", true, "Description: Fix head lamps to toggle on with Y, and Shift + Y to toggle modes").Value;
            HeadlightToggleKey = Config.Bind<KeyboardShortcut>("EXPERIEMENTAL Headlamps by SamSwat", "Helmet Light Toggle", new KeyboardShortcut(KeyCode.Y, Array.Empty<KeyCode>()), "Key for helmet light toggle");
            HeadlightModeKey = Config.Bind<KeyboardShortcut>("EXPERIEMENTAL Headlamps by SamSwat", "Helmet Light Mode", new KeyboardShortcut(KeyCode.Y, KeyCode.LeftShift), "Key for helemt light mode change");
            if (EnabledHeadLamps)
            {
                PatchConstants.Logger.LogInfo("Enabling Headlamps");
                PatchConstants.Logger.LogInfo("Headlamps Enabled");
            };


            // ----------------------------------------------------------------
            // MongoID. This forces bad JET ids to become what BSG Code expects
            //if (MongoIDPatch.MongoIDExists)
            //{
            //    new MongoIDPatch().Enable();
            //}

            //new HideoutItemViewFactoryShowPatch().Enable();

            new LootContainerInitPatch().Enable();
            new CollectLootPointsDataPatch().Enable();

            new SetupItemActionsSettingsPatch().Enable();

            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;

            SetupMoreGraphicsMenuOptions();

        }

        void SceneManager_sceneUnloaded(Scene arg0)
        {

        }

        GameWorld gameWorld = null;
        public void GetGameWorld()
        {
            //Logger.LogInfo($"gameWorld is {gameWorld}");
            gameWorld ??= gameWorld = Singleton<GameWorld>.Instance;
            //Logger.LogInfo($"gameWorld is {gameWorld}");
        }

        public void GetPlayer()
        {
            GetGameWorld();
            //Logger.LogInfo($"player is {player}");
            player = gameWorld.RegisteredPlayers.Find((Player p) => p.IsYourPlayer);
            //Logger.LogInfo($"player is {player}");
        }

        void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            GetPoolManager();
            GetBackendConfigurationInstance();
            GetGameWorld();
        }

        public void SetupMoreGraphicsMenuOptions()
        {
            Logger.LogInfo("Adjusting sliders for Overall Visibility and LOD Quality");
            var TypeOfGraphicsSettingsTab = typeof(EFT.UI.Settings.GraphicsSettingsTab);

            var readOnlyCollection_0 = TypeOfGraphicsSettingsTab.GetField(
                "readOnlyCollection_0",
                BindingFlags.Static |
                BindingFlags.NonPublic
                );

            var readOnlyCollection_3 = TypeOfGraphicsSettingsTab.GetField(
                "readOnlyCollection_3",
                BindingFlags.Static |
                BindingFlags.NonPublic
                );

            List<float> overallVisibility = new();
            for (int i = 0; i <= 11; i++)
            {
                overallVisibility.Add(400 + (i * 50));
            }

            for (int i = 0; i <= 4; i++)
            {
                overallVisibility.Add(1000 + (i * 500));
            }


            List<float> lodQuality = new();
            for (int i = 0; i <= 9; i++)
            {
                lodQuality.Add((float)(2 + (i * 0.25)));
            }

            var Collection_0 = Array.AsReadOnly<float>(overallVisibility.ToArray());
            var Collection_3 = Array.AsReadOnly<float>(lodQuality.ToArray());

            readOnlyCollection_0.SetValue(null, Collection_0);
            readOnlyCollection_3.SetValue(null, Collection_3);
            Logger.LogInfo("Adjusted sliders for Overall Visibility and LOD Quality");
        }
        void GetBackendConfigurationInstance()
        {
            if (
                PatchConstants.BackendStaticConfigurationType != null &&
                PatchConstants.BackendStaticConfigurationConfigInstance == null)
            {
                PatchConstants.BackendStaticConfigurationConfigInstance = PatchConstants.GetPropertyFromType(PatchConstants.BackendStaticConfigurationType, "Config").GetValue(null);
                //Logger.LogInfo($"BackendStaticConfigurationConfigInstance Type:{ PatchConstants.BackendStaticConfigurationConfigInstance.GetType().Name }");
            }

            if (PatchConstants.BackendStaticConfigurationConfigInstance != null
                && PatchConstants.CharacterControllerSettings.CharacterControllerInstance == null
                )
            {
                PatchConstants.CharacterControllerSettings.CharacterControllerInstance
                    = PatchConstants.GetFieldOrPropertyFromInstance<object>(PatchConstants.BackendStaticConfigurationConfigInstance, "CharacterController", false);
                Logger.LogInfo($"PatchConstants.CharacterControllerInstance Type:{PatchConstants.CharacterControllerSettings.CharacterControllerInstance.GetType().Name}");
            }

            if (PatchConstants.CharacterControllerSettings.CharacterControllerInstance != null
                && PatchConstants.CharacterControllerSettings.ClientPlayerMode == null
                )
            {
                PatchConstants.CharacterControllerSettings.ClientPlayerMode
                    = PatchConstants.GetFieldOrPropertyFromInstance<CharacterControllerSpawner.Mode>(PatchConstants.CharacterControllerSettings.CharacterControllerInstance, "ClientPlayerMode", false);

                PatchConstants.CharacterControllerSettings.ObservedPlayerMode
                    = PatchConstants.GetFieldOrPropertyFromInstance<CharacterControllerSpawner.Mode>(PatchConstants.CharacterControllerSettings.CharacterControllerInstance, "ObservedPlayerMode", false);

                PatchConstants.CharacterControllerSettings.BotPlayerMode
                    = PatchConstants.GetFieldOrPropertyFromInstance<CharacterControllerSpawner.Mode>(PatchConstants.CharacterControllerSettings.CharacterControllerInstance, "BotPlayerMode", false);
            }

        }


        void GetPoolManager()
        {
            if (PatchConstants.PoolManagerType == null)
            {
                PatchConstants.PoolManagerType = PatchConstants.EftTypes.Single(x => PatchConstants.GetAllMethodsForType(x).Any(x => x.Name == "LoadBundlesAndCreatePools"));
                //Logger.LogInfo($"Loading PoolManagerType:{ PatchConstants.PoolManagerType.FullName}");

                //Logger.LogInfo($"Getting PoolManager Instance");
                Type generic = typeof(Comfort.Common.Singleton<>);
                Type[] typeArgs = { PatchConstants.PoolManagerType };
                ConstructedBundleAndPoolManagerSingletonType = generic.MakeGenericType(typeArgs);
                //Logger.LogInfo(PatchConstants.PoolManagerType.FullName);
                //Logger.LogInfo(ConstructedBundleAndPoolManagerSingletonType.FullName);

                //new LoadBotTemplatesPatch().Enable();
                //new RemoveUsedBotProfile().Enable();
                //new CreateFriendlyAIPatch().Enable();
            }
        }

        Type ConstructedBundleAndPoolManagerSingletonType { get; set; }
        public static object BundleAndPoolManager { get; set; }

        public static Type poolsCategoryType { get; set; }
        public static Type AssemblyTypeType { get; set; }

        public static MethodInfo LoadBundlesAndCreatePoolsMethod { get; set; }

        public static async void LoadBundlesAndCreatePoolsAsync(ResourceKey[] resources)
        {
            try
            {
                if (BundleAndPoolManager == null)
                {
                    PatchConstants.Logger.LogInfo("LoadBundlesAndCreatePools: BundleAndPoolManager is missing");
                    return;
                }

                await Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(
                    PoolManager.PoolsCategory.Raid, PoolManager.AssemblyType.Local, resources, JobPriority.General, null, CancellationToken.None);

            }
            catch (Exception ex)
            {
                PatchConstants.Logger.LogInfo("LoadBundlesAndCreatePools -- ERROR ->>>");
                PatchConstants.Logger.LogInfo(ex.ToString());
            }
        }

        public static Task LoadBundlesAndCreatePools(ResourceKey[] resources)
        {
            try
            {
                if (BundleAndPoolManager == null)
                {
                    PatchConstants.Logger.LogInfo("LoadBundlesAndCreatePools: BundleAndPoolManager is missing");
                    return null;
                }

                var raidE = Enum.Parse(poolsCategoryType, "Raid");

                var localE = Enum.Parse(AssemblyTypeType, "Local");

                var GenProp = PatchConstants.GetPropertyFromType(PatchConstants.JobPriorityType, "General").GetValue(null, null);

                return PatchConstants.InvokeAsyncStaticByReflection(
                    LoadBundlesAndCreatePoolsMethod,
                    BundleAndPoolManager
                    , raidE
                    , localE
                    , resources
                    , GenProp
                    , (object o) => { PatchConstants.Logger.LogInfo("LoadBundlesAndCreatePools: Progressing!"); }
                    , default(CancellationToken)
                    );
            }
            catch (Exception ex)
            {
                PatchConstants.Logger.LogInfo("LoadBundlesAndCreatePools -- ERROR ->>>");
                PatchConstants.Logger.LogInfo(ex.ToString());
            }
            return null;
        }

        void Update()
        {
            if (EnabledHeadLamps)
            {
                HeadLamps();
            }
            if (EnabledAILimit)
            {
                AILimit();
            }
        }

        void AILimit()
        {
            if (!Singleton<GameWorld>.Instantiated)
            {
                return;
            }
            GetGameWorld();
            try
            {
                UpdateBots(gameWorld);
            }
            catch (Exception ex)
            {
                Logger.LogInfo(ex);
            }
        }

        void UpdateBots(GameWorld gameWorld)
        {
            int num = 0;
            for (int i = 0; i < gameWorld.RegisteredPlayers.Count; i++)
            {
                var players = gameWorld.RegisteredPlayers[i];
                //Logger.LogInfo($"players is {players}");
                if (!players.IsYourPlayer)
                {
                    if (!botMapping.ContainsKey(player.Id) && !playerMapping.ContainsKey(players.Id))
                    {
                        playerMapping.Add(players.Id, players);
                        BotPlayer value = new(players.Id);
                        botMapping.Add(players.Id, value);
                    }
                    bot = botMapping[players.Id];
                    bot.Distance = Vector3.Distance(players.Position, gameWorld.RegisteredPlayers[0].Position);
                    if (bot.EligibleNow && !botList.Contains(bot))
                    {
                        botList.Add(bot);
                    }
                    if (!bot.timer.Enabled && players.CameraPosition != null)
                    {
                        bot.timer.Enabled = true;
                        bot.timer.Start();
                    }
                }
            }
            if (botList.Count > 1)
            {
                for (int j = 1; j < botList.Count; j++)
                {
                    BotPlayer botPlayer = botList[j];
                    int num2 = j - 1;
                    while (num2 >= 0 && botList[num2].Distance > botPlayer.Distance)
                    {
                        botList[num2 + 1] = botList[num2];
                        num2--;
                    }
                    botList[num2 + 1] = botPlayer;
                }
            }
            for (int k = 0; k < botList.Count; k++)
            {
                if (num < BotLimit.Value && botList[k].Distance < BotDistance.Value)
                {
                    playerMapping[botList[k].Id].enabled = true;
                    num++;
                }
                else
                {
                    playerMapping[botList[k].Id].enabled = false;
                }
            }
        }

        public class BotPlayer
        {
            public int Id { get; set; }
            public float Distance { get; set; }
            public bool EligibleNow { get; set; }

            public BotPlayer(int newID)
            {
                this.Id = newID;
                this.EligibleNow = false;
                this.timer.Enabled = false;
                this.timer.AutoReset = false;
                this.timer.Elapsed += EligiblePool(this);
                playerMapping[this.Id].OnPlayerDeadOrUnspawn += delegate (Player deadArgs)
                {
                    botList.Remove(botMapping[deadArgs.Id]);
                    botMapping.Remove(deadArgs.Id);
                    playerMapping.Remove(deadArgs.Id);
                };
            }

            public System.Timers.Timer timer = new((double)(TimeAfterSpawn.Value * 1000f));
        }
        public static ElapsedEventHandler EligiblePool(BotPlayer botplayer)
        {
            if (!playerMapping.ContainsKey(botplayer.Id))
            {
                playerMapping.Remove(botplayer.Id);
                botMapping.Remove(botplayer.Id);
                botList.Remove(botplayer);
            }
            else
            {
                botplayer.timer.Stop();
                botplayer.EligibleNow = true;
            }
            return null;
        }

        void HeadLamps()
        {
            GetGameWorld();
            //Logger.LogInfo($"gameWorld is {gameWorld}");
            bool flag = gameWorld == null || gameWorld.RegisteredPlayers == null;
            if (!flag)
            {
                bool flag2 = _flashlight != null && _flashlight.GetComponent<WeaponModPoolObject>().IsInPool;
                if (flag2)
                {
                    _flashlight = null;
                    _currentMode = 1;
                }
                bool flag3 = HeadlightToggleKey.Value.IsUp() && PlayerHasFlashlight();
                if (flag3)
                {
                    ToggleLight();
                }
                bool flag4 = HeadlightModeKey.Value.IsUp() && PlayerHasFlashlight();
                if (flag4)
                {
                    ChangeMode();
                }
            }
        }

        void ToggleLight()
        {
            _modes[0].SetActive(!_modes[0].activeSelf);
            _modes[_currentMode].SetActive(!_modes[_currentMode].activeSelf);
        }

        void ChangeMode()
        {
            bool flag = !_modes[0].activeSelf;
            if (flag)
            {
                bool flag2 = _currentMode < _modes.Length - 1;
                if (flag2)
                {
                    _modes[_currentMode].SetActive(!_modes[_currentMode].activeSelf);
                    _currentMode++;
                    _modes[_currentMode].SetActive(!_modes[_currentMode].activeSelf);
                }
                else
                {
                    _modes[_currentMode].SetActive(!_modes[_currentMode].activeSelf);
                    _currentMode = 1;
                    _modes[_currentMode].SetActive(!_modes[_currentMode].activeSelf);
                }
            }
        }

        bool PlayerHasFlashlight()
        {
            bool flag = _flashlight == null;
            bool result;
            if (flag)
            {
                GetPlayer();
                //Logger.LogInfo($"player is {player}");
                TacticalComboVisualController componentInChildren = player.GetComponentInChildren<TacticalComboVisualController>();
                _flashlight = componentInChildren?.gameObject;
                bool flag2 = _flashlight == null;
                if (flag2)
                {
                    result = false;
                }
                else
                {
                    _modes = (from x in Array.ConvertAll(_flashlight.GetComponentsInChildren<Transform>(true), (Transform y) => y.gameObject)
                              where x.name.Contains("mode_")
                              select x).ToArray();
                    result = true;
                }
            }
            else
            {
                result = true;
            }
            return result;
        }

        void FixedUpdate()
        {
            if (PatchConstants.PoolManagerType != null && ConstructedBundleAndPoolManagerSingletonType != null && BundleAndPoolManager == null)
            {
                BundleAndPoolManager = PatchConstants.GetPropertyFromType(ConstructedBundleAndPoolManagerSingletonType, "Instance").GetValue(null, null); //Activator.CreateInstance(PatchConstants.PoolManagerType);
                if (BundleAndPoolManager != null)
                {
                    poolsCategoryType = BundleAndPoolManager.GetType().GetNestedType("PoolsCategory");
                    if (poolsCategoryType != null)
                    {
                        Logger.LogInfo(poolsCategoryType.FullName);
                    }
                    AssemblyTypeType = BundleAndPoolManager.GetType().GetNestedType("AssemblyType");
                    if (AssemblyTypeType != null)
                    {
                        Logger.LogInfo(AssemblyTypeType.FullName);
                    }
                    LoadBundlesAndCreatePoolsMethod = PatchConstants.GetMethodForType(BundleAndPoolManager.GetType(), "LoadBundlesAndCreatePools");
                    if (LoadBundlesAndCreatePoolsMethod != null)
                    {
                        Logger.LogInfo(LoadBundlesAndCreatePoolsMethod.Name);
                    }
                }
            }
        }
    }
}
