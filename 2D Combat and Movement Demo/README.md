# 2D Combat / Platformer
This is a personal project Ive spent a couple weeks working on. I was inspired to make this after playing Silksong! I'm quite happy with how the player movement and combat code turned out. I think that this would be fairly easy to scale up into more complex enemy behavior.

I experimented with using coroutines to handle certain states that the player and enemies can get into. Stuns and some other attack states just disable the relevant components, run a coroutine, and then reenable the components when its done.

The next step for this project will be to add some sprites on the enemies and add a more complex enemy, like maybe a boss.
