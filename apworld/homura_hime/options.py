"""Options of the Homura-Hime world, as players see them in their yaml and on the options page."""
from dataclasses import dataclass

from Options import Choice, DeathLink, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, Toggle


class LevelOrder(Choice):
    """How the eight chapters are ordered.

    vanilla: Chapters 1-8 in story order.
    randomized: the eight chapters are played in a random order (the final chapter stays last).
    In both cases the Chapter Select is always available; see the unlock rules in the setup guide."""
    display_name = "Level Order"
    option_vanilla = 0
    option_randomized = 1
    default = 0


class LevelUnlockItems(Toggle):
    """Chapters are unlocked by items ("Chapter N Unlock") that can be anywhere in the multiworld.
    With vanilla order a chapter also needs the previous chapter beaten; with randomized order the item alone is
    enough."""
    display_name = "Level Unlock Items"


class DashSkillStart(Toggle):
    """Start with one random dash skill (Phantom Blade, Spark Rush, Quake Strike or Bloodflame Palm).
    Level 1 needs one of them to cross a large gap at the start of its second area. When this is off (the default)
    the logic requires a dash skill there and one is placed early, in your world or in another player's world.
    (With advanced_movement_required the logic does not ask for a dash skill at all; this still gives you one.)"""
    display_name = "Start With A Dash Skill"
    default = 0


class AdvancedMovementRequired(Toggle):
    """The logic does not require a dash skill: it expects you to get past the gap at the start of Level 1's second
    area with advanced movement instead (parrying the laser of the enemy that shoots across it carries you over).
    On: dash skills are only useful items, none is placed early, and shops may sell them. Off (the default): the logic
    requires one of the four dash skills there and places one early."""
    display_name = "Advanced Movement Required"


class DangoCount(Range):
    """How many Special Hanami Dango are added to the item pool. The shop's Dango slot is a single location
    regardless."""
    display_name = "Special Hanami Dango In Pool"
    range_start = 0
    range_end = 99
    default = 15


class IncludeCutContent(Toggle):
    """Add content that exists in the game files but is never obtained normally: Ann's Board, Fortunate Misfortune,
    Ruptured Bones (guns) and the Flame-Stoking Explosion Charm (skill)."""
    display_name = "Include Cut Content"


class EncounterRewards(Toggle):
    """Optional Ambush fights that normally give no item (4 of them) become locations."""
    display_name = "Optional Encounter Rewards"


class EncounterClears(DefaultOnToggle):
    """Clearing any fight (every encounter, ambush, trial arena and boss phase, 110 of them) is a location."""
    display_name = "Encounter Clears"


class OptionalLocationImportance(Range):
    """How many of the optional locations (ambushes, trials, head chases, Ikai dungeon rooms) may hold progression or
    useful items, in percent. The default is all of them: the level select is always available, so optional fights can
    be replayed. Lower it to keep what you need out of optional fights; the rest then only hold filler."""
    display_name = "Important Items In Optional Locations"
    range_start = 0
    range_end = 100
    default = 100


class SenbeiCount(Range):
    """How many Senbei are handed out in total (shop items cost 1-3 each). Above 40 some of them come as "2 Senbei"
    items, so the pool does not fill up with them. The base game has 55."""
    display_name = "Senbei Total"
    range_start = 20
    range_end = 80
    default = 55


class FullHealCount(Range):
    """How many Full Heal items are in the pool."""
    display_name = "Full Heal Items"
    range_start = 0
    range_end = 50
    default = 10


class UltimateChargeCount(Range):
    """How many Ultimate Charge items are in the pool."""
    display_name = "Ultimate Charge Items"
    range_start = 0
    range_end = 50
    default = 10


class FillerSakuraRatio(Range):
    """Percent of the currency filler that is Sakura Petals instead of Konpeito. Harumi's last rewards need more Sakura
    than the logic counts on from the levels, so the pool always holds at least 60 Sakura Petals in items, whatever
    this is set to."""
    display_name = "Sakura Share Of Currency Filler"
    range_start = 0
    range_end = 100
    default = 20


class MinimumKonpeito(Range):
    """The item pool holds at least this much Konpeito in total: the payouts of chests, trials, Harumi and level clears
    (which the randomizer takes away and hands out as items) plus Konpeito filler, which grows in size when more is
    needed. The hub currency boards pay nothing; enemies, boxes and other pickups on the map still drop Konpeito on top.
    550,000 is a little more than S-ranking every mission in the base game pays; 750,000 is the in-game achievement.
    With fewer locations (encounter_clears off) the pool may not have room for high values; the most it can hold is used
    and a warning is logged."""
    display_name = "Minimum Konpeito In The Pool"
    range_start = 0
    range_end = 1000000
    default = 550000


