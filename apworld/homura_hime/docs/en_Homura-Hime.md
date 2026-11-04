# Homura-Hime

## Where is the options page?

The [player options page for this game](../player-options) contains all the options you need to configure and export a
config file.

## What is the goal?

Clear all eight chapters and the Climax, then finish the Finale.

## What is randomized?

Every item the game hands out becomes a location, and its item is shuffled into the multiworld:

- treasure chests (including ambush chests and the Kushinada head chases) and Senbei on the maps
- journal notes
- every shop slot (combos, skills, omamori, shards, crystals, cost upgrades, the Dango)
- stage-clear rewards, Harumi's seven rewards, the tutorial reward
- the glitched-spirit trials, the tournament, the festival stall and story hand-overs (key items, dash skills)
- optionally, clearing each fight (`encounter_clears`, on by default: 110 locations) and the four optional ambushes that
  normally give nothing (`encounter_rewards`)

The pool holds the game's items (skills, combos, omamori, Blessed Shots, HP/SP shards and crystals, key items, dash
skills) plus Konpeito, Sakura Petals, Senbei, Special Hanami Dango, Full Heals and Ultimate Charges. Each item group
(combos, skills, omamori, guns, shards, upgrades, key items) can be left in its vanilla places with a `shuffle_*` option.

## Are there traps?

Yes, two:

- **Puffer Swarm Trap**: four flying bombs and four flying spiky puffers appear around you, 6 to 20 metres away
  (`puffer_swarm_traps`, 20 by default). Like the dolls, it only goes off while you stand on solid ground.
- **Hexed Doll Trio Trap**: a Hexed Doll of each of the three colours appears as a group on the ground, 4 to 8 metres
  away (`hexed_doll_trio_traps`, 5 by default).

Both traps only go off while you stand on solid ground: in the air, on a rail or in a boost they wait until you land.

A trap waits until you are in a level (never in the hub), in control (no cutscene or menu) and alive. Each option sets how many of that trap are in the
pool (0 or `disabled` for none); they take free locations like the Special Hanami Dango do, and the counts are lowered
when the locations in play have no room. The first time you connect at the title screen with a seed that has traps, the screen fades to black ("Preparing AP World Settings") while the game briefly loads
level areas to copy the enemies (no prefab of them exists outside the levels), then unloads them again.

## Can I keep some items in my own world, or send them to other worlds?

Yes, with the common `local_items` and `non_local_items` options of every Archipelago game. Besides single item names they
accept these item groups: Key Items, Dash Skills, Chapter Unlocks, Guns, Omamori, Omamori Upgrades, Combos, Skills,
Abilities, Shards And Crystals, Currency and Utility. For example, `local_items: [Combos, Skills]` keeps every combo and
skill in your own world.

## How do chapters unlock?

Chapter Select (pause menu) is available from the start. The game has nine worlds before the Finale: the eight chapters
and the Climax (Climax: The Choice), which is a world of its own. By default they unlock in story order: a world opens
when the one before it is cleared (the Climax follows Chapter 5, Chapter 6 follows the Climax). `level_order` shuffles that order (the Finale stays last; the Climax is never the first world):
- `off` (default): story order.
- `balanced`: a random order that keeps the story's pacing in mind. The first world is always one of Chapters 1-4; the
  second is one of them 66% of the time and the third 40% of the time; each of the last three worlds is one of
  Chapters 5-8 or the Climax 60% of the time; the three in the middle are whatever is left.
- `full`: every order is equally likely.

`level_unlock_items` adds keys:

- `off` (default): no keys.
- `linear_progression`: nine Progressive Level Keys (one is yours from the start). A world needs the previous one beaten
  *and* one more key than the world before it, so worlds are played strictly in order. Nothing a world needs is ever hidden
  in a world that comes after it (for example Nurikabe's Hat is never behind a later level).
- `freeform_progression`: every world has its own key ("Chapter N Unlock", "Climax Unlock") and opens as soon as you have it,
  whatever the order; you may have to jump between worlds with the Chapter Select. The order decides how the keys turn up:
  a world's key is never in that world or in one that comes later in the order (in your own world; keys that go to other
  players can be anywhere), so the keys come in the order you are meant to play. The hub's great Torii gate opens the
  Chapter Select, so you choose the world there instead of being sent to the next one of the story.

