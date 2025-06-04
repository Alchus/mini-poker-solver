using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiniPokerSolver
{
    public static class Constants
    {
        public const int Ante = 2;
        public const int Bet = 1;
        public const int Raise = 3;
        public static string[] Descriptions => new[] { "       Bet", 
                                                       "  Continue", 
                                                       "     Raise", 
                                                       "Call Raise" };
        public static string CardNames =>  "            A       K       Q       J       10      9       8       7       6       5       4       3       2" ;
    }

    public struct Strategy
    {
        public double[][] Table;

        public Strategy(double[][] Table)
        {
            this.Table = Table;
        }

        public Strategy DeepCopy()
        {
            var newTable = new double[Table.Length][];
            for (int i = 0; i < Table.Length; i++)
            {
                newTable[i] = new double[Table[i].Length];
                Array.Copy(Table[i], newTable[i], Table[i].Length);
            }
            return new Strategy(newTable);
        }

        public static Strategy Uniform(int hands, int spots)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = 0.5;
                }
            }

            return new Strategy(tab);
        }
        public static Strategy Random(int hands, int spots)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = System.Random.Shared.NextDouble();
                }
            }

            return new Strategy(tab);
        }

        public Strategy Twiddle(int hands, int spots = 4)
        {
            var tab = new double[hands][];
            foreach (var h in Enumerable.Range(0, hands))
            {
                tab[h] = new double[spots];
                foreach (var s in Enumerable.Range(0, spots))
                {
                    tab[h][s] = Table[h][s];
                }
            }
            int hand = System.Random.Shared.Next(0, hands);
            int spot = System.Random.Shared.Next(0, spots);

            double delta = (System.Random.Shared.NextDouble() - 0.5) * 0.01;
            var v = tab[hand][spot];
            v += delta;
            v = Math.Max(v, 0.0001);
            v = Math.Min(v, 0.9999);
            tab[hand][spot] = v;

            return new Strategy(tab);
        }

        

        public override string ToString()
        {
            var hands = Table.Length;
            var spots = 4;
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(Constants.CardNames);

            foreach (var s in Enumerable.Range(0, spots))
            {
                sb.Append(Constants.Descriptions[s] + ": ");
                foreach (var h in Enumerable.Range(0, hands))
                {
                    sb.Append(Table[h][s].ToString("0.000") + " , ");
                }
                sb.Append("\n");
            }

            return sb.ToString();
        }

    }
    public class Solver
    {

        public (Strategy A, Strategy B) Solve_RandomWalk(int iterations = 1001 , int cards = 3, int spots = 4)
        {
            Strategy A = Strategy.Random(cards, spots);
            Strategy B = Strategy.Random(cards, spots);
            double value = 0;

            for (int i = 0; i < iterations; i++)
            {
                var trialA = A.Twiddle(cards);
                var trialVal = Evaluate(trialA, B);
                if (trialVal > value)
                {
                    value = trialVal;
                    A = trialA;
                    //Console.WriteLine("Updated A, value is now " + value);
                }

              
                var trialB = B.Twiddle(cards);
                trialVal = Evaluate(A, trialB);
                if (trialVal < value)
                {
                    value = trialVal;
                    B = trialB;
                    //Console.WriteLine("Updated B, value is now " + value);
                }


                if ( i % 1000 == 0)
                {
                    Console.WriteLine("Best Strategy for A (First to act):\n" + A.ToString());
                    Console.WriteLine("Best Strategy for B (Last to act):\n" + B.ToString());
                    Console.WriteLine("Expected Value for A:" + value);
                }
               
            }

            Console.WriteLine("Best Strategy for A (First to act):\n" + A.ToString());
            Console.WriteLine("Best Strategy for B (Last to act):\n" + B.ToString());
            Console.WriteLine("Expected Value for A:" +  value);

            return (A, B);
        }

        public (Strategy A, Strategy B) Solve_Iterative(int iterations = 1000, int cards = 3, int spots = 4, double temperature = 0.1, double coolingRate = 0.997, double minimumTemperature = 0.00001){
            Strategy A = Strategy.Uniform(cards, spots);
            Strategy B = Strategy.Uniform(cards, spots);


            for (int i = 0; i < iterations; i++)
            {
                if (temperature < minimumTemperature)
                {
                    Console.WriteLine($"Stopping early at iteration {i} - temperature {temperature} below minimum {minimumTemperature}");
                    break;
                }

                int entriesUpdatedThisIteration = 0;
                double currentValue = Evaluate(A, B);
                double startingValueForA = currentValue;
                foreach (var h in Enumerable.Range(0, cards))
                {

                    // For each hand h and spot s, calculate the improvement in payoff if we increase the value at that position.
                    // Also calculate the improvement in payoff if we decrease the value at that position.
                    // if the value is already at 0.99999 or 0.00001, then skip increasing/decreasing it
                    // If only one of the two improvements is positive, then we move in that direction.
                    // If both are positive, we move in the direction that has the highest improvement.
                    // If both are negative, we do nothing.

                    foreach (var s in Enumerable.Range(0, spots))
                    {
                        Strategy trialA_add = A.DeepCopy();
                        Strategy trialA_sub = A.DeepCopy();
                        var delta = temperature;

                        // ADD to A
                        double improvement_add_A = 0;
                        double trialVal_add = currentValue;
                        if (trialA_add.Table[h][s] < 1){
                            trialA_add.Table[h][s] = Math.Min( 1, trialA_add.Table[h][s] + delta);
                            trialVal_add = Evaluate(trialA_add, B);
                            improvement_add_A = trialVal_add - currentValue;
                        }
                        
                        // Do the same for subtracting from A
                        double improvement_sub_A = 0;           
                        double trialVal_sub = currentValue;
                        if (trialA_sub.Table[h][s] > 0.0){
                            trialA_sub.Table[h][s] = Math.Max( 0.0, trialA_sub.Table[h][s] - delta);
                            trialVal_sub = Evaluate(trialA_sub, B);
                            improvement_sub_A = trialVal_sub - currentValue;
                        }

                        if (improvement_add_A > 0 && improvement_sub_A > 0) // Both are improvements
                        {
                            // Move in the direction that has the highest improvement.
                            if (improvement_add_A > improvement_sub_A)
                            {
                                A = trialA_add;
                                currentValue = trialVal_add;
                                entriesUpdatedThisIteration++;
                            } else {
                                A = trialA_sub;
                                currentValue = trialVal_sub;
                                entriesUpdatedThisIteration++;
                            }
                        } else if (improvement_add_A > 0) // Only Adding is an improvement
                        {
                            A = trialA_add;
                            currentValue = trialVal_add;
                            entriesUpdatedThisIteration++;
                        } else if (improvement_sub_A > 0) // Only Subtracting is an improvement
                        {
                            A = trialA_sub;
                            currentValue = trialVal_sub;
                            entriesUpdatedThisIteration++;
                        }
                    }
                                        
                }

                double endingValueForA = currentValue;

                // Do the same for B
                foreach (var h in Enumerable.Range(0, cards))
                {
                    foreach (var s in Enumerable.Range(0, spots))
                    {
                        Strategy trialB_add = B.DeepCopy();
                        Strategy trialB_sub = B.DeepCopy();
                        var delta = temperature;

                        // ADD to B
                        double improvement_add_B = 0;
                        double trialVal_add = currentValue;
                        if (trialB_add.Table[h][s] < 1){
                            trialB_add.Table[h][s] = Math.Min(1, trialB_add.Table[h][s] + delta);
                            trialVal_add = Evaluate(A, trialB_add);
                            improvement_add_B = currentValue - trialVal_add; // Note: reversed since B wants to minimize
                        }
                        
                        // Do the same for subtracting from B
                        double improvement_sub_B = 0;           
                        double trialVal_sub = currentValue;
                        if (trialB_sub.Table[h][s] > 0.0){
                            trialB_sub.Table[h][s] = Math.Max(0.0, trialB_sub.Table[h][s] - delta);
                            trialVal_sub = Evaluate(A, trialB_sub);
                            improvement_sub_B = currentValue - trialVal_sub; // Note: reversed since B wants to minimize
                        }

                        if (improvement_add_B > 0 && improvement_sub_B > 0) // Both are improvements
                        {
                            // Move in the direction that has the highest improvement
                            if (improvement_add_B > improvement_sub_B)
                            {
                                B = trialB_add;
                                currentValue = trialVal_add;
                                entriesUpdatedThisIteration++;
                            } else {
                                B = trialB_sub;
                                currentValue = trialVal_sub;
                                entriesUpdatedThisIteration++;
                            }
                        } else if (improvement_add_B > 0) // Only Adding is an improvement
                        {
                            B = trialB_add;
                            currentValue = trialVal_add;
                            entriesUpdatedThisIteration++;
                        } else if (improvement_sub_B > 0) // Only Subtracting is an improvement
                        {
                            B = trialB_sub;
                            currentValue = trialVal_sub;
                            entriesUpdatedThisIteration++;
                        }
                    }
                }

                if (i % 100 == 0)
                {
                    Console.WriteLine("Iteration " + i + ", Temperature: " + temperature + ", Entries Updated: " + entriesUpdatedThisIteration);
                    Console.WriteLine("     A strategy starting value: " + startingValueForA + ", ending value: " + endingValueForA + ", change: " + (endingValueForA - startingValueForA));
                }

                if (entriesUpdatedThisIteration == 0)
                {
                    Console.WriteLine("No improvements this iteration (" + i + "). Temperature: " + temperature);
                    // Skip the normal cooling for this iteration since we're doing a more aggressive cool
                    temperature = Math.Pow(temperature, 10);
                }
                else 
                {
                    temperature *= coolingRate;
                }
            }

            return (A, B);
        }

        public (Strategy A, Strategy B) Solve_MonteCarlo(int iterations = 1000, int cards = 3, int spots = 4)
        {
            Strategy A = Strategy.Random(cards, spots);
            Strategy B = Strategy.Random(cards, spots);
            double value = 0;

            for (int i = 0; i < iterations; i++)
            {
                var trialA = Strategy.Random(cards, spots);
                var trialVal = Evaluate(trialA, B);
                if (trialVal > value)
                {
                    value = trialVal;
                    A = trialA;
                    Console.WriteLine("Updated A, value is now " + value);
                }

                var trialB = Strategy.Random(cards, spots);
                trialVal = Evaluate(A, trialB);
                if (trialVal < value)
                {
                    value = trialVal;
                    B = trialB;
                    Console.WriteLine("Updated B, value is now " + value);
                }


            }

            Console.WriteLine("Best Strategy for A:\n" + A.ToString());
            Console.WriteLine("Best Strategy for B:\n" + B.ToString());
            Console.WriteLine("Expected Value for A:" + value);

            return (A, B);
        }


        public double ChanceToReachState(double[] StrategyA, double[] StrategyB, (Func<double, double>[] pathA, Func<double, double>[] pathB) path)
        {
            return StrategyA.Zip(path.pathA, (s, f) => f(s) ).Aggregate((double x, double y) => x * y)
                * StrategyB.Zip(path.pathB, (s, f) => f(s)).Aggregate((double x, double y) => x * y);
        }

        public static readonly Func<double, double> Yes = (d => d);
        public static readonly Func<double, double> No  = (d => 1 - d);
        public static readonly Func<double, double> _   = (_ => 1.0);

        public double ShowdownValue(int handA, int handB, int ante, double calledAmount)
        {
            if (handA < handB)
            {
                return ante + calledAmount;
            } else
            {
                return -calledAmount;
            }
        }


        public double Evaluate(Strategy A, Strategy B) {

            // Odds of reaching each outcome, for each player
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S0 = (new Func<double, double>[] { No, _, _, _ },    new Func<double, double>[] { No, _, _, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S1 = (new Func<double, double>[] { No, No, _, _ },   new Func<double, double>[] { Yes, _, _, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S2 = (new Func<double, double>[] { No, Yes, No, _ },    new Func<double, double>[] { Yes, _, _, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S3 = (new Func<double, double>[] { No, Yes, Yes, _ },new Func<double, double>[] { Yes, _, _, No });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S4 = (new Func<double, double>[] { No, Yes, Yes, _ },new Func<double, double>[] { Yes, _, _, Yes });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S5 = (new Func<double, double>[] { Yes, _, _, _ },   new Func<double, double>[] { _, No, _, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S6 = (new Func<double, double>[] { Yes, _, _, _ }, new Func<double, double>[] { _, Yes, No, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S7 = (new Func<double, double>[] { Yes, _, _, No }, new Func<double, double>[] { _, Yes, Yes, _ });
            (Func<double, double>[] pathA, Func<double, double>[] pathB) S8 = (new Func<double, double>[] { Yes, _, _, Yes }, new Func<double, double>[] { _, Yes, Yes, _ });

            var paths = new[] { S0, S1, S2, S3, S4, S5, S6, S7, S8};

            double totalValue = 0.0;
            for (int handA = 0; handA < A.Table.Length; handA++)
            {
                for (int handB = 0; handB < B.Table.Length; handB++)
                {
                    if (handA == handB) continue;

                    //Console.WriteLine($"---I have:{handA}, Opponent has:{handB}");

                    //Value of each state, for player A
                    var values = new[]
                    {
                        ShowdownValue(handA, handB, Constants.Ante, 0),// X, X
                        0,                             // X, B, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Bet),// X, B, C
                        Constants.Ante + Constants.Bet,                             // X, B, R, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Raise),// X, B, R, C
                        Constants.Ante,                             // B, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Bet),// B, C
                        -Constants.Bet,                            // B, R, F
                        ShowdownValue(handA, handB, Constants.Ante, Constants.Raise) // B, R, C
                    };

                    for(int si = 0; si < paths.Length; si++)
                    {
                        var chanceToReachSi = ChanceToReachState(A.Table[handA], B.Table[handB], paths[si]);
                        var valueOfSi = values[si];
                        //Console.WriteLine($"  State {i}: Chance to reach={chanceToReachSi}, Value={valueOfSi}");
                        totalValue += chanceToReachSi * valueOfSi;

                    }


                }
            }

            return totalValue / (A.Table.Length * (A.Table.Length - 1));
        }


    }
}
