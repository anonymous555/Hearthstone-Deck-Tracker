#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Controls.Stats;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Stats;
using Hearthstone_Deck_Tracker.Utility;
using System.IO;

#endregion

namespace Hearthstone_Deck_Tracker.Hearthstone
{
	public class Deck : ICloneable, INotifyPropertyChanged
	{
		private const string baseHearthStatsUrl = @"http://hss.io/d/";

		private readonly string[] _relevantMechanics =
		{
			"Battlecry",
			"Charge",
			"Combo",
			"Deathrattle",
			"Divine Shield",
			"Freeze",
			"Inspire",
			"Secret",
			"Spellpower",
			"Taunt",
			"Windfury"
		};

		private bool _archived;

		private ArenaReward _arenaReward = new ArenaReward();
		private List<GameStats> _cachedGames;
		private Guid _deckId;
		private int? _dustReward;
		private int? _goldReward;
		private string _hearthStatsIdClone;
		private bool? _isArenaDeck;
		private bool _isSelectedInGui;
		private DateTime _lastCacheUpdate = DateTime.MinValue;
		private string _name;
		private string _note;
		private SerializableVersion _selectedVersion = new SerializableVersion(1, 0);
		private List<string> _tags;

		[XmlArray(ElementName = "Cards")]
		[XmlArrayItem(ElementName = "Card")]
		public ObservableCollection<Card> Cards;

		public string Class;
		public string HearthStatsArenaId;
		public string HearthStatsDeckVersionId;
		public string HearthStatsId;
		public DateTime LastEdited;

		[XmlArray(ElementName = "MissingCards")]
		[XmlArrayItem(ElementName = "Card")]
		public List<Card> MissingCards;

		public string Url;
		public SerializableVersion Version = new SerializableVersion(1, 0);

		[XmlArray(ElementName = "DeckHistory")]
		[XmlArrayItem(ElementName = "Deck")]
		public List<Deck> Versions;

		public Deck()
		{
			Cards = new ObservableCollection<Card>();
			MissingCards = new List<Card>();
			Tags = new List<string>();
			Note = string.Empty;
			Url = string.Empty;
			Name = string.Empty;
			Archived = false;
			SyncWithHearthStats = null;
			HearthStatsId = string.Empty;
			Version = SerializableVersion.Default;
			Versions = new List<Deck>();
			DeckId = Guid.NewGuid();
		}

		public Deck(string name, string className, IEnumerable<Card> cards, IEnumerable<string> tags, string note, string url,
		            DateTime lastEdited, bool archived, List<Card> missingCards, SerializableVersion version, IEnumerable<Deck> versions,
		            bool? syncWithHearthStats, string hearthStatsId, Guid deckId, string hearthStatsDeckVersionId,
		            string hearthStatsIdClone = null, SerializableVersion selectedVersion = null, bool? isArenaDeck = null,
		            ArenaReward reward = null)

		{
			Name = name;
			Class = className;
			Cards = new ObservableCollection<Card>();
			MissingCards = missingCards;
			foreach(var card in cards.ToSortedCardList())
				Cards.Add((Card)card.Clone());
			Tags = new List<string>(tags);
			Note = note;
			Url = url;
			LastEdited = lastEdited;
			Archived = archived;
			Version = version;
			SyncWithHearthStats = syncWithHearthStats;
			HearthStatsId = hearthStatsId;
			SelectedVersion = selectedVersion ?? version;
			Versions = new List<Deck>();
			DeckId = deckId;
			if(hearthStatsIdClone != null)
				HearthStatsIdForUploading = hearthStatsIdClone;
			if(isArenaDeck.HasValue)
				IsArenaDeck = isArenaDeck.Value;
			HearthStatsDeckVersionId = hearthStatsDeckVersionId;
			if(versions != null)
			{
				foreach(var d in versions)
					Versions.Add(d.CloneWithNewId(true) as Deck);
			}
			if(reward != null)
				_arenaReward = reward;
		}

		public bool Archived
		{
			get { return _archived; }
			set
			{
				_archived = value;
				OnPropertyChanged();
				OnPropertyChanged("ArchivedVisibility");
			}
		}

		public string Note
		{
			get { return _note; }
			set
			{
				_note = value;
				OnPropertyChanged();
				OnPropertyChanged("NoteVisibility");
			}
		}

