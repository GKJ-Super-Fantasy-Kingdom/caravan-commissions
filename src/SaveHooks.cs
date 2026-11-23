using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Newtonsoft.Json;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Tavern;
using UnityEngine;

namespace CaravanCommissions
{
    internal static class SaveHooks
    {
        private const string Key = "caravanCommissionsV1";
        [Serializable]
        private sealed class Envelope
        {
#pragma warning disable CS0649 // The game's serializer populates these fields.
            public string runID, hash;
            public OrderBook caravanCommissionsV1;
#pragma warning restore CS0649
        }

        private sealed class ReadContext
        {
            internal string Path;
            internal Envelope Selected;
        }
        private static readonly ConditionalWeakTable<GameData, OrderBook> Checkpoints =
            new ConditionalWeakTable<GameData, OrderBook>();
        private static ReadContext reading;
        private static OrderBook writing;
        private static string writePath;

        private static void Attach(GameData data)
        {
            var entry = reading?.Selected;
            if (entry?.caravanCommissionsV1?.spent == null || data == null ||
                data.runID != entry.runID || data.hash != entry.hash) return;
            Checkpoints.Remove(data);
            Checkpoints.Add(data, entry.caravanCommissionsV1);
        }

        [HarmonyPatch]
        private static class ReadScope
        {
            static MethodBase[] TargetMethods() => new[] {
                AccessTools.Method(typeof(GameDataManager), nameof(GameDataManager.PositionWorldBuildings)),
                AccessTools.Method(typeof(TavernSaveManager), "Load") };
            static void Prefix(object __instance, out ReadContext __state)
            {
                __state = reading;
                reading = new ReadContext { Path = (string)AccessTools.Method(__instance.GetType(), "GetPath").Invoke(__instance, null) };
            }
            static void Postfix(GameData ___m_GameData) => Attach(___m_GameData);
            static void Finalizer(ReadContext __state) => reading = __state;
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.LoadGameData))]
        private static class TavernCheckpoint
        {
            // The tavern clears GameData.hash immediately after this call returns.
            static void Postfix(GameData __result) => Attach(__result);
        }

        [HarmonyPatch]
        private static class ReadJson
        {
            static MethodBase TargetMethod() => AccessTools.FirstMethod(typeof(SaveManager),
                m => m.Name == "Load" && !m.IsGenericMethod && m.ReturnType == typeof(string));

            static void Postfix(string __0, string __result)
            {
                if (reading == null || __0 != reading.Path) return;
                reading.Selected = null;
                if (string.IsNullOrEmpty(__result) || !__result.Contains("\"" + Key + "\"")) return;
                // Unity's player serializer drops nested mod-defined classes from its envelope.
                try { reading.Selected = JsonConvert.DeserializeObject<Envelope>(__result); }
                catch (JsonException) { /* Let the native loader try its backup. */ }
            }
        }

        [HarmonyPatch(typeof(GameDataManager), nameof(GameDataManager.Load))]
        private static class Restore
        {
            static void Prefix(GameData ___m_GameData) => Plugin.Reset(
                ___m_GameData != null && Checkpoints.TryGetValue(___m_GameData, out var book) ? book : null);
        }

        [HarmonyPatch]
        private static class WriteScope
        {
            static MethodBase[] TargetMethods() => new[] {
                AccessTools.Method(typeof(GameDataManager), nameof(GameDataManager.Save)),
                AccessTools.Method(typeof(TavernSaveManager), nameof(TavernSaveManager.Save)) };
            static void Prefix(object __instance, out (OrderBook, string) __state)
            {
                __state = (writing, writePath);
                writePath = (string)AccessTools.Method(__instance.GetType(), "GetPath").Invoke(__instance, null);
                if (__instance is TavernSaveManager tavern)
                {
                    var data = tavern.GetGameData();
                    writing = data != null && Checkpoints.TryGetValue(data, out var saved) ? saved : null;
                }
                else writing = Plugin.Book;
            }
            static void Finalizer((OrderBook, string) __state) => (writing, writePath) = __state;
        }

        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), typeof(string), typeof(string))]
        private static class WriteJson
        {
            static void Prefix(string __0, ref string __1)
            {
                if (writing == null || __0 != writePath || (!writing.Pending && writing.spent.Count == 0)) return;
                int end = __1.LastIndexOf('}');
                if (end >= 0) __1 = __1.Insert(end, ",\"" + Key + "\":" + JsonUtility.ToJson(writing));
            }
        }
    }
}
