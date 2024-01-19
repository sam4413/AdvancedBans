# AdvancedBans
![image](https://github.com/sam4413/AdvancedBans/assets/43707772/ffb49741-3777-453b-a454-b68d5187c6f5)

## The ban hammer is in your hands.
Tempban users, permaban users with a reason, and more!

### This plugin is still in development!
**Have a bug / suggestion? Go to the Support Server!**

https://discord.gg/MNuyaT8ErB

## About
AdvancedBans is essentially a remake on how admins can ban other players. Before, if there was a troublesome player, you would usually ban the person forever potentially loosing on a potential player. Now with AdvancedBans, you can temp-ban players, with a reason!

## Required Software
As the plugin requires a MySQL Database, a MySQL server is required.
- MySQL (Required)
- phpMyAdmin (Recommended)

I recommend installing XAMPP for these programs as it is easy to set up.

https://www.apachefriends.org/

## How it works
AdvancedBans works by storing all bans in a MySQL database. This is useful if a server operator is running multiple servers, or running a cluster of servers and wants to share their ban list amongst other servers / clusters by referencing the database.

Every time a player joins the server, it will check if their Steam / UserID is on the database. If it detects that their ban exists, and is still active, it will then display a webpage that either the server is running, or redirects to an existing web server, on which the ban reason is displayed, then removes the user.

Due to how bans are stored, it is also possible to get the history of a certain player by doing `!ab history <name | steamid>`. 

## Ban Number System
Instead of rembering the user's steamId or name, all bans are now based off a ban numbering system. The ban number is displayed once a user has been banned from the server. It is also displayed to the administrator once the user is banned.

This means that if you are banned, the player might be shown a ban screen of Ban Number 30, and why they have been banned. A administrator can then simply do !ab unban 30 to unban the player.

## Commands
`!ab help` - Help (WIP)

`!ab ban <name | steamid> "<reason>"` - Permanently bans a user from a server, with a reason.

`!ab tempban <name | steamid> "<time:m:h:d>" "<reason>"` - Temperarly bans a user from the server until the given date. 

`!ab unban <banNumber>` - Unbans a user with their given ban Number.

`!ab history <name | steamid>` - Outputs all the times the user has been banned, if their ban is expired, when their ban will expire, and if their ban is permanent. 

## Wiki is coming Soon™, in the meantime you can join the discord for help
