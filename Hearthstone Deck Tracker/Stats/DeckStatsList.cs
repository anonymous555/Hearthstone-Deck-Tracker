#region

using Hearthstone_Deck_Tracker.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Hearthstone;

#endregion

namespace Hearthstone_Deck_Tracker.Stats
{
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
	        get
	        {
	            if (_instance == null)
	                Load();
	            return _instance ?? (_instance = new DeckStatsList());
	        }
	    }

	    public static void Load()
		{
            SetupDeckStatsFile();
			var file = Config.Instance.DataDir + "DeckStats.xml";
			if(!File.Exists(file))
				return;
			try
			{
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
					throw new Exception(
						"Can not load or move DeckStats.xml file. Please manually delete the file in \"%appdata\\HearthstoneDeckTracker\".");
				}

				//get latest backup file
				var backup =
					new DirectoryInfo(Config.Instance.DataDir).GetFiles("DeckStats_backup*").OrderByDescending(x => x.CreationTime).FirstOrDefault();
				if(backup != null)
				{
					try
					{
						File.Copy(backup.FullName, file);
						_instance = XmlManager<DeckStatsList>.Load(file);
					}
					catch(Exception ex)
					{
						throw new Exception(
							"Error restoring DeckStats backup. Please manually rename \"DeckStats_backup.xml\" to \"DeckStats.xml\" in \"%appdata\\HearthstoneDeckTracker\".",
							ex);
					}
				}
				else
					throw new Exception("DeckStats.xml is corrupted.");
			}
            analyzeAllGames();
		}

        internal static void SetupDeckStatsFile()
        {
            if(Config.Instance.SaveDataInAppData == null)
                return;
            var appDataPath = Config.AppDataPath + @"\DeckStats.xml";
            var appDataGamesDirPath = Config.AppDataPath + @"\Games";
            var dataDirPath = Config.Instance.DataDirPath + @"\DeckStats.xml";
            var dataGamesDirPath = Config.Instance.DataDirPath + @"\Games";
            if(Config.Instance.SaveDataInAppData.Value)
            {
                if(File.Exists(dataDirPath))
                {
                    if(File.Exists(appDataPath))
                    {
                        //backup in case the file already exists
                        var time = DateTime.Now.ToFileTime();
                        File.Move(appDataPath, appDataPath + time);
                        if(Directory.Exists(appDataGamesDirPath))
                        {
                            Helper.CopyFolder(appDataGamesDirPath, appDataGamesDirPath + time);
                            Directory.Delete(appDataGamesDirPath, true);
                        }
                        Logger.WriteLine("Created backups of DeckStats and Games in appdata", "Load");
                    }
                    File.Move(dataDirPath, appDataPath);
                    Logger.WriteLine("Moved DeckStats to appdata", "Load");
                    if(Directory.Exists(dataGamesDirPath))
                    {
                        Helper.CopyFolder(dataGamesDirPath, appDataGamesDirPath);
                        Directory.Delete(dataGamesDirPath, true);
                    }
                    Logger.WriteLine("Moved Games to appdata", "Load");
                }
            }
            else if(File.Exists(appDataPath))
            {
                if(File.Exists(dataDirPath))
                {
                    //backup in case the file already exists
                    var time = DateTime.Now.ToFileTime();
                    File.Move(dataDirPath, dataDirPath + time);
                    if(Directory.Exists(dataGamesDirPath))
                    {
                        Helper.CopyFolder(dataGamesDirPath, dataGamesDirPath + time);
                        Directory.Delete(dataGamesDirPath, true);
                    }
                    Logger.WriteLine("Created backups of deckstats and games locally", "Load");
                }
                File.Move(appDataPath, dataDirPath);
                Logger.WriteLine("Moved DeckStats to local", "Load");
                if(Directory.Exists(appDataGamesDirPath))
                {
                    Helper.CopyFolder(appDataGamesDirPath, dataGamesDirPath);
                    Directory.Delete(appDataGamesDirPath, true);
                }
                Logger.WriteLine("Moved Games to appdata", "Load");
            }

            var filePath = Config.Instance.DataDir + "DeckStats.xml";
            //create file if it does not exist
            if(!File.Exists(filePath))
            {
                using(var sr = new StreamWriter(filePath, false))
                    sr.WriteLine("<DeckStatsList></DeckStatsList>");
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
            String[] classes = new[] { "Druid", "Hunter", "Mage", "Priest", "Paladin", "Shaman", "Rogue", "Warlock", "Warrior" };
            String currentprediction;
            System.IO.StreamWriter file = new System.IO.StreamWriter(filename);


            foreach (string classname in classes)
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
        const int MAX_PREDICTIONS = 10;

        static public void doPrediction2away(String enemy, int turnnumber, List<string> lastplays, List<string> lastplays2away)
        {
            List<Dictionary<String, int>> possibledictionaries = new List<Dictionary<String, int>>();
            Dictionary<String, int> newdictionary = new Dictionary<String, int>();
            cardpercent newcardpercent;
            List<cardpercent> cardpredictions = new List<cardpercent>();


            foreach (String playstring in lastplays)
            {
                foreach (String playstring2away in lastplays2away)
                {
                    String hashstring = enemy + turnnumber + playstring2away + playstring;
                    if (predictiondictionary_2_awaypriorturn.ContainsKey(hashstring))
                    {
                        possibledictionaries.Add(predictiondictionary_2_awaypriorturn[hashstring]);
                    }
                }
            }

            foreach (Dictionary<String, int> currentdict in possibledictionaries)
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
                if (!Hearthstone.GameV2.OppopentPlayedMaxNumCards(cardid))
                {
                    numpossiblecards += innerhash[cardid];
                }
            }

            foreach (String cardid in innerhash.Keys)
            {
                if (!Hearthstone.GameV2.OppopentPlayedMaxNumCards(cardid))
                {
                    float thiscardcount = (float)innerhash[cardid];
                    newcardpercent.cardid = cardid;
                    newcardpercent.percent = thiscardcount / numpossiblecards;
                    cardpredictions.Add(newcardpercent);
                }
            }

            if (cardpredictions.Count == 0 )
            {
                doPredictionLastCard(enemy, turnnumber, lastplays);
                return;
            }

            List<cardpercent> SortedList = cardpredictions.OrderBy(o => (1.0 - o.percent)).ToList();
            String predictionstring = "\nPrediction for Turn " + turnnumber + "\nbased on 2nd last card\n\n";

            Hearthstone.Deck tempdeck = new Hearthstone.Deck();
            int i;
            for (i = 0; i < MAX_PREDICTIONS && i < SortedList.Count; i++)
            {
                Hearthstone.Card card = Hearthstone.Database.GetCardFromId(SortedList[i].cardid);
                if (card == null)
                {
                    string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
                    predictionstring += SortedList[i].cardid + " ??? " + percentstring + "%" + "\n";
                }
                else
                {
                    string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
                    /////                    predictionstring += card.Name + " " + percentstring + "%" + "\n";
                    card.LocalizedName = card.LocalizedName + " " + percentstring;
                    tempdeck.Cards.Add(card);
                }
            }


            // lastcards
            /*
                        predictionstring += "\nLast cards played were\n\n";
                        foreach (string lastcard in lastplays)
                        {
                            predictionstring += Hearthstone.Game.GetCardFromId(lastcard) + "\n";
                        }
             */

            doPrediction(enemy, turnnumber);

            ///////////////////// archetype

            var enemycards = GameV2.getOpponentCards();

            string archetypestring = ArchetypeDetector.getIngameArchetypeString(enemycards);
            ///////////////////////

            predictionText = /* predictionText + */ predictionstring + archetypestring;
            Helper.MainWindow.Overlay.ListViewPrediction.ItemsSource = tempdeck.Cards;

        }


        static public void doPredictionLastCard(String enemy, int turnnumber, List<string> lastplays)
        {
            List<Dictionary<String, int>> possibledictionaries = new List<Dictionary<String, int>>();
            Dictionary<String, int> newdictionary = new Dictionary<String, int>();
            cardpercent newcardpercent;
            List<cardpercent> cardpredictions = new List<cardpercent>();


            foreach (String playstring in lastplays)
            {
                String hashstring = enemy + turnnumber + playstring;
                if (predictiondictionary_priorturn.ContainsKey(hashstring))
                {
                    possibledictionaries.Add(predictiondictionary_priorturn[hashstring]);
                }
            }

            foreach (Dictionary<String, int> currentdict in possibledictionaries)
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
                if (!Hearthstone.GameV2.OppopentPlayedMaxNumCards(cardid))
                {
                    numpossiblecards += innerhash[cardid];
                }
            }

            foreach (String cardid in innerhash.Keys)
            {
                if (!Hearthstone.GameV2.OppopentPlayedMaxNumCards(cardid))
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

            Hearthstone.Deck tempdeck = new Hearthstone.Deck();
            int i;
            for (i = 0; i < MAX_PREDICTIONS && i < SortedList.Count; i++)
            {
                Hearthstone.Card card = Hearthstone.Database.GetCardFromId(SortedList[i].cardid);
                if (card == null)
                {
                    string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
                    predictionstring += SortedList[i].cardid + " ??? " + percentstring + "%" + "\n";
                }
                else
                {
                    string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
/////                    predictionstring += card.Name + " " + percentstring + "%" + "\n";
                    card.LocalizedName = card.LocalizedName + " " + percentstring;
                    tempdeck.Cards.Add(card);
                }
            }


            // lastcards
/*
            predictionstring += "\nLast cards played were\n\n";
            foreach (string lastcard in lastplays)
            {
                predictionstring += Hearthstone.Game.GetCardFromId(lastcard) + "\n";
            }
 */

            doPrediction(enemy, turnnumber);

            ///////////////////// archetype

            var enemycards = GameV2.getOpponentCards();

            string archetypestring = ArchetypeDetector.getIngameArchetypeString(enemycards);
            ///////////////////////

            predictionText = /* predictionText + */ predictionstring + archetypestring;
            Helper.MainWindow.Overlay.ListViewPrediction.ItemsSource = tempdeck.Cards;

        }

        static public void doPrediction(String enemy, int turnnumber)
        {
            Dictionary<String, int> innerhash;
            cardpercent newcardpercent;
            List<cardpercent> cardpredictions = new List<cardpercent>();
            Hearthstone.Deck tempdeck;

            if (!predictiondictionary.ContainsKey(enemy + turnnumber))
            {
                predictionText = "";
                tempdeck = new Hearthstone.Deck();
                Helper.MainWindow.Overlay.ListViewPrediction.ItemsSource = tempdeck.Cards;
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
            List<cardpercent> SortedList = cardpredictions.OrderBy(o => (1.0 - o.percent)).ToList();
            String predictionstring = "\nPrediction for Turn " + turnnumber + "\n\n";
            int i;

            tempdeck = new Hearthstone.Deck();

            for (i = 0; i < MAX_PREDICTIONS && i < SortedList.Count; i++)
            {
                Hearthstone.Card card = Hearthstone.Database.GetCardFromId(SortedList[i].cardid);
                string percentstring = (SortedList[i].percent * 100.0).ToString("0.0");
/////                predictionstring += card.Name + " " + percentstring + "%" + "\n";
                card.LocalizedName = card.LocalizedName + " " + percentstring;
                tempdeck.Cards.Add(card);

            }
            //////// archetype 
            var enemycards = GameV2.getOpponentCards();
            string archetypestring = ArchetypeDetector.getIngameArchetypeString(enemycards);

            //// archetype

            predictionText = predictionstring + archetypestring;
            Helper.MainWindow.Overlay.ListViewPrediction.ItemsSource = tempdeck.Cards;

        }
        public static String predictionText;


        private static bool isSpell(TurnStats.Play play)
        {
            Hearthstone.Card card = Hearthstone.Database.GetCardFromId(play.CardId);
            return play.Type == PlayType.OpponentHandDiscard && (card != null && card.Type == "Spell");
        }

        private static bool isSecret(TurnStats.Play play)
        {
            return (play.Type == PlayType.OpponentHandDiscard && play.CardId == "") ||
                   play.Type == PlayType.OpponentSecretPlayed;
        }

        public static Dictionary<String, Dictionary<String, int>> predictiondictionary_priorturn;
        public static Dictionary<String, Dictionary<String, int>> predictiondictionary_2_awaypriorturn;


        public static void analyzeAllGames2AwayPrior()
        {
            predictiondictionary_2_awaypriorturn = new Dictionary<String, Dictionary<String, int>>();

            List<DeckStats> mydeckstats = DeckStatsList.Instance.DeckStats;
            List<string> cards_played_last_turn = new List<string>();
            List<string> cards_played_2nd_to_last_turn = new List<string>();

            foreach (DeckStats deckstats in mydeckstats)
            {
                foreach (GameStats game in deckstats.Games)
                {
                    TimeSpan timespan = game.EndTime.Subtract(new DateTime(2014, 3, 11));
                    int nummonths = timespan.Days / 30;
                    int bias = nummonths;


                    string enemyname = game.OpponentHero;
                    foreach (TurnStats turn in game.TurnStats)
                    {
                        int turnnumbner = turn.Turn;
                        
                        foreach (String priorplayedcard in cards_played_2nd_to_last_turn)
                        {
                            string enemy_turn_hashid = enemyname + turnnumbner;

                            foreach (String second_to_last_card in cards_played_last_turn)
                            {
                                string enemy_turn_hashid_priorcard = enemy_turn_hashid + second_to_last_card + priorplayedcard;

                            Dictionary<String, int> innerdictionary;
                            if (predictiondictionary_2_awaypriorturn.ContainsKey(enemy_turn_hashid_priorcard))
                            {
                                innerdictionary = predictiondictionary_2_awaypriorturn[enemy_turn_hashid_priorcard];
                            }
                            else
                            {
                                innerdictionary = new Dictionary<String, int>();
                                predictiondictionary_2_awaypriorturn.Add(enemy_turn_hashid_priorcard, innerdictionary);
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
                                        if (cardid == "" || cardid == null)
                                        {
                                            cardid = "Secret Played";
                                        }
                                    }


                                    /// first create/get the inner hash
                                    /// 
                                    if (innerdictionary.ContainsKey(cardid))
                                    {
                                        int count = innerdictionary[cardid];
                                        count += bias;
                                        innerdictionary[cardid] = count;
                                    }
                                    else
                                    {
                                        innerdictionary.Add(cardid, bias);
                                    }
                                }
                            }/// 2nd to last played card
                            } //foreach turnstats in play
                        }/// for each prior played card
                        //
                        cards_played_2nd_to_last_turn = cards_played_last_turn;
                        cards_played_last_turn.Clear();
                        foreach (TurnStats.Play play in turn.Plays)
                        {
                            if (play.Type == PlayType.OpponentPlay || isSpell(play) || play.Type == PlayType.OpponentHeroPower)
                            {
                                cards_played_last_turn.Add(play.CardId);
                            }
                        }

                    }
                }
            }
        }


        public static void analyzeAllGamesPriorTurn()
        {
            predictiondictionary_priorturn = new Dictionary<String, Dictionary<String, int>>();

            List<DeckStats> mydeckstats = DeckStatsList.Instance.DeckStats;
            List<string> cards_played_last_turn = new List<string>();

            foreach (DeckStats deckstats in mydeckstats)
            {
                foreach (GameStats game in deckstats.Games)
                {
                    TimeSpan timespan = game.EndTime.Subtract(new DateTime(2014, 3, 11));
                    int nummonths = timespan.Days / 30;
                    int bias = nummonths;


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

                                    if (isSecret(play))
                                    {
                                        if (cardid == "" || cardid == null)
                                        {
                                            cardid = "Secret Played";
                                        }
                                    }


                                    /// first create/get the inner hash
                                    /// 
                                    if (innerdictionary.ContainsKey(cardid))
                                    {
                                        int count = innerdictionary[cardid];
                                        count += bias;
                                        innerdictionary[cardid] = count;
                                    }
                                    else
                                    {
                                        innerdictionary.Add(cardid, bias);
                                    }
                                }
                            } //foreach turnstats in play
                        }/// for each prior played card
                        //
                        cards_played_last_turn.Clear();
                        foreach (TurnStats.Play play in turn.Plays)
                        {
                            if (play.Type == PlayType.OpponentPlay || isSpell(play) || play.Type == PlayType.OpponentHeroPower)
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
            analyzeAllGames2AwayPrior();

            predictiondictionary = new Dictionary<String, Dictionary<String, int>>();

            List<DeckStats> mydeckstats = DeckStatsList.Instance.DeckStats;

            foreach (DeckStats deckstats in mydeckstats)
            {
                foreach (GameStats game in deckstats.Games)
                {
                    string enemyname = game.OpponentHero;

                    TimeSpan timespan = game.EndTime.Subtract(new DateTime(2014, 3, 11));
                    int nummonths = timespan.Days / 30;
                    int bias = nummonths;

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
                                    if (cardid == "" || cardid == null)
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
                                    count += bias;
                                    innerdictionary[cardid] = count;
                                }
                                else
                                {
                                    innerdictionary.Add(cardid, bias);
                                }
                            }
                        }
                    }
                }
            }

        }

	}
}