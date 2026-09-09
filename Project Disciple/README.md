# Project Disciple
This was a game project that I worked on for [USC's GLO class](https://www.learnglo.com/class) (Games as Live Ops). The idea was that we would build the foundation for a live-service mobile game which students in a future semester will add extra content onto. I worked as a contractor and ended up taking on most of the programming for the base building side of the game. This project is still in development, although everyone on the project has moved from being paid contractors to volunteers; so while I'm still a bit involved, I consider my time working there as complete.

Almost all of our code was done in Unreal Blueprints instead of C++ (I joined the project _after_ that decision was made), so I can't really show off much of the code.
I worked on this project for just over 4 months, and will talk about some of the stuff I did below.

## UI
We had a UI designer who created Figma drawings for each menu, and one of my jobs was to import them into the engine. I learned a lot about the UMG editor doing this. 

Since our game would be running on phones, it was important that we kept texture sizes small. To this end, I requested that most of the menu and button backgrounds were to be 9-sliced. I also thought that is was important that we kept consistent font sizes between menus and avoided any text scaling to keep the text looking crisp, so I created some common font styles and made sure that the menus avoided any form of scaling high up in the widget hierarchy.

I noticed that Unreal's built in WrapBox container would fail to properly create space around each child widget, and refused to keep each element in the box the exact same size. To fix this, I created my own box widget class in C++, which is basically just a vertical box full of horizontal boxes.

## Quests and Unlocks
We needed some sort of general system for detecting and saving the state of goals that the player would be required to complete. The tutorial, which I was building at the time, had a long list of back to back things the player must do, many of which were quite niche and we definetly weren't keeping track of. For example, we needed to detect when the player dropped a character in a specific building, or completed an attack on a specific enemy base. These all needed to connect up to one central system that could save the state of these interactions and trigger unlocks based on the players progress.

Importantly, we also wanted these unlocks to be easily tunable by designers, so they wouldn't need to go change the code if they wanted something to unlock based on a completely different thing happen. This meant that just hard coding a bunch of places which call functions on specific other bits of code was out of the question. I ended up building out our unlock system and quest system to accomplish these goals. 

I made a singleton containing a set of `FNames` (Unreal's term for string IDs) which acted as both locks and keys for various systems. For example, if a designer wanted a certain skill to only be researchable after reaching Reputation level 5, they could input `Reputation_5` as the lock in the skills data table. Then, if they want to change that skill to instead unlock after completing the quest `Tutorial_Stage5` instead, they could just change that string to `Completed_Tutorial_Stage5`. We kept a big list of all the available things that could be used as unlocks, which was kept _mostly_ up to date.

This tied in well with the quest system, which used these same unlocks to keep track of the players progress towards goals. You can see the quests on the right side of the screen in the video below.

## NPCs and Base Building
I was the primary engineer everything going on in the player's base. This includes the placeable buildings and their menus, as well as the NPCs walking around the base. Since this is intended to be a phone game, everything is based on clicking and dragging on things. The NPCs can be picked up and dropped on buildings to assign them jobs, which they work on during the day time. At night they sleep, and if they don't have a place to sleep they end up sleeping on the ground. They each have a loyalty, and if you don't let them eat or sleep comfortably, they may end up leaving the base. This was the first time that I had used behavior trees to do ai and there were some hardships in figuring things out. 

The top level of the behavior tree ended up looking a lot like a state machine, with the NPCs switching between different sub-behavior trees depending on if they were working, sleeping, or eating. I ended up using BTS nodes to handle states like "inside building". Since they were they only way I could find to guarantee that both the enter and exit logic would trigger, even if the behavior tree was fully paused or restarted.

##

Here is a video of me playing through what we ended up with.

[![Video thumbnail](https://img.youtube.com/vi/7Y026xAcld8/0.jpg)](https://www.youtube.com/watch?v=7Y026xAcld8)
