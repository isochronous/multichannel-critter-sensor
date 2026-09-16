using HarmonyLib;
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
			ModStrings.Register();
			Debug.Log("[MultichannelCritterSensor] Loaded version " + typeof(MultichannelCritterSensorMod).Assembly.GetName().Version);
		}
	}
}
