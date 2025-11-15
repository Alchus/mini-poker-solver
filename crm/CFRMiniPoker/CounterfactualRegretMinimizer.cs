using CFRMiniPoker;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

/// <summary>
/// Class to perform Counterfactual Regret Minimization on multiplayer games.
/// </summary>
/// <typeparam name="TAction">The type of actions</typeparam>
public class CounterfactualRegretMinimizer<TAction> : CFRPlayer<TAction>, IStrategySolver<TAction>
{
    private readonly IGame<TAction> game_instance;
    private readonly int _numPlayers;
    //private readonly ConcurrentDictionary<string, List<double>> _aggregateRegrets;
    //private readonly Random _random = new Random();

    public double exploration_epsilon = 0.05; // chance to choose a random move when sampling opponent's strategy. 
    // This makes sure we occasionally learn some responses to moves that the opponent is unlikely to play



    /// <summary>
    /// Initializes the CFR algorithm
    /// </summary>
    /// <param name="game">The game to analyze</param>
    public CounterfactualRegretMinimizer(IGame<TAction> game, bool symmetrical_strategies = false)
    {
        game_instance = game;
        _numPlayers = game.NumPlayers();

        _aggregateRegrets = new ConcurrentDictionary<string, List<double>>();

    }

    /// <summary>
    /// Runs CFR for an infinite number of iterations. The computed strategy will
    /// periodically be saved to an output file.
    /// </summary>
    /// <param name="outputFile">The file to save the current strategy to</param>
    /// <param name="itersPerSave">The number of iterations before the strategy should be checkpointed</param>
    /// <param name="itersPerUpdate">The number of iterations before an update message is printed to the console</param>
    public void Solve(string outputFile, int itersPerSave, int itersPerUpdate, int maxIterations = int.MaxValue, double epsilon = 0.05)
    {
        exploration_epsilon = epsilon;
        int saveCounter = 0;
        long totalIterations = 0;
        Console.WriteLine("BEGINNING TRAINING");

        while (totalIterations < maxIterations)
        {
            Train(itersPerUpdate);

            saveCounter += itersPerUpdate;
            totalIterations += itersPerUpdate;
            Console.WriteLine($"COMPLETED ITERATION: {totalIterations}. Seen information states: {_aggregateRegrets.Count}");

            if (saveCounter >= itersPerSave)
            {
                saveCounter %= itersPerSave;
                Console.WriteLine("SAVING...");
                Save(outputFile);
            }
        }
    }

    /// <summary>
    /// Runs CFR for the given number of iterations
    /// </summary>
    /// <param name="iterations">The number of iterations to run</param>
    public void Train(int iterations)
    {
        // Run iterations in parallel using threads. Each iteration is independent and selects
        // which player is being trained based on the iteration index.
        var options = new ParallelOptions { MaxDegreeOfParallelism = 32 };

        Parallel.For(0, iterations, options, i =>
        {
            int training_player = i % _numPlayers;
            var copy = game_instance.DeepCopy();
            copy.BeginGame();
            var probabilities = Enumerable.Repeat(1.0, _numPlayers).ToList();
            Train(copy, probabilities, training_player: training_player);
        });
    }

    /// <summary>
    /// Play through the game tree and update regrets based on the outcomes.
    /// 
    /// 
    /// 
    /// We choose a strategy based on how much we have regretted not playing it in the past, assuming we *have* positive regret for that
    /// action at that information set. At each decision point, we simulate the game forward in a copy from each possible decision, recursively.
    /// At each level we are calculating the expected payoff of the game (under the current strategies) for each player. 
    /// Once we learn the expected payoff for each of the subgames, we can calculate the expected value of the current level as the
    /// sum of the payoffs of the child node, after weighting them by how likely we are to choose them according to our strategy.
    /// Before we return, though, we'll save the regret information: for each action, add or subtract from its 
    /// cumulative_regret[information_set][action] the amount better or worse we would have done by always choosing that outcome. 
    /// This update increment gets scaled down based on the probability of our opponents bringing us to this game state,
    /// so that we don't overlearn strategies that only work when our opponents make bad choices.
    /// Finally, our new strategy at the information set becomes to choose proportionally between actions with positive regret, or randomly if there are none.
    /// </summary>
    /// 
    /// <returns>the payoff of the game for each player under the current strategy.</returns>


