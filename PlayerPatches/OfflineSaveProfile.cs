﻿using Comfort.Common;
using EFT;
using MTGA.Core;
using System;
using System.Linq;
using System.Reflection;

namespace MTGA.Core
{
    public class OfflineSaveProfile : ModulePatch
    {
        static OfflineSaveProfile()
        {
            // compile-time check
            //_ = nameof(ClientMetrics.Metrics);

            _ = nameof(TarkovApplication);
            _ = nameof(EFT.RaidSettings);

            //_defaultJsonConverters = Traverse.Create(converterClass).Field<JsonConverter[]>("Converters").Value;
        }

        protected override MethodBase GetTargetMethod()
        {
            var methods = PatchConstants.GetAllMethodsForType(typeof(TarkovApplication));
            foreach (var method in methods)
            {
                var paramameters = method.GetParameters();

                if (method.Name.StartsWith("method")
                    && paramameters.Length == 5
                    && paramameters[0].Name == "profileId"
                    && paramameters[0].ParameterType == typeof(string)
                    && paramameters[1].Name == "savageProfile"
                    && paramameters[1].ParameterType == typeof(EFT.Profile)
                    && paramameters[2].Name == "location"
                    && paramameters[2].ParameterType == typeof(LocationSettingsClass.SelectedLocation)
                    && paramameters[3].Name == "result"
                    && paramameters[3].ParameterType == typeof(Result<ExitStatus, TimeSpan, MetricsClass>)
                    && paramameters[4].Name == "timeHasComeScreenController"
                    && paramameters[4].ParameterType == typeof(EFT.UI.Matchmaker.MatchmakerTimeHasCome.TimeHasComeScreenController)
                    )
                {
                    Logger.Log(BepInEx.Logging.LogLevel.Info, method.Name);
                    return method;
                }
            }
            Logger.Log(BepInEx.Logging.LogLevel.Error, "OfflineSaveProfile::Method is not found!");

            return null;
        }

        [PatchPrefix]
        public static bool PatchPrefix(ref EFT.RaidSettings ____raidSettings, ref Result<EFT.ExitStatus, TimeSpan, MetricsClass> result)
        {
            Logger.LogInfo("OfflineSaveProfile::PatchPrefix");
            //Logger.LogInfo($"URL: {MTGA.Core.PatchConstants.GetBackendUrl()}");
            ____raidSettings.RaidMode = ERaidMode.Online;
            //Logger.LogInfo($"RaidMod = {____raidSettings.RaidMode}");


            var session = ClientAccesor.GetClientApp().GetClientBackEndSession();
            //Logger.LogInfo($"SESSION = {session}");

            var profile = (____raidSettings.IsScav && ____raidSettings.Side == ESideType.Savage) ? session.Profile : session.ProfileOfPet;
            //Logger.LogInfo($"profile = {profile}");
            var exitStatus = result.Value0.ToString().ToLower();
            //Logger.LogInfo($"exitStatus = {exitStatus}");
            var currentHealth = PlayerPatches.Health.HealthListener.Instance.CurrentHealth;
            //Logger.LogInfo($"currentHealth = {currentHealth}");

            SaveProfileRequest request = new SaveProfileRequest()
            {
                exit = exitStatus,
                profile = profile,
                health = currentHealth,
                isPlayerScav = ____raidSettings.IsScav
            };
            //Logger.LogInfo($"SaveProfileRequest fulfilled");

            var convertedJson = request.MTGAToJson();
            Logger.LogInfo($"convertedJson fulfilled : {convertedJson}");
            var sessionID = PatchConstants.GetPHPSESSID();
            var backendURL = PatchConstants.GetBackendUrl();

            new MTGA.Core.Request(sessionID, backendURL).PostJson("/client/raid/profile/save", convertedJson, true);
            Logger.LogInfo($"PostJson to /client/raid/profile/save fulfilled");

            return true;
        }

        public class SaveProfileRequest
        {
            public string exit { get; set; }
            public EFT.Profile profile { get; set; }
            public bool isPlayerScav { get; set; }
            //public PlayerHealth health { get; set; }
            public object health { get; set; }
        }
    }
}
