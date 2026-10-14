# Homura-Hime

## Where is the options page?

The [player options page for this game](../player-options) contains all the options you need to configure and export a
config file.

## What is the goal?

Clear all eight chapters and finish the Finale.

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

## How do chapters unlock?

Chapter Select (pause menu) is available from the start. By default the chapters unlock in story order: a chapter opens
when the one before it is cleared. `level_order: randomized` shuffles that order (the Finale stays last), and
`level_unlock_items` makes chapters open with "Chapter N Unlock" items instead (with the vanilla order, a chapter also
needs the previous one cleared). Inside a chapter, sub-chapters open one after another. The tutorial cannot be replayed:
until it is finished Chapter Select stays closed. The tutorial's two checks (its fight and its clear reward) are locations like any other; the
**Skip Tutorial** button on the pause menu's System page (shown only until the tutorial is finished) completes both and takes you to the hub.

## What gates progress inside a chapter?

Seven key items open the way in specific areas: Pretty Little Flower (Level 1), Elevator Lever and Nurikabe's Shoe
(Level 2), Nurikabe's Hat (Level 3), Fresh Fish and Inugami Certificate (Level 4) and Nurikabe's Gloves (Level 5).
Level 1 also needs one of the four dash skills (Phantom Blade, Spark Rush, Quake Strike, Bloodflame Palm) to cross a gap;
`dash_skill_start` gives you one at the start, otherwise one is placed early. With `advanced_movement_required` the logic asks for
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

## Is DeathLink supported?

Yes (`death_link`). With `death_link_skip_revives`, reviving with a Special Hanami Dango does not send a death; it is
only sent when you pick another option in the death menu.
