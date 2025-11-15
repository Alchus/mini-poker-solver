using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CFRMiniPoker
{
    internal class Trainer<TGame, TAction> where TGame : IGame<TAction>, new()
    {

        private readonly TGame _game;
        private IStrategySolver<TAction> _solver;

        public Trainer(TGame game, IStrategySolver<TAction> solver)
        {

            _solver = solver;
            _game = game;

        }


        public void TrainAndEvaluateLoop(int iterationsPerStep, int maxSteps)
        {

            var filename = $"{_game.GetType()}-{_solver.GetType().ToString().Split('`')[0]}.strategy";
            if (_solver.TryLoad(filename))
            {
                Console.WriteLine($"Loaded strategy file {filename}");
            } else
            {
                Console.WriteLine($"Could not load strategy file {filename}, starting from scratch.");
            }

            //_solver.Save(filename);

            var previousSolver = _solver.FreezeStrategy();

            Console.WriteLine($"Beginning training. Will take {iterationsPerStep} steps before save and evaluating.");
            for (int step = 0; step < maxSteps; step++)
            {
                _solver.Train(iterationsPerStep);
                Console.WriteLine($"Completed {(step + 1) * iterationsPerStep} training iterations.");
                Console.WriteLine("Saving...");
                _solver.Save(filename);
                var player = _solver.FreezeStrategy();
                var randomPlayer = new RandomPlayer<TAction>();

                double avgReward = EvaluateStrategy(player, randomPlayer, 1000);
                Console.WriteLine($"Average reward vs random as player 0: {avgReward}");
                avgReward = EvaluateStrategy(randomPlayer, player, 1000);
                Console.WriteLine($"Average reward vs random as player 1: {-1.0 * avgReward}");

                var avgReward_self = EvaluateStrategy(player, player, 10000);
                Console.WriteLine("Average reward for player 0: " + avgReward_self);

                var avgReward0 = EvaluateStrategy(player, previousSolver, 10000);
                Console.WriteLine($"Average reward vs previous iteration as player 0: {avgReward0}");
                var avgReward1 = EvaluateStrategy(previousSolver, player, 10000);
                Console.WriteLine($"Average reward vs previous iteration as player 1: {-1.0 * avgReward1}");
                Console.WriteLine($"\tTotal improvement over previous strategy: {(avgReward0 - avgReward1).ToString("0.###")}");
                previousSolver = player;

            }


        }



        public double EvaluateStrategy(IPlayer<TAction> player0, IPlayer<TAction> player1, int numGames)
        {
            double totalReward = 0.0;
            for (int i = 0; i < numGames; i++)
            {
                _game.BeginGame();
                while (!_game.IsTerminalState())
                {
                    int currentPlayer = _game.PlayerToAct();
                    string infoSet = _game.InformationSet();
                    var actions = _game.Actions();
                    TAction move;
                    if (currentPlayer == 0)
                    {
                        move = player0.GetMove(currentPlayer, infoSet, actions);
                    }
                    else
                    {
                        move = player1.GetMove(currentPlayer, infoSet, actions);
                    }
                    _game.MakeMove(move);
                }
                totalReward += _game.Payout()[0];
            }
            return totalReward / numGames;

        }
    }
}
