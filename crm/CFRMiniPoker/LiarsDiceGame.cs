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
    /// - Actions are strings: "CHALLENGE", "SPOT_ON", or a bid encoded as digits without 'x' (e.g. "11" for 1x1, "106" for 10x6).
    /// - Internally bids are represented as bytes: 11 = 1x1, 106 = 10x6.
    /// - Maximum bid is 10x6. After 10x6 only "CHALLENGE" and "SPOT_ON" are legal responses.
    /// - Player 0 always goes first.
    /// - Hands are stored as integers formed by concatenating the dice faces in descending order (e.g. 66521).
    /// - Resolution rules (simple zero-sum payoffs):
    ///   * CHALLENGE: if actual count >= bid count => bidder wins (+1), else challenger wins (+1).
    ///   * SPOT_ON: if actual count == bid count => caller (spot-on caller) wins (+1), else caller loses (+1).
    /// </summary>
    internal class LiarsDice : IGame<string>
    {
        private static readonly Random _random = new Random();

        private const int DICE_PER_PLAYER = 5;
        private const int MAX_COUNT = 6;
        private const int MAX_FACE = 6;

        // Cache for all bid byte codes so they are only computed once
        private static List<byte>? _allBidsBytesCache;

        // Cache for counts of (handInt, face) pairs. Key = (handInt << 3) + face
        private static readonly ConcurrentDictionary<int, int> _countFaceCache = new ConcurrentDictionary<int, int>();

        private int[] _hands; // stored as concatenated digits in descending order, e.g. 66521
        private List<string> _history;
        private int _player;
        private bool _end;
        private int _winner; // 0 or 1 when _end == true

        /// <summary>
        /// Creates a new LiarsDice object.
        /// </summary>
        public LiarsDice()
        {
            _hands = new int[2];
            _history = new List<string>();
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

            _history.Clear();
            _player = 0;
            _end = false;
            _winner = -1;
        }

        /// <summary>
        /// Returns a deep copy of this game state.
        /// </summary>
        /// <returns>A deep copy of this LiarsDice instance.</returns>
        public IGame<string> DeepCopy()
        {
            var copy = new LiarsDice
            {
                _hands = (int[])_hands.Clone(),
                _history = new List<string>(_history),
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
            sb.Append($"[Player:{_player},Hand:{_hands[_player]},History:");
            if (_history.Count > 0)
            {
                sb.Append(string.Join(",", _history));
            }
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
        public IReadOnlyList<string> Actions()
        {
            if (_end)
            {
                return Array.Empty<string>();
            }

            if (_history.Count == 0)
            {
                // Return all bids as numeric strings (e.g. "11","12",...,"106")
                return AllBids();
            }

            var last = _history[^1];
            if (IsBidString(last))
            {
                byte lastByte = ParseBidToByte(last);

                // Make impossible bids (given current player's private hand) only challengeable
                int lastCount = lastByte / 10;
                int lastFace = lastByte % 10;
                int ownCount = CountFaceInHand(_hands[_player], lastFace);
                if (lastCount > ownCount + DICE_PER_PLAYER)
                {
                    // The bid asserts more of the face than could exist given opponent's maximum contribution -> only challenge
                    return new List<string> { "CHALLENGE" };
                }

                if (lastByte == (byte)(MAX_COUNT * 10 + MAX_FACE))
                {
                    // Maximum bid reached -> only challenge or spot on allowed
                    return new List<string> { "CHALLENGE", "SPOT_ON" };
                }

                // Compare bids directly as bytes for speed
                var higherBytes = AllBidsBytes().Where(b => b > lastByte).ToList();

                var result = higherBytes.Select(b => b.ToString()).ToList();
                result.Add("CHALLENGE");
                result.Add("SPOT_ON");
                return result;
            }

            // If last action was CHALLENGE or SPOT_ON and game not marked end, no further moves should be allowed.
            return Array.Empty<string>();
        }

        /// <summary>
        /// Makes the specified move for the current player.
        /// - If move is a bid, it is appended to history and turn passes to the other player.
        /// - If move is "CHALLENGE" or "SPOT_ON", the game is resolved immediately.
        /// </summary>
        /// <param name="move">The action string to perform.</param>
        public void MakeMove(string move)
        {
            if (IsTerminalState())
            {
                throw new InvalidOperationException("Cannot make move in terminal state");
            }

            if (string.IsNullOrWhiteSpace(move))
            {
                throw new ArgumentException("Move must be non-empty", nameof(move));
            }

            if (IsBidString(move))
            {
                // Just record the bid and switch player
                _history.Add(move);
                _player = 1 - _player;
                return;
            }

            // move is CHALLENGE or SPOT_ON -> resolve immediately
            if (move == "CHALLENGE")
            {
                ResolveChallenge();
                _history.Add(move);
                _end = true;
            }
            else if (move == "SPOT_ON")
            {
                ResolveSpotOn();
                _history.Add(move);
                _end = true;
            }
            else
            {
                throw new ArgumentException($"Unknown move: {move}", nameof(move));
            }
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

            var last = _history.Last();
            double[] result = new double[2];

            if (last == "CHALLENGE")
            {
                result[_winner] = 1;
                result[1 - _winner] = -1;
                return result;
            }
            else if (last == "SPOT_ON")
            {
                result[_winner] = 1;
                result[1 - _winner] = -1;
                return result;
            }

            throw new InvalidOperationException("Unknown terminal action");
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
        /// Returns true if the string is a bid (decimal number encoding c*10+f).
        /// </summary>
        private static bool IsBidString(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!int.TryParse(s, out int v)) return false;
            int c = v / 10;
            int f = v % 10;
            return c >= 1 && c <= MAX_COUNT && f >= 1 && f <= MAX_FACE;
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
        /// Returns bids as string representations (no 'x' character), e.g. "11","106".
        /// </summary>
        /// <returns>List of bid strings.</returns>
        private static List<string> AllBids()
        {
            return AllBidsBytes().Select(b => b.ToString()).ToList();
        }

        /// <summary>
        /// Resolves a CHALLENGE action: compares the last bid to the actual total count across both hands,
        /// sets _winner accordingly (0 or 1).
        /// </summary>
        private void ResolveChallenge()
        {
            var lastBid = _history.LastOrDefault(h => IsBidString(h));
            if (lastBid == null)
            {
                throw new InvalidOperationException("No bid to challenge");
            }

            var (count, face) = ParseBid(lastBid);
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
            var lastBid = _history.LastOrDefault(h => IsBidString(h));
            if (lastBid == null)
            {
                throw new InvalidOperationException("No bid to spot on");
            }

            var (count, face) = ParseBid(lastBid);
            int actual = TotalFaceCount(face);

            int caller = _player;
            int bidder = 1 - _player;

            if (actual == count)
            {
                // spot-on correct => caller wins
                _winner = caller;
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
