using HarmonyLib;
using Isochronous;
using KMod;
using PeterHan.PLib.Core;

namespace MultichannelCritterSensor
{
	public sealed class MultichannelCritterSensorMod : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			PUtil.InitLibrary(false);
			Translations.Register(harmony, typeof(ModStrings));
			ThresholdAdapter.RegisterNoUnits();
			Debug.Log("[MultichannelCritterSensor] Loaded version " + typeof(MultichannelCritterSensorMod).Assembly.GetName().Version);
		}
	}
}
