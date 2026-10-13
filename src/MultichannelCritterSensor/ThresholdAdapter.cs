using UnityEngine;
using SIDE_SCREEN = MultichannelCritterSensor.ModStrings.UI.UISIDESCREENS.MULTICHANNEL_CRITTER_SENSOR_SIDE_SCREEN;

namespace MultichannelCritterSensor
{
	/// <summary>
	/// Exposes one of the sensor's three thresholds (combined, critters, eggs) through the
	/// game's IThresholdSwitch interface so a cloned vanilla ThresholdSwitchSideScreen can
	/// drive it. Lives on a helper GameObject owned by the side screen; the vanilla screen
	/// finds it via GetComponent on that object.
	/// </summary>
	internal sealed class ThresholdAdapter : MonoBehaviour, IThresholdSwitch
	{
		public enum Kind
		{
			Combined,
			Critters,
			Eggs,
		}

		/// <summary>
		/// Empty units for the cloned vanilla screen, which renders them through LocString.ToString()
		/// (a key lookup). Kept out of ModStrings: an empty text has no translation, and an empty msgid
		/// is a header to po tools.
		/// </summary>
		private static readonly LocString NoUnits = new LocString("", "STRINGS.UI.UISIDESCREENS.MULTICHANNEL_CRITTER_SENSOR_SIDE_SCREEN.NO_UNITS");

		/// <summary>Puts the empty units text in the string table; call once at mod load.</summary>
		public static void RegisterNoUnits()
		{
			Strings.Add(NoUnits.key.String, NoUnits.text);
		}

		public Kind kind;

		public MultichannelCritterSensor sensor;

		public float Threshold
		{
			get
			{
				if (sensor == null)
					return 0f;
				switch (kind)
				{
					case Kind.Critters: return sensor.critterThreshold;
					case Kind.Eggs: return sensor.eggThreshold;
					default: return sensor.combinedThreshold;
				}
			}
			set
			{
				if (sensor == null)
					return;
				int v = Mathf.Clamp(Mathf.RoundToInt(value), 0, MultichannelCritterSensor.MaxThreshold);
				switch (kind)
				{
					case Kind.Critters: sensor.critterThreshold = v; break;
					case Kind.Eggs: sensor.eggThreshold = v; break;
					default: sensor.combinedThreshold = v; break;
				}
				sensor.NotifyConfigChanged();
			}
		}

		public bool ActivateAboveThreshold
		{
			get
			{
				if (sensor == null)
					return true;
				switch (kind)
				{
					case Kind.Critters: return sensor.critterAbove;
					case Kind.Eggs: return sensor.eggAbove;
					default: return sensor.combinedAbove;
				}
			}
			set
			{
				if (sensor == null)
					return;
				switch (kind)
				{
					case Kind.Critters: sensor.critterAbove = value; break;
					case Kind.Eggs: sensor.eggAbove = value; break;
					default: sensor.combinedAbove = value; break;
				}
				sensor.NotifyConfigChanged();
			}
		}

		public float CurrentValue
		{
			get
			{
				if (sensor == null)
					return 0f;
				switch (kind)
				{
					case Kind.Critters: return sensor.CritterCount;
					case Kind.Eggs: return sensor.EggCount;
					default: return sensor.TrackedTotal;
				}
			}
		}

		public float RangeMin => 0f;

		public float RangeMax => MultichannelCritterSensor.MaxThreshold;

		public LocString Title => SIDE_SCREEN.TITLE;

		public LocString ThresholdValueName
		{
			get
			{
				switch (kind)
				{
					case Kind.Critters: return SIDE_SCREEN.VALUE_NAME_CRITTERS;
					case Kind.Eggs: return SIDE_SCREEN.VALUE_NAME_EGGS;
					default: return SIDE_SCREEN.VALUE_NAME_COUNT;
				}
			}
		}

		public string AboveToolTip
		{
			get
			{
				switch (kind)
				{
					case Kind.Critters: return SIDE_SCREEN.CRITTERS_ABOVE;
					case Kind.Eggs: return SIDE_SCREEN.EGGS_ABOVE;
					default: return SIDE_SCREEN.COMBINED_ABOVE;
				}
			}
		}

		public string BelowToolTip
		{
			get
			{
				switch (kind)
				{
					case Kind.Critters: return SIDE_SCREEN.CRITTERS_BELOW;
					case Kind.Eggs: return SIDE_SCREEN.EGGS_BELOW;
					default: return SIDE_SCREEN.COMBINED_BELOW;
				}
			}
		}

		public ThresholdScreenLayoutType LayoutType => ThresholdScreenLayoutType.SliderBar;

		public int IncrementScale => 1;

		public NonLinearSlider.Range[] GetRanges => NonLinearSlider.GetDefaultRange(RangeMax);

		public float GetRangeMinInputField() => RangeMin;

		public float GetRangeMaxInputField() => RangeMax;

		public LocString ThresholdValueUnits() => NoUnits;

		public string Format(float value, bool units) => Mathf.RoundToInt(value).ToString();

		public float ProcessedSliderValue(float input) => Mathf.Round(input);

		public float ProcessedInputValue(float input) => Mathf.Round(input);
	}
}
