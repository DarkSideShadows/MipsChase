# MipsChase
**Assignment for CS596 - Game State Machines**

Inspired by [this video](https://www.youtube.com/watch?v=Q2D1xoGjviU)  
Based on [MipsChase repo](https://github.com/Prof-Chris/MipsChase)  

### Demo Video
[![Watch the video](https://www.youtube.com/watch?v=pvuPb3OyqA8)](https://www.youtube.com/watch?v=pvuPb3OyqA8)

- **Player Behavior**
  - Moves slowly with full rotation
  - Moves fast with limited rotation
  - Slows down when rotation angle is outside threshold
  - Can dive to catch the target
  - Enters recovery phase after dive

- **Target Behavior**
  - Remains idle until player approaches
  - Hops away when too close, avoiding screen edges
  - Attaches to player when caught
