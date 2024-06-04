# ModShardDiff

A cli tool to export differences between two .win files from the same GameMaker-based game. 

> [!NOTE]  
> This tool relies heavily on [UTMT](https://github.com/UnderminersTeam/UndertaleModTool).

## What will I get from this tool ?

MSD will export in seperate files the following:
- list of added and removed `Codes` in txt
- list of added and removed `GameObjects` in txt
- list of added and removed `Rooms` in txt
- list of added and removed `Sounds` in txt
- list of added and removed `Sprites` in txt
- list of added and removed `TexturePageItems` in txt
- added `Codes` in gml
- added `GameObjects` in json
- added `Sprites` in png
- modified diff `Codes` in html
- modified diff `GameObjects` in html
- modified `Sprites` in png
- list of modified `Sprites origin` in txt

The diff for `Codes` and `GameObjects` is made using the `diff-match-patch` lib, and the html export relies on this same [api](https://github.com/google/diff-match-patch/wiki/API#diff_prettyhtmldiffs--html).

## How can I install it?

1. Install the latest [.NET Core SDK](https://dot.net).
2. Run `dotnet tool install --global ModShardDiff`.

## How can I use it?

#### Using the CLI:

To compare `data_modified.win` from `data_vanilla.win` and export the results in the folder `PATH/TO/EXPORT`, run `msd -n data_modified.win -r data_vanilla.win -o PATH/TO/EXPORT`.

## Known issues

This tool assumes that each element (`Codes`, `GameObjects`, `Sprites`, ...) is named with a unique name. If for any reasons, some elements share the same name (for instance two `Codes` using the same name), the tool will export only one (and maybe the one you are not interested in).