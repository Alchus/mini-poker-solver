using System;

namespace CFRMiniPoker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create the game and CFR solver
            var game = new LiarsDice();
            var solver = new CounterfactualRegretMinimizer<string>(game);

            // Training parameters
            const string outputFile = "LiarsDice.txt";

            Console.WriteLine("Starting Liars Dice training...");

                solver.Solve(outputFile, itersPerSave: 1, itersPerUpdate: 1, maxIterations: 1);

            Console.WriteLine("Training complete!");
        }
    }
}
