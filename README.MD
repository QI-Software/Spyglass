# Spyglass
A flexible Discord moderation bot for large communities, written for the Dead Matter community server.

## Requirements

- .NET 5 or higher
- PostgreSQL server with a user account for the bot.
- Discord [bot account](https://discord.com/developers/)

Once your bot account is created, you can use the following link to add it to your servers:
```
https://discord.com/oauth2/authorize?client_id=INSERT_CLIENT_ID_HERE&scope=bot&20applications.commands&permissions=1006890231
```

Do keep in mind the scopes are required for the slash commands to function properly.

## Installation

Run the following command in the root directory of the project:
```c
# dotnet publish -p:PublishProfile=FolderProfile -c Release
```

This will compile the bot and publish it to the `Spyglass/bin/Release/netX.X/publish` folder.
Copy the **entirety** of the folder and place it wherever you want your bot to run.

## Setting environment variables
Currently, the only environment variable required is the database connection string:
```bash
SPYGLASS_DBCONNSTRING="Username=username;Password=password;Host=localhost;Port=5432;Database=spyglass;"
```
Please refer to your OS documentation in order to set it.

## Starting the bot
For Windows systems, you can simply run the `.exe`
```
Spyglass.exe configs/
```

For Unix systems, you will need to run the `.dll` using the dotnet runtime you installed in the first step:
```
dotnet exec Spyglass.exe configs/
```

The first argument is the only required one, and is the path in which the configuration file will be created.
On first startup, you will be asked if you want to create the config file. Type in `y` to generate one. 
Open the generated file and set the `Token` to your bot's token.

Everything else can be configured using the bot itself, so I recommend leave to it as is.

## Why don't I see any commands?

Slash commands will take upwards of one hour after the first successful startup of the bot to appear. 
This is due to Discord's caching and I *technically* can't do anything about it if I want commands to stay global.

## Database setup
The bot only requires a user account, the ability to connect to the database server and the permission to create a database. On startup, it will create and update the database as it sees fit using EFCore's migration system. 

No additional setup is required.
