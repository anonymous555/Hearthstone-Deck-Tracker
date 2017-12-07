﻿#region

using System.Collections.Generic;
using System.Globalization;
using HearthDb.Enums;
using Rarity = Hearthstone_Deck_Tracker.Enums.Rarity;

#endregion

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	public static class HearthDbConverter
	{
		public static readonly Dictionary<int, string> SetDict = new Dictionary<int, string>
		{
			{0, null},
			{2, "Basic"},
			{3, "Classic"},
			{4, "Reward"},
			{5, "Missions"},
			{7, "System"},
			{8, "Debug"},
			{11, "Promotion"},
			{12, "Curse of Naxxramas"},
			{13, "Goblins vs Gnomes"},
			{14, "Blackrock Mountain"},
			{15, "The Grand Tournament"},
			{16, "Credits"},
			{17, "Hero Skins"},
			{18, "Tavern Brawl"},
			{20, "League of Explorers"},
            {21, "Whispers of the Old Gods"},
            {23, "One Night in Karazhan"},
            {25, "Mean Streets of Gadgetzan"},
           	{27, "Journey to Un'Goro"},
            {1001, "Knights of the Frozen Throne"},
            {1004, "Kobolds and Catacombs"} 
		};

		public static string ConvertClass(CardClass cardClass)
		{
			return (int)cardClass < 2 || (int)cardClass > 10
				       ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cardClass.ToString().ToLower());
		}

		public static Rarity RariryConverter(HearthDb.Enums.Rarity rarity)
		{
			switch(rarity)
			{
				case HearthDb.Enums.Rarity.FREE:
					return Rarity.Free;
				case HearthDb.Enums.Rarity.COMMON:
					return Rarity.Common;
				case HearthDb.Enums.Rarity.RARE:
					return Rarity.Rare;
				case HearthDb.Enums.Rarity.EPIC:
					return Rarity.Epic;
				case HearthDb.Enums.Rarity.LEGENDARY:
					return Rarity.Legendary;
				default:
					return Rarity.Free;
			}
		}

		public static string CardTypeConverter(CardType type)
		{
			if(type == CardType.HERO_POWER)
				return "Hero Power";
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(type.ToString().ToLower().Replace("_", ""));
		}


		public static string RaceConverter(Race race)
		{
			switch(race)
			{
				case Race.INVALID:
					return null;
				case Race.GOBLIN2:
					return "Goblin";
				case Race.PET:
					return "Beast";
				default:
					return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(race.ToString().ToLower());
			}
		}

		public static string SetConverter(CardSet set)
		{
			string str;
			SetDict.TryGetValue((int)set, out str);
			return str;
		}
	}
}