    private IReadOnlyList<double> Train(IGame<TAction> game, IReadOnlyList<double> player_reach_probabilities, int training_player)
    {

        if (game.IsTerminalState())
        {
            return game.Payout();
        }

        int player = game.PlayerToAct();


        if (player != training_player)
        {
            // For players not currently being trained, just sample an action according to their current strategy
            SampleStrategyAndApplyMove(game, exploration_epsilon);
            // Then recursively continue training from the resulting game state
            return Train(game, player_reach_probabilities, training_player);
        }


        string infoset = game.InformationSet();
        var actions = game.Actions();

        if (actions.Count == 1)
        {
            game.MakeMove(actions[0]);
            return Train(game, player_reach_probabilities, training_player);
        }


        IReadOnlyList<double> strategy = GetStrategy(player, infoset, actions); // get current strategy based on regrets as a probability distribution over actions
        var actionUtilities = new List<IReadOnlyList<double>>(); // expected payoffs for each player for the game state resulting after taking each action
        var nodeUtilities = Enumerable.Repeat(0.0, _numPlayers).ToList(); // expected payoffs for each player for the current game state, computed as the sum of action utilities
                                                                                // weighted by likelihood of being chosen in the mixed strategy

        for (int action = 0; action < actions.Count; action++)
        {

            var gameCopy = game.DeepCopy();
            gameCopy.MakeMove(actions[action]);
            var probabilitiesCopy = player_reach_probabilities.ToList();
            probabilitiesCopy[player] *= strategy[action];

            var actionUtility = Train(gameCopy, probabilitiesCopy, training_player);
            actionUtilities.Add(actionUtility);
            for (int agent = 0; agent < _numPlayers; agent++)
            {
                nodeUtilities[agent] += strategy[action] * actionUtility[agent];
            }
        }

        for (int action = 0; action < actions.Count; action++)
        {
            double big_pi_reach_probability = 1.0;
            for (int agent = 0; agent < _numPlayers; agent++)
            {
                if (agent != player)
                {
                    big_pi_reach_probability *= player_reach_probabilities[agent];
                }
            }

            double regret = actionUtilities[action][player] - nodeUtilities[player];
            double weightedRegret = big_pi_reach_probability * regret;

            
            var agg_regret = _aggregateRegrets[infoset][action];
            _aggregateRegrets[infoset][action] = Math.Max(0.0, agg_regret + weightedRegret);
        }

        return nodeUtilities;
    }

    

    /// <summary>
    /// Plays out a game by sampling actions according to the current strategies
    /// </summary>
    /// <returns>The payoff of the game for each player</returns>
    private IReadOnlyList<double> PlayoutGame(IGame<TAction> game)
    {

        while (!game.IsTerminalState())
        {
           SampleStrategyAndApplyMove(game);
        }

        return game.Payout();
    }

    private TAction SampleStrategy(IGame<TAction> game, double epsilon = 0.0)
    {
        int player = game.PlayerToAct();
        string infoset = game.InformationSet();
        var actions = game.Actions();

        if (epsilon > 0.0 && _random.Value.NextDouble() < epsilon)
        {
            int randomAction = _random.Value.Next(actions.Count);
            return(actions[randomAction]);
        }
        
        return GetMove(player, infoset, actions);
    }

    

    /// <summary>
    /// Makes a move in the game by sampling an action according to the current strategy
    /// </summary>
    /// <param name="game"></param>
    /// <param name="epsilon">Chance to make a random move instead of sampling the strategy</param>
    private void SampleStrategyAndApplyMove(IGame<TAction> game, double epsilon = 0.0)
    {
        var move = SampleStrategy(game, epsilon);
        game.MakeMove(move);
    }
    

    /// <summary>
    /// Saves the current strategy to the given file
    /// </summary>
    /// <param name="filename">The file to save to</param>
    public void Save(string filename)
    {
        const long MaxBytes = 50L * 1024 * 1024; // 50 MiB

        var infoStates = _aggregateRegrets.Keys.ToList();
        infoStates.Sort();

        int part = 0;
        string currentFile() => part == 0 ? filename : $"{filename}_{part}";

        StreamWriter writer = null;
        try
        {
            writer = new StreamWriter(currentFile(), false, Encoding.UTF8);
            writer.WriteLine("REGRETS");

            foreach (var infoState in infoStates)
            {
                string line = infoState + "\t" + string.Join(" ", _aggregateRegrets[infoState]) + Environment.NewLine;
                long byteCount = Encoding.UTF8.GetByteCount(line);

                // If writing this line would exceed the max file size, finish this file with CONTINUED and start a new one
                if (writer.BaseStream.Length + byteCount > MaxBytes)
                {
                    writer.WriteLine("CONTINUED");
                    writer.Dispose();
                    writer = null;

                    part++;
                    writer = new StreamWriter(currentFile(), false, Encoding.UTF8);
                    writer.WriteLine("REGRETS");
                }

                writer.Write(line);
            }

            // Write END on the last file
            writer.WriteLine("END");
        }
        finally
        {
            writer?.Dispose();
        }

    }

