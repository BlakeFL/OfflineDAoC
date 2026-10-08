# Human players in the host launcher

Active Population now includes real in-world characters alongside autonomous and companion bots.
The Type column distinguishes them; realm/search/online filters apply to player rows too.
The ONLINE card includes all three types. The footer separates human and bot counts, while
realm cards retain the saved bot roster count and show a combined online total.

The server appends an optional Players collection to the existing local bot-world.json snapshot.
It uses ClientService's playing clients, including GMs and LAN clients, and excludes link-dead
characters. Only character name, realm, race, gender, class, level and zone are published.
No new network endpoint or database table is required. The standalone LAN launcher is unaffected.

Player rows have no BotId, cannot be deleted or targeted by bot teleport controls, and do not
participate in bot group/objective management or bot population targets. The ordinary snapshot
refresh controls their visibility; use REFRESH after connecting or disconnecting to see changes
immediately. An older server or stale snapshot shows Players unavailable / + ? rather than
claiming that no humans are online. An authoritative empty snapshot clears previously seen players.

## Porting to later releases

The feature is confined to AutonomousBotDashboard.cs, MainForm.cs, and two test files:
UT_PlayerPopulationSnapshot.cs and PlayerPopulationTests.cs. Keep the new Players JSON field
optional to support older snapshots. Preserve the existing DashboardSnapshot constructor shape.
Upstream 0.35 includes the Sluaghbinder compile fix that was also part of the original backport.
Keep its single allowSluaghbinder declaration inside GenerateBotCharacters; do not duplicate it
when merging the population feature.

Build against each target release's source rather than deploying a newer upstream server into an
older played installation. Stop the launcher/server before deployment, back up replaced files,
and install GameServer.dll and its symbols in all three runtime/server locations. Replace the
host launcher build alongside its existing dependencies. Never replace world data or credentials.

## Verification

Launcher tests cover row mapping, bot-action protection, realm/search/online filtering, old/stale
snapshots, and removal after disconnect. Server tests cover the optional snapshot field and its
public character-only schema. Existing dashboard tests cover rolling snapshot requests.
Real two-PC checks remain necessary: start the server, log in with host and guest, press REFRESH,
check both rows/counts, log out the guest, refresh again, and confirm it disappears.
