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

This is an extension of Kuhn poker.

## Solver
The solver uses conterfactual regret minimization to evaluate the payoffs for potential move and update each player's strategy whenever another option would have resulted in a better outcome.

This is a port and small extension of https://github.com/Fro116/counterfactual-regret-minimization


## Output



An approximate solution reached after 100,000,000 rounds is:
```
Player 0 (Acting first)
-------------------------------------------------------------------------------------------
               A     K     Q     J     T     9     8     7     6     5     4     3     2  
CHECK+FOLD     0.00  0.00  0.00  0.00  0.00  0.67  0.42  0.23  0.13  0.69  0.85  0.75  0.56
CHECK+CALLBET  0.00  0.00  0.36  0.77  1.00  0.28  0.56  0.76  0.87  0.16  0.13  0.00  0.00
CHECK+RAISE    0.81  0.00  0.00  0.00  0.00  0.05  0.02  0.01  0.01  0.15  0.03  0.00  0.00
BET+FOLD       0.00  0.00  0.57  0.15  0.00  0.00  0.00  0.00  0.00  0.00  0.00  0.25  0.44
BET+CALLRAISE  0.19  1.00  0.07  0.09  0.00  0.00  0.00  0.00  0.00  0.00  0.00  0.00  0.00


Player 1's Strategy When Checked To
--------------------------------------------------------------------------------
               A     K     Q     J     T     9     8     7     6     5     4     3     2
BET+CALLRAISE  1.00  0.86  0.49  0.64  0.01  0.00  0.00  0.00  0.00  0.00  0.00  0.00  0.00
BET+FOLD       0.00  0.14  0.51  0.36  0.49  0.00  0.00  0.00  0.00  0.00  0.00  0.50  1.00
CHECK          0.00  0.00  0.00  0.00  0.50  1.00  1.00  1.00  1.00  1.00  1.00  0.50  0.00

Player 1's Strategy When Bet Into
--------------------------------------------------------------------------------
               A     K     Q     J     T     9     8     7     6     5     4     3     2
CALLBET        0.00  0.49  1.00  1.00  0.85  0.84  0.36  0.46  0.70  0.28  0.01  0.00  0.00
FOLD           0.00  0.00  0.00  0.00  0.15  0.12  0.58  0.54  0.12  0.51  0.99  1.00  1.00
RAISE          1.00  0.51  0.00  0.00  0.00  0.04  0.06  0.00  0.18  0.21  0.00  0.00  0.00
```