# ---------------------------------------------------------------- shuffle groups
# A group that is not shuffled keeps its items where the base game has them (see HomuraHimeWorld._vanilla_placements).


class ShuffleCombos(DefaultOnToggle):
    """On: combos can be anywhere. Off: every combo stays at the shop slot that sells it in the base game."""
    display_name = "Shuffle Combos"


class ShuffleSkills(DefaultOnToggle):
    """On: skills can be anywhere. Off: every skill stays where the base game gives it (shop slots, stage rewards,
    Harumi)."""
    display_name = "Shuffle Skills"


class ShuffleOmamori(DefaultOnToggle):
    """On: omamori can be anywhere. Off: every omamori stays where the base game gives it (shop, chests, rewards)."""
    display_name = "Shuffle Omamori"


class ShuffleGuns(DefaultOnToggle):
    """On: blessed shots (guns) can be anywhere. Off: they stay where the base game gives them."""
    display_name = "Shuffle Guns"


class ShuffleShards(DefaultOnToggle):
    """On: HP/SP shards and crystals can be anywhere. Off: they stay in their chests, shop slots and rewards."""
    display_name = "Shuffle Shards And Crystals"


class ShuffleUpgrades(DefaultOnToggle):
    """On: the two omamori cost upgrades can be anywhere. Off: they stay at their shop slots."""
    display_name = "Shuffle Omamori Cost Upgrades"


class ShuffleKeyItems(DefaultOnToggle):
    """On: the story key items (Pretty Little Flower, Elevator Lever, Nurikabe's items, ...) can be anywhere. Off: they
    stay with the characters and events that hand them over in the base game."""
    display_name = "Shuffle Key Items"


class DeathLinkSkipRevives(Toggle):
    """Off: a death is sent as soon as you die. On: reviving with a Special Hanami Dango does not send it, the death
    is only sent when you pick any other option in the death menu."""
    display_name = "Don't Send DeathLink On Revive"


class ShopHints(DefaultOnToggle):
    """Opening a shop tells the players concerned what the visible Archipelago items in it are: every shop entry you
    can see that holds an item for another player is sent as a hint (free, no hint points). Entries the shop hides are
    not hinted."""
    display_name = "Shop Hints"


@dataclass
class HomuraHimeOptions(PerGameCommonOptions):
    level_order: LevelOrder
    level_unlock_items: LevelUnlockItems
    dash_skill_start: DashSkillStart
    dango_count: DangoCount
    include_cut_content: IncludeCutContent
    encounter_rewards: EncounterRewards
    encounter_clears: EncounterClears
    optional_location_importance: OptionalLocationImportance
    senbei_count: SenbeiCount
    full_heal_count: FullHealCount
    ultimate_charge_count: UltimateChargeCount
    filler_sakura_ratio: FillerSakuraRatio
    minimum_konpeito: MinimumKonpeito
    shuffle_combos: ShuffleCombos
    shuffle_skills: ShuffleSkills
    shuffle_omamori: ShuffleOmamori
    shuffle_guns: ShuffleGuns
    shuffle_shards: ShuffleShards
    shuffle_upgrades: ShuffleUpgrades
    shuffle_key_items: ShuffleKeyItems
    advanced_movement_required: AdvancedMovementRequired
    death_link: DeathLink
    death_link_skip_revives: DeathLinkSkipRevives
    shop_hints: ShopHints


# How the options page groups the options.
option_groups = [
    OptionGroup("Chapters", [LevelOrder, LevelUnlockItems, DashSkillStart, AdvancedMovementRequired]),
    OptionGroup("Locations", [EncounterClears, EncounterRewards, IncludeCutContent, OptionalLocationImportance]),
    OptionGroup("Shuffled Items", [ShuffleCombos, ShuffleSkills, ShuffleOmamori, ShuffleGuns, ShuffleShards,
                                   ShuffleUpgrades, ShuffleKeyItems]),
    OptionGroup("Item Pool", [MinimumKonpeito, FillerSakuraRatio, SenbeiCount, DangoCount, FullHealCount,
                              UltimateChargeCount]),
    OptionGroup("Multiworld", [ShopHints, DeathLink, DeathLinkSkipRevives]),
]
