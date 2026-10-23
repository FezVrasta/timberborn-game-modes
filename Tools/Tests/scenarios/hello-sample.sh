# The Hello Mode sample (Samples/HelloMode) shows what a mod needs to add a game mode: a game started in its
# mode loads behind the mode's picture and reads the mode's setting from the game's record. Needs the sample
# built; one that isn't installed is built for the run and taken out again.

sample="$HOME/Documents/Timberborn/Mods/HelloMode"
installed=1
[ -f "$sample/version-1.1/HelloMode.dll" ] || {
    installed=0
    (cd "$BB_SCENARIO_DIR/../../../Samples/HelloMode" && dotnet build -c Release -nologo -v q 2>&1 | tail -1) >/dev/null
}
[ -f "$sample/version-1.1/HelloMode.dll" ] || { fail "the sample is built" "no HelloMode in the Mods folder"; return; }

bb_close
bb_launch host || { fail "the game starts" "no main menu"; return; }
send host "gamesetting hellomode.greeting hey"
send host "newgame ${TE_MAP:-user:Flat256} Folktails hellomode.hello"
wait_for host "Load time" 180 || { fail "the Hello game loads" "never loaded"; return; }
sleep 8
expect_log "the loading screen has the mode's picture" host "\[GameModes\] Loading screen: the picture of hellomode.hello"
expect_log "the game starts in the mode with the picked setting" host "\[GameSettings\] Starting a game with settings: .*hellomode.greeting=hey"
expect_log "the sample greets with the setting from the record" host "\[HelloMode\] hey, beavers!"
expect_no_errors "no errors"
bb_close
[ "$installed" = 1 ] || rm -rf "$sample"