The first world's key is always yours from the start, and the Climax always needs at least one other world cleared. Inside a chapter, sub-chapters open one after another. The tutorial cannot be replayed:
until it is finished Chapter Select stays closed. The tutorial's two checks (its fight and its clear reward) are locations like any other; the
**Skip Tutorial** button on the pause menu's System page (shown only until the tutorial is finished) completes both and takes you to the hub.

## What gates progress inside a chapter?

Seven key items open the way in specific areas: Pretty Little Flower (Level 1), Elevator Lever and Nurikabe's Shoe
(Level 2), Nurikabe's Hat (Level 3), Fresh Fish and Inugami Certificate (Level 4) and Nurikabe's Gloves (Level 5).
Level 1 also needs one of the four dash skills (Phantom Blade, Spark Rush, Quake Strike, Bloodflame Palm) to cross a gap;
one of them is placed early, in your world or in another player's world. With `advanced_movement_required` the logic asks for
no dash skill at all: that gap can be crossed by parrying the laser of the enemy shooting across it, and the dash skills become
ordinary useful items.

## How do money and the shops work?

The Konpeito the game pays at its locations (chests, trials, Harumi, stage clears) is taken out and put into the pool as
Konpeito items; the hub's currency boards pay nothing. Enemies, boxes and other pickups on the maps still drop Konpeito.
`minimum_konpeito` (default 550,000) sets how much Konpeito the pool holds in total.

The logic expects a shop slot to be bought only when 1.5 times the Konpeito items you have received covers its price (the
rest comes from enemies and pickups), and only after the slot's shop tier is unlocked by cleared chapters. After the
seed is generated, the progression items sitting in shops are checked together: what the multiworld needs from your shop
by any point must be affordable by then, otherwise items are moved out of the shop. Konpeito is unlimited (you can always
collect more), so a shop never locks you out, at worst it asks for some grinding.

Some slots also cost Senbei (1 to 3, 26 in all). Senbei cannot be collected from enemies, so the logic counts exactly the
Senbei you have received, and it keeps the Senbei the shops need from being locked behind purchases that need them.
A shop never holds your own Konpeito (that would be a wasted purchase); Senbei can be for sale.

Konpeito, Sakura Petals and Senbei count for the logic but are shown to everyone as plain filler.

## How does Harumi work?

Harumi rewards you at 100, 300, 600, 1,000, 1,400, 1,800 and 2,300 Sakura Petals. The flowers on the maps give about 320
petals per chapter; the logic counts on 280 per cleared chapter plus the Sakura Petals items you have received. The pool
always holds enough Sakura Petals items to reach the last reward. Harumi's rewards are never expected before your first
chapter is cleared, however many Sakura Petals you hold.

## What does another world's item look like in Homura-Hime?

Shops list Archipelago items by name; items that are both progression and useful are shown in gold, other items in the
colour of their classification. Finding an item for another player shows a notification. With `shop_hints`, opening a
shop sends a free hint for every visible entry that holds another player's item.

## What happens when I receive an item?

A notification shows the item and who sent it, and the item is added to your inventory right away. Journal locations
still give their original journal entry as well, silently.

## How do my game saves work with the multiworld?

A game save that has been played in a multiworld remembers its slot and seed. From the main menu it can only be loaded while
you are connected to that same slot and seed (press F10 at the title screen to connect); otherwise the slot button does
nothing and a message tells you why. A save that has never been in a multiworld loads as usual, and connecting at the title
screen before you start or load a game is what lets the plugin prepare the game for your seed (for example the trap enemies).

## Is DeathLink supported?

Yes (`death_link`). With `death_link_skip_revives`, reviving with a Special Hanami Dango does not send a death; it is
only sent when you pick another option in the death menu.
