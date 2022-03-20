using Amplitude;
using Amplitude.Mercury.AI;
using Amplitude.Mercury.Interop;
using Amplitude.Mercury.Presentation;
using Amplitude.Mercury.Sandbox;
using Amplitude.Mercury.Simulation;
using Amplitude.Mercury.Options;
using Amplitude.Mercury.UI;
using BepInEx;
using HarmonyLib;
using HumankindModTool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Amplitude.Framework.Options;
using Amplitude.Mercury.Data.GameOptions;

namespace AOM.Gedemon.DelayedAITurn
{
	#region BepInEx/Harmony initialization
	[BepInPlugin(pluginGuid, pluginName, pluginVersion)]
	public class DelayedAITurn : BaseUnityPlugin
	{
		public const string pluginGuid = "AOM.Gedemon.DelayedAITurn";
		public const string pluginName = "Delayed AI Turn";
		public const string pluginVersion = "1.0.0.1";
		void Awake()
		{
			Harmony harmony = new Harmony(pluginGuid);
			harmony.PatchAll();
		}

		public static DelayedAITurn Instance;

		public static GameOptionStateInfo Simultaneous = new GameOptionStateInfo
		{
			Value = nameof(Simultaneous),
			Title = "Simultaneous",
			Description = "Human and AI players take their turn at the same time"
		};
		public static GameOptionStateInfo HumanFirst = new GameOptionStateInfo
		{
			Value = nameof(HumanFirst),
			Title = "Humans First",
			Description = "AI players wait until all human players have finished their turn before starting theirs."
		};
		public const string GameOptionGroup_LobbyPaceOptions = nameof(GameOptionGroup_LobbyPaceOptions);
		public static GameOptionInfo TurnStartModeOption = new GameOptionInfo
		{
			ControlType = UIControlType.DropList,
			Key = nameof(GameOption) + "_AOM_TurnStartMode",
			DefaultValue = nameof(HumanFirst),
			Title = "AI Turn Mode",
			Description = "Determines when AI and Human players start their turn",
			GroupKey = GameOptionGroup_LobbyPaceOptions,
			States = {
				Simultaneous,
				HumanFirst
			}
		};

		internal static bool HumansFirst()
		{
			return GameOptionHelper.CheckGameOption(TurnStartModeOption, HumanFirst.Value);
		}

		public static List<int> HumansReady = new List<int>();
	}
	#endregion

	[HarmonyPatch(typeof(OptionsManager<GameOptionDefinition>))]
	public class OptionsManager_Patch
	{
		[HarmonyPatch(nameof(Load))]
		[HarmonyPrefix]
		public static bool Load(OptionsManager<GameOptionDefinition> __instance)
		{
			GameOptionHelper.Initialize(
				DelayedAITurn.TurnStartModeOption
			);
			return true;
		}
	}

	//*
	[HarmonyPatch(typeof(DepartmentOfCommunication))]
	public class DepartmentOfCommunication_Patch
	{
		[HarmonyPrefix]
		[HarmonyPatch(nameof(ProcessOrderEmpireReady))]
		public static bool ProcessOrderEmpireReady(DepartmentOfCommunication __instance, OrderEmpireReady order)
		{
			if (!DelayedAITurn.HumansFirst())
				return true;

			if (!__instance.Empire.IsControlledByHuman)
				return true;

			if(DelayedAITurn.HumansReady.Contains(__instance.Empire.Index))
			{
				Diagnostics.Log($"[Gedemon] ProcessOrderEmpireReady for Empire #{__instance.Empire.Index} (already called)");
				return true;
			}
			else
			{
				Diagnostics.Log($"[Gedemon] ProcessOrderEmpireReady for Empire #{__instance.Empire.Index} (first call)");
				DelayedAITurn.HumansReady.Add(__instance.Empire.Index);
				return false;
            }
		}
	}

