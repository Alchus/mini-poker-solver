using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CFRMiniPoker
{
    /// <summary>
    /// Implementation of a two-player Liar's Dice variant.
    /// - Each player has five dice.
    /// - Actions are strings: "CHALLENGE", "SPOT_ON", or a bid encoded as digits (e.g. "11" for 1x1, "106" for 10x6).
    /// - Internally bids are represented as bytes: 11 = 1x1, 106 = 10x6.
    /// - Maximum bid is 10x6. After 10x6 only "CHALLENGE" and "SPOT_ON" are legal responses.
    /// - Player 0 always goes first.
    /// - Hands are stored as integers formed by concatenating the dice faces in descending order (e.g. 66521).
    /// - Resolution rules (simple zero-sum payoffs):
    ///   * CHALLENGE or SPOT_ON: if actual count >= bid count => bidder wins (+1), else challenger wins (+1).
    /// </summary>
    internal class LiarsDice : IGame<byte>
    {
        private static readonly Random _random = new Random();

        private const int CHALLENGE = 254;
        private const int SPOT_ON = 255;

        private const int DICE_PER_PLAYER = 5;
        private const int MAX_COUNT = 6;   
        private const int MAX_FACE = 6;
        private const int MAX_BID_COUNT = 20;

        // Cache for all bid byte codes so they are only computed once
        private static List<byte>? _allBidsBytesCache;

        // Cache for counts of (handInt, face) pairs. Key = (handInt << 3) + face
        private static readonly ConcurrentDictionary<int, int> _countFaceCache = new ConcurrentDictionary<int, int>();

        private int[] _hands; // stored as concatenated digits in descending order, e.g. 66521

        // Replace per-face history with last-three-bids history. Encoding: c*10+f (e.g. three 4's -> 34)
        private byte _bid_hist_1; // most recent
        private byte _bid_hist_2;
        private byte _bid_hist_3; // oldest of the three

        // Total number of bids made in the round (used for the 10-bid rule)
        private int _bidCountTotal;
        // Last non-bid terminal action when game ends ("CHALLENGE" or "SPOT_ON")
        private byte _lastTerminalAction;
        private int _player;
        private bool _end;
        private int _winner; // 0 or 1 when _end == true

        /// <summary>
        /// Creates a new LiarsDice object.
        /// </summary>
        public LiarsDice()
        {
            _hands = new int[2];
            _bid_hist_1 = 0;
            _bid_hist_2 = 0;
            _bid_hist_3 = 0;
            _bidCountTotal = 0;
            _lastTerminalAction = 0;
            _player = 0;
            _end = false;
            _winner = -1;
        }

        /// <summary>
        /// Starts a new game: deal five dice to each player and reset history/state.
        /// </summary>
        public void BeginGame()
        {
            for (int p = 0; p < NumPlayers(); p++)
            {
                var dice = new int[DICE_PER_PLAYER];
                for (int i = 0; i < DICE_PER_PLAYER; i++)
                {
                    dice[i] = _random.Next(1, MAX_FACE + 1);
                }

                Array.Sort(dice);
                Array.Reverse(dice); // descending
                _hands[p] = DiceArrayToInt(dice);
            }

            _bid_hist_1 = 0;
            _bid_hist_2 = 0;
            _bid_hist_3 = 0;
            _bidCountTotal = 0;
            _lastTerminalAction = 0;
            _player = 0;
            _end = false;
            _winner = -1;
        }

        /// <summary>
        /// Returns a deep copy of this game state.
        /// </summary>
        /// <returns>A deep copy of this LiarsDice instance.</returns>
        public IGame<byte> DeepCopy()
        {
            var copy = new LiarsDice
            {
                _hands = (int[])_hands.Clone(),
                _bid_hist_1 = _bid_hist_1,
                _bid_hist_2 = _bid_hist_2,
                _bid_hist_3 = _bid_hist_3,
                _bidCountTotal = _bidCountTotal,
                _lastTerminalAction = _lastTerminalAction,
                _player = _player,
                _end = _end,
                _winner = _winner
            };
            return copy;
        }

        /// <summary>
        /// Returns the current player's information set as a unique string.
        /// The string contains: player index, the player's hand (as concatenated digits), betting history
        /// Must not contain newline or tab characters.
        /// </summary>
        /// <returns>The information set id for the current player.</returns>
        public string InformationSet()
        {
            var sb = new StringBuilder();
            sb.Append($"[Hand:{_hands[_player]}, ");

            if (_bidCountTotal == MAX_BID_COUNT) {
                sb.Append("TL!, ");
            }

            
            // History reported as three most recent bids, with _bid_hist_1 being the most recent (printed first)
            sb.Append("History:");
            sb.Append($"{_bid_hist_1},{_bid_hist_2},{_bid_hist_3}");
            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// Returns whether the game has reached a terminal state.
        /// </summary>
        /// <returns>True if the game has ended; otherwise false.</returns>
        public bool IsTerminalState()
        {
            return _end;
        }

        /// <summary>
        /// Returns the number of players (always 2).
        /// </summary>
        /// <returns>2</returns>
        public int NumPlayers()
        {
            return 2;
        }

        /// <summary>
        /// Returns the index of the player whose turn it is.
        /// </summary>
        /// <returns>The zero-based player index to act.</returns>
        public int PlayerToAct()
        {
            return _player;
        }

        /// <summary>
        /// Computes the list of legal actions for the current player, in deterministic order.
        /// - If no prior bid, all bids from 1x1 .. 10x6 are legal (encoded as digit strings without 'x').
        /// - If prior action is a bid B, legal actions are all strictly higher bids than B (in count, then face)
        ///   plus "CHALLENGE" and "SPOT_ON". If B is the maximum bid (10x6) then only "CHALLENGE" and "SPOT_ON" are legal.
        /// </summary>
        /// <returns>A deterministic list of legal action strings.</returns>
        public IReadOnlyList<byte> Actions()
        {
            if (_end)
            {
                return Array.Empty<byte>();
            }

            // New rule: if there have already been 20 bids made, only CHALLENGE and SPOT_ON are allowed
            if (_bidCountTotal >= MAX_BID_COUNT)
            {
                return new List<byte> { CHALLENGE, SPOT_ON };
            }

            // Help the solver a little: If the last bid is impossible, only allow CHALLENGE
            int total_dice = DICE_PER_PLAYER * NumPlayers();
            if (_bid_hist_1 != 0)
            {
                int last_bid_count = _bid_hist_1 / 10;
                int last_bid_face = _bid_hist_1 % 10;
                if (last_bid_count > ((DICE_PER_PLAYER * (NumPlayers() - 1)) +  CountFaceInHand(_hands[_player], last_bid_face)))
                {
                    return new List<byte> { CHALLENGE };
                }
            }


            var legalmoves = AllBidsBytes().Where(b => b > _bid_hist_1).ToList();
            if (_bid_hist_1 != 0)
            {
                legalmoves.Add(CHALLENGE);
                legalmoves.Add(SPOT_ON);
            }
            return legalmoves;

        }

        /// <summary>
        /// Makes the specified move for the current player.
        /// - If move is a bid, it is appended to history and turn passes to the other player.
        /// - If move is "CHALLENGE" or "SPOT_ON", the game is resolved immediately.
        /// </summary>
        /// <param name="move">The action string to perform.</param>
        public void MakeMove(byte move)
        {
            if (IsTerminalState())
            {
                throw new InvalidOperationException("Cannot make move in terminal state");
            }

                
            // move is CHALLENGE or SPOT_ON -> resolve immediately
            if (move == CHALLENGE)
            {
                ResolveChallenge();
                _lastTerminalAction = move;
                _end = true;
            }
            else if (move == SPOT_ON)
            {
                ResolveSpotOn();
                _lastTerminalAction = move;
                _end = true;
            }
            
            // Record the bid: shift history and update the last bid info
            _bid_hist_3 = _bid_hist_2;
            _bid_hist_2 = _bid_hist_1;
            _bid_hist_1 = move;
            _bidCountTotal++;
            _player = 1 - _player;
        }

        /// <summary>
        /// Computes the payouts for the finished game.
        /// Returns an array of two doubles where index i is the payout to player i.
        /// Payouts are zero-sum.
        /// </summary>
        /// <returns>Array of two payouts.</returns>
        public IReadOnlyList<double> Payout()
        {
            if (!IsTerminalState())
            {
                throw new InvalidOperationException("Cannot get payout for non-terminal state");
            }

            if (_winner != 0 && _winner != 1)
            {
                throw new InvalidOperationException("Invalid winner");
            }

            double[] result = new double[2];
            result[_winner] = 1;
            result[1 - _winner] = -1;
            return result;
        }

        // --------------------- Private helpers ---------------------

        /// <summary>
        /// Converts an array of dice (descending order expected) to an integer by concatenating digits.
        /// Example: [6,6,5,2,1] -> 66521.
        /// </summary>
        /// <param name="dice">Array of dice values.</param>
        /// <returns>Integer representation formed by concatenated digits.</returns>
        private static int DiceArrayToInt(int[] dice)
        {
            var sb = new StringBuilder();
            foreach (var d in dice)
            {
                sb.Append(d.ToString());
            }
            return int.Parse(sb.ToString());
        }

        /// <summary>
        /// Parses a bid string into its byte encoding (c*10+f).
        /// </summary>
        private static byte ParseBidToByte(string bid)
        {
            if (string.IsNullOrEmpty(bid)) throw new ArgumentException("Invalid bid", nameof(bid));
            if (!int.TryParse(bid, out int v)) throw new ArgumentException($"Invalid bid: {bid}", nameof(bid));
            if (v < 0 || v > 255) throw new ArgumentException($"Bid out of byte range: {bid}", nameof(bid));
            return (byte)v;
        }

        /// <summary>
        /// Parses a bid string like "11" (1x1) or "106" (10x6) into (count, face).
        /// </summary>
        private static (int count, int face) ParseBid(string bid)
        {
            byte b = ParseBidToByte(bid);
            int count = b / 10;
            int face = b % 10;
            if (count < 1 || count > MAX_COUNT || face < 1 || face > MAX_FACE)
            {
                throw new ArgumentException($"Invalid bid value: {bid}", nameof(bid));
            }
            return (count, face);
        }


        /// <summary>
        /// Generates the full list of all possible bids in deterministic order using byte codes:
        /// ascending by count (1..MAX_COUNT) then face (1..MAX_FACE).
        /// Each bid is encoded as c*10+f (e.g. 2x3 -> 23) and is returned as a byte.
        /// </summary>
        /// <returns>List of all bid byte codes.</returns>
        private static List<byte> AllBidsBytes()
        {
            if (_allBidsBytesCache != null) return _allBidsBytesCache;

            var bids = new List<byte>(MAX_COUNT * MAX_FACE);
            for (int c = 1; c <= MAX_COUNT; c++)
            {
                for (int f = 1; f <= MAX_FACE; f++)
                {
                    bids.Add((byte)(c * 10 + f));
                }
            }

            _allBidsBytesCache = bids;
            return _allBidsBytesCache;
        }

        

        /// <summary>
        /// Resolves a CHALLENGE action: compares the last bid to the actual total count across both hands,
        /// sets _winner accordingly (0 or 1).
        /// </summary>
        private void ResolveChallenge()
        {
            if (_bid_hist_1 == 0)
            {
                throw new InvalidOperationException("No bid to challenge");
            }

            int count = _bid_hist_1 / 10;
            int face = _bid_hist_1 % 10;
            int actual = TotalFaceCount(face);

            // The bidder is the player who made the last bid: that is 1 - _player (current player issued the CHALLENGE).
            int bidder = 1 - _player;
            int challenger = _player;

            if (actual >= count)
            {
                // bid was truthful or safe => bidder wins
                _winner = bidder;
            }
            else
            {
                // bid was a lie => challenger wins
                _winner = challenger;
            }
        }

        /// <summary>
        /// Resolves a SPOT_ON action: if actual total equals the bid count then the spot-on caller (_player) wins,
        /// otherwise the spot-on caller loses. Sets _winner accordingly.
        /// </summary>
        private void ResolveSpotOn()
        {
            if (_bid_hist_1 == 0)
            {
                throw new InvalidOperationException("No bid to challenge");
            }

            int count = _bid_hist_1 / 10;
            int face = _bid_hist_1 % 10;
            int actual = TotalFaceCount(face);

            // The bidder is the player who made the last bid: that is 1 - _player (current player issued the CHALLENGE).
            int bidder = 1 - _player;
            int challenger = _player;

            if (actual == count)
            {
                // spot-on correct => caller wins
                _winner = challenger;
            }
            else
            {
                // spot-on incorrect => caller loses => bidder wins
                _winner = bidder;
            }
        }

        /// <summary>
        /// Counts how many dice across both players show the given face value.
        /// </summary>
        /// <param name="face">Face value 1..6.</param>
        /// <returns>Total count across both players.</returns>
        private int TotalFaceCount(int face)
        {
            int total = 0;
            for (int p = 0; p < NumPlayers(); p++)
            {
                total += CountFaceInHand(_hands[p], face);
            }
            return total;
        }

        /// <summary>
        /// Counts occurrences of a face in a hand represented as concatenated descending digits.
        /// Example hand 66521, face 6 => 2.
        /// Uses a cache keyed by (handInt << 3) + face to avoid recomputation.
        /// </summary>
        /// <param name="handInt">Integer representation of a hand.</param>
        /// <param name="face">Face to count (1..6).</param>
        /// <returns>Number of dice equal to face in that hand.</returns>
        private static int CountFaceInHand(int handInt, int face)
        {
            int key = (handInt << 3) + face; // shift by 3 bits (multiply by 8) then add face
            if (_countFaceCache.TryGetValue(key, out int cached))
            {
                return cached;
            }

            int count = 0;
            int target = face;
            int temp = handInt;
            while (temp > 0)
            {
                int digit = temp % 10;
                if (digit == target) count++;
                temp /= 10;
            }

            _countFaceCache[key] = count;
            return count;
        }

        
    }
}
