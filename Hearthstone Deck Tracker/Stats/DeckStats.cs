using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;
using MahApps.Metro.Controls.Dialogs;

namespace Hearthstone_Deck_Tracker.Stats
{
	public class DeckStats
	{
		[XmlArray(ElementName = "Games")]
		[XmlArrayItem(ElementName = "Game")]
		public List<GameStats> Games;

		public string Name;

		public DeckStats()
		{
			Games = new List<GameStats>();
		}

		public DeckStats(string name)
		{
			Name = name;
			Games = new List<GameStats>();
		}

		public void AddGameResult(GameResult result, string opponentHero)
		{
			Games.Add(new GameStats(result, opponentHero));
		}

		public void AddGameResult(GameStats gameStats)
		{
			Games.Add(gameStats);
		}
	}

	public class DeckStatsList
	{
		private static DeckStatsList _instance;

		[XmlArray(ElementName = "DeckStats")]
		[XmlArrayItem(ElementName = "Deck")]
		public List<DeckStats> DeckStats;

		public DeckStatsList()
		{
			DeckStats = new List<DeckStats>();
		}

		public static DeckStatsList Instance
		{
			get { return _instance ?? (_instance = new DeckStatsList()); }
		}

		public static void Load()
		{
			var file = Config.Instance.DataDir + "DeckStats.xml";
			try
			{
   				_instance = XmlManager<DeckStatsList>.Load(file);
                analyzeAllGames();

                 printAllPredictionsToFile("c:\\temp\\allpredictions.txt");
			    _instance = XmlManager<DeckStatsList>.Load(file);

			}
			catch(Exception)
			{
				//failed loading deckstats 
				var corruptedFile = Helper.GetValidFilePath(Config.Instance.DataDir, "DeckStats_corrupted", "xml");
				try
				{
					File.Move(file, corruptedFile);
				}
				catch(Exception)
				{
					throw new Exception("Can not load or move DeckStats.xml file. Please manually delete the file in \"%appdata\\HearthstoneDeckTracker\".");
				}

				//get latest backup file
				var backup =
					new DirectoryInfo(Config.Instance.DataDir).GetFiles("DeckStats_backup*")
					                                          .OrderByDescending(x => x.CreationTime)
					                                          .FirstOrDefault();
				if(backup != null)
				{
					try
					{
						File.Copy(backup.FullName, file);
						_instance = XmlManager<DeckStatsList>.Load(file);
					}
					catch(Exception)
					{
						throw new Exception("Error restoring DeckStats backup. Please manually rename \"DeckStats_backup.xml\" to \"DeckStats.xml\" in \"%appdata\\HearthstoneDeckTracker\".");
					}
				}
				else
				{
					//can't call ShowMessageAsync on MainWindow at this point. todo: Add something like a message queue.
					MessageBox.Show("Your DeckStats file got corrupted and there was no backup to restore from.", "Error restoring DeckStats backup");
				}
			}
		}

		public static void Save()
		{
			var file = Config.Instance.DataDir + "DeckStats.xml";
			XmlManager<DeckStatsList>.Save(file, Instance);
		}

        public static Dictionary<String, Dictionary<String, int>> predictiondictionary;

        struct cardpercent
        {
            public string cardid;
            public float percent;
        };

        
        static public void printAllPredictionsToFile(string filename)
        {
            String []classes = new[] { "Druid", "Hunter", "Mage", "Priest", "Paladin", "Shaman", "Rogue", "Warlock", "Warrior" };
            string currentprediction;
            System.IO.StreamWriter file = new System.IO.StreamWriter(filename);


            foreach(string classname in classes)
            {
                int i;
                file.WriteLine("Class: " + classname + "\n");
                for (i = 1; i < 13; i++)
                {
                    doPrediction(classname, i);
                    currentprediction = predictionText;
                    file.WriteLine(currentprediction);
                }

            }
            file.Close();

        }