		public SerializableVersion SelectedVersion
		{
			get { return VersionsIncludingSelf.Contains(_selectedVersion) ? _selectedVersion : Version; }
			set
			{
				_selectedVersion = value;
				OnPropertyChanged();
				OnPropertyChanged("NameAndVersion");
			}
		}

		public bool HearthStatsIdsAlreadyReset { get; set; }

		[XmlIgnore]
		public bool IsSelectedInGui
		{
			get { return _isSelectedInGui; }
			set
			{
				_isSelectedInGui = value;
				OnPropertyChanged("GetFontWeight");
			}
		}

		public bool IsArenaDeck
		{
			get { return _isArenaDeck ?? (_isArenaDeck = CheckIfArenaDeck()) ?? false; }
			set { _isArenaDeck = value; }
		}

		public ArenaReward ArenaReward
		{
			get { return IsArenaDeck ? _arenaReward : null; }
			set
			{
				if(IsArenaDeck)
					_arenaReward = value;
			}
		}

		public bool? IsArenaRunCompleted
		{
			get
			{
				return IsArenaDeck
					       ? (DeckStats.Games.Count(g => g.Result == GameResult.Win) == 12
					          || DeckStats.Games.Count(g => g.Result == GameResult.Loss) == 3) as bool? : null;
			}
		}

		public Guid DeckId
		{
			get { return _deckId == Guid.Empty ? (_deckId = Guid.NewGuid()) : _deckId; }
			set { _deckId = value; }
		}

		public string Name
		{
			get { return _name; }
			set
			{
				_name = value;
				OnPropertyChanged();
				OnPropertyChanged("NameAndVersion");
			}
		}

		public bool? SyncWithHearthStats { get; set; }

		[XmlIgnore]
		public bool HasHearthStatsId
		{
			get { return !string.IsNullOrEmpty(HearthStatsId) || !string.IsNullOrEmpty(_hearthStatsIdClone); }
		}

		[XmlIgnore]
		public string HearthStatsUrl
		{
			get
			{
				return HasHearthStatsId
					       ? baseHearthStatsUrl + HearthStatsId : (HasHearthStatsArenaId ? baseHearthStatsUrl + HearthStatsArenaId : "");
			}
		}

		[XmlIgnore]
		public bool HasHearthStatsDeckVersionId
		{
			get { return !string.IsNullOrEmpty(HearthStatsDeckVersionId); }
		}

		[XmlIgnore]
		public List<SerializableVersion> VersionsIncludingSelf
		{
			get { return Versions.Select(x => x.Version).Concat(new[] {Version}).ToList(); }
		}

		[XmlIgnore]
		public string NameAndVersion
		{
			get
			{
				if(Config.Instance.DeckPickerCaps)
				{
					return Versions.Count == 0
						       ? Name.ToUpperInvariant()
						       : string.Format("{0} (v{1}.{2})", Name.ToUpperInvariant(), SelectedVersion.Major, SelectedVersion.Minor);
				}
				return Versions.Count == 0 ? Name : string.Format("{0} (v{1}.{2})", Name, SelectedVersion.Major, SelectedVersion.Minor);
			}
		}

		[XmlIgnore]
		public string WinLossString
		{
			get
			{
				var relevantGames = GetRelevantGames();
				if(relevantGames.Count == 0)
					return "0-0";
				return string.Format("{0}-{1}", relevantGames.Count(g => g.Result == GameResult.Win),
				                     relevantGames.Count(g => g.Result == GameResult.Loss));
			}
		}

		[XmlIgnore]
		public string WinPercentString
		{
			get
			{
				var relevantGames = GetRelevantGames();
				if(relevantGames.Count == 0)
					return "-";
				return Math.Round(100.0 * relevantGames.Count(g => g.Result == GameResult.Win) / relevantGames.Count, 0) + "%";
			}
		}

		[XmlIgnore]
		public double WinPercent
		{
			get
			{
				var relevantGames = GetRelevantGames();
				if(relevantGames.Count == 0)
					return 0.0;
				return 100.0 * relevantGames.Count(g => g.Result == GameResult.Win) / relevantGames.Count;
			}
		}

		[XmlIgnore]
		public string StatsString
		{
			get { return GetRelevantGames().Any() ? string.Format("{0} | {1}", WinPercentString, WinLossString) : "NO STATS"; }
		}

