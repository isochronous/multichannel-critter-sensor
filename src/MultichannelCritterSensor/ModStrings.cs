namespace MultichannelCritterSensor
{
	/// <summary>
	/// The mod's text, registered under the game's STRINGS tree by Isochronous.Translations
	/// (ModStrings.BUILDINGS.X is the key STRINGS.BUILDINGS.X, which is how the game finds a
	/// building's name) and replaced from translations/&lt;locale&gt;.po. Code reads the fields
	/// when it needs the text, so a translation loaded later is what the player sees.
	/// </summary>
	public static class ModStrings
	{
		private static readonly string Green = STRINGS.UI.FormatAsAutomationState("Green Signal", STRINGS.UI.AutomationState.Active);
		private static readonly string Red = STRINGS.UI.FormatAsAutomationState("Red Signal", STRINGS.UI.AutomationState.Standby);
		private static readonly string Ribbon = STRINGS.UI.FormatAsLink("Automation Ribbon", "LOGICRIBBON");
		private static readonly string KwCritters = STRINGS.UI.PRE_KEYWORD + "Critters" + STRINGS.UI.PST_KEYWORD;
		private static readonly string KwEggs = STRINGS.UI.PRE_KEYWORD + "Eggs" + STRINGS.UI.PST_KEYWORD;

		public static class BUILDINGS
		{
			public static class PREFABS
			{
				public static class MULTICHANNELCRITTERSENSOR
				{
					public static LocString NAME = STRINGS.UI.FormatAsLink("Multichannel Critter Sensor", "MULTICHANNELCRITTERSENSOR");
					public static LocString DESC = "Tracking exactly which critters and eggs share a room allows for much finer control over automated ranching.";
					public static LocString EFFECT =
						"Counts the selected " + STRINGS.UI.FormatAsLink("Critters", "CREATURES") + " and Eggs in its room and writes the results to an " + Ribbon + ":\n" +
						"\n" +
						"<b>Bit 1</b>: " + Green + " when the count passes the threshold\n" +
						"<b>Bit 2</b>: " + Green + " when the egg count passes its own threshold (separate-thresholds mode only)\n" +
						"<b>Bit 3</b>: a brief " + Green + " pulse each time the tracked count rises\n" +
						"<b>Bit 4</b>: a brief " + Green + " pulse each time the tracked count falls\n" +
						"\n" +
						Ribbon + " must be used as the output wire to avoid overloading.";

					public static LocString LOGIC_PORT = "Critter Count (4-Bit)";
					public static LocString LOGIC_PORT_ACTIVE =
						"Bit 1: " + Green + " if the tracked count is past the threshold\n" +
						"Bit 2: " + Green + " if the egg count is past its threshold (separate mode)\n" +
						"Bit 3: " + Green + " pulse when the tracked count rises\n" +
						"Bit 4: " + Green + " pulse when the tracked count falls";
					public static LocString LOGIC_PORT_INACTIVE = "Otherwise, sends a " + Red + " on each bit";
				}
			}
		}

		public static class UI
		{
			public static class UISIDESCREENS
			{
				public static class MULTICHANNEL_CRITTER_SENSOR_SIDE_SCREEN
				{
					// Read by the cloned vanilla ThresholdSwitchSideScreen through LocString.ToString(),
					// a key lookup, so these keys must stay as they are.
					public static LocString TITLE = "Multichannel Critter Sensor";
					public static LocString VALUE_NAME_COUNT = "Count";
					public static LocString VALUE_NAME_CRITTERS = "Critters";
					public static LocString VALUE_NAME_EGGS = "Eggs";

					// Threshold tooltips for the cloned vanilla screen. {0} is the threshold.
					public static LocString COMBINED_ABOVE = "Bit 1 sends a " + Green + " if there are more than <b>{0}</b> tracked " + KwCritters + " or " + KwEggs + " in the room";
					public static LocString COMBINED_BELOW = "Bit 1 sends a " + Green + " if there are fewer than <b>{0}</b> tracked " + KwCritters + " or " + KwEggs + " in the room";
					public static LocString CRITTERS_ABOVE = "Bit 1 sends a " + Green + " if there are more than <b>{0}</b> tracked " + KwCritters + " in the room";
					public static LocString CRITTERS_BELOW = "Bit 1 sends a " + Green + " if there are fewer than <b>{0}</b> tracked " + KwCritters + " in the room";
					public static LocString EGGS_ABOVE = "Bit 2 sends a " + Green + " if there are more than <b>{0}</b> tracked " + KwEggs + " in the room";
					public static LocString EGGS_BELOW = "Bit 2 sends a " + Green + " if there are fewer than <b>{0}</b> tracked " + KwEggs + " in the room";

					// This mod's own side screen.
					public static LocString MODE_LABEL = "Thresholds:";
					public static LocString MODE_COMBINED = "Combined";
					public static LocString MODE_COMBINED_TOOLTIP = "One threshold for critters and eggs together. Bit 1 sends a " + Green + " when the combined count passes it.";
					public static LocString MODE_SEPARATE = "Separate";
					public static LocString MODE_SEPARATE_TOOLTIP = "Separate thresholds for critters and eggs. Bit 1 reports the critter count, bit 2 reports the egg count.";

					public static LocString CURRENT_CRITTERS = "Current Critters: {0}";
					public static LocString CURRENT_EGGS = "Current Eggs: {0}";
					public static LocString NOT_COUNTING = "Nothing is being counted";
					public static LocString NOT_IN_ROOM = "Not in a room";

					public static LocString EXPANDER_TOOLTIP = "Show or hide the list of types to count";
					public static LocString ALL_CRITTERS = "All critters";
					public static LocString ALL_CRITTERS_TOOLTIP = "Count every critter species, including ones discovered later";
					public static LocString ALL_EGGS = "All eggs";
					public static LocString ALL_EGGS_TOOLTIP = "Count every egg type, including ones discovered later";
					public static LocString COUNT_CRITTERS_TOOLTIP = "Include critters in the count. The list below chooses which species; only discovered species are shown.";
					public static LocString COUNT_EGGS_TOOLTIP = "Include eggs in the count. The list below chooses which egg types; only discovered eggs are shown.";
				}
			}
		}
	}
}
