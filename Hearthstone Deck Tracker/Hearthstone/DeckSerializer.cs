using System;
using System.IO;
using System.Linq;
using Hearthstone_Deck_Tracker.Utility;
//using Hearthstone_Deck_Tracker.Utility.Logging;

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	public static class DeckSerializer
	{
#if false
		public static string Serialize(Deck deck)
		{

            string heroId = "";
            if(deck.Class == null)
                return null;
			if(!CardIds.HeroNameDict.TryGetValue(deck.Class, out heroId))
			{
	//			Log.Error("Deck has no hero");
				return null;
			}

            
			var heroDbfId = Database.GetCardFromId(heroId).DbfIf;
			if(heroDbfId == 0)
			{
		///		Log.Error("Could not find hero id");
				return null;
			}

			using(var ms = new MemoryStream())
			{
				void Write(int value)
				{
					if(value == 0)
						ms.WriteByte(0);
					else
					{
						var bytes = VarInt.GetBytes((ulong)value);
						ms.Write(bytes, 0, bytes.Length);
					}
				}

				ms.WriteByte(0);
				Write(1);
				Write(deck.IsWildDeck ? 1 : 2);
				Write(1);
				Write(heroDbfId);

				var cards = deck.Cards.OrderBy(x => x.DbfIf).ToList();
				var singleCards = cards.Where(x => x.Count == 1).ToList();
				var doubleCards = cards.Where(x => x.Count == 2).ToList();
				var multiCards = cards.Where(x => x.Count > 2).ToList();

				Write(singleCards.Count);
				foreach(var card in singleCards)
					Write(card.DbfIf);

				Write(doubleCards.Count);
				foreach(var card in doubleCards)
					Write(card.DbfIf);

				Write(multiCards.Count);
				foreach(var card in multiCards)
				{
					Write(card.DbfIf);
					Write(card.Count);
				}

				var bytes1 = ms.ToArray();
				return Convert.ToBase64String(bytes1);
			}
		}
#endif
		public static Deck Deserialize(string input)
		{
			Deck deck = null;
			var lines = input.Split('\n').Select(x => x.Trim());
			string deckName = null;
			foreach(var line in lines)
			{
				if(string.IsNullOrEmpty(line))
					continue;
				if(line.StartsWith("#"))
				{
					if(line.StartsWith("###"))
						deckName = line.Substring(3).Trim();
					continue;
				}
				try
				{
					if(deck == null)
						deck = DeserializeDeckString(line);
				}
				catch(Exception e)
				{
//					Log.Error(e);
					return null;
				}
			}
			if(deck != null && deckName != null)
				deck.Name = deckName;
			return deck;
		}

        private static ulong Read(byte [] bytes, ref int offset){
                int length;

				if(offset > bytes.Length)
					throw new ArgumentException("Input is not a valid deck string.");
				var value = VarInt.ReadNext(bytes.Skip(offset).ToArray(), out length);
				offset += length;
				return value;
        }

        private static void AddCard(int dbfId, int count, Deck deck)
        {
            var card = Database.GetCardFromDbfId(dbfId);
            card.Count = count;
            deck.Cards.Add(card);
        }

		public static Deck DeserializeDeckString(string deckString)
		{
			var deck = new Deck();
			byte[] bytes;
			try
			{
				bytes = Convert.FromBase64String(deckString);
			}
			catch(Exception e)
			{
				throw new ArgumentException("Input is not a valid deck string.", e);
			}
			int offset = 0;
			//Zero byte
			offset++;

			//Version - currently unused, always 1
			Read(bytes, ref offset);

			//Format - determined dynamically
			Read(bytes, ref offset);

			//Num Heroes - always 1
			Read(bytes, ref offset);

			var heroId =Read(bytes, ref offset);
            var heroCard = Database.GetCardFromDbfId((int) heroId, false);
			deck.Class = heroCard.PlayerClass;


            var numSingleCards = (int)Read(bytes, ref offset);
            for (var i = 0; i < numSingleCards; i++)
            {
                var dbfId = (int)Read(bytes, ref offset);
                AddCard(dbfId, 1, deck);
            }

            var numDoubleCards = (int)Read(bytes, ref offset);
            for (var i = 0; i < numDoubleCards; i++)
            {
                var dbfId = (int)Read(bytes, ref offset);
                AddCard(dbfId, 2, deck);

            }

            var numMultiCards = (int)Read(bytes, ref offset);
			for(var i = 0; i < numMultiCards; i++)
			{
                var dbfId = (int)Read(bytes, ref offset);
                var count = (int)Read(bytes, ref offset);
				AddCard(dbfId, count, deck);
			}
			return deck;
		}
	}
}