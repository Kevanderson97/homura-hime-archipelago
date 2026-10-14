# Testing

Three layers, all started by `tools/run_tests.ps1` (`-SkipGame` leaves out the in-game layer):

| Layer | Where | Needs |
|-------|-------|-------|
| World logic (pytest) | `apworld/homura_hime/test` | an Archipelago 0.6.x source checkout + its Python environment |
| Plugin logic (xUnit) | `plugin.tests` | .NET SDK and the game's assemblies (for compiling only) |
| In-game (Surity) | `plugin.gametests` | the game with BepInEx 5, a free port 38281, the Archipelago checkout |

## World tests

Link the world into an Archipelago checkout, then run pytest from the checkout:

```
New-Item -ItemType Junction -Path <Archipelago>\worlds\homura_hime -Target <repo>\apworld\homura_hime
cd <Archipelago>
python -m pytest worlds/homura_hime -q
```

`test_generation.py` generates a world per option set, `test_rules.py` checks the access logic, `test_shop_budget.py`
the post-fill shop affordability check and `test_sample_yaml.py` that `apworld/sample/Homura-Hime.yaml` is current.

## Plugin tests

`dotnet test plugin.tests`. They run without the game: the plugin code that touches Unity is kept behind small
seams (for example `TickGuard`, `SessionState.Clock`) that the tests replace. `SourceLintTests` scans the plugin
source for call patterns that crashed the game in the past.

## In-game tests

`plugin.gametests` is a second BepInEx plugin that Surity (a test runner for Unity games) starts inside a headless game
instance. The tests connect to a real Archipelago server and check the hooks against the installed game build, which
also shows when a game update has moved something the plugin patches.

```
$env:ARCHIPELAGO_DIR    = "<Archipelago source folder>"
$env:ARCHIPELAGO_PYTHON = "<its python.exe>"
tools\run_tests.ps1
```

The script builds and installs the plugin and the tests, generates the two-player seed in `tools/gametest_seed`
(a Homura-Hime slot "HimeTest" and an APQuest slot "Quester"; `tools/serve_test_seed.py`), serves it on
`localhost:38281`, runs the game headless and removes the test plugins afterwards. Close the game first. Failures are
listed with their messages; the script exits non-zero if any layer failed.