		[XmlIgnore]
		public DateTime LastPlayed
		{
			get
			{
				var games = DeckStats.Games;
				return !games.Any() ? DateTime.MinValue : games.OrderByDescending(g => g.StartTime).First().StartTime;
			}
		}

		[XmlIgnore]
		public DateTime LastPlayedNewFirst
		{
			get
			{
				var games = DeckStats.Games;
				return !games.Any() ? LastEdited : games.OrderByDescending(g => g.StartTime).First().StartTime;
			}
		}

		[XmlIgnore]
		public string GetClass
		{
			get { return string.IsNullOrEmpty(Class) ? "(No Class Selected)" : "(" + Class + ")"; }
		}

		[XmlIgnore]
		public FontWeight GetFontWeight
		{
			get { return IsSelectedInGui ? FontWeights.Black : FontWeights.Regular; }
		}

		[XmlArray(ElementName = "Tags")]
		[XmlArrayItem(ElementName = "Tag")]
		public List<string> Tags
		{
			get { return _tags; }
			set
			{
				_tags = value;
				OnPropertyChanged();
				OnPropertyChanged("TagList");
			}
		}

		[XmlIgnore]
		public string TagList
		{
			get { return Tags.Count > 0 ? Tags.Select(x => x.ToUpperInvariant()).Aggregate((t, n) => t + " | " + n) : ""; }
		}

		[XmlIgnore]
		public SolidColorBrush ClassColorBrush
		{
			get { return new SolidColorBrush(ClassColor); }
		}

		[XmlIgnore]
		public Color ClassColor
		{
			get { return Helper.GetClassColor(Class, false); }
		}

		[XmlIgnore]
		public BitmapImage ClassImage
		{
			get
			{
				HeroClassAll heroClass;
				if(Enum.TryParse(Class, out heroClass))
					return ImageCache.GetClassIcon(heroClass);
				return new BitmapImage();
			}
		}

		[XmlIgnore]
		public BitmapImage HeroImage
		{
			get { return ClassImage; }
		}

        private String lastachetype = null;
        [XmlIgnore]
        public String Archetype
        {
            get
            {
                if (lastachetype == null)
                {

                    lastachetype = ArchetypeDetector.getBestArchetypeString(this, true);
                }
                return lastachetype;
            }
        }

        [XmlIgnore]
        public String ArchetypeNoCounts
        {
            get
            {
                if (lastachetype == null)
                {

                    lastachetype = ArchetypeDetector.getBestArchetypeString(this, false);
                }
                return lastachetype;
            }
        }


		public DeckStats DeckStats
		{
			get
			{
				var deckStats = DeckStatsList.Instance.DeckStats.FirstOrDefault(ds => ds.BelongsToDeck(this));
				if(deckStats == null)
				{
					deckStats = new DeckStats(this);
					DeckStatsList.Instance.DeckStats.Add(deckStats);
				}
				return deckStats;
			}
		}

		[XmlIgnore]
		public bool HasVersions
		{
			get { return Versions != null && Versions.Count > 0; }
		}

		public bool HasHearthStatsArenaId
		{
			get { return !string.IsNullOrEmpty(HearthStatsArenaId); }
		}

		//I don't know why I need this but I apparently can't serialize anything if versions of a deck have the same hearthstatsid

		public string HearthStatsIdForUploading
		{
			get { return !string.IsNullOrEmpty(HearthStatsId) ? HearthStatsId : _hearthStatsIdClone; }
			set { _hearthStatsIdClone = value; }
		}

		public Visibility VisibilityStats
		{
			get { return GetRelevantGames().Any() ? Visibility.Visible : Visibility.Collapsed; }
		}

