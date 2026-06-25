using HarmonyLib;
using NOCounterMeasureKeybinds;
using Rewired;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace NOCounterMeasureKeybinds.Patches;

// derived from muj who derived it from https://github.com/9138noms/TargetCamControl/blob/main/Plugin.cs#L140

[HarmonyPatch(typeof(InputManager_Base))]
static class HInputManager_Base
{
    [HarmonyPrefix]
    [HarmonyWrapSafe]
    [HarmonyPatch(typeof(InputManager_Base), "Awake")]
    static void InputManager_BasePrefix(InputManager_Base __instance)
    {
        int nextId = 850;
        InputCategory targetCat = null;
        Rewired.Data.UserData userData = __instance.userData;

        if (userData is null)
        {
            return;
        }

        IList<InputCategory> categories = GetField<IList<InputCategory>>(userData, "actionCategories");
        IList<InputAction> actions = GetField<IList<InputAction>>(userData, "actions");

        if (categories is null || actions is null)
        {
            return;
        }

        foreach (InputCategory category in categories)
        {
            string name = category.name;
            bool matchesTargetCategory = string.Equals(name, "flight", StringComparison.OrdinalIgnoreCase);

            if (matchesTargetCategory)
            {
                targetCat = category;
                break;
            }
        }

        if (targetCat is null)
        {
            Plugin.Logger.LogWarning("[NOCounterMeasureKeybinds] Couldnt find control category, it wont be avaiable in the settings menu.");
            return;
        }

        foreach (InputAction _action in actions)
        {
            int id = _action.id;

            if (id >= nextId)
            {
                nextId = id + 1;
            }
        }

        var catMap = GetField<object>(userData, "actionCategoryMap");

        if (catMap is not null)
        {
            MethodInfo addActionMethod = AccessTools.Method(catMap.GetType(), "AddAction", new[] { typeof(int), typeof(int) });
            addActionMethod?.Invoke(catMap, new object[] { targetCat.id, nextId });
        }

        Plugin.LoadCounterMeasureStats();

        foreach (string displayName in Plugin.counterMeasureDisplayNames)
        {
            actions.Add(createAction(typeof(InputAction), catMap, targetCat.id, nextId, displayName, "Select " + displayName));
            nextId++;
        }

        for (int index = 0; index < Plugin.counterMeasureSlots; index++)
        {
            actions.Add(createAction(typeof(InputAction), catMap, targetCat.id, nextId, "counterMeasureSlot" + index, "Select Counter Measure slot #" + index));
            nextId++;
        }

        Plugin.RewiredReady = true;
    }

    private static InputAction createAction(Type actionType, object catMap, int catId, int nextId, string actionName, string descriptiveName)
    {
        InputAction action = (InputAction)Activator.CreateInstance(actionType, true);
        MethodInfo addActionMethod = AccessTools.Method(catMap.GetType(), "AddAction", new[] { typeof(int), typeof(int) });

        SetProp(actionType, action, "id", nextId);
        SetProp(actionType, action, "name", actionName);
        SetProp(actionType, action, "type", InputActionType.Button);
        SetProp(actionType, action, "descriptiveName", descriptiveName);
        SetProp(actionType, action, "categoryId", catId);
        SetField(actionType, action, "_userAssignable", true);

        addActionMethod?.Invoke(catMap, [catId, nextId]);

        return action;
    }

    private static T GetProp<T>(object instance, string name) => (T)(AccessTools.Property(instance.GetType(), name)?.GetValue(instance) ?? default(T));

    private static void SetProp<T>(Type type, object instance, string name, T value) => AccessTools.Property(type, name)?.SetValue(instance, value, null);

    private static T GetField<T>(object instance, string name) => (T)(AccessTools.Field(instance.GetType(), name)?.GetValue(instance) ?? default(T));

    private static void SetField<T>(Type type, object instance, string name, T value) => AccessTools.Field(type, name)?.SetValue(instance, value);
}