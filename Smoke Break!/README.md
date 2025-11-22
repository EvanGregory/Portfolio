# Smoke Break!
Smoke Break is a game that I was lead engineer on! It is available on Steam [here](https://store.steampowered.com/app/3564090/Smoke_Break/).

I worked on this project for a little over a year, and there were a lot of people on the project, so i've added here a couple of the files for which I was the primary author.

## Smoke Render
The main thing I want to show off here is the system I wrote for rendering our player character, Applewood, who is a blob of smoke.
When we were setting out, the goal of the smoke character was for it to react with other smoke in the environment and to get bigger or smaller as a representation of the player's health. Now, to accomblish that goal, I ended up building a few seperate layers that would achive that effect.

The core of this is the Smoke shader I wrote, which takes in a list of smoke balls and renders their surface using a raymarch. Basically, you render a mesh which encompasses the potential area in which the smoke could be, and then the pixel shader discards pixels which are not a part of the surface and adjusts the normals and lighting position for those that are.

![Smoke Drag Through - High](https://github.com/user-attachments/assets/3239dfb3-7db6-4a40-8f5c-a0049178129f)

Now, the player character is made up of a collection of these balls and they can gather more from their environment. The next step was to somehow shape these balls to give the player a specific look. After a lot of iterating, I ended up with a system similar in concept to how a skinned mesh works. The balls are like the verticies of a skinned mesh and there are underlying bones which they follow. The bones follow the player's head like a snake and as more balls are added on, they fill in locations farther down the snake, making the player longer.

![Wireframe Movement](https://github.com/user-attachments/assets/3915c343-fad9-4a82-a0b5-bf19211f0e3d)
![Smoke Movement](https://github.com/user-attachments/assets/a174c1fe-3e55-4459-9016-c9a426cb0e7a)

The file `Smoke.Shader` is the well, smoke shader...  
`SmokeContainer.cs`, `PlayerSmokeController.cs`, and `SmokeBall.cs` define how the smoke date is managed.  
`SnakeFollow.cs` slides the bones around to follow the player head.

## Enemies
The enemies in the game are designed to follow some pretty standard stealth game rules, they can detect and then chase the player and will return to their idle state once they have lost track of the player. As the project went on, we kept adding more to their behaviour.  
`EnemyBase.cs` is the base behavior for all enemy types and `EnemySecurity.cs` is an example of one of those derived types. We initially wanted to have 3 enemy types in the game but scoped it down to just 2, the security (which are the bakers in the game) and the janitor. 

I wrote the initial version of those two files, with MANY things being added over time. The core of the AI is a state machine with 4 possible states, Idle, Investigate, Alert, and Post Alert. The enemies bubble up through the states in that order as they detect the player, lose track of them, and then return back to idle.
