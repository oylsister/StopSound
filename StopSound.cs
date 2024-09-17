using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using static CounterStrikeSharp.API.Core.Listeners;

namespace StopSound
{
    public class StopSound : BasePlugin
    {
        public override string ModuleName => "Stop Weapon Sound";
        public override string ModuleAuthor => "Oylsister";
        public override string ModuleDescription => "Prevent client to hear a noise sound from firing weapon";
        public override string ModuleVersion => "Alpha 1.0";

        public enum SoundMode : int
        {
            M_NORMAL = 0,
            M_STOP = 1,
        }

        public Dictionary<CCSPlayerController, SoundMode> ClientSoundList = new Dictionary<CCSPlayerController, SoundMode>();
        public RecipientFilter NormalFilter = new RecipientFilter();

        public override void Load(bool hotReload)
        {
            HookUserMessage(452, Hook_WeaponFiring, HookMode.Pre);

            RegisterListener<OnClientPutInServer>(OnClientPutInServerHook);
            RegisterListener<OnClientDisconnect>(OnClientDisconnectHook);

            NormalFilter = new RecipientFilter();
        }

        private void OnClientPutInServerHook(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            ClientSoundList.Add(client, SoundMode.M_NORMAL);
            SetStopSoundStatus(client, SoundMode.M_NORMAL);
            NormalFilter.Add(client);

            return;
        }

        private void OnClientDisconnectHook(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            ClientSoundList.Remove(client);
            NormalFilter.Remove(client);
        }

        [ConsoleCommand("css_stopsound")]
        [CommandHelper(1, "css_stopsound <0-1>", CommandUsage.CLIENT_ONLY)]
        public void StopSoundCommand(CCSPlayerController client, CommandInfo info)
        {
            var arg = info.GetArg(1);

            if (int.Parse(arg) < 0 || int.Parse(arg) > 2)
            {
                info.ReplyToCommand($"{ChatColors.Green}[Stopsound]{ChatColors.White} You can't set number more than 2 or less than 0!");
                return;
            }

            var mode = (SoundMode)int.Parse(arg);
            SetStopSoundStatus(client, mode);
        }

        public HookResult Hook_WeaponFiring(UserMessage userMessage)
        {
            userMessage.Recipients = NormalFilter;
            return HookResult.Continue;
        }

        void SetStopSoundStatus(CCSPlayerController client, SoundMode mode)
        {
            if(client == null) return;

            if(!ClientSoundList.ContainsKey(client))
                ClientSoundList.Add(client, mode);

            ClientSoundList[client] = mode;

            if(mode == SoundMode.M_NORMAL)
                NormalFilter.Add(client);

            else 
                NormalFilter.Remove(client);

            client.PrintToChat($" {ChatColors.Green}[Stopsound]{ChatColors.White} You set to {ChatColors.Olive}{GetModeString(mode)}{ChatColors.White}.");
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
