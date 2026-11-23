using System;
using HarmonyLib;
using SuperFantasyKingdom;
using UnityEngine;

namespace CaravanCommissions
{
    internal static class SaveHooks
    {
        // Match the exact JSON selected by the game, including a restored backup.
        // Unity ignores the extra member when deserializing vanilla GameData.
        [Serializable]
        private sealed class Extension
        {
            public OrderBook caravanCommissionsV1;
        }

        private static bool reading, writing;
        private static OrderBook loaded;

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.PositionWorldBuildings))]
        internal static class ReadScope
        {
            static void Prefix() { loaded = null; reading = true; }
            static void Finalizer() { reading = false; }
        }

        [HarmonyPatch]
        internal static class ReadJson
        {
            static System.Reflection.MethodBase TargetMethod() =>
                AccessTools.FirstMethod(typeof(SaveManager), m => m.Name == "Load" &&
                    !m.IsGenericMethod && m.ReturnType == typeof(string));

            static void Postfix(string __result)
            {
                if (!reading || string.IsNullOrEmpty(__result)) return;
                var order = JsonUtility.FromJson<Extension>(__result)?.caravanCommissionsV1;
                if (order != null && order.spent != null) loaded = order;
            }
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        internal static class Restore
        {
            static void Prefix(GameData ___m_GameData)
            {
                Plugin.Reset(___m_GameData == null ? null : loaded);
                loaded = null;
            }
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Save))]
        internal static class WriteScope
        {
            static void Prefix() { writing = true; }
            static void Finalizer() { writing = false; }
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), typeof(string), typeof(string))]
        internal static class WriteJson
        {
            static void Prefix(string __0, ref string __1)
            {
                if (!writing || !__0.EndsWith("_game", StringComparison.Ordinal) ||
                    (!Plugin.Book.Pending && Plugin.Book.spent.Count == 0)) return;
                var extra = JsonUtility.ToJson(new Extension { caravanCommissionsV1 = Plugin.Book });
                int end = __1.LastIndexOf('}');
                __1 = __1.Insert(end, "," + extra.Substring(1, extra.Length - 2));
            }
        }
    }
}
