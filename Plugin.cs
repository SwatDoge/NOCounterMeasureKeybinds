using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NOCounterMeasureKeybinds
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> deployCMInstant { get; set; }
        public static List<string> counterMeasureDisplayNames;
        public static List<string> usedCounterMeasuresDisplayNames = new List<string>();
        public static List<string> lastUsedCounterMeasureDisplayNames = new List<string>();
        public static int counterMeasureSlots;
        public static bool RewiredReady = false;
        public static new ManualLogSource Logger;
        public static bool isActiveDecoyModLoaded = false;

        private void Awake()
        {
            Logger = base.Logger;
            deployCMInstant = Config.Bind("Counter Measures Selection", "Use countermeasure when button is held", true, new ConfigDescription("Wether countermeasures should be used when holding down their respective key.", null, new ConfigurationManagerAttributes { Order = 3 }));

            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
        }

        public static void LoadCounterMeasureStats()
        {
            Countermeasure[] allCounterMeasures = Resources.FindObjectsOfTypeAll<Countermeasure>();

            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                Plugin.Logger.LogInfo(info.Metadata.GUID);
            }

            isActiveDecoyModLoaded = BepInEx.Bootstrap.Chainloader.PluginInfos
                .Values
                .Any(mod => mod.Metadata.GUID == "com.nuclearoption.activedecoy");

            counterMeasureDisplayNames = allCounterMeasures
                .Where(counterMeasure => counterMeasure is not null && counterMeasure.enabled)
                    .Select(counterMeasure => counterMeasure.displayName)
                    .Distinct()
                    .ToList();

            counterMeasureSlots = allCounterMeasures
                .Where(counterMeasure => counterMeasure is not null)
                .GroupBy(counterMeasure => counterMeasure.name)
                .OrderByDescending(counterMeasure => counterMeasure.Count())
                .First()
                .Count();

            if (isActiveDecoyModLoaded)
            {
                counterMeasureSlots++;
                counterMeasureDisplayNames.Add("Active Decoy");
            }
        }

        private void Update()
        {
            GameManager.GetLocalAircraft(out Aircraft aircraft);
            Player player = ReInput.players?.GetPlayer(0);

            if (!RewiredReady)
            {
                return;
            }

            if (player is null)
            {
                return;
            }

            usedCounterMeasuresDisplayNames.Clear();

            usedCounterMeasuresDisplayNames = usedCounterMeasuresDisplayNames.Concat(
                counterMeasureDisplayNames
                .Where(counterMeasureDisplayName => player.GetButton(counterMeasureDisplayName))
            )
            .Concat(
                Enumerable.Range(0, counterMeasureSlots)
                .Where((counterMeasureSlot, index) => player.GetButton("counterMeasureSlot" + index))
                .Select((counterMeasureSlot, index) => aircraft.countermeasureManager.countermeasureStations[index].displayName)
            )
            .Distinct()
            .ToList();

            foreach (string counterMeasureDisplayName in usedCounterMeasuresDisplayNames)
            {
                int currentId = aircraft.countermeasureManager.activeIndex;
                bool _continue = false;

                if (!aircraft.IsServer)
                {
                    if (lastUsedCounterMeasureDisplayNames.Count >= usedCounterMeasuresDisplayNames.Count)
                    {
                        lastUsedCounterMeasureDisplayNames.Clear();
                    }

                    if (lastUsedCounterMeasureDisplayNames.Contains(counterMeasureDisplayName))
                    {
                        continue;
                    }

                    lastUsedCounterMeasureDisplayNames.Add(counterMeasureDisplayName);
                }

                while (aircraft.countermeasureManager.GetActiveCountermeasure().displayName != counterMeasureDisplayName)
                {
                    aircraft.countermeasureManager.NextCountermeasure();

                    if (currentId == aircraft.countermeasureManager.activeIndex)
                    {
                        _continue = true;
                        break;
                    }
                }

                if (_continue)
                {
                    continue;
                }

                aircraft.countermeasureManager.DeployCountermeasure(aircraft);
                aircraft.Countermeasures(true, aircraft.countermeasureManager.activeIndex);

                if (!aircraft.IsServer)
                {
                    break;
                }
            }
        }
    }
}
