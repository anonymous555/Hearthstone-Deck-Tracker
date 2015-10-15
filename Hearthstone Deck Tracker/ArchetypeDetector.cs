using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Hearthstone;


namespace Hearthstone_Deck_Tracker
{
    public class ArchetypeDeck
    {
        public Deck deck;
        public string archetypename;
        public int getNumMatches(Deck otherdeck)
        {
            int count = 0;

            if (!deck.Class.Equals(otherdeck.Class))
            {
                return 0;
            }

            foreach (Card c in deck.Cards)
            {
                    foreach (Card theircard in otherdeck.Cards)
                    {
                        if (c.Id.Equals(theircard.Id))
                        {
                            if (c.Count == 2 && theircard.Count == 2)
                            {
                                count += 2;
                            }
                            else
                            {
                                count += 1;
                            }
                        }
                    }
            }
            return count;
        }

    }

    public class ArchetypeDetector
    {
        private List<ArchetypeDeck> archetype_decks;

        public ArchetypeDetector()
        {
            DeckList decklist = DeckList.Instance;
            ObservableCollection<Deck> decks = decklist.Decks;

            archetype_decks = new List<ArchetypeDeck>();

            foreach (Deck deck in decks)
            {
                foreach (string tag in deck.Tags)
                {
                    if (tag.StartsWith("archetype_"))
                    {
                        ArchetypeDeck archetype = new ArchetypeDeck();
                        archetype.deck = deck;
                        archetype.archetypename = tag.Remove(0, 10);
                        archetype_decks.Add(archetype);
                    }
                }
            }

            /// now assign all archetypes
            /// 
/*            foreach (Deck deck in decks)
            {
                deck.Archetype = getArchetypeString(deck);
            }
 */

        }

        static ArchetypeDetector instance = null;

        public static String getBestArchetypeString(Deck deck, bool concatcount)
        {
            if (instance == null)
            {
                instance = new ArchetypeDetector();
            }
            return instance.getArchetypeString(deck, concatcount);

        }

        public static String getIngameArchetypeString(ObservableCollection<Card> OpponentCards)
        {
            Deck newdeck = new Deck();
            newdeck.Class = API.Core.Game.CurrentGameStats.OpponentHero;

            newdeck.Cards = OpponentCards;
            if (instance == null)
            {
                instance = new ArchetypeDetector();
            }
            return instance.getArchetypeStringInGame(newdeck);

        }


        public static ArchetypeDeck best_archetype_deck = null;

        // returns the single best archetype
        public String getArchetypeString(Deck otherdeck, bool concatcount )
        {
            int bestcount = 0;
            ArchetypeDeck bestarchetype = null;

            foreach (ArchetypeDeck archetype in archetype_decks)
            {
                int newcount = archetype.getNumMatches(otherdeck);
                float percentmatch = (float) newcount / (float) otherdeck.GetTotalNumCards();
                if (newcount > bestcount && percentmatch > .51)
                {
                    bestcount = newcount;
                    bestarchetype = archetype;
                }

            }

            if (bestcount == 0)
                return "";
            else if (concatcount)
                return bestarchetype.archetypename + " (" + bestcount.ToString() + ")";
            else
                return bestarchetype.archetypename ;
        }

        struct ArchetypeResult
        {
            public int percent;
            public string name;
            public ArchetypeDeck archetype;
        }

        public String getArchetypeStringInGame(Deck otherdeck)
        {
            String returnstring = "";
            

            ObservableCollection<Card> newcards = new ObservableCollection<Card> ( otherdeck.Cards.Where(c => c.Id != "GAME_005"  ).ToList() );
            otherdeck.Cards = newcards;

            int otherdeckcardcount = otherdeck.GetTotalNumCards();

            List<ArchetypeResult> archresults = new System.Collections.Generic.List<ArchetypeResult>();
                
                //SortedList = cardpredictions.OrderBy(o => (1.0 - o.percent)).ToList();

            foreach (ArchetypeDeck archetype in archetype_decks)
            {
                int newcount = archetype.getNumMatches(otherdeck);
                if (newcount > 0)
                {
                    float percent = (float)newcount / (float)otherdeckcardcount * 100.0f;
                    int intpercent = (int) percent;
                    ArchetypeResult newresult;
                    newresult.percent = intpercent;
                    newresult.name = archetype.archetypename;
                    newresult.archetype = archetype;

                    archresults.Add(newresult);
                    
                }

            }

            List<ArchetypeResult> SortedList = archresults.OrderBy(o => (100 - o.percent)).ToList();

            int count = 0;
            foreach(ArchetypeResult archetyperesult in SortedList)
            {
                if (count < 5 && archetyperesult.percent > 40)
                {
                    if (count == 0)
                    {
                        /// assign the global
                        /// 
                        best_archetype_deck = archetyperesult.archetype;
                    }
                    returnstring += archetyperesult.name + " %" + archetyperesult.percent + "\n";
                }
                count += 1;
            }
            return returnstring;
        }
    }
}
