Plugin release: A connection screen styled like the game, a Server window for commands, controller support for connection UI, and fixes for reconnecting, Death Link and the death menu. See README for installation instructions.

**Update the plugin and the .apworld together.** The world only changes its version number: seeds from 0.4.1 and 0.4.2 play with this plugin without generating anything new (checked against real slot data of both), and saves and server records are not touched.

### New:

**The connection screen (F10) is a full-screen menu in the game's own style:**

- It uses the look of the title screen's Continue menu
- **Server window:** the right side shows what the server says (command results, chat, hints for you, the items you send and receive) and has a box to type into. Enter sends the text as your slot: `!hint`, `!collect`, `!getitem`, ordinary chat.
- **Keyboard:** Up/Down move through the fields and buttons, Left/Right move between the left column and the Server window (Left returns to the item you were on), Tab goes through everything, Enter does what the selected item does, Ctrl+V pastes, F11 connects, F12 disconnects, Esc closes.
- **Controller:** the D-Pad (or left stick) moves, A does what Enter does, B or Start closes. Hold **Start, Select, LB, RB, LT and RT** together to open the screen without a keyboard.
- **More readable disconnected text:** the text containing the plugin version and instructing how to open the Server Connect menu has a style background and is larger and more readable now. It returns to being small and unintrusive when connected

### Fixes:

- **Reconnecting gave the same items again.** Connecting again while playing (a manual disconnect and connect, or pressing Connect twice) put the progress stored in the game's save file back over the live progress, so items received since the last save were granted a second time. The same went for unlocked chapters, progressive keys, cleared worlds and anything set aside. The stored progress now only replaces the live one when the game loads a save or you switch to another save or slot. Locations checked while offline are still sent when you connect.
- **Death menu:** after a Death Link death the death menu could fail to appear when tabbed out and leave the game stuck. The game re-evaluates the menu when it is lost.
- **Death Link messages** no longer name the enemy that killed you; they say that you died, like the other games do, so a message sent to other players is no longer wrong about the cause.
- Connecting no longer fails because of keys the game did not receive: F11, F12 and Esc are read from the game's update, so they work with an input method or a keyboard tool running.
- Connecting twice from one key press no longer connects twice; the log says what is going on ("Connecting to ... as ...", "Connect ignored: ...", "Disconnected from Archipelago").

### Changes:

- The setup guide and READMEs describe the new connection screen and the controller combination.
- Tests for the new screen's logic (focus, controller edge detection, the console log, the death menu, the death cause) and for reading the slot data of real 0.4.1 and 0.4.2 seeds.
