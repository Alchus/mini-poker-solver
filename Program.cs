namespace MiniPokerSolver
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var solver = new Solver();

            (var A, var B) = solver.Solve_Anneal(iterations:2000000, cards:13);

            //Console.WriteLine(A);
            //Console.WriteLine(B);
            
            /*
             * Strategy A = Strategy.Random(3, 4);
            Strategy B = Strategy.Uniform(3, 4);

            var x = solver.Evaluate(A, B);


            Console.WriteLine(A.Table.ToString());

            Console.WriteLine(x);
            */



        }
    }
}