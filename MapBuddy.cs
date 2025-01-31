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
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Interfaces;
using ImGuiNET;



namespace MapBuddy
{
	public class MapInfo
	{
		public string Name { get; private set; }
		public List<(string Name, string DisplayName, bool IsPrefix)> Mods { get; private set; } = new();
		public int PrefixCount { get; private set; }
		public int SuffixCount { get; private set; }
		public ItemRarity Rarity { get; private set; }
		public bool IsCorrupted { get; private set; }
		public bool IsIdentified { get; private set; }

		public static async Task<MapInfo> FromItem(NormalInventoryItem item)
		{
			if (item?.Item == null) return null;

			var info = new MapInfo();
			var baseComponent = item.Item.GetComponent<Base>();
			var modsComponent = item.Item.GetComponent<Mods>();

			if (baseComponent == null || modsComponent == null) return null;

			var mapComponent = item.Item.GetComponent<Map>();
			info.Name = baseComponent.Name;
			
			
			info.Rarity = modsComponent.ItemRarity;
			info.IsCorrupted = baseComponent.isCorrupted;
			info.IsIdentified = modsComponent.Identified;

			// Process mods
			var allMods = modsComponent.ItemMods?.ToList() ?? new List<ItemMod>();
			if (allMods.Count == 0)
			{
				// Wait a bit and try again in case the mods haven't updated yet
				await Task.Delay(100);
				allMods = modsComponent.ItemMods?.ToList() ?? new List<ItemMod>();
			}

			foreach (var mod in allMods)
			{
				
				//info.AllMods.Add((mod.Name, mod.DisplayName, !mod.DisplayName.StartsWith("of", StringComparison.OrdinalIgnoreCase)));
				
				 // Skip handling Delirium mod completely
				if (mod.Name == "InstilledMapDelirium") 
				{
					continue;
				}
				
				var isPrefix = !mod.DisplayName.StartsWith("of", StringComparison.OrdinalIgnoreCase);
				info.Mods.Add((mod.Name, mod.DisplayName, isPrefix));
			}

			// Count prefixes and suffixes
			info.PrefixCount = info.Mods.Count(m => m.IsPrefix && m.Name != "InstilledMapDelirium");
			info.SuffixCount = info.Mods.Count(m => !m.IsPrefix && m.Name != "InstilledMapDelirium");

			return info;
		}
	}
	
	
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
		private bool _stopCrafting = false;   // Tracks if crafting should be interrupted

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
		private const string VAAL_PATH = "Metadata/Items/Currency/CurrencyCorrupt";
		private const string EXALT_PATH = "Metadata/Items/Currency/CurrencyAddModToRare";
		
		private const string DISTILLEDIRE_PATH = "Metadata/Items/Currency/DistilledEmotion1";
		private const string DISTILLEDGUILT_PATH = "Metadata/Items/Currency/DistilledEmotion2";
		private const string DISTILLEDGREED_PATH = "Metadata/Items/Currency/DistilledEmotion3";
		private const string DISTILLEDPARANOIA_PATH = "Metadata/Items/Currency/DistilledEmotion4";
		private const string DISTILLEDENVY_PATH = "Metadata/Items/Currency/DistilledEmotion5";


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

        public void LogDebug(string message)
        {
            if (!Settings.ShowDebugWindow) return;
            
            _debugLog.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            while (_debugLog.Count > MaxDebugLogLines)
                _debugLog.RemoveAt(0);
        }

