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
			ThrowHotkey = new HotkeyNodeV2(Keys.Oemtilde); 
			
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
			TripleCraftWhiteMaps = new ToggleNode(false);
			AlchemyWaystones = new ToggleNode(true);
			TransmutePrecursorTablets = new ToggleNode(true);
			AugmentMagicItems = new ToggleNode(true);
			RegalMagicMaps = new ToggleNode(true);
			
			// Currency Management
			AutoFillCurrency = new ToggleNode(true);
			CurrencyTabName = new TextNode("Currency");
			ScrollStackSize = new RangeNode<int>(40, 0, 40);      
			AlchemyStackSize = new RangeNode<int>(20, 0, 20);    
			TransmutationStackSize = new RangeNode<int>(40, 0, 40); 
			AugmentationStackSize = new RangeNode<int>(30, 0, 30); 
			RegalStackSize = new RangeNode<int>(20, 0, 20);     

			// UI Settings
			ShowDebugWindow = new ToggleNode(false);
			ShowIdentificationWindow = new ToggleNode(true);
			
			
			// Destroy button when throwing away items in town
			DestroyButtonX = new RangeNode<int>(1900, 0, 3840); // Default 1900, min 0, max 3840 (4K width)
			DestroyButtonY = new RangeNode<int>(745, 0, 2160);  // Default 745, min 0, max 2160 (4K height)
			
		}

		[Menu("Enable")]
		public ToggleNode Enable { get; set; }

		[Menu("Identification + Crafting + Currency Management Hotkey")]
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
		
		[Menu("Alchemy Normal Items (Maps handled separately below)", 2002, 2000)]
		public ToggleNode AlchemyNormalItems { get; set; }
		
		[Menu("Transmute -> Augment -> Regal Normal Maps (Supercedes below)", 2003, 2000)]
		public ToggleNode TripleCraftWhiteMaps { get; set; }
		
		[Menu("Alchemy Normal Maps", 2004, 2000)]
		public ToggleNode AlchemyWaystones { get; set; }

		[Menu("Transmute Precursor Tablets", 2005, 2000)]
		public ToggleNode TransmutePrecursorTablets { get; set; }

		[Menu("Augment Magic Maps and Precursor Tablets with 1 Affix", 2006, 2000)]
		public ToggleNode AugmentMagicItems { get; set; }

		[Menu("Regal Magic Maps", 2007, 2000)]
		public ToggleNode RegalMagicMaps { get; set; }

		// Currency Management
		[Menu("Currency Management", 3000)]
		public EmptyNode CurrencySettings { get; set; } = new EmptyNode();

		[Menu("Auto-fill Currency from Stash **CAREFUL**", 3001, 3000)]
		public ToggleNode AutoFillCurrency { get; set; }
		
		[Menu("Scroll of Wisdom Stack Size", 3003, 3000)]
		public RangeNode<int> ScrollStackSize { get; set; }

		[Menu("Orb of Alchemy Stack Size", 3004, 3000)]
		public RangeNode<int> AlchemyStackSize { get; set; }

		[Menu("Orb of Transmutation Stack Size", 3005, 3000)]
		public RangeNode<int> TransmutationStackSize { get; set; }

		[Menu("Orb of Augmentation Stack Size", 3006, 3000)]
		public RangeNode<int> AugmentationStackSize { get; set; }

		[Menu("Regal Orb Stack Size", 3007, 3000)]
		public RangeNode<int> RegalStackSize { get; set; }

		[Menu("Currency Tab Name **THIS FEATURE IS CURRENTLY BROKEN, PLEASE SELECT CURRENCY TAB MANUALLY**", 3002, 3000)]
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
