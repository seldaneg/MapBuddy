using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ImGuiNET;
using ExileCore2.Shared.Helpers;
using System.Numerics;
using System.Drawing;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.MemoryObjects;
using Map = ExileCore2.PoEMemory.Components.Map;

namespace MapBuddy
{
    public sealed class MapBuddy : BaseSettingsPlugin<Settings>
    {
        // Action states
        private Vector2 _windowOffset;
        private bool _isIdentifying = false;
        private bool _isCrafting = false;
        private bool _isThrowingItem = false;
		private bool _isFillingCurrency = false;
        private DateTime _lastIdentifyAttempt = DateTime.MinValue;
        private DateTime _lastCraftAttempt = DateTime.MinValue;
        private const int MinTimeBetweenActions = 500;

        // Logging
        private List<string> _debugLog = new();
        private const int MaxDebugLogLines = 100;

        // Statistics
        private int _totalIdentified;
        private int _sessionIdentified;
        private int _totalCrafted;
        private int _sessionCrafted;
        private int _totalThrown;
        private int _sessionThrown;
        private DateTime _sessionStart;

        // Currency paths
        private const string SCROLL_PATH = "Metadata/Items/Currency/CurrencyIdentification";
        private const string ALCHEMY_PATH = "Metadata/Items/Currency/CurrencyUpgradeToRare";
        private const string TRANSMUTATION_PATH = "Metadata/Items/Currency/CurrencyUpgradeToMagic";
        private const string AUGMENTATION_PATH = "Metadata/Items/Currency/CurrencyAddModToMagic";
        private const string REGAL_PATH = "Metadata/Items/Currency/CurrencyUpgradeMagicToRare";

        // Special item paths
        private const string WAYSTONE_PATH = "Metadata/Items/AtlasUpgrades/AtlasRegionUpgrade";
        private const string PRECURSOR_PATH = "Metadata/Items/TowerAugment/";  
		
		
		private const int MIN_INPUT_INTERVAL = 50; // ms
		private DateTime _lastInputTime = DateTime.MinValue;
		private bool _isTransitioning = false;
		
		
		
		private Dictionary<string, int> GetMaxStackSizes()
		{
			return new Dictionary<string, int>
			{
				{ SCROLL_PATH, Settings.ScrollStackSize.Value },
				{ ALCHEMY_PATH, Settings.AlchemyStackSize.Value },
				{ TRANSMUTATION_PATH, Settings.TransmutationStackSize.Value },
				{ AUGMENTATION_PATH, Settings.AugmentationStackSize.Value },
				{ REGAL_PATH, Settings.RegalStackSize.Value }
			};
		}
		

        public override bool Initialise()
        {
            Name = "MapBuddy";
            _windowOffset = GameController.Window.GetWindowRectangle().TopLeft;
            _sessionStart = DateTime.Now;
            return true;
        }

        private void LogDebug(string message)
        {
            if (!Settings.ShowDebugWindow) return;
            
            _debugLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            while (_debugLog.Count > MaxDebugLogLines)
                _debugLog.RemoveAt(0);
        }

        public override void Tick()
        {
            if (!GameController.Window.IsForeground())
            {
                _isIdentifying = false;
                _isCrafting = false;
                _isThrowingItem = false;
            }
        }
		
		
		public override void Render()
		{
			if (!Settings.Enable.Value) return;

			if (Settings.ShowIdentificationWindow)
				DrawMainWindow();
			if (Settings.ShowDebugWindow)
				DrawDebugWindow();

			var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
			var now = DateTime.Now;

			// Handle throwing items with hotkey
			if (Settings.ThrowHotkey.PressedOnce())
			{
				HandleItemThrow();
				return;
			}

			// Don't process other actions if inventory isn't visible
			if (!inventoryPanel.IsVisible) return;

			// Don't process if we're in the middle of an action
			if (_isIdentifying || _isCrafting || _isThrowingItem || 
				(now - _lastIdentifyAttempt).TotalMilliseconds < MinTimeBetweenActions ||
				(now - _lastCraftAttempt).TotalMilliseconds < MinTimeBetweenActions)
				return;

			

			// Combined hotkey handling for both identification and crafting
			if (Settings.HotKey.PressedOnce())
			{
				if (Settings.Enable.Value)
				{
					_isIdentifying = true;
					try
					{
						IdentifyItems();
						
						// If crafting is enabled, proceed with crafting after identification
						if (Settings.EnableCrafting)
						{
							_isCrafting = true;
							CraftItems();
						}
						
						
						// Check for currency auto-fill when stash is open
						if (Settings.AutoFillCurrency && 
							GameController.IngameState.IngameUi.StashElement.IsVisible)
						{
							TryFillCurrencyFromStash();
						}
						
					}
					catch (Exception ex)
					{
						LogError($"Error during processing: {ex.Message}");
						LogDebug($"Exception: {ex}");
					}
					finally
					{
						_isIdentifying = false;
						_isCrafting = false;
						_lastIdentifyAttempt = now;
						_lastCraftAttempt = now;
					}
				}
			}
		}

