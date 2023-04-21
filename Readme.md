# Mini Poker Solver
Thomas Sowders



## Game Rules
This project generates solutions for a miniature poker-like game with the following design:

* Use a deck of N=13 cards with distinct ranks *(i.e. Ace, King, Queen, Jack, Ten, Nine ... Three, Two)*
* Deal one card to each player
* The pot starts with $2 in ante
* Player A may either check or bet exactly $1. If A checks, then B may also check or bet exactly $1.
* The player facing a bet may fold, call, or raise to exactly $3.
* When facing a raise, the initial bettor may fold or call (for $2 more), but cannot re-raise.
* If no player folded, the player with the higher-ranked card wins the pot.

## Solver
The solver uses simulated annealing to iteratively improve the strategies for A and B by making random adjustments and discarding ones that don't improve performance.

Note: A player facing a bet has a choice between three options - fold, call, or raise. The game tree node labelled "Continue" denotes the choice to NOT fold. A player who chooses to continue then decides whether to raise. If they do not raise, the result is a call. For example a solution of `Continue=0.50, Raise=0.60` indicates that the player should fold 50% of the time, call 20%, and raise 30%.



An approximate solution reached after 500,000 random tweaks to each player's strategy is:

```
Best Strategy for A (First to act):
            A      K      Q      J      10     9      8      7      6      5      4      3      2
       Bet: 0.23 , 1.00 , 0.42 , 0.38 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.25 , 0.38 ,
  Continue: 1.00 , 1.00 , 1.00 , 1.00 , 1.00 , 1.00 , 0.97 , 0.99 , 0.13 , 0.16 , 0.09 , 0.00 , 0.00 ,
     Raise: 1.00 , 1.00 , 0.00 , 0.00 , 0.00 , 0.07 , 0.02 , 0.06 , 0.46 , 0.04 , 0.79 , 0.66 , 1.00 ,
Call Raise: 1.00 , 1.00 , 0.09 , 0.14 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 ,

Best Strategy for B (Second to act):
            A      K      Q      J      10     9      8      7      6      5      4      3      2
       Bet: 1.00 , 1.00 , 1.00 , 1.00 , 0.64 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.55 , 1.00 ,
  Continue: 1.00 , 1.00 , 1.00 , 1.00 , 0.98 , 0.97 , 0.97 , 0.75 , 0.15 , 0.20 , 0.07 , 0.00 , 0.00 ,
     Raise: 1.00 , 0.48 , 0.00 , 0.00 , 0.00 , 0.01 , 0.03 , 0.15 , 0.53 , 0.93 , 0.91 , 0.89 , 1.00 ,
Call Raise: 1.00 , 0.85 , 0.75 , 0.37 , 0.09 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 , 0.00 ,
```

## Commentary

This solution is a close approximation, but it has some non-linear entries that are unlikely to be optimal. For example, Player A calls a raise with more Jacks(14%) than Queens(9%). This is likely because the annealing process only tweaks one variable at a time, and the evaluation is significantly more responsive to the total number of calls made with either a Jack or Queen than the exact distribution between the two cards. Thus, any change to the strategy in only one dimension is not an advantage. This could be improved by adjusting the annealing process to attempt to "pull" or "push" probability mass to neighboring card ranks in a given spot.