		public Visibility VisibilityNoStats
		{
			get { return VisibilityStats == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility NoteVisibility
		{
			get { return string.IsNullOrEmpty(Note) ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility ArchivedVisibility
		{
			get { return Archived ? Visibility.Visible : Visibility.Collapsed; }
		}

		private TimeSpan ValidCacheDuration
		{
			get { return new TimeSpan(0, 0, 1); }
		}

		private bool CacheIsValid
		{
			get { return _cachedGames != null && DateTime.Now - _lastCacheUpdate < ValidCacheDuration; }
		}

		[XmlIgnore]
		public List<Mechanic> Mechanics
		{
			get { return _relevantMechanics.Select(x => new Mechanic(x, this)).Where(m => m.Count > 0).ToList(); }
		}

		public object Clone()
		{
			return new Deck(Name, Class, Cards, Tags, Note, Url, LastEdited, Archived, MissingCards, Version, Versions, SyncWithHearthStats,
			                HearthStatsId, DeckId, HearthStatsDeckVersionId, HearthStatsIdForUploading, SelectedVersion, _isArenaDeck,
			                ArenaReward);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public List<GameStats> GetRelevantGames()
		{
			if(CacheIsValid)
				return _cachedGames;
			var filtered = Config.Instance.DisplayedMode == GameMode.All
				               ? DeckStats.Games
				               : (IsArenaDeck
					                  ? DeckStats.Games.Where(g => g.GameMode == GameMode.Arena).ToList()
					                  : DeckStats.Games.Where(g => g.GameMode == Config.Instance.DisplayedMode).ToList());
			switch(Config.Instance.DisplayedTimeFrame)
			{
				case DisplayedTimeFrame.AllTime:
					break;
				case DisplayedTimeFrame.CurrentSeason:
					filtered = filtered.Where(g => g.StartTime > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).ToList();
					break;
				case DisplayedTimeFrame.ThisWeek:
					filtered = filtered.Where(g => g.StartTime > DateTime.Today.AddDays(-((int)g.StartTime.DayOfWeek + 1))).ToList();
					break;
				case DisplayedTimeFrame.Today:
					filtered = filtered.Where(g => g.StartTime > DateTime.Today).ToList();
					break;
				case DisplayedTimeFrame.Custom:
					if(Config.Instance.CustomDisplayedTimeFrame.HasValue)
						filtered = filtered.Where(g => g.StartTime > Config.Instance.CustomDisplayedTimeFrame.Value).ToList();
					break;
			}
			switch(Config.Instance.DisplayedStats)
			{
				case DisplayedStats.All:
					break;
				case DisplayedStats.Latest:
					filtered = filtered.Where(g => g.BelongsToDeckVerion(this)).ToList();
					break;
				case DisplayedStats.Selected:
					filtered = filtered.Where(g => g.BelongsToDeckVerion(GetSelectedDeckVersion())).ToList();
					break;
				case DisplayedStats.LatestMajor:
					filtered =
						filtered.Where(g => VersionsIncludingSelf.Where(v => v.Major == Version.Major).Select(GetVersion).Any(g.BelongsToDeckVerion))
						        .ToList();
					break;
				case DisplayedStats.SelectedMajor:
					filtered =
						filtered.Where(
						               g =>
						               VersionsIncludingSelf.Where(v => v.Major == SelectedVersion.Major).Select(GetVersion).Any(g.BelongsToDeckVerion))
						        .ToList();
					break;
			}
			_cachedGames = filtered;
			_lastCacheUpdate = DateTime.Now;
			return filtered;
		}

		public void ResetHearthstatsIds()
		{
			HearthStatsArenaId = null;
			HearthStatsDeckVersionId = null;
			HearthStatsId = null;
			_hearthStatsIdClone = null;
			HearthStatsIdsAlreadyReset = true;
		}

		public bool? CheckIfArenaDeck()
		{
			return !DeckStats.Games.Any() ? (bool?)null : DeckStats.Games.All(g => g.GameMode == GameMode.Arena);
		}

		public Deck GetVersion(int major, int minor)
		{
			var target = new SerializableVersion(major, minor);
			if(Version == target)
				return this;
			return Versions.FirstOrDefault(x => x.Version == target);
		}

		public Deck GetVersion(SerializableVersion version)
		{
			if(version == null)
				return null;
			return GetVersion(version.Major, version.Minor);
		}

		public bool HasVersion(SerializableVersion version)
		{
			return Version == version || Versions.Any(v => v.Version == version);
		}

		public object CloneWithNewId(bool isVersion)
		{
			return new Deck(Name, Class, Cards, Tags, Note, Url, LastEdited, Archived, MissingCards, Version, Versions, SyncWithHearthStats, "",
			                Guid.NewGuid(), HearthStatsDeckVersionId, isVersion ? HearthStatsIdForUploading : "", SelectedVersion, _isArenaDeck);
		}

		public void ResetVersions()
		{
			Versions = new List<Deck>();
			Version = SerializableVersion.Default;
			SelectedVersion = Version;
		}

		public Deck GetSelectedDeckVersion()
		{
			return Versions == null ? this : Versions.FirstOrDefault(d => d.Version == SelectedVersion) ?? this;
		}

		public void SelectVersion(SerializableVersion version)
		{
			SelectedVersion = version;
		}

		public void SelectVersion(Deck deck)
		{
			SelectVersion(deck.Version);
		}

		public string GetDeckInfo()
		{
			return string.Format("deckname:{0}, class:{1}, cards:{2}", Name.Replace("{", "").Replace("}", ""), Class, Cards.Sum(x => x.Count));
		}

        public int GetTotalNumCards()
        {
            int count = 0;
            foreach (var card in Cards)
            {
                count += card.Count;
            }
            return count;

        }


		/// returns the number of cards in the deck with mechanics matching the newmechanic.
		/// The mechanic attribute, such as windfury or taunt, comes from the cardDB json file
		public int GetMechanicCount(string newmechanic)
		{
			return
				Cards.Where(card => card.Mechanics != null)
				     .Sum(card => card.Mechanics.Count(mechanic => mechanic.Equals(newmechanic)) * card.Count);
		}

		public int GetNumTaunt()
		{
			return GetMechanicCount("Taunt");
		}

		public int GetNumBattlecry()
		{
			return GetMechanicCount("Battlecry");
		}

		public int GetNumImmuneToSpellpower()
		{
			return GetMechanicCount("ImmuneToSpellpower");
		}

		public int GetNumSpellpower()
		{
			return GetMechanicCount("Spellpower");
		}

		public int GetNumOneTurnEffect()
		{
			return GetMechanicCount("OneTurnEffect");
		}

		public int GetNumCharge()
		{
			return GetMechanicCount("Charge") + GetMechanicCount("GrantCharge");
		}

		public int GetNumFreeze()
		{
			return GetMechanicCount("Freeze");
		}

		public int GetNumAdjacentBuff()
		{
			return GetMechanicCount("AdjacentBuff");
		}

		public int GetNumSecret()
		{
			return GetMechanicCount("Secret");
		}

		public int GetNumDeathrattle()
		{
			return GetMechanicCount("Deathrattle");
		}

		public int GetNumWindfury()
		{
			return GetMechanicCount("Windfury");
		}

		public int GetNumDivineShield()
		{
			return GetMechanicCount("Divine Shield");
		}

		public int GetNumCombo()
		{
			return GetMechanicCount("Combo");
		}

		public override string ToString()
		{
			return string.Format("{0} ({1})", Name, Class);
		}

        public bool isBrawlDeck()
        {
            foreach (String tag in _tags)
            {
                if (tag.ToLower().Contains("brawl")){

                    return true;
                }
            }
            return false;
        }

		public override bool Equals(object obj)
		{
			var deck = obj as Deck;
			if(deck == null)
				return false;
			if(deck.HasHearthStatsId && HasHearthStatsId)
				return HearthStatsId.Equals(deck.HearthStatsId);
			return DeckId.Equals(deck.DeckId);
		}

		public override int GetHashCode()
		{
			if(HasHearthStatsId)
				return HasHearthStatsId.GetHashCode();
			return DeckId.GetHashCode();
		}

		public static List<Card> operator -(Deck first, Deck second)
		{
			if(first == null || second == null)
				return new List<Card>();
			var diff = new List<Card>();
			//removed
			foreach(var c in second.Cards.Where(c => !first.Cards.Contains(c)))
			{
				var cd = c.Clone() as Card;
				cd.Count = -cd.Count; //merk as negative for visual
				diff.Add(cd);
			}
			//added
			diff.AddRange(first.Cards.Where(c => !second.Cards.Contains(c)));

			//diff count
			var diffCount =
				first.Cards.Where(c => second.Cards.Any(c2 => c2.Id == c.Id) && second.Cards.First(c2 => c2.Id == c.Id).Count != c.Count);
			foreach(var card in diffCount)
			{
				var cardclone = card.Clone() as Card;
				cardclone.Count = cardclone.Count - second.Cards.Where(c => c.Id == cardclone.Id).First().Count;
				diff.Add(cardclone);
			}

			return diff;
		}

		public SerializableVersion GetMaxVerion()
		{
			return VersionsIncludingSelf.OrderByDescending(x => x).First();
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if(handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}

		public void Edited()
		{
			LastEdited = DateTime.Now;
		}

		public void StatsUpdated()
		{
			OnPropertyChanged("StatsString");
			OnPropertyChanged("WinLossString");
			OnPropertyChanged("WinPercent");
			OnPropertyChanged("WinPercentString");
		}

        private bool isDeckContainingSet(string setname)
        {
            if (Cards == null)
                return false;
            foreach (Card newcard in Cards)
            {
                if (newcard.Set == (setname))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsNaxxDeck()
        {
            bool result;
            result = isDeckContainingSet("Curse of Naxxramas");
            return result;
        }

        public bool IsGvgDeck()
        {
            return isDeckContainingSet("Goblins vs Gnomes");
        }

        public bool IsBrmDeck()
        {
            return isDeckContainingSet("Blackrock Mountain");
        }

        public bool IsTgtDeck()
        {
            bool result;
            result = isDeckContainingSet("The Grand Tournament");
            return result;
        }
        
        public bool IsLoeDeck()
        {
            bool result;
            result = isDeckContainingSet("League of Explorers");
            return result;
        }

        public bool IsOldGodsDeck()
        {
            bool result;
            result = isDeckContainingSet("Whispers of the Old Gods");
            return result;
        }

        public bool IsKarazadDeck()
        {
            bool result;
            result = isDeckContainingSet("One Night in Karazhan");
            return result;
        }

        public bool IsMeanstreetsDeck()
        {
            bool result;
            result = isDeckContainingSet("Mean Streets of Gadgetzan");
            return result;

        }

        public bool IsUngoroDeck()
        {
            bool result;
            result = isDeckContainingSet("Journey to Un'Goro");
            return result;

        }

        public bool IsStandardKraken()
        {
            if (IsNaxxDeck())
                return false;
            if (IsGvgDeck())
                return false;
            return true;
        }

        public bool IsStandardMammoth()
        {
            if (IsNaxxDeck())
                return false;
            if (IsGvgDeck())
                return false;
            if (IsLoeDeck())
                return false;
            if (IsTgtDeck())
                return false;
            if (IsBrmDeck())
                return false;

            return true;
        }

        public bool IsStandard()
        {
            return IsStandardMammoth();
        }

        public Visibility StandardVisibility
        {
            get { return IsStandard() ? Visibility.Visible : Visibility.Collapsed; }
        }


        public Visibility NaxxVisibility
        {
            get { return IsNaxxDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility BrmVisibility
        {
            get { return IsBrmDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility GvgVisibility
        {
            get { return IsGvgDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility TgtVisibility
        {
            get { return IsTgtDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility LoeVisibility
        {
            get { return IsLoeDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }
        public Visibility OldGodsVisibility
        {
            get { return IsOldGodsDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility KarazadVisibility
        {
            get { return IsKarazadDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility MeanstreetsVisibility
        {
            get { return IsMeanstreetsDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility UngoroVisibility
        {
            get { return IsUngoroDeck() ? Visibility.Visible : Visibility.Collapsed; }
        }

        public ImageBrush Background
        {
            get { return getDeckRepresentativeCard().BackgroundImageOnly; }
        }


        [NonSerialized]
        private ImageBrush wildimage=null;
        private ImageBrush krakenimage = null;
        private ImageBrush mammothimage = null;
        private ImageBrush tavernbrawlimage = null;

        public ImageBrush StandardWildImage
        {
            get
            {
                if (wildimage == null)
                {
                    try
                    {

                        //card graphic
                        DrawingGroup drawingGroup;
                        ImageBrush brush;
                        //// WILD                        
                        drawingGroup= new DrawingGroup();

                        if (File.Exists("Images/" + "Mode_Wild.png"))
                        {
                            drawingGroup.Children.Add(new ImageDrawing(new BitmapImage(new Uri("Images/" + "Mode_Wild.png", UriKind.Relative)),
                                                                       new Rect(0, 0, 64, 64)));
                        }


                        brush = new ImageBrush { ImageSource = new DrawingImage(drawingGroup) };
                        wildimage = brush;
                        ////// Kraken
                        drawingGroup = new DrawingGroup();
                        if (File.Exists("Images/" + "Mode_Standard_Kraken.png"))
                        {
                            drawingGroup.Children.Add(new ImageDrawing(new BitmapImage(new Uri("Images/" + "Mode_Standard_Kraken.png", UriKind.Relative)),
                                                                       new Rect(0, 0, 64, 64)));
                        }


                        brush = new ImageBrush { ImageSource = new DrawingImage(drawingGroup) };
                        krakenimage = brush;
                        //// Mammoth
                        drawingGroup = new DrawingGroup();
                        if (File.Exists("Images/" + "Mode_Standard_Mammoth.png"))
                        {
                            drawingGroup.Children.Add(new ImageDrawing(new BitmapImage(new Uri("Images/" + "Mode_Standard_Mammoth.png", UriKind.Relative)),
                                                                       new Rect(0, 0, 64, 64)));
                        }


                        brush = new ImageBrush { ImageSource = new DrawingImage(drawingGroup) };
                        mammothimage = brush;

                        //// brawl
                        drawingGroup = new DrawingGroup();
                        if (File.Exists("Images/" + "brawlmug.png"))
                        {
                            drawingGroup.Children.Add(new ImageDrawing(new BitmapImage(new Uri("Images/" + "brawlmug.png", UriKind.Relative)),
                                                                       new Rect(0, 0, 64, 64)));
                        }


                        brush = new ImageBrush { ImageSource = new DrawingImage(drawingGroup) };
                        tavernbrawlimage = brush;


                    }
                    catch (Exception)
                    {
                        return new ImageBrush();
                    }
                }
                {
                    if (isBrawlDeck())
                    {
                        return tavernbrawlimage;
                    }
                    else if (IsStandardMammoth())
                    {
                        return mammothimage;
                    }
                    else if (IsStandardKraken())
                    {
                        return krakenimage;
                    }
                    return wildimage;
                }
            }
        }



        private Card getDeckRepresentativeCard()
        {
            string [] namelist  = 
            {
                "Awaken the Makers",
                "Fire Plume's Heart",
                "Jungle Giants",
                "Lakkari Sacrifice",
                "Open the Waygate",
                "The Caverns Below",
                "The Last Kaleidosaur",
                "The Marsh Queen",
                "Unite the Murlocs",
                "Kazakus",
                "Reno Jackson",    
                "Aya Blackpaw",
                "Jade Idol",
                "C'Thun",
                "N'Zoth, the Corruptor",
                "Yogg-Saron, Hope's End",
                "Snowchugger",
                "Mechwarper",
                "Pyros",
                "Blazecaller",
                "Steam Surger",
                "Lyra the Sunshard",
                "The Curator",
                "Cloaked Huntress",
                "Silverware Golem",
                "Hallazeal the Ascended",
                "Malygos",
                "Gang Up",
                "Starving Buzzard",
                "Savannah Highmane",
                "Quick Shot",
                "Gadgetzan Auctioneer", 
                "Ice Block",
                "Flamewaker",
                "Archmage Antonidas",
                "Grim Patron",
                "Mysterious Challenger",
                "Patches the Pirate",
                "Dread Corsair",
                "Prophet Velen",
                "Molten Giant",
                "Unearthed Raptor",
                "Tinker's Sharpsword Oil",
                "Anyfin Can Happen",
                "Everyfin is Awesome",
                "Twilight Guardian",
                "Fierce Monkey",
                "Grommash Hellscream",
                "Dreadsteed",
                "Darkshire Councilman",
                "Dragon Egg",
                "Doomguard",
                "Voidwalker",
                "Purify",
                "Priest of the Feast",  
                "Cabal Shadow Priest",
                "Evolve",
                "Thunder Bluff Valiant",
                "Bloodlust",
                "Tunnel Trogg",
                "Lava Shock",
                "Desert Camel",
                "Echo of Medivh",
                "Finja, the Flying Star",
                "Grimestreet Outfitter",
                "Selfless Hero",
                "Blood Knight",
                "Blessing of Might",
                "Muster for Battle",
                "Guardian of Kings",
                "Fel Reaver",
                "Mounted Raptor",
                "Ancient of War",
                "Wild Growth",
                "Lock and Load", 
                "Kirin Tor Mage",
                "Humongous Razorleaf"



            };

            foreach (String name in namelist)
            {
                foreach ( Card card in Cards)
                {
                    if(card.Name.Equals(name) )
                    {
                        return card;
                    }
                }
            }
            return Cards[0];
        }

	}
}