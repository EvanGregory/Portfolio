# Game Engine
This project was the primary focus of my favorite class at USC, ITP-485. Over the course of the semester, we each built a game engine from scratch. I coded it in C++ and used DX11 for the rendering. I am very proud with how my version of the engine turned out. Sadly, since I don't want any future students to be able to cheat off of my work, I cannot post any of the code here, though I do have a private GitHub repo I would love to share with anyone interested! 

There were many different things that I added to the engine, including an animation system, a multithreaded jobs system, text renderer, some post-processes, some simple collision detection, and a profiler. I won't be going into detail about everything here, but it culminates with this clip of a skinned mesh character running and jumping around a simple world.

https://github.com/user-attachments/assets/1ecf014e-9fca-4faf-85b5-2d324f571edc

https://github.com/user-attachments/assets/26b194c1-cd00-4f4d-bb00-ae7ac2924192

## Shell Texture
One extra thing that I added was a shell texturing shader. This uses the geometry shader to extrude out multiple layers of mesh and then discards through them in the fragment shader according to some noise value.

<img width="2880" height="1800" alt="Game Engine - Static Shell Texture" src="https://github.com/user-attachments/assets/9a4fff69-54fa-4207-bcc4-f5a5394d0e16" />

![Game Engine - Spinning Shell Texture](https://github.com/user-attachments/assets/228c60ba-feef-4099-900d-a4f0b62a6f19)
