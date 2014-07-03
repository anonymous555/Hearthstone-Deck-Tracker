﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Hearthstone_Deck_Tracker.Hearthstone;

namespace Hearthstone_Deck_Tracker.Stats
{
    public class DeckIteration
    {
        [XmlArray(ElementName = "Cards")]
        [XmlArrayItem(ElementName = "Card")]
        public List<Card> Cards;

        [XmlArray(ElementName = "Games")]
        [XmlArrayItem(ElementName = "Game")]
        public List<GameStats> GameStats;
        private GameStats _currentGame;

        public DeckIteration()
        {
            GameStats = new List<GameStats>();
        }

        public DeckIteration(Deck deck)
        {
            GameStats = new List<GameStats>();
            Cards = ((Deck)deck.Clone()).Cards.ToList();
        }

        public void CardDrawn(string cardId)
        {
            var cardStats = _currentGame.CardStats.FirstOrDefault(c => c.CardId.Equals(cardId));
            if (cardStats == null)
            {
                cardStats = new CardStats(cardId);
                _currentGame.CardStats.Add(cardStats);
            }
            cardStats.Drawn++;
        }

        public void CardPlayed(string cardId)
        {
            var cardStats = _currentGame.CardStats.FirstOrDefault(c => c.CardId.Equals(cardId));
            if (cardStats == null)
            {
                cardStats = new CardStats(cardId);
                _currentGame.CardStats.Add(cardStats);
            }
            cardStats.Played++;
        }

        public void NewGame(string opponentClass)
        {
            _currentGame = new GameStats();
            _currentGame.OpponentClass = opponentClass;
        }
        public void GameEnd()
        {
            if(_currentGame != null)
            {
                GameStats.Add(_currentGame);
            }
        }

        public void CardMulliganed(string cardId)
        {
            var cardStats = _currentGame.CardStats.FirstOrDefault(c => c.CardId.Equals(cardId));
            if (cardStats == null)
            {
                cardStats = new CardStats(cardId);
                _currentGame.CardStats.Add(cardStats);
            }
            cardStats.Mulliganed++;
            cardStats.Drawn--;
        }

        public void GameResult(GameStats.Result result)
        {
            if(_currentGame != null)
            {
                _currentGame.GameResult = result;
            }
        }

        public GameStats.Result GetGameResult()
        {
            return _currentGame == null ? Stats.GameStats.Result.None : _currentGame.GameResult;
        }

        public void SetGameStats(GameStats stats)
        {
            _currentGame = stats;
        }

        public void ClearGameStats()
        {
            _currentGame = null;
        }

        public GameStats GetGameStats()
        {
            return _currentGame;
        }
    }
}
