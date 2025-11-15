using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Implementation of Kuhn Poker, a simplified poker variant.
/// </summary>
public class KuhnPoker13Game : IGame<string>
{
    private string[] _cards;
    private List<string> _history;
    private double[] _pot;
    private int _player;
    private int _winner;
    private bool _end;

    private static readonly Random _random = new Random();
    private static readonly string[] POSSIBLE_CARDS = { "A", "K", "Q", "J", "T", "9", "8", "7", "6", "5", "4", "3", "2" };
    
    
    private static readonly string[] INITIAL_ACTIONS = { "BET", "CHECK" };
    private static readonly string[] BET_RESPONSES = { "CALLBET", "FOLD", "RAISE" };
    private static readonly string[] RAISE_RESPONSES = { "CALLRAISE", "FOLD" };
  
    private static readonly Dictionary<string, int> HAND_RANKS = new Dictionary<string, int>
    {
        {"A", 14}, {"K", 13}, {"Q", 12}, {"J", 11}, {"10", 10},
        {"9", 9}, {"8", 8}, {"7", 7}, {"6", 6},
        {"5", 5}, {"4", 4}, {"3", 3}, {"2", 2}
    };

    public KuhnPoker13Game()
    {
        _cards = new string[2]; // Fixed size for 2 players
        _history = new List<string>();
        _pot = new double[2]; // Fixed size for 2 players
        _player = 0;
        _winner = 2;
        _end = false;
    }

    public void BeginGame()
    {
        // Deal cards
        var availableCards = POSSIBLE_CARDS.ToList(); // Only create list temporarily for dealing
        
        for (int player = 0; player < NumPlayers(); player++)
        {
            int sample = _random.Next(0, availableCards.Count);
            _cards[player] = availableCards[sample];
            availableCards.RemoveAt(sample);
        }

        _history.Clear();
        _pot[0] = 1; // Post antes
        _pot[1] = 1;
        _player = 0;
        _winner = 2; // Invalid until game has ended
        _end = false;
    }

    public IGame<string> DeepCopy()
    {
        var copy = new KuhnPoker13Game
        {
            _cards = (string[])_cards.Clone(),
            _history = new List<string>(_history),
            _pot = (double[])_pot.Clone(),
            _player = _player,
            _winner = _winner,
            _end = _end
        };
        return copy;
    }

    public string InformationSet()
    {
        // Include player information
        var id = $"[Player: {_player}, Card: {_cards[_player]}, Betting History: ";

        // Include betting history
        id += string.Join(", ", _history);

        // Include actions
        id += " Actions: ";
        var moves = Actions();
        id += string.Join(", ", moves);

        id += "]";
        return id;
    }

    public IReadOnlyList<double> Payout()
    {
        if (!IsTerminalState())
        {
            throw new InvalidOperationException("Cannot get payout for non-terminal state");
        }

        // Determine the payout
        double pot = _pot[0] + _pot[1];
        if (_winner == 0)
        {
            return new[] { pot - _pot[0], -_pot[1] };
        }
        else if (_winner == 1)
        {
            return new[] { -_pot[0], pot - _pot[1] };
        }

        throw new InvalidOperationException("Invalid winner");
    }

    public bool IsTerminalState()
    {
        return _end;
    }

    public int NumPlayers()
    {
        return 2;
    }

    public int PlayerToAct()
    {
        return _player;
    }

    public IReadOnlyList<string> Actions()
    {
        if (_history.Count > 0 && _history[^1] == "BET")
        {
            return BET_RESPONSES;
        }
        if (_history.Count > 0 && _history[^1] == "RAISE")
        {
            return RAISE_RESPONSES;
        }

        return INITIAL_ACTIONS;
    }

    public void MakeMove(string action)
    {
        if (IsTerminalState())
        {
            throw new InvalidOperationException("Cannot make move in terminal state");
        }

        switch (action)
        {
            case "BET":
                _pot[_player] += 1;
                break;

            case "CHECK":
                if (_history.Count == 1 && _history[0] == "CHECK")
                {
                    _winner = WinningPlayer();
                    _end = true;
                }
                break;

            case "FOLD":
                _winner = 1 - _player;
                _end = true;
                break;

            case "CALLBET":
                _pot[_player] += 1;
                _winner = WinningPlayer();
                _end = true;
                break;

            case "CALLRAISE":
                _pot[_player] += 2;
                _winner = WinningPlayer();
                _end = true;
                break;

            case "RAISE":
                _pot[_player] += 3;
                break;

            default:
                throw new ArgumentException("Invalid action", nameof(action));
        }

        _history.Add(action);
        _player = 1 - _player;
    }

    private int WinningPlayer()
    {
        int rank0 = HAND_RANKS[_cards[0]];
        int rank1 = HAND_RANKS[_cards[1]];
        return rank0 > rank1 ? 0 : 1;
    }
}