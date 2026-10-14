"""
What a seed makes everybody buy in the shops must be affordable by the time it is needed.

Konpeito is unlimited in the game (a player who spends it on the wrong things just has to collect more), so the only
question is whether the purchases the multiworld needs can be paid for in time. The players are assumed to buy exactly
those: the progression items sitting in shop slots, and only the ones that are required by that point. If player 1's
shop holds A, B, C and D, and player 2 needs A and B (but not yet C and D) once player 1 has been paid 20,000 Konpeito
(30,000 money), then A and B together must cost at most 30,000; C and D only count from the moment something needs them.
The same goes for the Senbei some slots cost on top: they cannot be farmed, so what the needed purchases cost in Senbei
must not exceed the Senbei received by then either.

The access rules only know the price of each slot on its own, so this is worked out on the finished placement, in
finalize_multiworld (after progression balancing, which can still move items):
  1. The seed is played sphere by sphere by a player who buys only the progression in shop slots (what else is in a shop
     slot is left alone). The money and the Senbei before every sphere are recorded.
  2. Cheap check: an item cannot be needed before the sphere after its own, so if everything bought is affordable from
     that sphere on, the seed is fine (most seeds stop here, after one playthrough).
  3. Otherwise each shop item is taken out in turn and the seed replayed from the item's own sphere until the first
     sphere that is not completed (or the last sphere where the cheap check failed): that sphere needs the item, which
     has to be bought before it.
  4. For every sphere, the items needed by then must not cost more than the money (or the Senbei) before it.
When that does not hold, the dearest item needed by then (in the resource that is short) is swapped with a plain
filler or useful item that sits outside the shops in a location reachable no later than the shop slot itself (so the
location cannot depend on the item it receives, and where both items are allowed), and the playthrough is resumed
from before that location's sphere.

Cost, for multiworlds of thousands of players: one playthrough checks every location once per sphere, like AP's own
spoiler playthrough. A replay only checks the locations of each original sphere once (without the item, the state is a
subset of the original one, so a sphere is either reached in full or the item is needed there). The state is only copied
before the spheres that hold a purchase (where replays start), and a repair resumes the playthrough instead of starting
again.
"""
import logging
from typing import Dict, List, Optional, Sequence, Tuple

from BaseClasses import CollectionState, Item, Location, MultiWorld

from .game_data import GAME_NAME, KONPEITO_VALUES

# The most items moved to repair one multiworld; a seed that still breaks the budget after that is kept with a warning.
MAX_SWAPS = 60


def needed(item: Item) -> bool:
    """
    An item somebody may need to progress, so it is bought when it sits in a shop slot. That is any advancement item
    (progression_skip_balancing ones of other games too, e.g. collectibles counted towards a goal) except Homura-Hime's
    Konpeito, which is progression only to count as money.
    """
    return item.advancement and not (item.game == GAME_NAME and item.name in KONPEITO_VALUES)


class Playthrough:
    """
    The seed played sphere by sphere by a player who buys only the progression in shop slots: the sphere of every
    location, the money of each Homura-Hime player before every sphere, and the state before every sphere that holds a
    purchase (to replay from there).
    """

    def __init__(self, multiworld: MultiWorld, worlds: Sequence):
        # worlds: the Homura-Hime worlds of the multiworld (their shop slots are the ones that cost money)
        self.multiworld = multiworld
        self.worlds = worlds
        self.shop_slots = {slot for world in worlds for slot in world.shop_slots}
        self.sphere_of: Dict[Location, int] = {}
        self.spheres: List[List[Location]] = []
        self.states: Dict[int, CollectionState] = {}
        self.money: Dict[int, List[int]] = {world.player: [] for world in worlds}
        self.senbei: Dict[int, List[int]] = {world.player: [] for world in worlds}
        self._play(CollectionState(multiworld), list(multiworld.get_locations()))

    def _play(self, state: CollectionState, remaining: List[Location]) -> None:
        """Plays sphere by sphere from state until no more locations can be reached, recording what each sphere holds."""
        while remaining:
            reachable = [location for location in remaining if location.can_reach(state)]
            if not reachable:
                break
            sphere = len(self.spheres)
            if any(self.bought(location) for location in reachable):
                self.states[sphere] = state.copy()
            for world in self.worlds:
                self.money[world.player].append(world._money(state))
                self.senbei[world.player].append(world._senbei(state))
            reached = set(reachable)
            remaining = [location for location in remaining if location not in reached]
            for location in reachable:
                self.sphere_of[location] = sphere
                self.collect(state, location)
            self.spheres.append(reachable)

    def resume(self, sphere: int) -> None:
        """Plays again from before this sphere, after the items of this or later spheres changed."""
        start = max((saved for saved in self.states if saved <= sphere), default=0)
        state = self.states[start].copy() if start in self.states else CollectionState(self.multiworld)
        for dropped in self.spheres[start:]:
            for location in dropped:
                del self.sphere_of[location]
        remaining = [location for location in self.multiworld.get_locations() if location not in self.sphere_of]
        del self.spheres[start:]
        self.states = {saved: kept for saved, kept in self.states.items() if saved < start}
        for resource in (self.money, self.senbei):
            for amounts in resource.values():
                del amounts[start:]
        self._play(state, remaining)

    def bought(self, location: Location) -> bool:
        """Whether the player buys this location: a shop slot that holds an item somebody needs."""
        return location in self.shop_slots and location.item is not None and needed(location.item)

    def collect(self, state: CollectionState, location: Location, removed: Optional[Item] = None) -> None:
        """Adds the item of a location to the state, as the player would have it (emoved is pretended not to exist)."""
        item = location.item
        if item is None or item is removed or (location in self.shop_slots and not needed(item)):
            return  # nothing there, the item taken out, or a shop item nobody needs (not bought)
        state.collect(item, True, location)

    def first_delay(self, slot: Location, until: int) -> Optional[int]:
        """
        The first sphere that cannot be completed without the item in this slot, or None if there is none up to
        `until`. Nothing changes before the slot's own sphere, so the replay starts from the state saved there.
        """
        removed = slot.item
        start = self.sphere_of[slot]
        state = self.states[start].copy()
        for sphere in range(start, min(until + 1, len(self.spheres))):
            locations = self.spheres[sphere]
            if not all(location.can_reach(state) for location in locations):
                return sphere
            for location in locations:
                self.collect(state, location, removed)
        return None