        public override void Tick()
        {
			// Add this check to handle the hotkey
			if (Settings.StopCraftingHotkey.PressedOnce())
			{
				_stopCrafting = true;
				LogDebug("Crafting interruption requested by hotkey.");
			}

			
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

			if (Settings.HotKey.PressedOnce())
			{
				if (Settings.Enable.Value)
				{
					ProcessHotkeyAction();
				}
			}
		}
		
		
		private async void ProcessHotkeyAction()
		{
			var now = DateTime.Now;
			_isIdentifying = true;
			try
			{
				IdentifyItems();
				
				if (Settings.EnableCrafting)
				{
					_isCrafting = true;
					await CraftItems();
				}
				
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
				
				
				ImGui.SameLine();
                if (ImGui.Button("Copy Log"))
                {
                    var logText = string.Join("\n", _debugLog);
                    ImGui.SetClipboardText(logText);
                    LogDebug("Debug log copied to clipboard.");
                }


                ImGui.BeginChild("DebugLog", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Border); // Enables scrolling
				
                foreach (var line in _debugLog)
                    ImGui.TextWrapped(line);
					
                if (_debugLog.Any() && ImGui.GetScrollY() >= ImGui.GetScrollMaxY()) // Auto-scroll only when at bottom
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
			public bool IsJewel { get; set; } 
            public int AffixCount { get; set; }
        }
		
		
		private async Task<int> CountPrefixes(NormalInventoryItem item)
		{
			var info = await MapInfo.FromItem(item);
			if (info != null)
			{
				foreach (var mod in info.Mods)
				{
					LogDebug($"Found mod - Name: {mod.Name}, DisplayName: {mod.DisplayName}, IsPrefix: {mod.IsPrefix}");
				}
			}
			return info?.PrefixCount ?? 0;
		}

		private async Task<int> CountSuffixes(NormalInventoryItem item)
		{
			var info = await MapInfo.FromItem(item);
			return info?.SuffixCount ?? 0;
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

        private async Task CraftItems()
		{
			_stopCrafting = false;
			// Initial crafting of normal/magic items
			RefreshAndCraftItems((items) => {
				ProcessNormalItems(items.Where(x => x.Rarity == ItemRarity.Normal).ToList());
				items = GetCurrentItems();
				ProcessMagicItems(items.Where(x => x.Rarity == ItemRarity.Magic).ToList());
			});


			if (Settings.ExaltJewels.Value)
			{
				var items = GetCurrentItems();
				foreach (var item in items.Where(x => x.IsJewel && x.Rarity == ItemRarity.Rare))
				{
					var jewelName = item.Item.Item.GetComponent<Base>()?.Name ?? "Unknown";
					LogDebug($"Processing rare jewel: {jewelName}");
					
					var prefixCount = await CountPrefixes(item.Item);
					var suffixCount = await CountSuffixes(item.Item);
					
					// Skip if already has 4 mods
					if (prefixCount + suffixCount >= 4)
					{
						LogDebug($"Skipping jewel - Already has {prefixCount + suffixCount} mods");
						continue;
					}
					
					if (TryGetCurrency(EXALT_PATH, out var exalt))
					{
						LogDebug($"Applying exalt to jewel: {jewelName}");
						ApplyCurrency(exalt, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
					}
					else
					{
						LogDebug("No more exalts available");
						break;
					}
				}
			}

			// Exalting map process (up to 3 times)
			if (Settings.ExaltRareMaps.Value)
			{
				for (int i = 0; i < 3; i++)
				{
					var items = GetCurrentItems();
					foreach (var initialItem in items.Where(x => x.IsMap && x.Rarity == ItemRarity.Rare))
					{
						
						// Track our current map through exalting process
						var currentItem = initialItem;
						
						
						
						
						var mapName = currentItem.Item.Item.GetComponent<Mods>().UniqueName;
						
           

						LogDebug($"Processing map: {mapName}");
						
						
						//var mapName = currentItem.Item.Item.GetComponent<Base>()?.Name ?? "Unknown";
						//LogDebug($"Processing map: {mapName}");
						
						
						
						
						
						var prefixCount = await CountPrefixes(currentItem.Item);
						var suffixCount = await CountSuffixes(currentItem.Item);

						if (prefixCount + suffixCount >=6) {
							LogDebug($"Skipping exalt - Cannot add further mods.  Prefixes: {prefixCount}, Suffixes: {suffixCount}");
							continue;
						}

						if ((Settings.StopAt3Prefixes.Value && prefixCount >= 3) ||
							(Settings.StopAt3Suffixes.Value && suffixCount >= 3))
						{
							LogDebug($"Skipping exalt - Prefixes: {prefixCount}, Suffixes: {suffixCount}");
							continue;
						}


						if (TryGetCurrency(EXALT_PATH, out var exalt))
						{
							LogDebug($"Applying exalt to map with {prefixCount} prefixes and {suffixCount} suffixes    Map name {mapName}");
							ApplyCurrency(exalt, currentItem.Item);
							
							// Wait for item to update
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
							await Task.Delay(500); // Additional delay for game state to update
							
							// Get fresh inventory state and find our map by name
							var refreshedItems = GetCurrentItems();
							var updatedItem = refreshedItems.FirstOrDefault(x => 
								x.IsMap && 
								x.Rarity == ItemRarity.Rare && 
								x.Item.Item.GetComponent<Mods>().UniqueName == mapName);
							
							if (updatedItem != null)
							{
								currentItem = updatedItem; // Update our reference to current map
								var newPrefixCount = await CountPrefixes(currentItem.Item);
								var newSuffixCount = await CountSuffixes(currentItem.Item);
								LogDebug($"After exalt on {mapName} - Prefixes: {newPrefixCount}, Suffixes: {newSuffixCount}");
							}
							else
							{
								LogDebug("Could not find map after exalt - moving to next map");
								continue;
							}
						}
						else
						{
							LogDebug("No more exalts available");
							break; // exit Exalt process
						}

						


					}
					//RefreshAndCraftItems((itemsAfterExalt) => {}); // Refresh inventory after each exalt pass
				}
			}

		
			
			
			
			// For Delirium
			if (Settings.UseDistilledEmotions.Value)
			{
				var items = GetCurrentItems();
				foreach (var initialItem in items.Where(x => x.IsMap && x.Rarity == ItemRarity.Rare))
				{
					var currentItem = initialItem;
					var mapName = currentItem.Item.Item.GetComponent<Mods>().UniqueName;
					LogDebug($"Processing map for Delirium: {mapName}");

					ApplyDeliriumToRareMaps(new List<CraftableItem> { currentItem });
				}
			}
			
			
			
			
			if (Settings.VaalMapsAfterCrafting.Value)
			{
				var items = GetCurrentItems();
				
				
				foreach (var initialItem in items.Where(x => x.IsMap && x.Rarity == ItemRarity.Rare))
				{
					
					
					var currentItem = initialItem;
					var mapName = currentItem.Item.Item.GetComponent<Mods>().UniqueName;
					
					
					
					LogDebug($"Processing map for Vaal: {mapName}");
					if (TryGetCurrency(VAAL_PATH, out var vaal))
					{
						LogDebug($"Applying Vaal to {mapName}");
						ApplyCurrency(vaal, currentItem.Item);
						Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
					}
					
				}
					
					
					
			}
			
			
			_stopCrafting = false;
			
			
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
					if (Settings.TripleCraftWhiteMaps.Value)
					{
						LogDebug("Processing white map with triple-craft sequence");
						if (TryGetCurrency(TRANSMUTATION_PATH, out var transmute))
						{
							ApplyCurrency(transmute, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);

							if (TryGetCurrency(AUGMENTATION_PATH, out var augment))
							{
								ApplyCurrency(augment, item.Item);
								Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);

								if (TryGetCurrency(REGAL_PATH, out var regal))
								{
									ApplyCurrency(regal, item.Item);
									Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
								}
								
							}
						}
					}
					
					else if (!Settings.AlchemyWaystones.Value)
					{
						LogDebug($"Skipping waystone: transmute setting disabled");
						continue;
					}

					else if (TryGetCurrency(ALCHEMY_PATH, out var alchemy))
					{
						ApplyCurrency(alchemy, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY + Settings.ExtraDelay);
					}
					
					if (Settings.VaalMapsAfterCrafting.Value && TryGetCurrency(VAAL_PATH, out var vaal))
					{
						ApplyCurrency(vaal, item.Item);
						Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
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
			if (!Settings.AugmentMagicItems.Value && !Settings.RegalMagicMaps.Value && !Settings.AugmentRegalJewels.Value)
			{
				LogDebug("All magic item processing is disabled in settings");
				return;
			}

			var processedIds = new HashSet<string>();
			
			// Get all magic maps and precursor tablets
			var magicItems = items.Where(x => (x.IsMap || x.IsPrecursorTablet || x.IsJewel) && x.Rarity == ItemRarity.Magic).ToList();
			
			foreach (var item in  magicItems)
			{
				var itemId = item.Item.GetHashCode().ToString();
				if (processedIds.Contains(itemId)) continue;
				processedIds.Add(itemId);

				// Debug log the item details
				LogDebug($"Processing item - Path: {item.Item.Item.Path}, IsPrecursor: {item.IsPrecursorTablet}, IsMap: {item.IsMap}, IsJewel: {item.IsJewel}");
				
				// Get explicit count of mods for better accuracy
				var mods = item.Item.Item.GetComponent<Mods>();
				var explicitCount = mods.ExplicitMods.Count();
				LogDebug($"Explicit mod count: {explicitCount}");



				// Handle items with 1 explicit mod
				if (explicitCount == 1)
				{
					
					// Jewel handling
					if (Settings.AugmentRegalJewels.Value && item.IsJewel) {
						LogDebug("Found jewel with 1 explicit mod, applying Augmentation");
						
						if (TryGetCurrency(AUGMENTATION_PATH, out var augment))
						
						{
							
							ApplyCurrency(augment, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
							
							LogDebug("Applying Regal Orb to Jewel");
							if (TryGetCurrency(REGAL_PATH, out var regal))
							{
								ApplyCurrency(regal, item.Item);
								Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
								
								LogDebug("Applying Exalt Orb to Jewel");
								if (TryGetCurrency(EXALT_PATH, out var exalt))
								{
									ApplyCurrency(exalt, item.Item);
									Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
								}
							}
							
						}
						
					}
					
					// Only augment if setting is enabled
					else if (Settings.AugmentMagicItems.Value && !item.IsJewel)
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
				else if (explicitCount == 2 && !item.IsPrecursorTablet)
				{
					
					if (Settings.AugmentRegalJewels.Value && item.IsJewel) {
						LogDebug("Applying Regal Orb to Jewel with 2 explicit mods");
						if (TryGetCurrency(REGAL_PATH, out var regal))
						{
							ApplyCurrency(regal, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
						}
						
						LogDebug("Applying Exalt Orb to Jewel");
						if (TryGetCurrency(EXALT_PATH, out var exalt))
						{
							ApplyCurrency(exalt, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
						}
						
					}
					
					else if (Settings.RegalMagicMaps.Value && item.IsMap) {
						LogDebug("Applying Regal Orb to map with 2 explicit mods");
						if (TryGetCurrency(REGAL_PATH, out var regal))
						{
							ApplyCurrency(regal, item.Item);
							Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
						}
					}
				}
			}
		}
		
		
		
		
		
		private void ApplyDeliriumToRareMaps(List<CraftableItem> items)
		{
		   var rareMaps = items.Where(x => x.IsMap && x.Rarity == ItemRarity.Rare).ToList();
		   
		   foreach (var item in rareMaps)
		   {
			   var mods = item.Item.Item.GetComponent<Mods>();
			   if (mods.ItemMods.Any(x => x.Name == "InstilledMapDelirium"))
			   {
				   LogDebug("Map already has Delirium mod, skipping");
				   continue;
			   }

			   // Select emotion type based on settings priority
			   string emotionPath = null;
			   if (Settings.UseDistilledIre.Value)
				   emotionPath = DISTILLEDIRE_PATH;
			   else if (Settings.UseDistilledGuilt.Value)
				   emotionPath = DISTILLEDGUILT_PATH;
			   else if (Settings.UseDistilledGreed.Value)
				   emotionPath = DISTILLEDGREED_PATH;
			   else if (Settings.UseDistilledParanoia.Value)
				   emotionPath = DISTILLEDPARANOIA_PATH;
			   else if (Settings.UseDistilledEnvy.Value)
				   emotionPath = DISTILLEDENVY_PATH;

			   if (emotionPath == null)
			   {
				   LogDebug("No emotion type selected in settings");
				   continue;
			   }

			   if (TryGetCurrency(emotionPath, out var emotion))
			   {
				   LogDebug($"Applying Distilled Emotion to map: {item.Item.Item.Path}");
				   
				   // Right click emotion
				   var emotionPos = emotion.GetClientRect().Center;
				   Input.SetCursorPos(new Vector2(emotionPos.X + _windowOffset.X, emotionPos.Y + _windowOffset.Y));
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);
				   Input.Click(MouseButtons.Right);
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);

				   Thread.Sleep(200);  // wait for dialog to load
			   
				   // Control-click emotion 3 times
				   Input.KeyDown(Keys.LControlKey);
				   for (int i = 0; i < 3; i++)
				   {
					   Input.Click(MouseButtons.Left);
					   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);
				   }

				   // Control-click map
				   var itemPos = item.Item.GetClientRect().Center;
				   Input.SetCursorPos(new Vector2(itemPos.X + _windowOffset.X, itemPos.Y + _windowOffset.Y));
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);
				   Input.Click(MouseButtons.Left);
				   Input.KeyUp(Keys.LControlKey);
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);

				   // Click Instill button
				   // Get the screen width and height
				   //var screenWidth = GameController.Window.GetWindowRectangle().Width;
				   //var screenHeight = GameController.Window.GetWindowRectangle().Height;
				   var screenWidth = GameController.Game.IngameState.Camera.Width;
				   var screenHeight = GameController.Game.IngameState.Camera.Height;

					// Calculate the yfactor
					var yfactor = screenHeight / 1080f;

					// Calculate the X and Y positions dynamically
					var xPos = (screenWidth - (660 * yfactor)) / 2;
					var yPos = 840 * yfactor;

					// Set the cursor position and perform the click
					Input.SetCursorPos(new Vector2(xPos + _windowOffset.X, yPos + _windowOffset.Y));
					Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);
					Input.Click(MouseButtons.Left);
					Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);



				   xPos = (screenWidth - (660 * yfactor)) / 2;
				   yPos = 435 * yfactor;
					
					
				   // Click map in delirium interface
				   Input.KeyDown(Keys.LControlKey);
				   Input.SetCursorPos(new Vector2(xPos + _windowOffset.X, yPos + _windowOffset.Y));
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);
				   Input.Click(MouseButtons.Left);
				   Input.KeyUp(Keys.LControlKey);
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay * 3);
				   
				   // Close interface with Escape
				   Input.KeyDown(Keys.Escape);
				   Thread.Sleep(Constants.INPUT_DELAY);
				   Input.KeyUp(Keys.Escape);
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);

