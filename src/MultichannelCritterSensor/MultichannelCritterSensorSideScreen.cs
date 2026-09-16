using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MultichannelCritterSensor
{
	/// <summary>
	/// Side screen for the Multichannel Critter Sensor. The frame is PLib UI; the threshold
	/// editors are clones of the vanilla ThresholdSwitchSideScreen prefab, and the species
	/// lists are clones of the vanilla storage-filter row/element prefabs (the tree used by
	/// every storage container), driven directly rather than through their own scripts.
	///
	/// Layout (top to bottom):
	///   current count line
	///   [Combined threshold] [Separate thresholds]
	///   combined threshold editor            (combined mode)
	///   [x] Count Critters
	///       [v] All critters                 (vanilla filter row, collapsed by default)
	///           [x] icon Species ...
	///       critter threshold editor         (separate mode)
	///   [x] Count Eggs
	///       [v] All eggs
	///           [x] icon Egg ...
	///       egg threshold editor             (separate mode)
	/// </summary>
	public sealed class MultichannelCritterSensorSideScreen : SideScreenContent, IRender200ms
	{
		private static readonly FieldInfo SideScreensField = AccessTools.Field(typeof(DetailsScreen), "sideScreens");
		private static readonly FieldInfo CurrentValueField = AccessTools.Field(typeof(ThresholdSwitchSideScreen), "currentValue");
		private static readonly FieldInfo RowPrefabField = AccessTools.Field(typeof(TreeFilterableSideScreen), "rowPrefab");
		private static readonly FieldInfo ElementPrefabField = AccessTools.Field(typeof(TreeFilterableSideScreen), "elementPrefab");
		private static readonly FieldInfo RowNameField = AccessTools.Field(typeof(TreeFilterableSideScreenRow), "elementName");
		private static readonly FieldInfo RowGroupField = AccessTools.Field(typeof(TreeFilterableSideScreenRow), "elementGroup");
		private static readonly FieldInfo RowCheckField = AccessTools.Field(typeof(TreeFilterableSideScreenRow), "checkBoxToggle");
		private static readonly FieldInfo RowArrowField = AccessTools.Field(typeof(TreeFilterableSideScreenRow), "arrowToggle");
		private static readonly FieldInfo RowBgField = AccessTools.Field(typeof(TreeFilterableSideScreenRow), "bgImg");
		private static readonly FieldInfo ElementNameField = AccessTools.Field(typeof(TreeFilterableSideScreenElement), "elementName");
		private static readonly FieldInfo ElementCheckField = AccessTools.Field(typeof(TreeFilterableSideScreenElement), "checkBox");
		private static readonly FieldInfo ElementImageField = AccessTools.Field(typeof(TreeFilterableSideScreenElement), "elementImg");

		private const int Indent = 24;
		/// <summary>Horizontal inset for this screen's own rows; the cloned vanilla widgets carry their own.</summary>
		private const int Inset = 8;
		private const float MaxListHeight = 220f;
		private static readonly Vector2 CheckSize = new Vector2(16f, 16f);

		// Vanilla row checkbox states (TreeFilterableSideScreenRow.State).
		private const int RowOff = 0;
		private const int RowMixed = 1;
		private const int RowOn = 2;

		private sealed class ThresholdBlock
		{
			public GameObject host;
			public ThresholdSwitchSideScreen screen;
			public ThresholdAdapter adapter;
		}

		private sealed class ElementRefs
		{
			public GameObject go;
			public MultiToggle check;
			public KImage checkMark;
		}

		private sealed class SpeciesList
		{
			public bool critters;
			public bool expanded;
			public GameObject toggle;
			/// <summary>Fixed-height wrapper around the scroll pane.</summary>
			public GameObject wrapper;
			public LayoutElement wrapperLayout;
			/// <summary>Scroll content; holds the single vanilla filter row.</summary>
			public GameObject panel;
			// Vanilla row parts.
			public GameObject row;
			public MultiToggle rowCheck;
			public MultiToggle rowArrow;
			public GameObject rowGroup;
			public KImage rowBg;
			public readonly List<Tag> visible = new List<Tag>();
			public readonly Dictionary<Tag, ElementRefs> elements = new Dictionary<Tag, ElementRefs>();
			public ThresholdBlock threshold;
		}

		private MultichannelCritterSensor target;
		private bool built;
		private bool discoverHooked;

		private GameObject root;
		private GameObject header;
		private GameObject combinedButton;
		private GameObject separateButton;
		private ThresholdBlock combined;
		private readonly SpeciesList critters = new SpeciesList { critters = true };
		private readonly SpeciesList eggs = new SpeciesList { critters = false };

		public override bool IsValidForTarget(GameObject go)
		{
			return go != null && go.GetComponent<MultichannelCritterSensor>() != null;
		}

		public override string GetTitle()
		{
			return ModStrings.SideScreenTitle;
		}

		public override int GetSideScreenSortOrder()
		{
			return 1;
		}

		public override void SetTarget(GameObject go)
		{
			base.SetTarget(go);
			target = go != null ? go.GetComponent<MultichannelCritterSensor>() : null;
			if (target == null)
				return;
			EnsureBuilt();
			HookDiscover(true);
			RebuildList(critters);
			RebuildList(eggs);
			RefreshAll();
		}

		public override void ClearTarget()
		{
			base.ClearTarget();
			target = null;
			HookDiscover(false);
		}

		protected override void OnCleanUp()
		{
			HookDiscover(false);
			base.OnCleanUp();
		}

		public void Render200ms(float dt)
		{
			if (target == null || !built || !gameObject.activeInHierarchy)
				return;
			UpdateHeader();
			ResizeList(critters);
			ResizeList(eggs);
		}

		// ---- construction ----

		private void EnsureBuilt()
		{
			if (built)
				return;
			built = true;

			// The frame must not report a preferred width above the vanilla side screen's
			// 280px: the details panel grows to fit, while its Options header stays 280 and
			// ends up right-aligned with bare panel showing on the left. The cloned threshold
			// editor is exactly 280 wide, so the root carries no horizontal margin; this
			// screen's own rows inset themselves instead.
			PPanel rootPanel = new PPanel("MultichannelCritterSensorRoot")
			{
				Direction = PanelDirection.Vertical,
				Alignment = TextAnchor.UpperLeft,
				Spacing = 6,
				Margin = new RectOffset(0, 0, 8, 8),
				FlexSize = Vector2.right,
				DynamicSize = true,
			};

			rootPanel.AddChild(new PLabel("Header")
			{
				Text = " ",
				TextStyle = PUITuning.Fonts.TextDarkStyle,
				TextAlignment = TextAnchor.MiddleLeft,
				Margin = new RectOffset(Inset, Inset, 0, 0),
				FlexSize = Vector2.right,
				DynamicSize = true,
			}.AddOnRealize(go => header = go));

			PPanel modeRow = new PPanel("ModeRow")
			{
				Direction = PanelDirection.Horizontal,
				Alignment = TextAnchor.MiddleLeft,
				Spacing = 6,
				Margin = new RectOffset(Inset, Inset, 0, 0),
				FlexSize = Vector2.right,
				DynamicSize = true,
			};
			modeRow.AddChild(new PLabel("ModeLabel")
			{
				Text = ModStrings.ModeLabel,
				TextStyle = PUITuning.Fonts.TextDarkStyle,
				TextAlignment = TextAnchor.MiddleLeft,
			});
			modeRow.AddChild(new PButton("Combined")
			{
				Text = ModStrings.ModeCombined,
				ToolTip = ModStrings.ModeCombinedTooltip,
				Margin = new RectOffset(8, 8, 5, 5),
				FlexSize = Vector2.right,
				OnClick = _ => SetMode(false),
			}.AddOnRealize(go => combinedButton = go));
			modeRow.AddChild(new PButton("Separate")
			{
				Text = ModStrings.ModeSeparate,
				ToolTip = ModStrings.ModeSeparateTooltip,
				Margin = new RectOffset(8, 8, 5, 5),
				FlexSize = Vector2.right,
				OnClick = _ => SetMode(true),
			}.AddOnRealize(go => separateButton = go));
			rootPanel.AddChild(modeRow);

			combined = new ThresholdBlock();
			rootPanel.AddChild(Host("CombinedThreshold", 0).AddOnRealize(go => combined.host = go));

			AddSpeciesSection(rootPanel, critters, STRINGS.BUILDINGS.PREFABS.LOGICCRITTERCOUNTSENSOR.COUNT_CRITTER_LABEL, ModStrings.CountCrittersTooltip);
			AddSpeciesSection(rootPanel, eggs, STRINGS.BUILDINGS.PREFABS.LOGICCRITTERCOUNTSENSOR.COUNT_EGG_LABEL, ModStrings.CountEggsTooltip);

			root = rootPanel.AddTo(gameObject);

			CreateScrollList(critters);
			CreateScrollList(eggs);

			CreateThresholdEditor(combined, ThresholdAdapter.Kind.Combined);
			CreateThresholdEditor(critters.threshold, ThresholdAdapter.Kind.Critters);
			CreateThresholdEditor(eggs.threshold, ThresholdAdapter.Kind.Eggs);
		}

		private void AddSpeciesSection(PPanel rootPanel, SpeciesList list, string label, string tooltip)
		{
			string prefix = list.critters ? "Critter" : "Egg";
			rootPanel.AddChild(new PCheckBox("Count" + prefix)
			{
				Text = label,
				ToolTip = tooltip,
				TextStyle = PUITuning.Fonts.TextDarkStyle,
				TextAlignment = TextAnchor.MiddleLeft,
				CheckSize = CheckSize,
				Margin = new RectOffset(Inset, Inset, 0, 0),
				FlexSize = Vector2.right,
				OnChecked = (_, __) => ToggleCounting(list),
			}.AddOnRealize(go => list.toggle = go));

			// The scroll wrapper is inserted after this checkbox in CreateScrollList.

			list.threshold = new ThresholdBlock();
			rootPanel.AddChild(Host(prefix + "Threshold", Indent).AddOnRealize(go => list.threshold.host = go));
		}

		private static PPanel Host(string name, int indent)
		{
			return new PPanel(name)
			{
				Direction = PanelDirection.Vertical,
				Alignment = TextAnchor.UpperLeft,
				Margin = new RectOffset(indent, 0, 0, 0),
				FlexSize = Vector2.right,
				DynamicSize = true,
			};
		}

		/// <summary>
		/// Creates the fixed-height scroll wrapper (a plain object whose height is set by
		/// ResizeList) holding the vanilla filter row for this list.
		/// </summary>
		private void CreateScrollList(SpeciesList list)
		{
			string prefix = list.critters ? "Critter" : "Egg";
			list.wrapper = PUIElements.CreateUI(root, prefix + "ListWrapper");
			list.wrapper.transform.SetSiblingIndex(list.toggle.transform.GetSiblingIndex() + 1);
			list.wrapperLayout = list.wrapper.AddComponent<LayoutElement>();
			list.wrapperLayout.flexibleWidth = 1f;
			list.wrapperLayout.flexibleHeight = 0f;
			list.wrapperLayout.minHeight = 0f;
			list.wrapperLayout.preferredHeight = 0f;

			PPanel rows = new PPanel(prefix + "Rows")
			{
				Direction = PanelDirection.Vertical,
				Alignment = TextAnchor.UpperLeft,
				Margin = new RectOffset(0, 0, 0, 0),
				FlexSize = Vector2.right,
				DynamicSize = true,
			}.AddOnRealize(go => list.panel = go);
			GameObject scroll = new PScrollPane(prefix + "Scroll")
			{
				Child = rows,
				ScrollVertical = true,
				ScrollHorizontal = false,
				AlwaysShowVertical = false,
				FlexSize = Vector2.one,
			}.AddTo(list.wrapper);
			PUIElements.SetAnchors(scroll, PUIAnchoring.Stretch, PUIAnchoring.Stretch);

			CreateFilterRow(list);
		}

		/// <summary>
		/// Clones the vanilla TreeFilterableSideScreenRow prefab (the "category" row of the
		/// storage filter tree) and takes over its widgets. The row's own script is removed
		/// because it routes every click through a TreeFilterableSideScreen parent.
		/// </summary>
		private void CreateFilterRow(SpeciesList list)
		{
			TreeFilterableSideScreen treePrefab = FindSideScreenPrefab<TreeFilterableSideScreen>();
			TreeFilterableSideScreenRow rowPrefab = treePrefab != null && RowPrefabField != null ? RowPrefabField.GetValue(treePrefab) as TreeFilterableSideScreenRow : null;
			if (rowPrefab == null)
			{
				Debug.LogWarning("[MultichannelCritterSensor] Vanilla filter row prefab not found; species list unavailable");
				return;
			}
			GameObject rowGo = Util.KInstantiateUI(rowPrefab.gameObject, list.panel, force_active: true);
			rowGo.name = (list.critters ? "Critter" : "Egg") + "FilterRow";
			TreeFilterableSideScreenRow rowScript = rowGo.GetComponent<TreeFilterableSideScreenRow>();
			LocText name = RowNameField.GetValue(rowScript) as LocText;
			list.rowGroup = RowGroupField.GetValue(rowScript) as GameObject;
			list.rowCheck = RowCheckField.GetValue(rowScript) as MultiToggle;
			list.rowArrow = RowArrowField.GetValue(rowScript) as MultiToggle;
			list.rowBg = RowBgField.GetValue(rowScript) as KImage;
			UnityEngine.Object.DestroyImmediate(rowScript);

			if (name != null)
				name.text = list.critters ? ModStrings.AllCritters : ModStrings.AllEggs;
			SpeciesList captured = list;
			if (list.rowCheck != null)
			{
				list.rowCheck.onClick = () => ToggleAll(captured);
				ToolTip tip = list.rowCheck.GetComponent<ToolTip>();
				if (tip != null)
					tip.SetSimpleTooltip(list.critters ? ModStrings.AllCrittersTooltip : ModStrings.AllEggsTooltip);
			}
			if (list.rowArrow != null)
				list.rowArrow.onClick = () => SetExpanded(captured, !captured.expanded);
			// Remove any placeholder elements the prefab may carry.
			if (list.rowGroup != null)
				for (int i = list.rowGroup.transform.childCount - 1; i >= 0; i--)
					Destroy(list.rowGroup.transform.GetChild(i).gameObject);
			list.row = rowGo;
			ApplyExpanded(list);
		}

		private void CreateThresholdEditor(ThresholdBlock block, ThresholdAdapter.Kind kind)
		{
			if (block == null || block.host == null)
				return;
			ThresholdSwitchSideScreen prefab = FindSideScreenPrefab<ThresholdSwitchSideScreen>();
			if (prefab == null)
			{
				Debug.LogWarning("[MultichannelCritterSensor] Vanilla ThresholdSwitchSideScreen prefab not found; threshold editor unavailable");
				return;
			}

			GameObject adapterGo = new GameObject("ThresholdAdapter_" + kind);
			adapterGo.transform.SetParent(gameObject.transform, false);
			block.adapter = adapterGo.AddComponent<ThresholdAdapter>();
			block.adapter.kind = kind;

			GameObject clone = Util.KInstantiateUI(prefab.gameObject, block.host, force_active: false);
			clone.name = "ThresholdEditor_" + kind;
			block.screen = clone.GetComponent<ThresholdSwitchSideScreen>();

			// The prefab sizes itself to a fixed 280px width. Let it stretch to whatever the
			// host offers instead (the vanilla details body does the same to its instance).
			ContentSizeFitter fitter = clone.GetComponent<ContentSizeFitter>();
			if (fitter != null)
				fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			LayoutElement stretch = clone.AddOrGet<LayoutElement>();
			stretch.layoutPriority = 2;
			stretch.minWidth = 0f;
			stretch.preferredWidth = 0f;
			stretch.flexibleWidth = 1f;

			// The vanilla editor shows "Current Count:\n<n>" above its controls; this screen
			// has its own one-line header instead. Keep the label's GameObject active (other
			// mods, e.g. CustomizeBuildings, address the editor's labels by child index) and
			// hide it by disabling the text component and taking it out of the layout.
			LocText currentValue = CurrentValueField != null ? CurrentValueField.GetValue(block.screen) as LocText : null;
			if (currentValue != null)
			{
				currentValue.enabled = false;
				currentValue.gameObject.AddOrGet<LayoutElement>().ignoreLayout = true;
				// The prefab reserves room for the two-line count: its container has a fixed
				// minimum height and the content block a fixed preferred height. Release both
				// so the remaining line sits directly above the toggles.
				Transform container = currentValue.transform.parent;
				if (container != null && container != clone.transform)
				{
					LayoutElement containerLayout = container.GetComponent<LayoutElement>();
					if (containerLayout != null)
					{
						containerLayout.minHeight = -1f;
						containerLayout.preferredHeight = -1f;
					}
					Transform content = container.parent;
					if (content != null && content != clone.transform)
					{
						LayoutElement contentLayout = content.GetComponent<LayoutElement>();
						if (contentLayout != null)
							contentLayout.preferredHeight = -1f;
					}
				}
			}
		}

		private static T FindSideScreenPrefab<T>() where T : SideScreenContent
		{
			if (DetailsScreen.Instance == null || SideScreensField == null)
				return null;
			List<DetailsScreen.SideScreenRef> refs = SideScreensField.GetValue(DetailsScreen.Instance) as List<DetailsScreen.SideScreenRef>;
			if (refs == null)
				return null;
			foreach (DetailsScreen.SideScreenRef r in refs)
			{
				T screen = r.screenPrefab as T;
				if (screen != null)
					return screen;
			}
			return null;
		}

		// ---- species lists ----

		private void HookDiscover(bool hook)
		{
			DiscoveredResources dr = DiscoveredResources.Instance;
			if (dr == null)
				return;
			if (hook && !discoverHooked)
			{
				dr.OnDiscover += OnDiscovered;
				discoverHooked = true;
			}
			else if (!hook && discoverHooked)
			{
				dr.OnDiscover -= OnDiscovered;
				discoverHooked = false;
			}
		}

		private void OnDiscovered(Tag tag, Tag category)
		{
			if (target == null || !built)
				return;
			RebuildList(critters);
			RebuildList(eggs);
			RefreshRows(critters);
			RefreshRows(eggs);
		}

		/// <summary>
		/// Recomputes the visible entries (discovered plus anything currently selected) and
		/// rebuilds the element widgets under the row when the set changed.
		/// </summary>
		private void RebuildList(SpeciesList list)
		{
			if (list.rowGroup == null)
				return;
			List<Tag> visible = list.critters ? CollectCritters() : CollectEggs();
			if (list.elements.Count > 0 && SameTags(visible, list.visible))
				return;

			list.visible.Clear();
			list.visible.AddRange(visible);
			foreach (ElementRefs element in list.elements.Values)
				if (element.go != null)
					Destroy(element.go);
			list.elements.Clear();

			TreeFilterableSideScreen treePrefab = FindSideScreenPrefab<TreeFilterableSideScreen>();
			TreeFilterableSideScreenElement elementPrefab = treePrefab != null && ElementPrefabField != null ? ElementPrefabField.GetValue(treePrefab) as TreeFilterableSideScreenElement : null;
			if (elementPrefab == null)
				return;

			SpeciesList captured = list;
			foreach (Tag tag in list.visible)
			{
				Tag capturedTag = tag;
				GameObject go = Util.KInstantiateUI(elementPrefab.gameObject, list.rowGroup, force_active: true);
				go.name = tag.Name;
				TreeFilterableSideScreenElement script = go.GetComponent<TreeFilterableSideScreenElement>();
				LocText name = ElementNameField.GetValue(script) as LocText;
				MultiToggle check = ElementCheckField.GetValue(script) as MultiToggle;
				KImage image = ElementImageField.GetValue(script) as KImage;
				UnityEngine.Object.DestroyImmediate(script);

				if (name != null)
					name.text = tag.ProperName();
				if (image != null)
				{
					var ui = Def.GetUISprite(tag);
					if (ui != null && ui.first != null)
					{
						image.sprite = ui.first;
						image.color = ui.second;
						image.gameObject.SetActive(true);
					}
					else
					{
						image.gameObject.SetActive(false);
					}
				}
				ElementRefs refs = new ElementRefs { go = go, check = check, checkMark = FindCheckMark(check) };
				if (check != null)
					check.onClick = () => ToggleSpecies(captured, capturedTag);
				list.elements[tag] = refs;
			}
		}

		/// <summary>The check-mark image is the KImage on a child of the checkbox toggle.</summary>
		private static KImage FindCheckMark(MultiToggle check)
		{
			if (check == null)
				return null;
			foreach (KImage image in check.GetComponentsInChildren<KImage>(true))
				if (image.gameObject != check.gameObject)
					return image;
			return null;
		}

		private static bool SameTags(List<Tag> a, List<Tag> b)
		{
			if (a.Count != b.Count)
				return false;
			for (int i = 0; i < a.Count; i++)
				if (a[i] != b[i])
					return false;
			return true;
		}

		/// <summary>Discovered baggable species (the Critter Pick-Up list) plus any selected tag.</summary>
		private List<Tag> CollectCritters()
		{
			HashSet<Tag> set = new HashSet<Tag>();
			if (DiscoveredResources.Instance != null)
				foreach (Tag tag in DiscoveredResources.Instance.GetDiscoveredResourcesFromTag(GameTags.BagableCreature))
					set.Add(tag);
			if (target != null && target.critterTags != null)
				foreach (Tag tag in target.critterTags)
					set.Add(tag);
			List<Tag> result = new List<Tag>(set);
			result.Sort((x, y) => string.Compare(x.ProperName(), y.ProperName(), StringComparison.CurrentCultureIgnoreCase));
			return result;
		}

		/// <summary>Discovered egg prefabs in the incubator's order, plus any selected tag.</summary>
		private List<Tag> CollectEggs()
		{
			HashSet<Tag> selected = new HashSet<Tag>();
			if (target != null && target.eggTags != null)
				foreach (Tag tag in target.eggTags)
					selected.Add(tag);
			List<KeyValuePair<Tag, int>> entries = new List<KeyValuePair<Tag, int>>();
			HashSet<Tag> seen = new HashSet<Tag>();
			foreach (GameObject prefab in Assets.GetPrefabsWithTag(GameTags.Egg))
			{
				if (prefab == null)
					continue;
				Tag tag = prefab.PrefabID();
				if (!seen.Add(tag))
					continue;
				bool discovered = DiscoveredResources.Instance != null && DiscoveredResources.Instance.IsDiscovered(tag);
				if (!discovered && !selected.Contains(tag) && !DebugHandler.InstantBuildMode)
					continue;
				IHasSortOrder sortable = prefab.GetComponent<IHasSortOrder>();
				entries.Add(new KeyValuePair<Tag, int>(tag, sortable != null ? sortable.sortOrder : int.MaxValue));
			}
			foreach (Tag tag in selected)
				if (seen.Add(tag))
					entries.Add(new KeyValuePair<Tag, int>(tag, int.MaxValue));
			entries.Sort((x, y) =>
			{
				int c = x.Value.CompareTo(y.Value);
				return c != 0 ? c : string.Compare(x.Key.ProperName(), y.Key.ProperName(), StringComparison.CurrentCultureIgnoreCase);
			});
			List<Tag> result = new List<Tag>(entries.Count);
			foreach (KeyValuePair<Tag, int> entry in entries)
				result.Add(entry.Key);
			return result;
		}

		// ---- interaction ----

		private void SetMode(bool separate)
		{
			if (target == null)
				return;
			target.separateThresholds = separate;
			target.NotifyConfigChanged();
			RefreshAll();
		}

		private void ToggleCounting(SpeciesList list)
		{
			if (target == null)
				return;
			if (list.critters)
				target.countCritters = !target.countCritters;
			else
				target.countEggs = !target.countEggs;
			target.NotifyConfigChanged();
			RefreshAll();
		}

		private void SetExpanded(SpeciesList list, bool expanded)
		{
			if (list.expanded == expanded)
				return;
			list.expanded = expanded;
			ApplyExpanded(list);
		}

		private static void ApplyExpanded(SpeciesList list)
		{
			if (list.rowArrow != null)
				list.rowArrow.ChangeState(list.expanded ? 1 : 0);
			if (list.rowGroup != null)
				list.rowGroup.SetActive(list.expanded);
			if (list.rowBg != null)
				list.rowBg.enabled = list.expanded;
			ResizeList(list, force: true);
		}

		private void ToggleAll(SpeciesList list)
		{
			if (target == null)
				return;
			bool all = list.critters ? target.allCritters : target.allEggs;
			target.SetAllSpecies(list.critters, !all);
			RefreshRows(list);
			UpdateHeader();
		}

		private void ToggleSpecies(SpeciesList list, Tag tag)
		{
			if (target == null)
				return;
			target.ToggleSpecies(list.critters, tag, list.visible);
			RefreshRows(list);
			UpdateHeader();
		}

		// ---- refresh ----

		private void RefreshAll()
		{
			if (target == null || !built)
				return;
			bool separate = target.separateThresholds;
			SetButtonSelected(combinedButton, !separate);
			SetButtonSelected(separateButton, separate);

			ShowThreshold(combined, !separate);
			RefreshSection(critters, target.countCritters, separate);
			RefreshSection(eggs, target.countEggs, separate);
			UpdateHeader();
		}

		private void RefreshSection(SpeciesList list, bool counting, bool separate)
		{
			if (list.toggle != null)
				PCheckBox.SetCheckState(list.toggle, counting ? PCheckBox.STATE_CHECKED : PCheckBox.STATE_UNCHECKED);
			if (list.wrapper != null)
				list.wrapper.SetActive(counting);
			if (counting)
			{
				RefreshRows(list);
				ResizeList(list, force: true);
			}
			ShowThreshold(list.threshold, separate && counting);
		}

		/// <summary>Pushes the model's selection into the row checkbox (tri-state) and elements.</summary>
		private void RefreshRows(SpeciesList list)
		{
			if (target == null)
				return;
			bool all = list.critters ? target.allCritters : target.allEggs;
			if (list.rowCheck != null)
			{
				int state = all ? RowOn : (target.AnySpeciesSelected(list.critters) ? RowMixed : RowOff);
				list.rowCheck.ChangeState(state);
			}
			foreach (KeyValuePair<Tag, ElementRefs> entry in list.elements)
			{
				bool selected = target.IsSpeciesSelected(list.critters, entry.Key);
				if (entry.Value.check != null)
					entry.Value.check.ChangeState(selected ? 1 : 0);
				if (entry.Value.checkMark != null)
					entry.Value.checkMark.enabled = selected;
			}
		}

		/// <summary>
		/// Sizes the scroll wrapper to the row's preferred height (collapsed: just the
		/// header row; expanded: header plus elements), capped so long lists scroll.
		/// </summary>
		private static void ResizeList(SpeciesList list, bool force = false)
		{
			if (list.wrapperLayout == null || list.panel == null || list.wrapper == null || !list.wrapper.activeSelf)
				return;
			RectTransform rect = list.panel.transform as RectTransform;
			if (rect == null)
				return;
			if (force)
				LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
			float wanted = Mathf.Min(LayoutUtility.GetPreferredHeight(rect) + 4f, MaxListHeight);
			if (wanted <= 0f || Mathf.Approximately(wanted, list.wrapperLayout.preferredHeight))
				return;
			list.wrapperLayout.minHeight = wanted;
			list.wrapperLayout.preferredHeight = wanted;
		}

		private void ShowThreshold(ThresholdBlock block, bool visible)
		{
			if (block == null || block.host == null)
				return;
			block.host.SetActive(visible && block.screen != null);
			if (!visible || block.screen == null)
			{
				if (block.screen != null && block.screen.gameObject.activeSelf)
					block.screen.Show(false);
				return;
			}
			block.adapter.sensor = target;
			block.screen.SetTarget(block.adapter.gameObject);
			block.screen.Show(true);
		}

		private static void SetButtonSelected(GameObject button, bool selected)
		{
			if (button == null)
				return;
			KImage image = button.GetComponent<KImage>();
			if (image == null)
				return;
			image.colorStyleSetting = selected ? PUITuning.Colors.ButtonPinkStyle : PUITuning.Colors.ButtonBlueStyle;
			image.ApplyColorStyleSetting();
		}

		private void UpdateHeader()
		{
			if (header == null || target == null)
				return;
			string text;
			if (!target.InRoom)
				text = ModStrings.NotInRoom;
			else if (target.countCritters && target.countEggs)
				text = string.Format(ModStrings.CurrentCritters, target.CritterCount) + "    " + string.Format(ModStrings.CurrentEggs, target.EggCount);
			else if (target.countCritters)
				text = string.Format(ModStrings.CurrentCritters, target.CritterCount);
			else if (target.countEggs)
				text = string.Format(ModStrings.CurrentEggs, target.EggCount);
			else
				text = ModStrings.NotCounting;
			PUIElements.SetText(header, text);
		}
	}
}