        static public void doPredictionLastCard(String enemy, int turnnumber, List<string> lastplays)
        {
            List< Dictionary<String, int>> possibledictionaries = new List< Dictionary<String, int>>();
            Dictionary<String, int> newdictionary = new Dictionary<String, int>();
            cardpercent newcardpercent;
            List<cardpercent> cardpredictions = new List<cardpercent>();


            foreach(String playstring in lastplays)
            {
                String hashstring = enemy + turnnumber + playstring;
                if(predictiondictionary_priorturn.ContainsKey(hashstring) )
                {
                    possibledictionaries.Add(predictiondictionary_priorturn[hashstring]);
                }
            }

            foreach(Dictionary<String, int> currentdict in possibledictionaries)
            {
                foreach (String key in currentdict.Keys)
                {
                    if (newdictionary.ContainsKey(key))
                    {
                        newdictionary[key] += currentdict[key];
                    }
                    else
                    {
                        newdictionary.Add(key, currentdict[key]);
                    }
                }
            }

            Dictionary<String, int> innerhash = newdictionary;
            ///////////

            float numpossiblecards = 0;

            foreach (String cardid in innerhash.Keys)
            {
                if (! Hearthstone.Game.OppopentPlayedMaxNumCards(cardid))
                {
                    numpossiblecards += innerhash[cardid];
                }
            }

            foreach (String cardid in innerhash.Keys)
            {
                if (!Hearthstone.Game.OppopentPlayedMaxNumCards(cardid))
                {
                    float thiscardcount = (float)innerhash[cardid];
                    newcardpercent.cardid = cardid;
                    newcardpercent.percent = thiscardcount / numpossiblecards;
                    cardpredictions.Add(newcardpercent);
                }
            }
            if (cardpredictions.Count == 0)
            {
                doPrediction(enemy, turnnumber);
                return;
            }

            List<cardpercent> SortedList = cardpredictions.OrderBy(o => (1.0 - o.percent)).ToList();
            String predictionstring = "\nPrediction for Turn " + turnnumber + "\nbased on last card\n\n";
            int i;
            for (i = 0; i < 7 && i < SortedList.Count; i++)
            {
                Hearthstone.Card card = Hearthstone.Game.GetCardFromId(SortedList[i].cardid);
                string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
                predictionstring += card.Name + " " + percentstring + "%" + "\n";
            }

            // lastcards

            predictionstring += "\nLast cards played were\n\n";
            foreach (string lastcard in lastplays)
            {
                predictionstring += Hearthstone.Game.GetCardFromId(lastcard) + "\n";
            }
            doPrediction(enemy, turnnumber);
            predictionText = predictionText + predictionstring;

        }

        static public void doPrediction(String enemy, int turnnumber)
        {
            Dictionary<String, int> innerhash;  
            cardpercent newcardpercent;
            List<cardpercent> cardpredictions = new List<cardpercent>();

            if (! predictiondictionary.ContainsKey(enemy + turnnumber) )
            {
                predictionText = "";
                return;
            }

            innerhash = predictiondictionary[enemy + turnnumber];

            float numpossiblecards = 0;

            foreach (String cardid in innerhash.Keys)
            {
                numpossiblecards += innerhash[cardid];
            }

            foreach (String cardid in innerhash.Keys)
            {
                float thiscardcount = (float)innerhash[cardid];
                newcardpercent.cardid = cardid;
                newcardpercent.percent = thiscardcount / numpossiblecards;
                cardpredictions.Add(newcardpercent);
            }
            List<cardpercent> SortedList = cardpredictions.OrderBy(o => (1.0 -  o.percent) ).ToList();
            String predictionstring = "\nPrediction for Turn " + turnnumber + "\n\n";
            int i;
            for (i = 0; i < 8 && i < SortedList.Count; i++)
            {
                Hearthstone.Card card = Hearthstone.Game.GetCardFromId(SortedList[i].cardid);
                string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
                predictionstring += card.Name + " " + percentstring + "%" + "\n";
            }
            predictionText = predictionstring;

        }
        public static String predictionText;


        private static bool isSpell(TurnStats.Play play)
        {
            Hearthstone.Card card = Hearthstone.Game.GetCardFromId(play.CardId);
            return play.Type == PlayType.OpponentHandDiscard && (card != null && card.Type == "Spell");
        }

        private static bool isSecret(TurnStats.Play play)
        {
            return (play.Type == PlayType.OpponentHandDiscard && play.CardId == "") ||
                   play.Type == PlayType.OpponentSecretPlayed;
        }

        public static Dictionary<String, Dictionary<String, int>> predictiondictionary_priorturn;

