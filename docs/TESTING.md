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
`test_presets.py` checks the option presets of the options page (and generates them with many random rolls),
`test_package.py` the package rules of Archipelago's "adding games" and APWorld documents, and `test_local_items.py`
the `local_items` / `non_local_items` lists through Archipelago's real generator.

### Style

The Python of the world follows Archipelago's style guide (PEP8, 120 columns, double quotes, new-style type annotations).
`ruff.toml` in the repository root is Archipelago's own lint configuration; `uvx ruff check` (or `ruff check`) has to
report nothing for `apworld/` and the packaging scripts in `tools/` (the scripts that read the game's files are left
out). Markdown is wrapped at 120 columns as well.

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
