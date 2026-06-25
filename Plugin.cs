using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NOCounterMeasureKeybinds
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<bool> deployCMInstant { get; set; }
        public static List<string> counterMeasureDisplayNames;
        public static int counterMeasureSlots;
        public static bool RewiredReady = false;
        public static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            deployCMInstant = Config.Bind("Counter Measures Selection", "Use countermeasure when button is held", true, new ConfigDescription("Wether countermeasures should be used when holding down their respective key.", null, new ConfigurationManagerAttributes { Order = 3 }));

            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();
        }

        public static void LoadCounterMeasureStats()
        {
            Countermeasure[] allCounterMeasures = Resources.FindObjectsOfTypeAll<Countermeasure>();

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

            for (int index = 0; index < counterMeasureSlots; index++)
            {
                if (player.GetButton("counterMeasureSlot" + index))
                {
                    int currentId = aircraft.countermeasureManager.activeIndex;

                    while (aircraft.countermeasureManager.activeIndex != index)
                    {
                        aircraft.countermeasureManager.NextCountermeasure();

                        if (currentId == aircraft.countermeasureManager.activeIndex)
                        {
                            return;
                        }
                    }

                    if (deployCMInstant.Value)
                    {
                        aircraft.countermeasureManager.DeployCountermeasure(aircraft);
                    }
                }
            }

            foreach (string counterMeasureDisplayName in counterMeasureDisplayNames)
            {
                if (player.GetButton(counterMeasureDisplayName))
                {
                    int currentId = aircraft.countermeasureManager.activeIndex;

                    while (aircraft.countermeasureManager.GetActiveCountermeasure().displayName != counterMeasureDisplayName)
                    {
                        aircraft.countermeasureManager.NextCountermeasure();

                        if (currentId == aircraft.countermeasureManager.activeIndex)
                        {
                            return;
                        }
                    }

                    if (deployCMInstant.Value)
                    {
                        aircraft.countermeasureManager.DeployCountermeasure(aircraft);
                    }
                }
            }
        }
    }
}
