using System;

namespace CFRMiniPoker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create the game and CFR solver
            var game = new KuhnPoker13Game();
            var solver = new CounterfactualRegretMinimizer<string>(game);

            // Training parameters
            const string outputFile = "kuhn_poker13_strategy.txt";

            Console.WriteLine("Starting Kuhn Poker training...");

                solver.Solve(outputFile, itersPerSave: 100000, itersPerUpdate: 10000, maxIterations: 100000000);

            Console.WriteLine("Training complete!");
        }
    }
}
