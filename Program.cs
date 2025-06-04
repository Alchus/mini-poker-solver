namespace MiniPokerSolver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var solver = new Solver();

            (var A, var B) = solver.Solve_Iterative(iterations:2000, cards:13);

            Console.WriteLine("Strategy A");
            Console.WriteLine(A);
            
            Console.WriteLine("Strategy B");
            Console.WriteLine(B);
           
            var value = solver.Evaluate(A, B);
            Console.WriteLine("Value: " + value);


            var BaselineA = new Strategy(new double[][] { 
                new double[] { 0.23, 1.00, 0.42, 0.38, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.25, 0.38 },
                new double[] { 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 0.97, 0.99, 0.13, 0.16, 0.09, 0.00, 0.00 },
                new double[] { 1.00, 1.00, 0.00, 0.00, 0.00, 0.07, 0.02, 0.06, 0.46, 0.04, 0.79, 0.66, 1.00 },
                new double[] { 1.00, 1.00, 0.09, 0.14, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00 }
            });
            var BaselineB = new Strategy(new double[][] {
                new double[] { 1.00, 1.00, 1.00, 1.00, 0.64, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.55, 1.00 },
                new double[] { 1.00, 1.00, 1.00, 1.00, 0.98, 0.97, 0.97, 0.75, 0.15, 0.20, 0.07, 0.00, 0.00 },
                new double[] { 1.00, 0.48, 0.00, 0.00, 0.00, 0.01, 0.03, 0.15, 0.53, 0.93, 0.91, 0.89, 1.00 },
                new double[] { 1.00, 0.85, 0.75, 0.37, 0.09, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00 }
            });

            
            Console.WriteLine("new A vs baseline B");
            var newA_vs_baselineB = solver.Evaluate(A, BaselineB);
            Console.WriteLine("Value: " + newA_vs_baselineB);

            Console.WriteLine("new B vs baseline A");
            var newB_vs_baselineA = solver.Evaluate(BaselineA, B);
            Console.WriteLine("Value: " + newB_vs_baselineA);



        }
    }
}