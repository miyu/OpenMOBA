***Warning: OpenMOBA is IN DEVELOPMENT and NOT considered stable!***  
[Development Imgur Image Blog](https://imgur.com/a/LBbM5)  
[@WartyTheNerd on Twitter for Updates](https://twitter.com/WartyTheNerd)  

# OpenMOBA

OpenMOBA is an open-source RTS/MOBA engine which represents game worlds through constructive solid geometry (CSG) as opposed to traditional grid-based approaches.

Of note, OpenMOBA supports:
* 2D Maps (stitchable to form 3D surface-constrained worlds)
* Significant agent radius variation
* Arbitrary hole introduction into game world
* Arbitrary land introduction into game world (e.g. for bridges)
* Near-optimal Pathfinding (With configurable performance vs optimality tradeoffs)
* Iterative Pathfinding (Leverage prior solutions for fast pathfinding)
* Flocking
* Line of Sight Visibility Checks

## Test Game the Game
You can play with OpenMOBA's sandbox by running TestGameTheGame.

Current controls are as follows:

* Right Mouse Button - Pathfind to Point
* Q - Test Raycast to Point
* Wall Introduction (Scribble lines, then introduce them as walls)
  * W - Add point to scribble path.
  * E - Remove last point from scribble path.
  * R - Submit wall introduction scribble as world hole (hold shift for permanent).
