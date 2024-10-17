using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
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
        public override string ModuleVersion => "1.1";

        public enum SoundMode : long
        {
            M_NORMAL = 0,
            M_STOP = 1,
            M_SILENCER = 2
        }

        public Dictionary<CCSPlayerController, SoundMode> ClientSoundList = new Dictionary<CCSPlayerController, SoundMode>();

        public SqliteConnection? Connection = null;

        public override void Load(bool hotReload)
        {
            HookUserMessage(452, Hook_WeaponFiring, HookMode.Pre);
            HookUserMessage(417, Hook_WeaponMuzzle, HookMode.Pre);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<OnClientPutInServer>(OnClientPutInServerHook);
            RegisterListener<OnClientDisconnect>(OnClientDisconnectHook);

            LoadDatabase();
        }

        private async void LoadDatabase()
        {
            Connection = new SqliteConnection($"Data Source={Path.Join(ModuleDirectory, "stopsound.db")}");
            Connection.Open();

            await Connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS stopsound (player_auth VARCHAR(64) PRIMARY KEY, sound_mode INT);");
        }

        private void OnClientPutInServerHook(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            ClientSoundList.Add(client, SoundMode.M_NORMAL);

            return;
        }

        private void OnClientDisconnectHook(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            ClientSoundList.Remove(client);
        }

        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (client == null || client.IsBot || client.IsHLTV)
                return HookResult.Continue;

            if (client.AuthorizedSteamID == null)
                return HookResult.Continue;

            GetPlayerSoundMode(client);

            return HookResult.Continue;
        }

        private async void GetPlayerSoundMode(CCSPlayerController client)
        {
            if (client == null) return;

            var query = "SELECT sound_mode FROM stopsound WHERE player_auth = @Auth;";

            var param = new
            {
                Auth = client.AuthorizedSteamID!.SteamId3
            };

            var result = await Connection!.ExecuteReaderAsync(query, param);

            if (result == null) return;

            if (result.HasRows)
            {
                result.Read();
                ClientSoundList[client] = (SoundMode)(long)result["sound_mode"];
            }
            else
                InsertPlayerData(client);
        }

        private async void InsertPlayerData(CCSPlayerController client, SoundMode mode = SoundMode.M_NORMAL)
        {
            var query = "INSERT INTO stopsound (player_auth, sound_mode) VALUES(@Auth, @Mode) ON CONFLICT(player_auth) DO UPDATE SET sound_mode = @Mode;";
            var param = new
            {
                Auth = client.AuthorizedSteamID!.SteamId3,
                Mode = (long)mode
            };

            await Connection!.ExecuteAsync(query, param);
        }

        [ConsoleCommand("css_stopsound")]
        [CommandHelper(1, "css_stopsound <0-2>", CommandUsage.CLIENT_ONLY)]
        public void StopSoundCommand(CCSPlayerController client, CommandInfo info)
        {
            var arg = info.GetArg(1);

            if (int.Parse(arg) < 0 || int.Parse(arg) > 3)
            {
                info.ReplyToCommand($"{ChatColors.Green}[Stopsound]{ChatColors.White} You can't set number more than 3 or less than 0!");
                return;
            }

            var mode = (SoundMode)long.Parse(arg);
            SetStopSoundStatus(client, mode, true);
        }

        public HookResult Hook_WeaponFiring(UserMessage userMessage)
        {
            var weaponid = userMessage.ReadUInt("weapon_id");
            var soundType = userMessage.ReadInt("sound_type");
            var itemdefindex = userMessage.ReadUInt("item_def_index");

            userMessage.SetUInt("weapon_id", 0);
            userMessage.SetInt("sound_type", 9);
            userMessage.SetUInt("item_def_index", 61);

            // send for people who use silencer.
            userMessage.Recipients = GetRecipientFromMode(SoundMode.M_SILENCER);
            userMessage.Send();

            userMessage.SetUInt("weapon_id", weaponid);
            userMessage.SetInt("sound_type", soundType);
            userMessage.SetUInt("item_def_index", itemdefindex);

            userMessage.Recipients = GetRecipientFromMode(SoundMode.M_NORMAL);

            return HookResult.Continue;
        }

        public HookResult Hook_WeaponMuzzle(UserMessage userMessage)
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

            if(database)
                InsertPlayerData(client, mode);

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

                case SoundMode.M_SILENCER:
                    return "Silencer sound";

                default:
                    return "Invalid";
            }
        }
    }
}