        private void DrawMainWindow()
        {
            var showWindow = Settings.ShowIdentificationWindow.Value;
            if (ImGui.Begin("MapBuddy", ref showWindow))
            {
                Settings.ShowIdentificationWindow.Value = showWindow;
                
                ImGui.Text($"Session Runtime: {(DateTime.Now - _sessionStart):hh\\:mm\\:ss}");
                
                if (ImGui.CollapsingHeader("Statistics"))
                {
                    ImGui.Text($"Items Identified (Session/Total): {_sessionIdentified}/{_totalIdentified}");
                    ImGui.Text($"Items Crafted (Session/Total): {_sessionCrafted}/{_totalCrafted}");
                    ImGui.Text($"Items Thrown (Session/Total): {_sessionThrown}/{_totalThrown}");
                }

                ImGui.Separator();
                
                if (ImGui.CollapsingHeader("Settings"))
                {
                    var identifyAll = Settings.IdentifyAll.Value;
                    if(ImGui.Checkbox("Identify ALL Items (Override other settings)", ref identifyAll))
                    {
                        Settings.IdentifyAll.Value = identifyAll;
                    }

                    ImGui.BeginDisabled(Settings.IdentifyAll.Value);
                    
                    var identifyMagic = Settings.IdentifyMagicItems.Value;
                    if(ImGui.Checkbox("Identify Magic Items", ref identifyMagic))
                    {
                        Settings.IdentifyMagicItems.Value = identifyMagic;
                    }

                    var identifyRares = Settings.IdentifyRares.Value;
                    if(ImGui.Checkbox("Identify Rares", ref identifyRares))
                    {
                        Settings.IdentifyRares.Value = identifyRares;
                    }

                    var identifyUniques = Settings.IdentifyUniques.Value;
                    if(ImGui.Checkbox("Identify Uniques", ref identifyUniques))
                    {
                        Settings.IdentifyUniques.Value = identifyUniques;
                    }

                    var identifyMaps = Settings.IdentifyMaps.Value;
                    if(ImGui.Checkbox("Identify Maps", ref identifyMaps))
                    {
                        Settings.IdentifyMaps.Value = identifyMaps;
                    }

                    ImGui.EndDisabled();

                    if (ImGui.CollapsingHeader("Crafting Settings"))
                    {
                        var enableCrafting = Settings.EnableCrafting.Value;
                        if(ImGui.Checkbox("Enable Crafting", ref enableCrafting))
                        {
                            Settings.EnableCrafting.Value = enableCrafting;
                        }
                    }
                }

                if (_isIdentifying)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FF00);
                    ImGui.Text("Identifying...");
                    ImGui.PopStyleColor();
                }
                else if (_isCrafting)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FF00);
                    ImGui.Text("Crafting...");
                    ImGui.PopStyleColor();
                }

                ImGui.End();
            }
        }

        private void DrawDebugWindow()
        {
            var showDebug = Settings.ShowDebugWindow.Value;
            if (ImGui.Begin("MapBuddy Debug", ref showDebug))
            {
                Settings.ShowDebugWindow.Value = showDebug;
                
                if (ImGui.Button("Clear Log"))
                    _debugLog.Clear();

                ImGui.BeginChild("DebugLog", new Vector2(0, 0), ImGuiChildFlags.None);
                foreach (var line in _debugLog)
                    ImGui.TextWrapped(line);
                if (_debugLog.Any())
                    ImGui.SetScrollHereY(1.0f);
                ImGui.EndChild();

                ImGui.End();
            }
        }
		
		private class CraftableItem
        {
            public NormalInventoryItem Item { get; set; }
            public bool IsMap { get; set; }
			public bool IsWaystone { get; set; }
            public bool IsPrecursorTablet { get; set; }
            public ItemRarity Rarity { get; set; }
            public int AffixCount { get; set; }
        }

        private void IdentifyItems()
        {
            var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
            var playerInventory = inventoryPanel[InventoryIndex.PlayerInventory];
            var latency = 100;

            var scrollOfWisdom = GetItemWithBaseName(SCROLL_PATH, playerInventory.VisibleInventoryItems);
            if (scrollOfWisdom == null)
            {
                LogDebug("No Scroll of Wisdom found");
                return;
            }

            var itemsToIdentify = new List<NormalInventoryItem>();
            var inventoryItems = new List<NormalInventoryItem>(playerInventory.VisibleInventoryItems);

            if (Settings.IdentifyStashItems && 
                GameController.IngameState.IngameUi.StashElement.IsVisible)
            {
                inventoryItems.AddRange(
                    GameController.IngameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems);
            }

            foreach (var item in inventoryItems)
            {
                if (!item.Item.HasComponent<Mods>())
                    continue;
				

                var mods = item.Item.GetComponent<Mods>();
				
				var baseItem = item.Item;
					
					
					
				// Skip corrupted items
				if (baseItem.GetComponent<Base>().isCorrupted)
				{
					LogDebug($"Skipping corrupted item: {baseItem.Path}");
					continue;
				}
					
				
                if (mods.Identified)
                    continue;

                if (!ShouldIdentifyItem(item))
                    continue;

                itemsToIdentify.Add(item);
            }

            if (!itemsToIdentify.Any())
            {
                LogDebug("No items to identify found");
                return;
            }

            LogDebug($"Found {itemsToIdentify.Count} items to identify");
            PerformIdentification(scrollOfWisdom, itemsToIdentify);
        }

        private void PerformIdentification(NormalInventoryItem scroll, List<NormalInventoryItem> items)
        {
            var scrollPos = scroll.GetClientRect().Center;
            Input.SetCursorPos(new Vector2(scrollPos.X + _windowOffset.X, scrollPos.Y + _windowOffset.Y));
            Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
            Input.Click(MouseButtons.Right);
            Thread.Sleep(Constants.INPUT_DELAY);

            Input.KeyDown(Keys.LShiftKey);
            try
            {
                foreach (var item in items)
                {
                    if (Settings.ShowDebugWindow)
                        Graphics.DrawFrame(item.GetClientRect(), Color.Aqua, 2);

                    var itemPos = item.GetClientRect().Center;
                    Input.SetCursorPos(new Vector2(itemPos.X + _windowOffset.X, itemPos.Y + _windowOffset.Y));
                    Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
                    Input.Click(MouseButtons.Left);
                    Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);

                    _sessionIdentified++;
                    _totalIdentified++;
                }
            }
            finally
            {
                Input.KeyUp(Keys.LShiftKey);
            }
        }

        private void CraftItems()
		{
			var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
			var playerInventory = inventoryPanel[InventoryIndex.PlayerInventory];
			
			var inventoryItems = new List<NormalInventoryItem>(playerInventory.VisibleInventoryItems);
			if (Settings.IdentifyStashItems && 
				GameController.IngameState.IngameUi.StashElement.IsVisible)
			{
				inventoryItems.AddRange(
					GameController.IngameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems);
			}

			var craftableItems = GetCraftableItems(inventoryItems);
			
			// Process normal items first (Alchemy/Transmute)
			ProcessNormalItems(craftableItems.Where(x => x.Rarity == ItemRarity.Normal).ToList());
			
			// Process magic items (Augment)
			ProcessMagicItems(craftableItems.Where(x => x.Rarity == ItemRarity.Magic).ToList());
			
			// Process magic maps (Regal)
			//ProcessMagicMaps(craftableItems.Where(x => x.Rarity == ItemRarity.Magic && x.IsMap).ToList());
		}

        private void ProcessNormalItems(List<CraftableItem> items)
		{
			if (!Settings.AlchemyNormalItems.Value && !Settings.AlchemyWaystones.Value && !Settings.TransmutePrecursorTablets.Value)
			{
				LogDebug("All normal item processing is disabled in settings");
				return;
			}

			foreach (var item in items)
			{
				// Handle Precursor Tablets
				if (item.IsPrecursorTablet)
				{
					if (!Settings.TransmutePrecursorTablets.Value)
					{
						LogDebug($"Skipping precursor tablet: transmute setting disabled");
						continue;
					}

					if (TryGetCurrency(TRANSMUTATION_PATH, out var transmute))
					{
						ApplyCurrency(transmute, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
						
						// Now immediately apply Augmentation if enabled
						if (Settings.AugmentMagicItems.Value && TryGetCurrency(AUGMENTATION_PATH, out var augment))
						{
							ApplyCurrency(augment, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
						}
					}
				}
				// Handle Waystones
				else if (item.IsWaystone)
				{
					if (!Settings.AlchemyWaystones.Value)
					{
						LogDebug($"Skipping waystone: transmute setting disabled");
						continue;
					}

					if (TryGetCurrency(ALCHEMY_PATH, out var alchemy))
					{
						ApplyCurrency(alchemy, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
					}
				}
				// Handle other normal items
				else
				{
					if (!Settings.AlchemyNormalItems.Value)
					{
						LogDebug($"Skipping normal item: alchemy setting disabled");
						continue;
					}

					var baseComponent = item.Item.Item.GetComponent<Base>();
					var baseName = baseComponent?.Name ?? "";
					LogDebug($"Processing normal item: {baseName}");

					// Skip specific base types
					if (baseName.Contains("Stellar Amulet") || 
						baseName.Contains("Heavy Belt") || 
						baseName.Contains("Attuned Wand") || 
						baseName.Contains("Sapphire Ring") ||
						baseName.Contains("Expedition Logbook") 
						)
					{
						LogDebug($"Skipping alchemy on filtered base type: {baseName}");
						continue;
					}

					if (TryGetCurrency(ALCHEMY_PATH, out var alchemy))
					{
						ApplyCurrency(alchemy, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
					}
				}
			}
		}

		
		
		private void ProcessMagicItems(List<CraftableItem> items)
		{
			// Early exit if all relevant settings are disabled
			if (!Settings.AugmentMagicItems.Value && !Settings.RegalMagicMaps.Value)
			{
				LogDebug("All magic item processing is disabled in settings");
				return;
			}

			var processedIds = new HashSet<string>();
			
			// Get all magic maps and precursor tablets
			var magicItems = items.Where(x => (x.IsMap || x.IsPrecursorTablet) && x.Rarity == ItemRarity.Magic).ToList();
			
			foreach (var item in magicItems)
			{
				var itemId = item.Item.GetHashCode().ToString();
				if (processedIds.Contains(itemId)) continue;
				processedIds.Add(itemId);

				// Debug log the item details
				LogDebug($"Processing item - Path: {item.Item.Item.Path}, IsPrecursor: {item.IsPrecursorTablet}, IsMap: {item.IsMap}");
				
				// Get explicit count of mods for better accuracy
				var mods = item.Item.Item.GetComponent<Mods>();
				var explicitCount = mods.ExplicitMods.Count();
				LogDebug($"Explicit mod count: {explicitCount}");

				// Handle items with 1 explicit mod
				if (explicitCount == 1)
				{
					// Only augment if setting is enabled
					if (Settings.AugmentMagicItems.Value)
					{
						LogDebug("Found item with 1 explicit mod, applying Augmentation");
						if (TryGetCurrency(AUGMENTATION_PATH, out var augment))
						{
							ApplyCurrency(augment, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
							
							// Only apply Regal to Maps if setting is enabled
							if (!item.IsPrecursorTablet && Settings.RegalMagicMaps.Value)
							{                        
								LogDebug("Map has 2 explicit mods, applying Regal");
								if (TryGetCurrency(REGAL_PATH, out var regal))
								{
									ApplyCurrency(regal, item.Item);
									Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
								}
							}
						}
					}
				}
				// Handle items with 2 mods
				else if (explicitCount == 2 && !item.IsPrecursorTablet && Settings.RegalMagicMaps.Value)
				{
					LogDebug("Applying Regal Orb to map with 2 explicit mods");
					if (TryGetCurrency(REGAL_PATH, out var regal))
					{
						ApplyCurrency(regal, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
					}
				}
			}
		}

        private void ProcessMagicMaps(List<CraftableItem> items)
        {
            var twoAffixMaps = items.Where(x => x.AffixCount == 2 && x.IsMap);
            
            if (TryGetCurrency(REGAL_PATH, out var regal))
            {
                foreach (var item in twoAffixMaps)
                {
                    ApplyCurrency(regal, item.Item);
                }
            }
        }
		
		
		private List<CraftableItem> GetCraftableItems(IEnumerable<NormalInventoryItem> items)
		{
			var craftableItems = new List<CraftableItem>();

			foreach (var invItem in items)
			{
				var baseItem = invItem.Item;  // Get the base item first
				if (!baseItem.HasComponent<Mods>())
					continue;

				var mods = baseItem.GetComponent<Mods>();
				
				// Skip corrupted items
				if (baseItem.GetComponent<Base>().isCorrupted)
				{
					LogDebug($"Skipping corrupted item: {baseItem.Path}");
					continue;
				}
				
				var path = baseItem.Path;
				
				// More specific path checking
				var isMap = baseItem.HasComponent<Map>();
				var isPrecursorTablet = path.StartsWith(PRECURSOR_PATH, StringComparison.OrdinalIgnoreCase);

				LogDebug($"Found item - Path: {path}, IsMap: {isMap}, IsPrecursor: {isPrecursorTablet}");

				craftableItems.Add(new CraftableItem
				{
					Item = invItem,
					IsMap = isMap,
					IsWaystone = isMap, // Since these are the same
					IsPrecursorTablet = isPrecursorTablet,
					Rarity = mods.ItemRarity,
					AffixCount = mods.ImplicitMods.Count() + mods.ExplicitMods.Count()
				});
			}

			return craftableItems;
		}

		private NormalInventoryItem GetItemWithBaseName(string path, IEnumerable<NormalInventoryItem> items)
		{
			try
			{
				return items.FirstOrDefault(invItem => invItem.Item.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
			}
			catch (Exception ex)
			{
				LogDebug($"Error finding item {path}: {ex.Message}");
				return null;
			}
		}
		

        private void TryFillCurrencyFromStash()
		{
			if (_isFillingCurrency)
			{
				LogDebug("Already filling currency, skipping");
				return;
			}

			LogDebug("Starting currency fill process");
			var stashElement = GameController.IngameState.IngameUi.StashElement;
			if (!stashElement.IsVisibleLocal)
			{
				LogDebug("Stash is not visible");
				return;
			}

			_isFillingCurrency = true;
			try
			{
				LogDebug($"Attempting to switch to currency tab: {Settings.CurrencyTabName}");
				if (!TryGetStashTabByName(Settings.CurrencyTabName.Value))  
					return;

				Thread.Sleep(250); // Longer wait for stash to update
				ProcessCurrencyStacks();
				//Thread.Sleep(500); // Wait for inventory to update
				//ProcessCurrencyStacks(); // Second pass to handle any remaining stacks
			}
			finally
			{
				_isFillingCurrency = false;
				LogDebug("Currency fill process complete");
			}
		}

		private void ProcessCurrencyStacks()
		{
			var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
			var stashElement = GameController.IngameState.IngameUi.StashElement;
			
			foreach (var kvp in GetMaxStackSizes())
			{
				var currencyPath = kvp.Key;
				var maxStack = kvp.Value;
				
				// Skip if stack size is set to 0 in settings
				if (maxStack == 0)
				{
					LogDebug($"Skipping {currencyPath} (disabled in settings: stack size 0)");
					continue;
				}

				var currencyInInventory = GetItemWithBaseName(currencyPath, 
					inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems);
					
				// Skip if we already have enough of this currency
				if (currencyInInventory != null)
				{
					var stack = currencyInInventory.Item.GetComponent<Stack>();
					if (stack != null && stack.Size >= maxStack)
					{
						LogDebug($"Skipping {currencyPath} (already have {stack.Size} >= {maxStack})");
						continue;
					}
				}

				var currencyInStash = GetItemWithBaseName(currencyPath, 
					stashElement.VisibleStash.VisibleInventoryItems);
					
				if (currencyInStash != null)
				{
					LogDebug($"Taking {currencyPath} from stash (target: {maxStack})");
					var pos = currencyInStash.GetClientRect().Center;
					Input.SetCursorPos(new Vector2(pos.X + _windowOffset.X, pos.Y + _windowOffset.Y));
					Thread.Sleep(Constants.INPUT_DELAY);
					Input.KeyDown(Keys.LControlKey);
					Input.Click(MouseButtons.Left);
					Input.KeyUp(Keys.LControlKey);
					Thread.Sleep(Constants.CLICK_DELAY);
					
					// Verification logic
					int attempts = 0;
					bool stackFound = false;
					while (attempts < 10 && !stackFound)
					{
						Thread.Sleep(100);
						var freshInventory = GameController.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
						var newStacks = freshInventory.VisibleInventoryItems
							.Count(x => x.Item.Path.Contains(currencyPath));
						
						LogDebug($"Attempt {attempts + 1}: Found {newStacks} stacks of {currencyPath}");
						if (newStacks > 0)
						{
							stackFound = true;
							LogDebug("Stack successfully taken from stash");
						}
						attempts++;
					}
				}
			}
		}

		private void HandleItemThrow()
		{
			if (_isThrowingItem) return;

			var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
			if (!inventoryPanel.IsVisible)
			{
				LogDebug("Inventory panel not visible");
				return;
			}

			var playerInventory = inventoryPanel[InventoryIndex.PlayerInventory];
			
			var uiHover = GameController.Game.IngameState.UIHover;
			if (uiHover == null)
			{
				LogDebug("No UI element under cursor");
				return;
			}

			var hoveredItem = uiHover.AsObject<NormalInventoryItem>();
			if (hoveredItem?.Item == null)
			{
				LogDebug("Cursor not over an inventory item");
				return;
			}

			// Additional check to ensure we're hovering over an item in the player's inventory
			if (!playerInventory.VisibleInventoryItems.Contains(hoveredItem))
			{
				LogDebug("Hovered item not in player inventory");
				return;
			}

			_isThrowingItem = true;
			var originalPos = Input.MousePosition;
			try
			{
				LogDebug("Starting item throw sequence");
				
				// Pick up item
				Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
				Input.Click(MouseButtons.Left);
				Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
				LogDebug("Clicked to pick up item");

				// Move to center of screen
				var screenCenter = new Vector2(
					GameController.Window.GetWindowRectangle().Width / 2f,
					GameController.Window.GetWindowRectangle().Height / 2f
				);
				Input.SetCursorPos(screenCenter);
				Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 2);
				LogDebug("Moved to screen center");
				
				// Drop item
				Input.Click(MouseButtons.Left);
				Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);
				LogDebug("Clicked to drop item");

				// If in town or hideout, click the  Destroy button position
				var area = GameController.Area.CurrentArea;
				if (area.IsTown || area.IsHideout)
				{
					LogDebug("In town/hideout, clicking Destroy button");
					Thread.Sleep(250); // Wait for dialog
					
					// Click the  Destroy button position
					Input.SetCursorPos(new Vector2(Settings.DestroyButtonX.Value + _windowOffset.X, Settings.DestroyButtonY.Value + _windowOffset.Y));
					Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
					Input.Click(MouseButtons.Left);
					LogDebug("Clicked Destroy button position");
				}

				_sessionThrown++;
				_totalThrown++;
			}
			catch (Exception ex)
			{
				LogDebug($"Error in throw sequence: {ex.Message}");
			}
			finally
			{
				// Return to original position
				Input.SetCursorPos(originalPos);
				LogDebug("Returned to original position");
				_isThrowingItem = false;
			}
		}


        private bool TryGetCurrency(string path, out NormalInventoryItem currency)
        {
            var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
            var playerInventory = inventoryPanel[InventoryIndex.PlayerInventory];
            
            currency = GetItemWithBaseName(path, playerInventory.VisibleInventoryItems);
            
            if (currency == null)
            {
                LogDebug($"Currency not found: {path}");
                return false;
            }
            
            return true;
        }

        private void ApplyCurrency(NormalInventoryItem currency, NormalInventoryItem target)
		{
			var currencyPos = currency.GetClientRect().Center;
			var targetPos = target.GetClientRect().Center;
			
			// Right click currency
			Input.SetCursorPos(new Vector2(currencyPos.X + _windowOffset.X, currencyPos.Y + _windowOffset.Y));
			Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 2);
			Input.Click(MouseButtons.Right);
			Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);
			
			// Apply to target
			Input.SetCursorPos(new Vector2(targetPos.X + _windowOffset.X, targetPos.Y + _windowOffset.Y));
			Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);
			Input.Click(MouseButtons.Left);
			Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 2);

			_sessionCrafted++;
			_totalCrafted++;
		}

       private bool TryGetStashTabByName(string name)  // name is already a string
		{
			var stashPanel = GameController.Game.IngameState.IngameUi.StashElement;
			var allStashNames = stashPanel.AllStashNames.ToList();  
			var currentIndex = stashPanel.IndexVisibleStash;

			LogDebug($"Searching for tab: {name}");

			// Find the index of the currency tab
			var targetIndex = allStashNames.FindIndex(x => 
				x.Equals(name, StringComparison.OrdinalIgnoreCase));

			if (targetIndex == -1)
			{
				LogDebug($"Could not find stash tab: {name}");
				return false;
			}

			if (targetIndex == currentIndex)
			{
				LogDebug("Already on correct tab");
				return true;
			}

			// Get child element at index
			var tabElement = GameController.Game.IngameState.IngameUi.StashElement
				.ViewAllStashPanel?.Children?[targetIndex];

			if (tabElement == null)
			{
				LogDebug("Tab element not found");
				return false;
			}

			// Click the tab
			var center = tabElement.GetClientRect().Center;
			Input.SetCursorPos(center + _windowOffset);
			Thread.Sleep(Constants.INPUT_DELAY);
			Input.Click(MouseButtons.Left);
			Thread.Sleep(Constants.CLICK_DELAY);

			return true;
		}

		public override void ReceiveEvent(string eventId, object args)
		{
			if (!Settings.Enable.Value) return;

			switch (eventId)
			{
				case "switch_to_tab":
					var index = (int)args;
					if (GameController.Game.IngameState.IngameUi.StashElement.IsVisibleLocal)
					{
						// Get the tab element and simulate clicking it
						var stashElement = GameController.Game.IngameState.IngameUi.StashElement;
						var visibleStashPanel = stashElement.ViewAllStashPanel;
						if (visibleStashPanel != null && index < visibleStashPanel.Children.Count)
						{
							var tabElement = visibleStashPanel.Children[index];
							var tabPos = tabElement.GetClientRect().Center;
							
							Input.SetCursorPos(new Vector2(tabPos.X + _windowOffset.X, tabPos.Y + _windowOffset.Y));
							Thread.Sleep(Constants.INPUT_DELAY + Settings.ExtraDelay);
							Input.Click(MouseButtons.Left);
							Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
						}
					}
					break;
			}
		}

        private bool ShouldIdentifyItem(NormalInventoryItem item)
        {
            if (Settings.IdentifyAll)
                return true;

            var mods = item.Item.GetComponent<Mods>();
            
            switch (mods.ItemRarity)
            {
                case ItemRarity.Magic when !Settings.IdentifyMagicItems:
                case ItemRarity.Rare when !Settings.IdentifyRares:
                case ItemRarity.Unique when !Settings.IdentifyUniques:
                case ItemRarity.Normal:
                    return false;
            }

            if (!Settings.IdentifyMaps && item.Item.HasComponent<Map>())
                return false;

            return true;
        }
    }
}
