using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Numerics;
using ExileCore2.PoEMemory.Elements;

namespace MapBuddy
{	
    public class Settings : ISettings
	{
		public Settings()
		{
			// Core settings
			Enable = new ToggleNode(true);
			HotKey = new HotkeyNodeV2(Keys.F2);
			ExtraDelay = new RangeNode<int>(50, 30, 200);
			ThrowHotkey = new HotkeyNodeV2(Keys.LShiftKey); 
			
			// Identification settings
			IdentifyAll = new ToggleNode(false);
			IdentifyMagicItems = new ToggleNode(true);
			IdentifyRares = new ToggleNode(true);
			IdentifyUniques = new ToggleNode(true);
			IdentifyMaps = new ToggleNode(false);
			IdentifyStashItems = new ToggleNode(true);

			// Crafting settings
			EnableCrafting = new ToggleNode(false);
			AlchemyNormalItems = new ToggleNode(true);
			TransmuteWaystones = new ToggleNode(true);
			TransmutePrecursorTablets = new ToggleNode(true);
			AugmentMagicItems = new ToggleNode(true);
			RegalMagicMaps = new ToggleNode(true);
			
			// Currency Management
			AutoFillCurrency = new ToggleNode(true);
			CurrencyTabName = new TextNode("Currency");

			// UI Settings
			ShowDebugWindow = new ToggleNode(false);
			ShowIdentificationWindow = new ToggleNode(true);
			
			
			// Destroy button when throwing away items in town
			DestroyButtonX = new RangeNode<int>(1900, 0, 3840); // Default 1900, min 0, max 3840 (4K width)
			DestroyButtonY = new RangeNode<int>(745, 0, 2160);  // Default 745, min 0, max 2160 (4K height)
			
		}

		[Menu("Enable")]
		public ToggleNode Enable { get; set; }

		[Menu("Identification Hotkey")]
		public HotkeyNodeV2 HotKey { get; set; }

		[Menu("Throw Item Hotkey")]
		public HotkeyNodeV2 ThrowHotkey { get; set; }

		[Menu("Extra Delay")]
		public RangeNode<int> ExtraDelay { get; set; }

		// Identification Settings Group
		[Menu("Identification Settings", 1000)]
		public EmptyNode IdentificationSettings { get; set; } = new EmptyNode();

		[Menu("Identify ALL Items", 1001, 1000)]
		public ToggleNode IdentifyAll { get; set; }

		[Menu("Magic Items", 1002, 1000)]
		public ToggleNode IdentifyMagicItems { get; set; }

		[Menu("Rares", 1003, 1000)]
		public ToggleNode IdentifyRares { get; set; }

		[Menu("Uniques", 1004, 1000)]
		public ToggleNode IdentifyUniques { get; set; }

		[Menu("Maps", 1005, 1000)]
		public ToggleNode IdentifyMaps { get; set; }

		[Menu("Include Stash Items", 1006, 1000)]
		public ToggleNode IdentifyStashItems { get; set; }

		// Crafting Settings Group
		[Menu("Crafting Settings", 2000)]
		public EmptyNode CraftingSettings { get; set; } = new EmptyNode();

		[Menu("Enable Crafting", 2001, 2000)]
		public ToggleNode EnableCrafting { get; set; }

		[Menu("Alchemy Normal Items", 2003, 2000)]
		public ToggleNode AlchemyNormalItems { get; set; }

		[Menu("Transmute Waystones", 2004, 2000)]
		public ToggleNode TransmuteWaystones { get; set; }

		[Menu("Transmute Precursor Tablets", 2005, 2000)]
		public ToggleNode TransmutePrecursorTablets { get; set; }

		[Menu("Augment Magic Items", 2006, 2000)]
		public ToggleNode AugmentMagicItems { get; set; }

		[Menu("Regal Magic Maps", 2007, 2000)]
		public ToggleNode RegalMagicMaps { get; set; }

		// Currency Management
		[Menu("Currency Management", 3000)]
		public EmptyNode CurrencySettings { get; set; } = new EmptyNode();

		[Menu("Auto-fill Currency from Stash **CAREFUL**", 3001, 3000)]
		public ToggleNode AutoFillCurrency { get; set; }

		[Menu("Currency Tab Name", 3002, 3000)]
		public TextNode CurrencyTabName { get; set; }

		// UI Settings
		[Menu("Show Debug Window")]
		public ToggleNode ShowDebugWindow { get; set; }

		[Menu("Show Identification Window")]
		public ToggleNode ShowIdentificationWindow { get; set; }
		
		
		[Menu("Destroy Button Coordinates ", 4000)]
		public EmptyNode DestroyButtonSettings { get; set; } = new EmptyNode();
		
		[Menu("Destroy Button X Position", "X coordinate for clicking Destroy button when throwing away items in town")]
		public RangeNode<int> DestroyButtonX { get; set; }

		[Menu("Destroy Button Y Position", "Y coordinate for clicking Destroy button when throwing away items in town")]
		public RangeNode<int> DestroyButtonY { get; set; }
		
	
	}
}