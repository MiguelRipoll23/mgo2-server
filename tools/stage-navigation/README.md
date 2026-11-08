# Stage navigation generator

Extracts the navigation data the gameplay server ships, so a host never needs the
client's stage files.

```sh
dotnet run --project tools/stage-navigation -- \
  "/Users/miguel/Documents/Metal Gear Online 2/game/stage" \
  .gametools/Haven/dictionary.txt \
  src/GameplayServer/Bots/Stage/Data/stage-navigation.bin
```

It reads every `<stage>/<stage>.geom` in the directory and writes one file holding,
per stage:

* the box the stage states (`PRP_STAGE_SIZE_01`/`02`, falling back to the union of
  its spatial groups);
* the placements of each mode's spawn family, resolved through Haven's
  `dictionary.txt` (`PRP_RES_TEAM_DEATHMATCH_A01`…, `PRP_TEAM_SNEAKING_*`,
  `PRP_HOMEBASE_*`, `PRP_RESCUE_*`, `PRP_BOMB_*`, `PRP_SCAP_*`), with the
  `PRP_MINI_*` minimap twins dropped;
* the centre of each cell of the stage's own spatial division, which is the graph
  a route is found over.

The file is embedded into `Mgo2Server.GameplayServer` as
`stage-navigation.bin` (`src/GameplayServer/Mgo2Server.GameplayServer.csproj`).
Run the generator again and rebuild when a stage's data is wanted; nothing else in
the server reads the client's install.

The GEOM reader is in `Stage/StageGeomFileUtils.cs`. It reads Haven's layout
(`.gametools/Haven/HavenStudio/Formats/Geo`): a 32-byte header, a chunk table, the
group run in chunk 0 and the props tree in chunk 6. It refuses a file that states
a size other than the bytes present, and bounds its effect walk so a cyclic tree
cannot hang it.
