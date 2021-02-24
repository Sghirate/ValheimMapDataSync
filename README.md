# ValheimMapDataSync

A very simple plugin to make Valheim a wee bit more coop-friendly

## Prerequisites
* Valheim (obviously)
* [BepInEx 4 Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)

## Install
* Copy **ValheimMapDataSync.dll** to **[ValheimGameDir]/BepInEx/plugins/**

## Features
* **Autovisible(.cs)**: Automatically set the 'visible to other players' flag when joining a game
* **RememberIP(.cs)**: Store the IP of the last server you joined in the config. That way you don't have to re-enter it every time you join the game
* **SharedExploration(.cs)**: Also removes the fog of war around other players. For now it is only runtime and does not sync previously discovered regions between players!
* **SharedPins(.cs)**: Sends map pins to other players (via RPC). Note: Only other players using this plugin will receive the pins!