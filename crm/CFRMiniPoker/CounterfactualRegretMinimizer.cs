using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Class to perform Counterfactual Regret Minimization on multiplayer games.
/// </summary>
/// <typeparam name="T">The type of actions</typeparam>
public class CounterfactualRegretMinimizer<T>
{
    private readonly IGame<T> _game;
    private readonly int _numPlayers;
    private readonly List<Dictionary<string, List<double>>> _aggregateRegrets;
    private readonly List<Dictionary<string, List<double>>> _aggregateStrategies;

    /// <summary>
    /// Initializes the CFR algorithm
    /// </summary>
    /// <param name="game">The game to analyze</param>
    public CounterfactualRegretMinimizer(IGame<T> game)
    {
        _game = game;
        _numPlayers = game.NumPlayers();
        _aggregateRegrets = Enumerable.Range(0, _numPlayers)
            .Select(_ => new Dictionary<string, List<double>>())
            .ToList();
        _aggregateStrategies = Enumerable.Range(0, _numPlayers)
            .Select(_ => new Dictionary<string, List<double>>())
            .ToList();
    }

    /// <summary>
    /// Runs CFR for an infinite number of iterations. The computed strategy will
    /// periodically be saved to an output file.
    /// </summary>
    /// <param name="outputFile">The file to save the current strategy to</param>
    /// <param name="itersPerSave">The number of iterations before the strategy should be checkpointed</param>
    /// <param name="itersPerUpdate">The number of iterations before an update message is printed to the console</param>
    public void Solve(string outputFile, int itersPerSave, int itersPerUpdate, int maxIterations = int.MaxValue)
    {
        int saveCounter = 0;
        long totalIterations = 0;
        Console.WriteLine("BEGINNING TRAINING");

        while (totalIterations < maxIterations)
        {
            Train(itersPerUpdate);

            saveCounter += itersPerUpdate;
            totalIterations += itersPerUpdate;
            Console.WriteLine($"COMPLETED ITERATION: {totalIterations}");

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
        for (int i = 0; i < iterations; i++)
        {
            var copy = _game.DeepCopy();
            copy.BeginGame();
            var probabilities = Enumerable.Repeat(1.0, _numPlayers).ToList();
            Train(copy, probabilities);
        }
    }

    private IReadOnlyList<double> Train(IGame<T> game, IReadOnlyList<double> probabilities)
    {
        int player = game.PlayerToAct();
        if (game.IsTerminalState())
        {
            return game.Payout();
        }

        string id = game.InformationSet();
        var actions = game.Actions();

        if (!_aggregateStrategies[player].ContainsKey(id))
        {
            _aggregateStrategies[player][id] = Enumerable.Repeat(0.0, actions.Count).ToList();
            _aggregateRegrets[player][id] = Enumerable.Repeat(0.0, actions.Count).ToList();
        }

        var strategy = GetStrategy(player, id, actions);
        var actionUtilities = new List<IReadOnlyList<double>>();
        var nodeUtilities = Enumerable.Repeat(0.0, _numPlayers).ToList();

        for (int action = 0; action < actions.Count; action++)
        {
            var gameCopy = game.DeepCopy();
            gameCopy.MakeMove(actions[action]);
            var probabilitiesCopy = probabilities.ToList();
            probabilitiesCopy[player] *= strategy[action];

            var actionUtility = Train(gameCopy, probabilitiesCopy);
            actionUtilities.Add(actionUtility);
            for (int agent = 0; agent < _numPlayers; agent++)
            {
                nodeUtilities[agent] += strategy[action] * actionUtility[agent];
            }
        }

        for (int action = 0; action < actions.Count; action++)
        {
            double counterfactual = 1.0;
            for (int agent = 0; agent < _numPlayers; agent++)
            {
                if (agent != player)
                {
                    counterfactual *= probabilities[agent];
                }
            }

            double regret = actionUtilities[action][player] - nodeUtilities[player];
            _aggregateRegrets[player][id][action] += counterfactual * regret;
            _aggregateStrategies[player][id][action] += counterfactual * strategy[action];
        }

        return nodeUtilities;
    }

    private IReadOnlyList<double> GetStrategy(int player, string id, IReadOnlyList<T> actions)
    {
        var cumulativeRegrets = _aggregateRegrets[player][id];
        var strategy = new List<double>();
        double normalizingSum = 0;

        for (int action = 0; action < actions.Count; action++)
        {
            double regret = Math.Max(cumulativeRegrets[action], 0);
            strategy.Add(regret);
            normalizingSum += regret;
        }

        for (int action = 0; action < actions.Count; action++)
        {
            strategy[action] = normalizingSum > 0 
                ? strategy[action] / normalizingSum 
                : 1.0 / actions.Count;
        }

        return strategy;
    }

    /// <summary>
    /// Saves the current strategy to the given file
    /// </summary>
    /// <param name="filename">The file to save to</param>
    public void Save(string filename)
    {
        using (var writer = new StreamWriter(filename))
        {
            writer.WriteLine("PROBABILITIES");
            for (int player = 0; player < _numPlayers; player++)
            {
                writer.WriteLine($"PLAYER: {player}");
                foreach (var kvp in _aggregateStrategies[player])
                {
                    writer.Write(kvp.Key + "\t");
                    double total = kvp.Value.Sum();
                    writer.WriteLine(string.Join(" ", kvp.Value.Select(v => (v / total).ToString())));
                }
                writer.WriteLine("END");
            }

            writer.WriteLine("STRATEGIES");
            for (int player = 0; player < _numPlayers; player++)
            {
                writer.WriteLine($"PLAYER: {player}");
                foreach (var kvp in _aggregateStrategies[player])
                {
                    writer.Write(kvp.Key + "\t");
                    writer.WriteLine(string.Join(" ", kvp.Value));
                }
                writer.WriteLine("END");
            }

            writer.WriteLine("REGRETS");
            for (int player = 0; player < _numPlayers; player++)
            {
                writer.WriteLine($"PLAYER: {player}");
                foreach (var kvp in _aggregateRegrets[player])
                {
                    writer.Write(kvp.Key + "\t");
                    writer.WriteLine(string.Join(" ", kvp.Value));
                }
                writer.WriteLine("END");
            }
        }
    }

    /// <summary>
    /// Loads the strategy that was saved into the file. Training can be
    /// resumed without loss of information.
    /// </summary>
    /// <param name="filename">The file to load from</param>
    public void Load(string filename)
    {
        using (var reader = new StreamReader(filename))
        {
            string buffer = reader.ReadLine();
            if (buffer != "PROBABILITIES")
            {
                Console.Error.WriteLine($"Could not parse file {filename}");
                return;
            }

            // Skip probabilities section
            for (int player = 0; player < _numPlayers; player++)
            {
                reader.ReadLine(); // discard PLAYER: line
                while ((buffer = reader.ReadLine()) != "END") { }
            }

            buffer = reader.ReadLine();
            if (buffer != "STRATEGIES")
            {
                Console.Error.WriteLine($"Could not parse file {filename}");
                return;
            }

            _aggregateStrategies.Clear();
            for (int player = 0; player < _numPlayers; player++)
            {
                _aggregateStrategies.Add(new Dictionary<string, List<double>>());
                reader.ReadLine(); // discard PLAYER: line
                while ((buffer = reader.ReadLine()) != "END")
                {
                    var parts = buffer.Split('\t');
                    string key = parts[0];
                    var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(double.Parse)
                        .ToList();
                    _aggregateStrategies[player][key] = values;
                }
            }

            buffer = reader.ReadLine();
            if (buffer != "REGRETS")
            {
                Console.Error.WriteLine($"Could not parse file {filename}");
                return;
            }

            _aggregateRegrets.Clear();
            for (int player = 0; player < _numPlayers; player++)
            {
                _aggregateRegrets.Add(new Dictionary<string, List<double>>());
                reader.ReadLine(); // discard PLAYER: line
                while ((buffer = reader.ReadLine()) != "END")
                {
                    var parts = buffer.Split('\t');
                    string key = parts[0];
                    var values = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(double.Parse)
                        .ToList();
                    _aggregateRegrets[player][key] = values;
                }
            }
        }
    }
} 