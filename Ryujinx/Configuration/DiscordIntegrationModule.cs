﻿using DiscordRPC;
using Ryujinx.Common;
using System;
using System.Linq;

namespace Ryujinx.Configuration
{
    static class DiscordIntegrationModule
    {
        private static DiscordRpcClient _discordClient;

        private static string LargeDescription = "Ryujinx is a Nintendo Switch emulator.";

        public static RichPresence DiscordPresence { get; private set; }

        public static void Initialize()
        {
            DiscordPresence = new RichPresence
            {
                Assets     = new Assets
                {
                    LargeImageKey  = "ryujinx",
                    LargeImageText = LargeDescription
                },
                Details    = "Main Menu",
                State      = "Idling",
                Timestamps = new Timestamps(DateTime.UtcNow)
            };

            ConfigurationState.Instance.EnableDiscordIntegration.Event += Update;
        }

        private static void Update(object sender, ReactiveEventArgs<bool> e)
        {
            if (e.OldValue != e.NewValue)
            {
                // If the integration was active, disable it and unload everything
                if (e.OldValue)
                {
                    _discordClient?.Dispose();

                    _discordClient = null;
                }

                // If we need to activate it and the client isn't active, initialize it
                if (e.NewValue && _discordClient == null)
                {
                    _discordClient = new DiscordRpcClient("568815339807309834");

                    _discordClient.Initialize();
                    _discordClient.SetPresence(DiscordPresence);
                }
            }
        }

        public static void SwitchToPlayingState(string titleId, string titleName)
        {
            if (SupportedTitles.Contains(titleId))
            {
                DiscordPresence.Assets.LargeImageKey = titleId;
            }

            string state = titleId;

            if (state == null)
            {
                state = "Ryujinx";
            }
            else
            {
                state = state.ToUpper();
            }

            string details = "Idling";

            if (titleName != null)
            {
                details = $"Playing {titleName}";
            }

            DiscordPresence.Details               = details;
            DiscordPresence.State                 = state;
            DiscordPresence.Assets.LargeImageText = titleName;
            DiscordPresence.Assets.SmallImageKey  = "ryujinx";
            DiscordPresence.Assets.SmallImageText = LargeDescription;
            DiscordPresence.Timestamps            = new Timestamps(DateTime.UtcNow);

            _discordClient?.SetPresence(DiscordPresence);
        }

        public static void SwitchToMainMenu()
        {
            DiscordPresence.Details               = "Main Menu";
            DiscordPresence.State                 = "Idling";
            DiscordPresence.Assets.LargeImageKey  = "ryujinx";
            DiscordPresence.Assets.LargeImageText = LargeDescription;
            DiscordPresence.Assets.SmallImageKey  = null;
            DiscordPresence.Assets.SmallImageText = null;
            DiscordPresence.Timestamps            = new Timestamps(DateTime.UtcNow);

            _discordClient?.SetPresence(DiscordPresence);
        }

        public static void Exit()
        {
            _discordClient?.Dispose();
        }

        private static readonly string[] SupportedTitles =
        {
            "0100000000010000",
            "01000B900D8B0000",
            "01000d200ac0c000",
            "01000d700be88000",
            "01000dc007e90000",
            "01000e2003fa0000",
            "0100225000fee000",
            "010028d0045ce000",
            "01002b30028f6000",
            "01002fc00c6d0000",
            "010034e005c9c000",
            "010036B0034E4000",
            "01003D200BAA2000",
            "01004f8006a78000",
            "010051f00ac5e000",
            "010055D009F78000",
            "010056e00853a000",
            "0100574009f9e000",
            "01005D700E742000",
            "0100628004bce000",
            "0100633007d48000",
            "010065500b218000",
            "010068f00aa78000",
            "01006BB00C6F0000",
            "01006F8002326000",
            "01006a800016e000",
            "010072800cbe8000",
            "01007300020fa000",
            "01007330027ee000",
            "0100749009844000",
            "01007a4008486000",
            "01007ef00011e000",
            "010080b00ad66000",
            "010082400BCC6000",
            "01008db008c2c000",
            "010094e00b52e000",
            "01009aa000faa000",
            "01009b90006dc000",
            "01009cc00c97c000",
            "0100EA80032EA000",
            "0100a4200a284000",
            "0100a5c00d162000",
            "0100abf008968000",
            "0100ae000aebc000",
            "0100b3f000be2000",
            "0100bc2004ff4000",
            "0100cf3007578000",
            "0100d5d00c6be000",
            "0100d6b00cd88000",
            "0100d870045b6000",
            "0100e0c00adac000",
            "0100e46006708000",
            "0100e7200b272000",
            "0100e9f00b882000",
            "0100eab00605c000",
            "0100efd00a4fa000",
            "0100f6a00a684000",
            "0100f9f00c696000",
            "051337133769a000"
        };
    }
}
