using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;

namespace OffGridNS;

[BepInPlugin("OffGrid", "OffGrid", "0.1.0")]
public class OffGridPlugin : BaseUnityPlugin
{
	public static ManualLogSource L;
	public static void Log(string s)
	{
		L.LogInfo((object)(DateTime.Now.ToString("HH:MM:ss") + ": " + s));
	}
	public static Harmony HarmonyInstance;
	public static HashSet<string> OffGridCards = new HashSet<string>();
	public static ConfigEntry<UnityEngine.InputSystem.Key> OffGridKey;
	public static ConfigEntry<UnityEngine.InputSystem.Key> AltOffGridKey;
	public void Awake()
	{
		L = ((OffGridPlugin)this).Logger;

		try
		{
			HarmonyInstance = new Harmony("OffGridPlugin");
			HarmonyInstance.PatchAll(typeof(OffGridPlugin));
		}
		catch (Exception ex3)
		{
			Log("Patching failed: " + ex3.Message);
		}
		OffGridKey = Config.Bind("Off Grid Plugin", "Key", UnityEngine.InputSystem.Key.LeftCtrl, $"Holding that key you can put a card off grid.");
		AltOffGridKey = Config.Bind("Off Grid Plugin", "Alt Key", UnityEngine.InputSystem.Key.RightCtrl, $"Holding that key you can put a card off grid also.");
	}

	[HarmonyPatch(typeof(GameCard), "StartDragging")]
	[HarmonyPostfix]
	public static void GameCard_StartDragging_Postfix(GameCard __instance)
	{
		if (__instance.CardData && OffGridCards.Contains(__instance.CardData.UniqueId))
		{
			OffGridCards.Remove(__instance.CardData.UniqueId);
			OffGridCardsSave();
		}
	}
	[HarmonyPatch(typeof(GameCard), "StopDragging")]
	[HarmonyPostfix]
	public static void GameCard_StopDragging_Postfix(GameCard __instance)
	{
		if (InputController.instance.GetKey(OffGridKey.Value) || InputController.instance.GetKey(AltOffGridKey.Value)
			&& !__instance.HasParent)
		{
			if (__instance.CardData)
			{
				OffGridCards.Add(__instance.CardData.UniqueId);
				OffGridCardsSave();
			}
		}
		else
		{
			if (__instance.CardData)
			{
				OffGridCards.Remove(__instance.CardData.UniqueId);
				OffGridCardsSave();
			}
		}
	}

	
	[HarmonyPatch(typeof(WorldManager), "SnapCardsToGrid")]
	[HarmonyPrefix]
	[HarmonyPriority(Priority.High)]
	public static void WorldManager_SnapCardsToGrid_Prefix(ref Dictionary<string, Vector3> __state)
	{
		__state = new Dictionary<string, Vector3>();
		foreach (string card in OffGridCards)
		{
			GameCard cg = WorldManager.instance.GetCardWithUniqueId(card);
			if (cg)
				__state.Add(cg.CardData.UniqueId, cg.TargetPosition);
		}
	}
	
	[HarmonyPatch(typeof(WorldManager), "SnapCardsToGrid")]
	[HarmonyPostfix]
	public static void WorldManager_SnapCardsToGrid_Postfix(WorldManager __instance, Dictionary<string, Vector3> __state)
	{
		if (__state == null) return;
		foreach(KeyValuePair<string, Vector3> entry in __state)
		{
			GameCard gc = __instance.GetCardWithUniqueId(entry.Key);
			if (!gc || !gc.CardData) continue;
			gc.TargetPosition = entry.Value;
		}
	}

	[HarmonyPatch(typeof(GameCard), "Update")]
	[HarmonyPostfix]
	public static void WorldManager_Update_Postfix(GameCard __instance)
	{
		if (__instance.CardData == null) return;
		if (__instance.HasParent)
		{
			if (OffGridCards.Contains(__instance.CardData.UniqueId)) OffGridCards.Remove(__instance.CardData.UniqueId);
			return;
		}
		bool StillDragged = __instance.BeingDragged
				&& (InputController.instance.GetKey(OffGridKey.Value)
					|| InputController.instance.GetKey(AltOffGridKey.Value));
			
		if (!StillDragged)
		{
			if (WorldManager.instance.gridAlpha < 0.001f) return;
			if (!OffGridCards.Contains(__instance.CardData.UniqueId)) return;
		}
		if (__instance.HighlightRectangle.enabled) return;
		__instance.HighlightRectangle.enabled = true;
		__instance.HighlightRectangle.Color = WorldManager.instance.CurrentBoard.CardHighlightColor.AlphaMultiplied((StillDragged ? 1f : WorldManager.instance.gridAlpha) * 0.7f);
		__instance.HighlightRectangle.DashOffset -= Time.deltaTime;
	
	}

	
	[HarmonyPatch(typeof(SelectSaveScreen), "SetSave")]
	[HarmonyPostfix]
	public static void SelectSaveScreen_SetSaved_Postfix()
	{
		OffGridCards.Clear();
		OffGridCardsLoad();
	}
	[HarmonyPatch(typeof(WorldManager), "Load")]
	[HarmonyPostfix]
	public static void WorldManager_Load_Postfix()
	{
		OffGridCards.Clear();
		OffGridCardsLoad();
	}
	public static void OffGridCardsSave()
	{
		OffGridCards.Remove("");
		var OffGridCardsString = OffGridCards.Join(delimiter:",");
		SerializedKeyValuePairHelper.SetOrAdd(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "OffGridCards", OffGridCardsString);
	}
	public static void OffGridCardsLoad()
	{
		string OffGridCardsString = SerializedKeyValuePairHelper.GetWithKey(WorldManager.instance.CurrentSaveGame.ExtraKeyValues, "OffGridCards")?.Value;
		var OffGridCardsSave = (OffGridCardsString ?? "").Split(',').ToList();
		foreach (string cardId in OffGridCardsSave)
		{
			if (cardId != "")
			{
				OffGridCards.Add(cardId);
			}
		}

	}
	
}
