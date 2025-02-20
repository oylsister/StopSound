using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using static CounterStrikeSharp.API.Core.Listeners;

namespace StopSound
{
    public class StopSound : BasePlugin
    {
        public override string ModuleName => "Stop Weapon Sound";
        public override string ModuleAuthor => "Oylsister";
        public override string ModuleDescription => "Prevent client to hear a noise sound from firing weapon";
        public override string ModuleVersion => "1.3";

        public enum SoundMode : long
        {
            M_NORMAL = 0,
            M_STOP = 1
        }

        public Dictionary<CCSPlayerController, SoundMode> ClientSoundList = new Dictionary<CCSPlayerController, SoundMode>();
        public SqliteConnection? Connection = null;

        public override void Load(bool hotReload)
        {
            HookUserMessage(452, Hook_WeaponFiring, HookMode.Pre);

            RegisterListener<OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);

            LoadDatabase().Wait();
        }

        private async Task LoadDatabase()
        {
            Connection = new SqliteConnection($"Data Source={Path.Join(ModuleDirectory, "stopsound.db")}");
            Connection.Open();

            await Connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS stopsound (player_auth VARCHAR(64) PRIMARY KEY, sound_mode INT);");
        }

        private void OnClientPutInServer(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            if (client.IsBot)
                return;

            ClientSoundList.Add(client, SoundMode.M_NORMAL);

            var steamid = client.AuthorizedSteamID?.SteamId3;

            if(steamid == null)
            {
                return;
            }

            Task.Run(async () => await GetPlayerSoundMode(client, steamid));
        }

        private void OnClientDisconnect(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            if (client.IsBot)
                return;

            ClientSoundList.Remove(client);
        }

        private async Task GetPlayerSoundMode(CCSPlayerController client, string steamid)
        {
            if (client == null) return;

            var query = "SELECT sound_mode FROM stopsound WHERE player_auth = @Auth;";

            var param = new
            {
                Auth = steamid
            };

            if(Connection == null) return;

            var result = await Connection.ExecuteReaderAsync(query, param);

            if (result == null) return;

            if (await result.ReadAsync())
            {
                var mode = result["sound_mode"];

                if((long)mode == 2)
                    mode = 0;

                ClientSoundList[client] = (SoundMode)mode;
            }

            else
                await InsertPlayerData(client, steamid);
        }

        private async Task InsertPlayerData(CCSPlayerController client, string steamid, SoundMode mode = SoundMode.M_NORMAL)
        {
            var query = "INSERT INTO stopsound (player_auth, sound_mode) VALUES(@Auth, @Mode) ON CONFLICT(player_auth) DO UPDATE SET sound_mode = @Mode;";
            var param = new
            {
                Auth = steamid,
                Mode = (long)mode
            };

            if (Connection == null) return;

            await Connection.ExecuteAsync(query, param);
        }

        [ConsoleCommand("css_stopsound")]
        [CommandHelper(0, "css_stopsound <0-2>", CommandUsage.CLIENT_ONLY)]
        public void StopSoundCommand(CCSPlayerController client, CommandInfo info)
        {
            if(info.ArgCount < 2)
            {
                // set to normal first;
                SoundMode change = SoundMode.M_NORMAL;

                // if player already have normal mode, then switch to stop.
                if(ClientSoundList[client] == SoundMode.M_NORMAL)
                    change = SoundMode.M_STOP;

                SetStopSoundStatus(client, change, true);
                return;
            }

            var arg = info.GetArg(1);

            if (int.Parse(arg) < 0 || int.Parse(arg) > 2)
            {
                info.ReplyToCommand($" {ChatColors.Green}[Stopsound]{ChatColors.White} Usage: css_stopsound <0-1>");
                return;
            }

            var mode = (SoundMode)long.Parse(arg);
            SetStopSoundStatus(client, mode, true);
        }

        public HookResult Hook_WeaponFiring(UserMessage userMessage)
        {
            userMessage.Recipients = GetRecipientFromMode(SoundMode.M_NORMAL);
            return HookResult.Continue;
        }

        void SetStopSoundStatus(CCSPlayerController client, SoundMode mode, bool database = false)
        {
            if(client == null) return;

            if(!ClientSoundList.ContainsKey(client))
                ClientSoundList.Add(client, mode);

            ClientSoundList[client] = mode;

            if (database)
            {
                var steamid = client.AuthorizedSteamID?.SteamId3;

                if(steamid != null) 
                    Task.Run(async () => await InsertPlayerData(client, steamid, mode));
            }

            client.PrintToChat($" {ChatColors.Green}[Stopsound]{ChatColors.White} You set to {ChatColors.Olive}{GetModeString(mode)}{ChatColors.White}.");
        }

        RecipientFilter GetRecipientFromMode(SoundMode mode)
        {
            var recipientfilter = new RecipientFilter();

            foreach(var client in ClientSoundList)
            {
                if(client.Value == mode)
                {
                    recipientfilter.Add(client.Key);
                }
            }

            return recipientfilter;
        }

        string GetModeString(SoundMode mode)
        {
            switch (mode)
            {
                case SoundMode.M_NORMAL:
                    return "Enable Sound";

                case SoundMode.M_STOP:
                    return "No weapon sound";

                default:
                    return "Invalid";
            }
        }
    }
}
