using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

namespace MultichannelCritterSensor
{
	/// <summary>
	/// Building definition: a 1x1 sensor that reuses the vanilla critter sensor art
	/// (flipped vertically as placeholder art) and exposes a single 4-bit ribbon output.
	/// </summary>
	public sealed class MultichannelCritterSensorConfig : IBuildingConfig
	{
		public const string ID = "MultichannelCritterSensor";

		public override BuildingDef CreateBuildingDef()
		{
			BuildingDef def = BuildingTemplates.CreateBuildingDef(ID, 1, 1, "critter_sensor_kanim", 30, 30f,
				new float[] { 50f, 50f },
				new string[] { MATERIALS.REFINED_METALS[0], MATERIALS.PLASTICS[0] },
				1600f, BuildLocationRule.Anywhere,
				noise: NOISE_POLLUTION.NONE, decor: TUNING.BUILDINGS.DECOR.PENALTY.TIER0);
			def.Overheatable = false;
			def.Floodable = false;
			def.Entombable = false;
			def.ViewMode = OverlayModes.Logic.ID;
			def.AudioCategory = "Metal";
			def.SceneLayer = Grid.SceneLayer.Building;
			def.AlwaysOperational = true;
			def.LogicOutputPorts = new List<LogicPorts.Port>
			{
				LogicPorts.Port.RibbonOutputPort(MultichannelCritterSensor.PortId, new CellOffset(0, 0),
					ModStrings.PortName, ModStrings.PortActive, ModStrings.PortInactive, show_wire_missing_icon: true)
			};
			def.AddSearchTerms(SEARCH_TERMS.CRITTER);
			def.AddSearchTerms(SEARCH_TERMS.AUTOMATION);
			GeneratedBuildings.RegisterWithOverlay(OverlayModes.Logic.HighlightItemIDs, ID);
			return def;
		}

		public override void DoPostConfigureComplete(GameObject go)
		{
			go.AddOrGet<MultichannelCritterSensor>();
			go.GetComponent<KPrefabID>().AddTag(GameTags.OverlayInFrontOfConduits);
		}
	}
}
