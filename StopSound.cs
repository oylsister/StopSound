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

        public ulong silencer;
        public ulong stopsound;
        public ulong normal;

        public enum SoundMode : int
        {
            M_NORMAL = 0,
            M_STOP = 1,
            M_SILENCER = 2
        }

        public Dictionary<CCSPlayerController, SoundMode> ClientSoundList = new Dictionary<CCSPlayerController, SoundMode>();

        public override void Load(bool hotReload)
        {
            HookUserMessage(452, Hook_WeaponFiring, HookMode.Pre);

            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<OnClientDisconnect>(OnClientDisconnected);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo eventInfo)
        {
            var client = @event.Userid;

            if (client == null)
                return HookResult.Continue;

            ClientSoundList.Add(client, SoundMode.M_NORMAL);
            SetStopSoundStatus(client, SoundMode.M_NORMAL);
            return HookResult.Continue;
        }

        private void OnClientDisconnected(int playerSlot)
        {
            var client = Utilities.GetPlayerFromSlot(playerSlot);

            if (client == null)
                return;

            ClientSoundList.Remove(client);

            normal |= ((ulong)1L << client.Slot);
            stopsound |= ((ulong)1L << client.Slot);
            silencer &= ((ulong)1L << client.Slot);
        }

        [ConsoleCommand("css_stopsound")]
        [CommandHelper(1, "css_stopsound <0-2>", CommandUsage.CLIENT_ONLY)]
        public void StopSoundCommand(CCSPlayerController client, CommandInfo info)
        {
            var arg = info.GetArg(1);

            if(int.Parse(arg) > 2 || int.Parse(arg) < 3)
            {
                info.ReplyToCommand($"{ChatColors.Green}[Stopsound]{ChatColors.White} You can't set number more than 2 or less than 0!");
                return;
            }

            var mode = (SoundMode)int.Parse(arg);
            SetStopSoundStatus(client, mode);
        }

        public HookResult Hook_WeaponFiring(UserMessage userMessage)
        {
            var silencer_mask = new RecipientFilter(silencer);

            var weaponid = userMessage.ReadUInt("weapon_id");
            var soundType = userMessage.ReadInt("sound_type");
            var itemdefindex = userMessage.ReadUInt("item_def_index");

            userMessage.SetUInt("weapon_id", 0);
            userMessage.SetInt("sound_type", 9);
            userMessage.SetUInt("item_def_index", 61);

            // send for people who use silencer.
            userMessage.Send(silencer_mask);

            var normal_mask = new RecipientFilter(normal);

            userMessage.SetUInt("weapon_id", weaponid);
            userMessage.SetInt("sound_type", soundType);
            userMessage.SetUInt("item_def_index", itemdefindex);
            userMessage.Send(normal_mask);

            return HookResult.Handled;
        }

        void SetStopSoundStatus(CCSPlayerController client, SoundMode mode)
        {
            if(client == null) return;

            if(!ClientSoundList.ContainsKey(client))
            {
                ClientSoundList.Add(client, mode);
            }

            var clientMode = ClientSoundList[client];

            if (mode == SoundMode.M_NORMAL)
            {
                normal |= ((ulong)1L << client.Slot);

                if (clientMode == SoundMode.M_STOP)
                    stopsound &= ((ulong)1L << client.Slot);

                else if (clientMode == SoundMode.M_SILENCER)
                    silencer &= ((ulong)1L << client.Slot);
            }

            else if (mode == SoundMode.M_STOP)
            {
                stopsound |= ((ulong)1L << client.Slot);

                if (clientMode == SoundMode.M_NORMAL)
                    normal &= ((ulong)1L << client.Slot);

                else if (clientMode == SoundMode.M_SILENCER)
                    silencer &= ((ulong)1L << client.Slot);
            }

            else
            {
                silencer |= ((ulong)1L << client.Slot);

                if (clientMode == SoundMode.M_NORMAL)
                    normal &= ((ulong)1L << client.Slot);

                else if (clientMode == SoundMode.M_STOP)
                    stopsound &= ((ulong)1L << client.Slot);
            }

            ClientSoundList[client] = mode;
            client.PrintToChat($"{ChatColors.Green}[Stopsound]{ChatColors.White} You set to {ChatColors.Lime}{GetModeString(mode)}{ChatColors.White}.");
        }

        string GetModeString(SoundMode mode)
        {
            switch (mode)
            {
                case SoundMode.M_NORMAL:
                    return "Enable Sound";

                case SoundMode.M_SILENCER:
                    return "Silencer Sound";

                case SoundMode.M_STOP:
                    return "No weapon sound";

                default:
                    return "Invalid";
            }
        }
    }
}
