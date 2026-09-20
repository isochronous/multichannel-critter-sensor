using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using KSerialization;
using UnityEngine;

#pragma warning disable 649, 169 // [MyCmpGet]/[MyCmpAdd] fields are populated by the game via reflection

namespace MultichannelCritterSensor
{
	/// <summary>
	/// Room-based critter/egg counter that writes a 4-bit value to a ribbon output port.
	///
	/// Bit layout (0-based internally, 1-based in the UI):
	///   bit 0: threshold result (combined count, or the critter count in separate mode)
	///   bit 1: egg-count threshold result (separate mode only; otherwise off)
	///   bit 2: one logic tick pulse whenever the tracked total rises
	///   bit 3: one logic tick pulse whenever the tracked total falls
	///
	/// Counting mirrors the vanilla LogicCritterCountSensor: every 200ms the room prober is
	/// asked for the building's room and the room's creature and egg lists are walked, but
	/// each entry is additionally filtered by the selected species / egg prefab tags.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class MultichannelCritterSensor : KMonoBehaviour, ISim200ms
	{
		public static readonly HashedString PortId = new HashedString("MultichannelCritterSensorOutput");

		public const int MaxThreshold = 64;

		public const int PrimaryBit = 0;
		public const int EggBit = 1;
		public const int RisingBit = 2;
		public const int FallingBit = 3;

		private const int ThresholdMask = (1 << PrimaryBit) | (1 << EggBit);
		private const int PulseMask = (1 << RisingBit) | (1 << FallingBit);

		private static readonly EventSystem.IntraObjectHandler<MultichannelCritterSensor> OnCopySettingsDelegate =
			new EventSystem.IntraObjectHandler<MultichannelCritterSensor>(delegate(MultichannelCritterSensor component, object data)
			{
				component.OnCopySettings(data);
			});

		// ---- configuration (saved with the building) ----

		[Serialize]
		public bool separateThresholds;

		[Serialize]
		public bool countCritters = true;

		[Serialize]
		public bool countEggs = true;

		/// <summary>When true every critter species counts, including ones discovered later.</summary>
		[Serialize]
		public bool allCritters = true;

		[Serialize]
		public bool allEggs = true;

		[Serialize]
		public List<Tag> critterTags = new List<Tag>();

		[Serialize]
		public List<Tag> eggTags = new List<Tag>();

		[Serialize]
		public int combinedThreshold;

		[Serialize]
		public bool combinedAbove = true;

		[Serialize]
		public int critterThreshold;

		[Serialize]
		public bool critterAbove = true;

		[Serialize]
		public int eggThreshold;

		[Serialize]
		public bool eggAbove = true;

		// ---- runtime state ----

		[MyCmpAdd]
		private CopyBuildingSettings copyBuildingSettings;

		[MyCmpGet]
		private KSelectable selectable;

		[MyCmpGet]
		private LogicPorts ports;

		[MyCmpGet]
		private KBatchedAnimController animController;

		private readonly HashSet<Tag> critterSet = new HashSet<Tag>();
		private readonly HashSet<Tag> eggSet = new HashSet<Tag>();

		/// <summary>Tracked total at the previous scan, or -1 when unknown (no room, config change).</summary>
		private int lastTotal = -1;

		private bool pulseRising;
		private bool pulseFalling;
		private bool logicTickHooked;
		private string currentAnim;
		private int outputValue = -1;

		public int CritterCount { get; private set; }

		public int EggCount { get; private set; }

		public bool InRoom { get; private set; }

		public int TrackedTotal => (countCritters ? CritterCount : 0) + (countEggs ? EggCount : 0);

		public int OutputValue => outputValue < 0 ? 0 : outputValue;

		public bool IsOn => (OutputValue & ThresholdMask) != 0;

		// ---- lifecycle ----

		protected override void OnPrefabInit()
		{
			base.OnPrefabInit();
			Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
		}

		protected override void OnSpawn()
		{
			base.OnSpawn();
			EnsureLists();
			RebuildSets();
			HookLogicTick(true);
			Evaluate();
			UpdateVisualState(force: true);
		}

		protected override void OnCleanUp()
		{
			HookLogicTick(false);
			base.OnCleanUp();
		}

		[OnDeserialized]
		private void OnDeserialized()
		{
			EnsureLists();
			if (isSpawned)
				NotifyConfigChanged();
		}

		private void HookLogicTick(bool hook)
		{
			LogicCircuitManager manager = Game.Instance != null ? Game.Instance.logicCircuitManager : null;
			if (manager == null)
				return;
			if (hook && !logicTickHooked)
			{
				manager.onLogicTick += OnLogicTick;
				logicTickHooked = true;
			}
			else if (!hook && logicTickHooked)
			{
				manager.onLogicTick -= OnLogicTick;
				logicTickHooked = false;
			}
		}

		// ---- configuration API used by the side screen ----

		/// <summary>
		/// Call after changing any configuration field. Rebuilds the tag lookups, suppresses
		/// the rising/falling pulse for the next scan (configuration edits are not events)
		/// and re-evaluates immediately so the UI and circuit reflect the change.
		/// </summary>
		public void NotifyConfigChanged()
		{
			EnsureLists();
			RebuildSets();
			lastTotal = -1;
			if (!isSpawned)
				return;
			Evaluate();
			// A mode switch can leave the output value as it was yet change which animation fits.
			UpdateVisualState();
		}

		public bool IsSpeciesSelected(bool critters, Tag tag)
		{
			return critters ? (allCritters || critterSet.Contains(tag)) : (allEggs || eggSet.Contains(tag));
		}

		public bool AnySpeciesSelected(bool critters)
		{
			return critters ? (allCritters || critterTags.Count > 0) : (allEggs || eggTags.Count > 0);
		}

		public void SetAllSpecies(bool critters, bool all)
		{
			EnsureLists();
			if (critters)
			{
				allCritters = all;
				critterTags.Clear();
			}
			else
			{
				allEggs = all;
				eggTags.Clear();
			}
			NotifyConfigChanged();
		}

		/// <summary>
		/// Toggles one species. Leaving the "all" state deselects just the clicked entry and
		/// keeps every other currently visible entry selected.
		/// </summary>
		public void ToggleSpecies(bool critters, Tag tag, IEnumerable<Tag> visible)
		{
			EnsureLists();
			List<Tag> list = critters ? critterTags : eggTags;
			bool all = critters ? allCritters : allEggs;
			if (all)
			{
				list.Clear();
				foreach (Tag other in visible)
					if (other != tag && !list.Contains(other))
						list.Add(other);
				if (critters)
					allCritters = false;
				else
					allEggs = false;
			}
			else if (list.Contains(tag))
			{
				list.Remove(tag);
			}
			else
			{
				list.Add(tag);
			}
			NotifyConfigChanged();
		}

		private void OnCopySettings(object data)
		{
			GameObject sourceGo = data as GameObject;
			MultichannelCritterSensor source = sourceGo != null ? sourceGo.GetComponent<MultichannelCritterSensor>() : null;
			if (source == null)
				return;
			source.EnsureLists();
			separateThresholds = source.separateThresholds;
			countCritters = source.countCritters;
			countEggs = source.countEggs;
			allCritters = source.allCritters;
			allEggs = source.allEggs;
			critterTags = new List<Tag>(source.critterTags);
			eggTags = new List<Tag>(source.eggTags);
			combinedThreshold = source.combinedThreshold;
			combinedAbove = source.combinedAbove;
			critterThreshold = source.critterThreshold;
			critterAbove = source.critterAbove;
			eggThreshold = source.eggThreshold;
			eggAbove = source.eggAbove;
			NotifyConfigChanged();
		}

		private void EnsureLists()
		{
			if (critterTags == null)
				critterTags = new List<Tag>();
			if (eggTags == null)
				eggTags = new List<Tag>();
		}

		private void RebuildSets()
		{
			critterSet.Clear();
			foreach (Tag tag in critterTags)
				critterSet.Add(tag);
			eggSet.Clear();
			foreach (Tag tag in eggTags)
				eggSet.Add(tag);
		}

		// ---- counting and output ----

		public void Sim200ms(float dt)
		{
			Evaluate();
		}

		private void Evaluate()
		{
			if (Game.Instance == null || Game.Instance.roomProber == null)
				return;

			Room room = Game.Instance.roomProber.GetRoomOfGameObject(gameObject);
			int value = 0;
			if (room != null && room.cavity != null)
			{
				InRoom = true;
				CritterCount = countCritters ? CountTracked(room.cavity.creatures, allCritters, critterSet) : 0;
				EggCount = countEggs ? CountTracked(room.cavity.eggs, allEggs, eggSet) : 0;

				int total = TrackedTotal;
				if (lastTotal >= 0)
				{
					if (total > lastTotal)
						pulseRising = true;
					else if (total < lastTotal)
						pulseFalling = true;
				}
				lastTotal = total;

				if (separateThresholds)
				{
					if (countCritters && Passes(CritterCount, critterThreshold, critterAbove))
						value |= 1 << PrimaryBit;
					if (countEggs && Passes(EggCount, eggThreshold, eggAbove))
						value |= 1 << EggBit;
				}
				else if (Passes(total, combinedThreshold, combinedAbove))
				{
					value |= 1 << PrimaryBit;
				}
			}
			else
			{
				InRoom = false;
				CritterCount = 0;
				EggCount = 0;
				lastTotal = -1;
			}

			if (selectable != null)
				selectable.ToggleStatusItem(Db.Get().BuildingStatusItems.NotInAnyRoom, !InRoom);

			if (pulseRising)
				value |= 1 << RisingBit;
			if (pulseFalling)
				value |= 1 << FallingBit;
			SetOutput(value);
		}

		private static int CountTracked(List<KPrefabID> entities, bool all, HashSet<Tag> selected)
		{
			if (entities == null)
				return 0;
			if (all)
				return entities.Count;
			int count = 0;
			foreach (KPrefabID entity in entities)
				if (entity != null && selected.Contains(entity.PrefabTag))
					count++;
			return count;
		}

		private static bool Passes(int count, int threshold, bool above)
		{
			return above ? count > threshold : count < threshold;
		}

		/// <summary>
		/// The circuit manager samples every sender's value, then fires this callback, once
		/// per logic tick. Clearing the pulse bits here guarantees each pulse is read by
		/// exactly one tick and that consecutive pulses are separated by a low tick.
		/// </summary>
		private void OnLogicTick()
		{
			if (!pulseRising && !pulseFalling)
				return;
			pulseRising = false;
			pulseFalling = false;
			SetOutput(OutputValue & ~PulseMask);
		}

		private void SetOutput(int value)
		{
			if (value == outputValue)
				return;
			outputValue = value;
			if (ports != null)
				ports.SendSignal(PortId, value);
			UpdateVisualState();
			UpdateStatus();
		}

		/// <summary>
		/// One looping animation per signal state (see tools/make_art.py): the left antenna and
		/// the paw prints follow bit 0, the right antenna and the egg follow bit 1. In combined
		/// mode bit 0 covers both kinds, so both antennae light up.
		/// </summary>
		private void UpdateVisualState(bool force = false)
		{
			bool primary = (OutputValue & (1 << PrimaryBit)) != 0;
			bool eggs = (OutputValue & (1 << EggBit)) != 0;
			string anim;
			if (!separateThresholds)
				anim = primary ? "on_combined" : "off";
			else if (primary)
				anim = eggs ? "on_both" : "on_critter";
			else
				anim = eggs ? "on_egg" : "off";
			if (!force && anim == currentAnim)
				return;
			currentAnim = anim;
			if (animController != null)
				animController.Play(anim, anim == "off" ? KAnim.PlayMode.Once : KAnim.PlayMode.Loop);
		}

		private void UpdateStatus()
		{
			if (selectable == null)
				return;
			StatusItem item = IsOn ? Db.Get().BuildingStatusItems.LogicSensorStatusActive : Db.Get().BuildingStatusItems.LogicSensorStatusInactive;
			selectable.SetStatusItem(Db.Get().StatusItemCategories.Power, item);
		}
	}
}