    /// <summary>
    /// Loads the strategy that was saved into the file. Training can be
    /// resumed without loss of information.
    /// </summary>
    /// <param name="filename">The file to load from</param>
    public bool TryLoad(string filename)
    {

        if (!File.Exists(filename))
        {
            return false;
        }

        _aggregateRegrets.Clear();

        int part = 0;
        bool finished = false;

        while (!finished)
        {
            string current = part == 0 ? filename : $"{filename}_{part}";
            if (!File.Exists(current))
            {
                // If first part missing, fail. If subsequent part missing while expecting CONTINUED, fail.
                return false;
            }

            using (var reader = new StreamReader(current, Encoding.UTF8))
            {
                string header = reader.ReadLine();
                if (header != "REGRETS")
                {
                    Console.Error.WriteLine($"Could not parse file {current}");
                    return false;
                }

                string buffer;
                while ((buffer = reader.ReadLine()) != null)
                {
                    if (buffer == "END")
                    {
                        finished = true;
                        break;
                    }
                    if (buffer == "CONTINUED")
                    {
                        // move on to next part
                        break;
                    }

                    var parts = buffer.Split('\t');
                    if (parts.Length < 2) continue;
                    string key = parts[0];
                    var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(double.Parse)
                        .ToList();
                    _aggregateRegrets[key] = values;
                }
            }

            part++;
        }

        Console.WriteLine("Loaded file");
        return true;
    }

    public IPlayer<TAction> FreezeStrategy()
    {
        return this.DeepCopy();
    }
}

public class CFRPlayer<TAction> : IPlayer<TAction>, IDisposable
{

    protected  ConcurrentDictionary<string, List<double>> _aggregateRegrets;
    private bool disposedValue;
    protected readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

    public CFRPlayer<TAction> DeepCopy()
    {
        var copy = new CFRPlayer<TAction>();
        copy._aggregateRegrets = new ConcurrentDictionary<string, List<double>>(_aggregateRegrets);
        foreach (var key in _aggregateRegrets.Keys)
        {
            copy._aggregateRegrets[key] = new List<double>(_aggregateRegrets[key]);
        }
        return copy;
    }

    public TAction GetMove(int player, string infoset, IReadOnlyList<TAction> actions)
    {
        if (actions.Count == 1)
        {
            return actions[0];
        }
        var strategy = GetStrategy(player, infoset, actions);
        // Sample an action according to the current strategy
        double rnd = _random.Value.NextDouble();
        double cumulativeProbability = 0.0;
        int chosenAction = 0;
        for (int action = 0; action < actions.Count; action++)
        {
            cumulativeProbability += strategy[action];
            if (rnd <= cumulativeProbability)
            {
                chosenAction = action;
                break;
            }
        }
        return (actions[chosenAction]);
    }

    /// <summary>
    /// Get a probability distribution for choosing an available action at an infoset.
    /// Weight between actions with positive regret proportionally to their share of the positive regret,
    /// or choose randomly from all actions if none have positive regret
    /// </summary>
    public IReadOnlyList<double> GetStrategy(int player, string infosetId, IReadOnlyList<TAction> actions)
    {
        // Ensure an entry exists for this information set. Use optimistic initialization: start each action with regret 10.0
        var cumulativeRegrets = _aggregateRegrets.GetOrAdd(infosetId, _ => Enumerable.Repeat(10.0, actions.Count).ToList());
        var strategy = new List<double>();
        double totalPositiveRegret = 0;

        for (int action = 0; action < actions.Count; action++)
        {
            double positiveRegret = Math.Max(cumulativeRegrets[action], 0);
            strategy.Add(positiveRegret);
            totalPositiveRegret += positiveRegret;
        }

        // Normalize to sum probabilities to sum to 1.0
        if (totalPositiveRegret > 0)
        {
            for (int action = 0; action < actions.Count; action++)
            {
                strategy[action] = strategy[action] / totalPositiveRegret;
            }
        }
        else
        {
            for (int action = 0; action < actions.Count; action++)
            {
                strategy[action] = 1.0 / actions.Count; // uniform random if no actions have positive regret
            }
        }
        return strategy;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
                _aggregateRegrets.Clear();
                _aggregateRegrets = null;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~CFRPlayer()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}