        public static void analyzeAllGamesPriorTurn()
        {
            predictiondictionary_priorturn = new Dictionary<String, Dictionary<String, int>>();

            List<DeckStats> mydeckstats = DeckStatsList.Instance.DeckStats;
            List<string> cards_played_last_turn = new List<string>();

            foreach (DeckStats deckstats in mydeckstats)
            {
                foreach (GameStats game in deckstats.Games)
                {
                    string enemyname = game.OpponentHero;
                    foreach (TurnStats turn in game.TurnStats)
                    {
                        int turnnumbner = turn.Turn;
                        string enemy_turn_hashid = enemyname + turnnumbner;
                        foreach (String priorplayedcard in cards_played_last_turn)
                        {
                            string enemy_turn_hashid_priorcard = enemy_turn_hashid + priorplayedcard;

                            Dictionary<String, int> innerdictionary;
                            if (predictiondictionary_priorturn.ContainsKey(enemy_turn_hashid_priorcard))
                            {
                                innerdictionary = predictiondictionary_priorturn[enemy_turn_hashid_priorcard];
                            }
                            else
                            {
                                innerdictionary = new Dictionary<String, int>();
                                predictiondictionary_priorturn.Add(enemy_turn_hashid_priorcard, innerdictionary);
                            }

                            foreach (TurnStats.Play play in turn.Plays)
                            {
                                if (play.Type == PlayType.OpponentPlay || isSpell(play) ||
                                    play.Type == PlayType.OpponentHeroPower ||
                                    isSecret(play)
                                    )
                                {
                                    string cardid = play.CardId;

                                    if(  isSecret(play))
                                    {
                                        if (cardid == "")
                                        {
                                            cardid = "Secret Played";
                                        }
                                    }


                                    /// first create/get the inner hash
                                    /// 
                                    if (innerdictionary.ContainsKey(cardid))
                                    {
                                        int count = innerdictionary[cardid];
                                        count++;
                                        innerdictionary[cardid] = count;
                                    }
                                    else
                                    {
                                        innerdictionary.Add(cardid, 1);
                                    }
                                }
                            } //foreach turnstats in play
                        }/// for each prior played card
                        //
                        cards_played_last_turn.Clear();
                        foreach (TurnStats.Play play in turn.Plays)
                        {
                            if (play.Type == PlayType.OpponentPlay || isSpell(play))
                            {
                                cards_played_last_turn.Add(play.CardId);
                            }
                        }

                    }
                }
            }
        }

        public static void analyzeAllGames()
        {
            analyzeAllGamesPriorTurn();

            predictiondictionary = new Dictionary<String, Dictionary<String, int>>();

            List<DeckStats> mydeckstats = DeckStatsList.Instance.DeckStats;

            foreach (DeckStats deckstats in mydeckstats)
            {
                foreach (GameStats game in deckstats.Games)
                {
                    string enemyname = game.OpponentHero;
                    foreach (TurnStats turn in game.TurnStats)
                    {
                        int turnnumbner = turn.Turn;
                        string enemy_turn_hashid = enemyname + turnnumbner;
                        Dictionary<String, int> innerdictionary;
                        if (predictiondictionary.ContainsKey(enemy_turn_hashid))
                        {
                            innerdictionary = predictiondictionary[enemy_turn_hashid];
                        }
                        else
                        {
                            innerdictionary = new Dictionary<String, int>();
                            predictiondictionary.Add(enemy_turn_hashid, innerdictionary);
                        }

                        foreach (TurnStats.Play play in turn.Plays)
                        {
                            if (play.Type == PlayType.OpponentPlay || isSpell(play) ||
                                play.Type == PlayType.OpponentHeroPower ||
                                isSecret(play)
                                )
                            {
                                string cardid = play.CardId;

                                if (isSecret(play)) 
                                {
                                    if (cardid == "")
                                    {
                                        cardid = "Secret Played";
                                    }
                                    else
                                    {
                                        int i = 5;
                                    }
                                }

                                /// first create/get the inner hash
                                /// 
                                if (innerdictionary.ContainsKey(cardid))
                                {
                                    int count = innerdictionary[cardid];
                                    count++;
                                    innerdictionary[cardid] = count;
                                }
                                else
                                {
                                    innerdictionary.Add(cardid, 1);
                                }
                            }
                        }
                    }
                }
            }

        }
	}
}