	[HarmonyPatch(typeof(Empire))]
	public class Empire_Patch
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(SetReady))]
		public static void SetReady(Empire __instance, bool isReady)
		{
			if (!DelayedAITurn.HumansFirst())
				return;

			if (!isReady)
			{
				DelayedAITurn.HumansReady.Remove(__instance.Index);
			}
		}
	}

	[HarmonyPatch(typeof(AIController))]
	public class AIController_Patch
	{

		[HarmonyPatch(nameof(RunAIDecisionCycle))]
		[HarmonyPrefix]
		public static void RunAIDecisionCycle(AIController __instance)
		{

			if (!DelayedAITurn.HumansFirst())
				return;

			if (__instance.state != 0 && !AllHumansReady(__instance))
			{
				WaitForHumans(__instance);
			}
		}

		public static void WaitForHumans(AIController __instance)
        {
            while (__instance.state != 0 && !AllHumansReady(__instance))
            {
                Thread.Sleep(100);
            }
        }
		public static bool AllHumansReady(AIController __instance)
		{

			for (int i = 0; i < Sandbox.MajorEmpires.Length; i++)
			{
				var empire = Sandbox.MajorEmpires[i];
				if ((empire?.IsControlledByHuman ?? false) && !DelayedAITurn.HumansReady.Contains(empire.Index))
				{
					return false;
				}
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(EndTurnWindow))]
	public class EndTurnWindow_Patch
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(Refresh))]
		public static void Refresh(EndTurnWindow __instance)
		{
			if (!DelayedAITurn.HumansFirst())
				return;

			int localEmpireIndex = SandboxManager.Sandbox.LocalEmpireIndex;
			if (!DelayedAITurn.HumansReady.Contains(localEmpireIndex))
			{
				if (__instance.endTurnTextLabel.Text == "%EndTurnWindowEndGameTitle" || __instance.endTurnTextLabel.Text == "%EndTurnWindowEndTurnTitle")
				{
					__instance.endTurnTextLabel.Text = "AI"+ Environment.NewLine+"Turn";
					__instance.endTurnTooltipLabel.Text = "Start AI Turn";
				}
			}
		}
	}

	[HarmonyPatch(typeof(MandatoryOngoingBattle))]
	public class MandatoryOngoingBattle_Patch
	{
		[HarmonyPatch(nameof(RefreshData))]
		[HarmonyPrefix]
		public static bool RefreshData(MandatoryOngoingBattle __instance, ref bool __result)
		{

			if (!DelayedAITurn.HumansFirst())
				return true;

			var onGoingBattles = __instance.onGoingBattles;
			var battles = Presentation.PresentationBattleReportController.Battles;
			var localEmpireIndex = Amplitude.Mercury.Interop.Snapshots.GameSnapshot.PresentationData.LocalEmpireInfo.EmpireIndex;
			onGoingBattles.Clear();
			for (int i = 0; i < battles.Count; i++)
			{
				var presentationBattle = battles[i];
				if (presentationBattle.GetRoleFor(localEmpireIndex) != Amplitude.Mercury.Interop.BattleGroupRoleType.None && presentationBattle.CurrentBattleState != PresentationBattleStatus.WaitForTurnBegin && presentationBattle.CurrentBattleState > PresentationBattleStatus.Sieging && presentationBattle.CurrentBattleState < PresentationBattleStatus.ResultAcknowledge)
				{
					if (!(presentationBattle.HasContenderConfirmed(localEmpireIndex)&&!DelayedAITurn.HumansReady.Contains(localEmpireIndex)) || presentationBattle.Contenders.Data.All(x => presentationBattle.HasContenderConfirmed(x.EmpireIndex)))
						onGoingBattles.Add(presentationBattle);
				}
			}
			__result = onGoingBattles.Count > 0;
			return false;
		}
	}

	[HarmonyPatch(typeof(Sandbox))]
	public class TCL_Sandbox
	{
		[HarmonyPatch("ThreadStart")]
		[HarmonyPostfix]
		public static void ThreadStartExit(Sandbox __instance, object parameter)
		{
			DelayedAITurn.HumansReady.Clear();
		}
	}
}
