# Edit Save Difficulty's "Change difficulty" button, in a game, opens its dialog: the mod builds a New Game panel of
# its own and presses its Customize button, which is the game's own there, not the main menu's Customize dialog (a
# player's game stopped on it). The mod and TimberUi come from the Workshop, downloaded without subscribing, into
# this run's Mods folder only, and leave it at the end
mods=$(bb_mods_dir)
workshop_get() {
    send host "workshopget $1"
    wait_for host "\[Test\] workshop $1 k_EResultOK at " 300 || return 1
    grep -aEo "\[Test\] workshop $1 k_EResultOK at .*" "$(bb_log host)" | tail -1 | sed 's/.* at //'
}
bb_close
bb_launch host || { fail "the game starts" "no main menu"; return; }
esd=$(workshop_get 3476629381) || { fail "Edit Save Difficulty downloads" "no download in the host log"; bb_close; return; }
ui=$(workshop_get 3430626620) || { fail "TimberUi downloads" "no download in the host log"; bb_close; return; }
pass "Edit Save Difficulty and TimberUi download"
bb_close
rm -rf "$mods/EditSaveDifficulty" "$mods/TimberUi"
cp -R "$esd" "$mods/EditSaveDifficulty"
cp -R "$ui" "$mods/TimberUi"
bb_launch host || { fail "the game starts with them" "no main menu"; rm -rf "$mods/EditSaveDifficulty" "$mods/TimberUi"; return; }
send host "newgame ${TE_MAP:-user:Flat256} Folktails"
wait_for host "Load time" 180 || { fail "the game loads" "never loaded"; bb_close; rm -rf "$mods/EditSaveDifficulty" "$mods/TimberUi"; return; }
sleep 3
send host dismiss; sleep 1
send host menu; sleep 1
send host "clicktext Change difficulty"; sleep 3
shot change-difficulty
expect_no_log "the main menu's Customize dialog stays out of the game" host "\[GameSettings\] Customize dialog open"
expect_no_log "the game doesn't stop" host "First uncaught exception"
expect_no_errors "no errors"
bb_close
rm -rf "$mods/EditSaveDifficulty" "$mods/TimberUi"
