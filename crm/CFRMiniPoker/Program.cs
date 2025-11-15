using System;

namespace CFRMiniPoker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create the game and CFR solver
            var game = new LiarsDice();
            var solver = new CounterfactualRegretMinimizer<byte>(game);

            var trainer = new Trainer<LiarsDice, byte>(game, solver);

            trainer.TrainAndEvaluateLoop(iterationsPerStep: 2000000, maxSteps: int.MaxValue);



            //// Training parameters
            //const string outputFile = "LiarsDice.txt";

            //solver.TryLoad(outputFile);

            //Console.WriteLine("Starting Liars Dice training...");

            //solver.Solve(outputFile, itersPerSave: 500000, itersPerUpdate: 1000, maxIterations: int.MaxValue);

            //Console.WriteLine("Training complete!");
        }
    }
}