				   // Reopen inventory
				   Input.KeyDown(Keys.I);
				   Thread.Sleep(Constants.INPUT_DELAY);
				   Input.KeyUp(Keys.I);
				   Thread.Sleep(Constants.INPUT_DELAY * 2 + Settings.ExtraDelay);
			   }
		   }
		}
		
		
		
		
		
		
		
		
		

        private void ProcessMagicMaps(List<CraftableItem> items)
        {
            var twoAffixMaps = items.Where(x => x.AffixCount == 2 && x.IsMap);
            
            if (Settings.RegalMagicMaps.Value && TryGetCurrency(REGAL_PATH, out var regal))
            {
                foreach (var item in twoAffixMaps)
                {
                    ApplyCurrency(regal, item.Item);
					Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
                }
            }
			if (Settings.VaalMapsAfterCrafting.Value && TryGetCurrency(VAAL_PATH, out var vaal))
			{
				foreach (var item in twoAffixMaps)
                {
					ApplyCurrency(vaal, item.Item);
					Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
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
				var isMap = baseItem.HasComponent<Map>();
				var isPrecursorTablet = path.StartsWith(PRECURSOR_PATH, StringComparison.OrdinalIgnoreCase);
				var isJewel = path.Contains("/Jewels/"); // Add this check for jewels
				
				LogDebug($"Found item - Path: {path}, IsMap: {isMap}, IsPrecursor: {isPrecursorTablet}, Rarity: {mods.ItemRarity}");
				craftableItems.Add(new CraftableItem
				{
					Item = invItem,
					IsMap = isMap,
					IsWaystone = isMap, // Since these are the same
					IsPrecursorTablet = isPrecursorTablet,
					IsJewel = isJewel,
					Rarity = mods.ItemRarity,
					AffixCount = mods.ImplicitMods.Count() + mods.ExplicitMods.Count()
				});
			}
			return craftableItems;
		}
		
		
		private List<CraftableItem> GetCurrentItems()
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

			return GetCraftableItems(inventoryItems);
		}
		
		private void RefreshAndCraftItems(Action<List<CraftableItem>> craftingAction)
		{
			var items = GetCurrentItems();
			craftingAction(items);
		}
		
	
		
		private void ProcessVaalOrbs(List<CraftableItem> items)
		{
			foreach (var item in items)
			{
				if (TryGetCurrency(VAAL_PATH, out var vaal))
				{
					LogDebug($"Applying Vaal orb to map");
					ApplyCurrency(vaal, item.Item);
					Thread.Sleep(Constants.CLICK_DELAY * 2 + Settings.ExtraDelay * 3);
				}
			}
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
				LogDebug($"Attempting to switch to currency tab: {Settings.CurrencyTabName.Value}");
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
		
		private NormalInventoryItem GetNonFullStack(string path, IEnumerable<NormalInventoryItem> items, int maxStackSize)
		{
			try
			{
				return items
					.Where(invItem => 
						invItem.Item.Path.Equals(path, StringComparison.OrdinalIgnoreCase) &&
						invItem.Item.GetComponent<Stack>()?.Size < maxStackSize)
					.OrderBy(x => x.Item.GetComponent<Stack>()?.Size ?? 0)
					.FirstOrDefault();
			}
			catch (Exception ex)
			{
				LogDebug($"Error finding non-full stack for {path}: {ex.Message}");
				return null;
			}
		}

		private void ProcessCurrencyStacks()
		{
			
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

				
				// Get direct inventory reference
				var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
				var inventoryItems = inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
				
				// var playerInvCount = GameController?.Game?.IngameState?.Data?.ServerData?.PlayerInventories?.Count;
				// if (playerInvCount is null or 0) {
					// LogDebug($"playerInvCount is null or 0.  What happened?");
					// return;
					
				// }
				
				// var inventoryItemsTemp = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
				// var inventoryItems = inventoryItemsTemp.ToList();
				

				// Check for full stacks with direct inventory check
				// Count full stacks of this currency
				var fullStacks = inventoryItems.Where(x => 
					x.Item.Path.Equals(currencyPath, StringComparison.OrdinalIgnoreCase) &&
					(x.Item.GetComponent<Stack>()?.Size ?? 0) >= maxStack).ToList();
				
				LogDebug($"Found {fullStacks.Count} full stacks of {currencyPath}");
				
				if (!fullStacks.Any())
				{
					var currencyInStash = GetItemWithBaseName(currencyPath, 
						GameController.IngameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems);
						
					if (currencyInStash != null)
					{
						LogDebug($"Taking {currencyPath} from stash");
						var pos = currencyInStash.GetClientRect().Center;
						Input.SetCursorPos(new Vector2(pos.X + _windowOffset.X, pos.Y + _windowOffset.Y));
						Thread.Sleep(Constants.INPUT_DELAY);
						Input.KeyDown(Keys.LControlKey);
						Input.Click(MouseButtons.Left);
						Input.KeyUp(Keys.LControlKey);
						Thread.Sleep(Constants.CLICK_DELAY);
					}
					else {
						LogDebug($"Skipping {currencyPath} - Stash is empty");
					}
				}
				else {
					LogDebug($"Skipping {currencyPath} - Found full stack");
				}
				
				
				Thread.Sleep(250);				
				
				inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;  // Get fresh panel reference
				inventoryItems = inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;  // Get fresh items list
				
				//inventoryItemsTemp = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
				//inventoryItems = inventoryItemsTemp.ToList();
				
				var stacksToReturn = inventoryItems.Where(x => {
					if (!x.Item.Path.Equals(currencyPath, StringComparison.OrdinalIgnoreCase))
						return false;

					var stackSize = x.Item.GetComponent<Stack>()?.Size ?? 0;
					
					// Keep track if this is a full stack we want to keep
					if (stackSize >= maxStack && fullStacks.Count == 1)
						return false;

					return true;
				}).ToList();

				// Return stacks to stash
				foreach (var stack in stacksToReturn)
				{
					var stackSize = stack.Item.GetComponent<Stack>()?.Size ?? 0;
					LogDebug($"Returning stack of size {stackSize} to stash");
					
					var pos = stack.GetClientRect().Center;
					Input.SetCursorPos(new Vector2(pos.X + _windowOffset.X, pos.Y + _windowOffset.Y));
					Thread.Sleep(Constants.INPUT_DELAY);
					Input.KeyDown(Keys.LControlKey);
					Input.Click(MouseButtons.Left);
					Input.KeyUp(Keys.LControlKey);
					Thread.Sleep(Constants.CLICK_DELAY);
				}
				
			}
		}
		
		
		
		
		
		// private void ProcessCurrencyStacks()
		// {
			
			// var stashElement = GameController.IngameState.IngameUi.StashElement;
			
			// foreach (var kvp in GetMaxStackSizes())
			// {
				// var currencyPath = kvp.Key;
				// var maxStack = kvp.Value;
				
				// // Skip if stack size is set to 0 in settings
				// if (maxStack == 0)
				// {
					// LogDebug($"Skipping {currencyPath} (disabled in settings: stack size 0)");
					// continue;
				// }

				
				// // Get direct inventory reference
				// //var inventoryPanel = GameController.IngameState.IngameUi.InventoryPanel;
				// // = inventoryPanel[InventoryIndex.PlayerInventory].VisibleInventoryItems;
				
				// var playerInvCount = GameController?.Game?.IngameState?.Data?.ServerData?.PlayerInventories?.Count;
				// if (playerInvCount is null or 0) {
					// LogDebug($"playerInvCount is null or 0.  What happened?");
					// return;
					
				// }
				
				// var inventoryItems = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
				
				

				// // Check for full stacks with direct inventory check
				// // Count full stacks of this currency
				// var fullStacks = new List<Entity>();
				// foreach (var item in inventoryItems)
				// {
					// if (item.Item.Path.Equals(currencyPath, StringComparison.OrdinalIgnoreCase) &&
						// (item.Item.GetComponent<Stack>()?.Size ?? 0) >= maxStack)
					// {
						// fullStacks.Add(item);
					// }
				// }

				// LogDebug($"Found {fullStacks.Count} full stacks of {currencyPath}");
				
				// if (fullStacks.Count == 0)
				// {
					// var currencyInStash = GetItemWithBaseName(currencyPath,
						// GameController.IngameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems);

					// if (currencyInStash != null)
					// {
						// LogDebug($"Taking {currencyPath} from stash");
						// var pos = currencyInStash.GetClientRect().Center;
						// Input.SetCursorPos(new Vector2(pos.X + _windowOffset.X, pos.Y + _windowOffset.Y));
						// Thread.Sleep(Constants.INPUT_DELAY);
						// Input.KeyDown(Keys.LControlKey);
						// Input.Click(MouseButtons.Left);
						// Input.KeyUp(Keys.LControlKey);
						// Thread.Sleep(Constants.CLICK_DELAY);
					// }
					// else
					// {
						// LogDebug($"Skipping {currencyPath} - Stash is empty");
					// }
				// }
				// else
				// {
					// LogDebug($"Skipping {currencyPath} - Found full stack");
				// }
				
				
				// Thread.Sleep(250);				
				
				// // Refresh inventory items
				// inventoryItems = GameController.Game.IngameState.Data.ServerData.PlayerInventories[0].Inventory;
				
				// var stacksToReturn = new List<Entity>();
				
				// foreach (var item in inventoryItems)
				// {
					// if (!item.Item.Path.Equals(currencyPath, StringComparison.OrdinalIgnoreCase))
						// continue;

					// var stackSize = item.Item.GetComponent<Stack>()?.Size ?? 0;

					// // Keep track if this is a full stack we want to keep
					// if (stackSize >= maxStack && fullStacks.Count == 1)
						// continue;

					// stacksToReturn.Add(item);
				// }

				// // Return stacks to stash
				// foreach (var stack in stacksToReturn)
				// {
					// var stackSize = stack.Item.GetComponent<Stack>()?.Size ?? 0;
					// LogDebug($"Returning stack of size {stackSize} to stash");

					// var pos = stack.GetClientRect().Center;
					// Input.SetCursorPos(new Vector2(pos.X + _windowOffset.X, pos.Y + _windowOffset.Y));
					// Thread.Sleep(Constants.INPUT_DELAY);
					// Input.KeyDown(Keys.LControlKey);
					// Input.Click(MouseButtons.Left);
					// Input.KeyUp(Keys.LControlKey);
					// Thread.Sleep(Constants.CLICK_DELAY);
				// }
				
			// }
		// }
		
		
		
		
		
		
		
		

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
			
			if (_stopCrafting)
			{
				LogDebug("Crafting stopped: Currency not detected or user requested to stop crafting.");
				return false;
			}
            
            if (currency == null)
            {
                LogDebug($"Currency not found: {path}");
				_stopCrafting = true;
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
