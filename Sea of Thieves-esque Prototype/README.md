# Sea of Thieves Demo

## Water Shader
I was inspired to write a water shader after watching [this](https://www.youtube.com/watch?v=PH9q0HNBjT4) youtube video on the subject. I use a sum of sines to offset the verticies and then take the derivitive to get the normals. I wanted pixel perfect normals, so I ended up repeating the summation in the pixel shader, though this was probably not necessary. I'm not totally happy with how the light interacts with the water at the moment, but I did fade the transparency of the water based on the depth of water the camera is looking through, which really helps to sell the look of it. 

![Boat Game - Water](https://github.com/user-attachments/assets/e1c345b2-6c8c-4ac6-9486-e47f473529f4)

## Sailable Boat
Once I had the water working, I wanted to get a boat sailing around on it, and responding to the rocking of the waves. To give the boat buoyancy, I segmented the volume of the boat into voxels. Each voxel then measures how under the water it is and applies a buoyancy force based on that displacement. It took a lot of tweaking to get the boat to look and feel heavy while still being noticably effected by the waves. 

I then wrote an system of interactables, which lets the player climb onto the boat, and use the sails, wheel, and anchor. I wanted these systems to mirror how they work in Sea of Thieves. I'm quite happy with how the interactables turned out, I wanted to avoid giving an interactable direct control of the player and their inputs, since I've seen on previous projects how clunky that can be to write and debug. In this case, the PlayerMove component is constantly in control and updating. PlayerMove decides which interactable is the optimal one to select and when it cedes control to the interactable, it does so by calling `OnRecieveInput` on the Interactable every frame. This way, if I ever need to interrupt an interaction (say, if the player took damage), I can do so without needing to worry about making sure the interactable gives up control first.

## Decal Shader
Unity's built in decal shader wasn't working correctly so I made my own! I render a cube in the scene where the decal is supposed to be and then determine whether the scene geometry lies inside of that cube in the fragment shader. If it does, then I draw the decal texture over top of the terrain. I had a big breakthough moment working on this when I realized that all the math get so much easer if I use model space positions instead of world space.

![Boat Game - Decal](https://github.com/user-attachments/assets/66c4fb83-cfc6-4f64-9263-a613bcf88c3e)
