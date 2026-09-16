using HarmonyLib;
using PeterHan.PLib.UI;

namespace MultichannelCritterSensor
{
	public static class Patches
	{
		// Build menu: Automation > Sensors, right after the vanilla Critter Sensor.
		[HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.LoadGeneratedBuildings))]
		public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
		{
			public static void Prefix()
			{
				ModUtil.AddBuildingToPlanScreen("Automation", MultichannelCritterSensorConfig.ID, "sensors",
					LogicCritterCountSensorConfig.ID, ModUtil.BuildingOrdering.After);
			}
		}

		// Research: Multiplexing (tier 6, the ribbon-heavy automation node), two tiers past
		// the vanilla sensor's Animal Control.
		[HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
		public static class Db_Initialize_Patch
		{
			public const string TechId = "Multiplexing";

			public static void Postfix()
			{
				Tech tech = Db.Get().Techs.TryGet(TechId);
				if (tech == null)
				{
					Debug.LogWarning("[MultichannelCritterSensor] Tech '" + TechId + "' not found; building will be unlocked from the start");
					return;
				}
				if (!tech.unlockedItemIDs.Contains(MultichannelCritterSensorConfig.ID))
					tech.unlockedItemIDs.Add(MultichannelCritterSensorConfig.ID);
			}
		}

		[HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
		public static class DetailsScreen_OnPrefabInit_Patch
		{
			public static void Postfix()
			{
				PUIUtils.AddSideScreenContent<MultichannelCritterSensorSideScreen>();
			}
		}
	}
}