def violations(required: Dict[Location, int], money: List[int],
               senbei: Optional[List[int]] = None) -> List[Tuple[int, List[Location], str]]:
    """
    Every sphere whose required purchases cost more than the money (or, if given, the Senbei) before it: the sphere,
    those purchases and the resource that is short ("konpeito" or "senbei").
    """
    found = []
    for sphere in sorted(set(required.values())):
        due = [slot for slot, by in required.items() if by <= sphere]
        if sum(slot.price for slot in due) > money[sphere]:
            found.append((sphere, due, "konpeito"))
        elif senbei is not None and sum(slot.senbei for slot in due) > senbei[sphere]:
            found.append((sphere, due, "senbei"))
    return found


def first_violation(required: Dict[Location, int], money: List[int],
                    senbei: Optional[List[int]] = None) -> Optional[Tuple[int, List[Location]]]:
    """The first sphere whose required purchases cost more than the money before it, with those purchases."""
    found = violations(required, money, senbei)
    return found[0][:2] if found else None


class Violation:
    """A world whose shops ask for more than the money or the Senbei before a sphere."""

    def __init__(self, world, sphere: int, due: List[Location], resource: str, available: int):
        self.world, self.sphere, self.due, self.resource, self.available = world, sphere, due, resource, available


def check(run: Playthrough) -> Optional[Violation]:
    """The first world that breaks the budget (in Konpeito or in Senbei), or None."""
    for world in run.worlds:
        # the shop slots of this world whose item is bought by the players
        bought = [slot for slot in world.shop_slots if slot in run.sphere_of and run.bought(slot)]
        if not bought:
            continue
        money, senbei = run.money[world.player], run.senbei[world.player]
        # Cheap first: an item cannot be needed before the sphere after its own, so counting every purchase from there
        # on asks for at least as much as the exact answer. Only the spheres where that fails can fail at all, so only
        # up to the last of them is it worked out which purchases are really due.
        cheap = violations({slot: run.sphere_of[slot] + 1 for slot in bought}, money + [money[-1]],
                           senbei + [senbei[-1]])
        if not cheap:
            continue
        until = cheap[-1][0]
        required = {slot: by for slot in bought if (by := run.first_delay(slot, until)) is not None}
        found = violations(required, money, senbei)
        if found:
            sphere, due, resource = found[0]
            return Violation(world, sphere, due, resource, (money if resource == "konpeito" else senbei)[sphere])
    return None


def swap(a: Location, b: Location) -> None:
    """Exchanges the items of two locations."""
    a.item, b.item = b.item, a.item
    a.item.location, b.item.location = a, b


def repair(multiworld: MultiWorld, run: Playthrough, violation: Violation) -> Optional[int]:
    """
    Moves the dearest purchase due (in the resource that is short) out of its shop slot. Returns the sphere of the
    location it went to (the playthrough is unchanged before it), or None when no location can take it.
    """
    short = "senbei" if violation.resource == "senbei" else "price"
    slot = max(violation.due, key=lambda s: (getattr(s, short), s.price, s.name))
    # reachable no later than the slot itself, so the location cannot be behind the item it receives
    latest_allowed = run.sphere_of[slot]
    state = CollectionState(multiworld)
    candidates = [
        location for sphere in run.spheres[:latest_allowed + 1] for location in sphere
        if location is not slot and location not in run.shop_slots
        and location.item is not None and not location.item.advancement and location.address is not None
        and not location.locked
        and location.can_fill(state, slot.item, False) and slot.can_fill(state, location.item, False)
    ]
    if not candidates:
        return None
    # the latest such location keeps the early ones free
    latest = max(run.sphere_of[location] for location in candidates)
    swap(slot, violation.world.random.choice([location for location in candidates
                                              if run.sphere_of[location] == latest]))
    return latest


def enforce(multiworld: MultiWorld, worlds: Sequence) -> None:
    """
    Checks the placement and repairs it. A seed that cannot be repaired is kept and the problem is logged: Konpeito is
    unlimited in the game (enemies and map pickups keep paying), so the seed stays beatable, it only asks for more
    grinding than the logic intends. Failing the whole multiworld for that would be worse.
    """
    run = Playthrough(multiworld, worlds)
    for _ in range(MAX_SWAPS):
        violation = check(run)
        if violation is None:
            return
        changed = repair(multiworld, run, violation)
        if changed is None:
            cost = sum(s.senbei if violation.resource == "senbei" else s.price for s in violation.due)
            logging.warning("Homura-Hime (%s): the shops need %d %s by sphere %d but only %d is available by then",
                            violation.world.player_name, cost, violation.resource, violation.sphere,
                            violation.available)
            return
        run.resume(changed)
    logging.warning("Homura-Hime: the shop purchases still exceed the Konpeito after %d swaps", MAX_SWAPS)
