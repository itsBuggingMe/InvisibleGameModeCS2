using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using System.Drawing;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Timers;

namespace InvisPlugin;

[MinimumApiVersion(80)]
public class InvisPlugin : BasePlugin
{
    public override string ModuleName => "InvisPlugin";
    public override string ModuleVersion => "0.1.1";
    public override string ModuleAuthor => "Manio";
    public override string ModuleDescription => "Invisibility plugin";
    private static Timer? visibilityTimer;
    private static HashSet<int?> InvisIds = new HashSet<int?>();
    private static float invisTimeMultiplier = 1.2f;
    private CancellationTokenSource visibilityTokenSource;
    public override void Load(bool hotReload)
    {
        Console.WriteLine(" ");
        Console.WriteLine(" ___   __    _  __   __  ___   _______      _______  ___      __   __  _______  ___   __    _ ");
        Console.WriteLine("|   | |  |  | ||  | |  ||   | |       |    |       ||   |    |  | |  ||       ||   | |  |  | |");
        Console.WriteLine("|   | |   |_| ||  |_|  ||   | |  _____|    |    _  ||   |    |  | |  ||    ___||   | |   |_| |");
        Console.WriteLine("|   | |       ||       ||   | | |_____     |   |_| ||   |    |  |_|  ||   | __ |   | |       |");
        Console.WriteLine("|   | |  _    ||       ||   | |_____  |    |    ___||   |___ |       ||   ||  ||   | |  _    |");
        Console.WriteLine("|   | | | |   | |     | |   |  _____| |    |   |    |       ||       ||   |_| ||   | | | |   |");
        Console.WriteLine("|___| |_|  |__|  |___|  |___| |_______|    |___|    |_______||_______||_______||___| |_|  |__|");
        Console.WriteLine("			     >> Version: 0.1.1");
        Console.WriteLine("		>> GitHub: https://github.com/maniolos/Cs2Invis");
        Console.WriteLine(" ");
        Server.ExecuteCommand("sv_disable_radar 1");
        Server.ExecuteCommand("sv_cheats true");
        Server.ExecuteCommand("mp_freezetime 0");
        Server.ExecuteCommand("mp_roundtime 60");
        Server.ExecuteCommand("mp_warmup_end");
    }

    [ConsoleCommand("css_invis", "Invisible command")]
    public void OnInvisCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (commandInfo.ArgString.Length == 0)
        {
            commandInfo.ReplyToCommand("Usage: css_invis <player>");
            return;
        }
        if (player == null)
            return;
        if (!InvisIds.Contains(player.UserId))
        {
            commandInfo.ReplyToCommand("User " + player.PlayerName + " is now invisible");
            player.PlayerPawn.Value.NextRadarUpdateTime = 0.0f;
            SetPlayerInvisible(player);
            InvisIds.Add(player.UserId);
            commandInfo.ReplyToCommand("Invisiblity enabled");
        }

    }

    [ConsoleCommand("css_uninvis", "Make Visible command")]
    public void OnUnInvisCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive)
            return;
        if (InvisIds.Contains(player.UserId))
        {
            InvisIds.Remove(player.UserId);
            SetPlayerVisible(player);
            commandInfo.ReplyToCommand("Invisiblity disabled");
        }
    }
    [ConsoleCommand("css_invis_time", "Set invisibility time")]
    public void OnInvisTimeCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (commandInfo.ArgString.Length == 0)
        {
            commandInfo.ReplyToCommand("Usage: css_invis_time <time> default 1");
            return;
        }
        if (player == null)
            return;
        if (float.TryParse(commandInfo.ArgString, out float time))
        {
            invisTimeMultiplier = time;
            commandInfo.ReplyToCommand("Invisibility time is multiplied by " + time);
        }
    }
    public void SetPlayerVisibleForLimitedTime(CCSPlayerController player, float timeInMs)
    {
        // Cancel the previous timer if it exists and reset the cancellation token
        visibilityTokenSource?.Cancel();
        visibilityTimer?.Kill();
        visibilityTokenSource = new CancellationTokenSource();
        var token = visibilityTokenSource.Token;

        // Ensure remainingTime is non-negative and rounds appropriately
        int remainingTime = Math.Max((int)Math.Round(timeInMs / 100), 0);

        // Make the player visible immediately
        SetPlayerVisible(player);

        // Set up the visual timer display
        string timeMessage = "_ _ _ _ _ _ _ _ _ _";
        int totalSegments = 10; // Adjust to match the length of timeMessage
        visibilityTimer = new Timer(0.2f, () =>
        {
            if (remainingTime <= 0 || token.IsCancellationRequested)
            {
                // End of the timer or cancellation requested, make the player invisible
                player.PrintToCenter(""); // Clear the message
                SetPlayerInvisible(player);
                visibilityTimer?.Kill(); // Stop the timer
                return; // Exit the timer callback
            }

            // Calculate filled segments based on remaining time
            int filledSegments = Math.Max(0, Math.Min(totalSegments, totalSegments - remainingTime));

            // Update the timer display based on filled and empty segments
            timeMessage = new string('#', filledSegments) + new string('_', totalSegments - filledSegments);

            // Show the updated timer and remaining time to the player
            player.PrintToCenter(timeMessage);

            // Decrement the remaining time
            remainingTime--;
        }, TimerFlags.REPEAT);

        // Add the timer to the collection to ensure it's managed by the plugin
        Timers.Add(visibilityTimer);

        // Set up a final failsafe to ensure player becomes invisible if the timer somehow doesn't complete
        _ = Task.Delay((int)timeInMs, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
            {
                player.PrintToCenter("");
                SetPlayerInvisible(player);
                visibilityTimer?.Kill(); // Make sure the timer is stopped
            }
        }, TaskScheduler.Default);
    }
    public static void SetPlayerVisible(CCSPlayerController player)
    {
        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null)
            return;
        playerPawnValue.Render = Color.FromArgb(255, 255, 255, 255);
        Utilities.SetStateChanged(playerPawnValue, "CBaseModelEntity", "m_clrRender");
        var activeWeapon = playerPawnValue!.WeaponServices?.ActiveWeapon.Value;
        if (activeWeapon != null && activeWeapon.IsValid)
        {
            activeWeapon.Render = Color.FromArgb(255, 255, 255, 255);
            activeWeapon.ShadowStrength = 1.0f;
            Utilities.SetStateChanged(activeWeapon, "CBaseModelEntity", "m_clrRender");
        }

        var myWeapons = playerPawnValue.WeaponServices?.MyWeapons;
        if (myWeapons != null)
        {
            foreach (var gun in myWeapons)
            {
                var weapon = gun.Value;
                if (weapon != null)
                {
                    weapon.Render = Color.FromArgb(255, 255, 255, 255);
                    weapon.ShadowStrength = 1.0f;
                    Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }



    private static void ShowTimerToPlayer(CCSPlayerController player, string timerDisplay)
    {
        // Implémentation d'affichage en jeu, exemple simple avec un message
        player.PrintToCenterHtml(timerDisplay);
    }

    public static void SetPlayerInvisible(CCSPlayerController player)
    {

        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null || !playerPawnValue.IsValid)
        {
            return;
        }

        if (playerPawnValue != null && playerPawnValue.IsValid)
        {
            playerPawnValue.Render = Color.FromArgb(0, 255, 255, 255);
            // run a console command for the player to desactivate the radar
            playerPawnValue.NextRadarUpdateTime = 0.0f;
            Utilities.SetStateChanged(playerPawnValue, "CBaseModelEntity", "m_clrRender");
        }

        var activeWeapon = playerPawnValue!.WeaponServices?.ActiveWeapon.Value;
        // if user is terrorist
        C4LightEffect_t c4LightEffect_T;
        c4LightEffect_T = C4LightEffect_t.eLightEffectNone;


        //public enum C4LightEffect_t : uint
        //{
        //    eLightEffectNone,
        //    eLightEffectDropped,
        //    eLightEffectThirdPersonHeld
        //}

        if (activeWeapon != null && activeWeapon.IsValid)
        {
            activeWeapon.Render = Color.FromArgb(0, 255, 255, 255);
            activeWeapon.AnimatedEveryTick = false;
            activeWeapon.AnimationUpdateScheduled = false;
            activeWeapon.AnimTime = 0.0f;
            activeWeapon.Blinktoggle = false;
            activeWeapon.ShadowStrength = 0.0f;
            activeWeapon.Effects = 0;
            activeWeapon.RenderMode = 0;
            activeWeapon.RenderFX = 0;
            activeWeapon.RenderMode = 0;
            activeWeapon.AnimationUpdateScheduled = false;
            activeWeapon.AnimatedEveryTick = false;
            Utilities.SetStateChanged(activeWeapon, "CBaseModelEntity", "m_clrRender");
        }

        var myWeapons = playerPawnValue.WeaponServices?.MyWeapons;
        if (myWeapons != null)
        {
            foreach (var gun in myWeapons)
            {
                var weapon = gun.Value;
                if (weapon != null)
                {
                    weapon.Render = Color.FromArgb(0, 255, 255, 255);
                    weapon.ShadowStrength = 0.0f;
                    weapon.AnimatedEveryTick = false;
                    weapon.AnimationUpdateScheduled = false;
                    weapon.AnimTime = 0.0f;
                    weapon.Blinktoggle = false;
                    weapon.Effects = 0;
                    weapon.RenderMode = 0;
                    weapon.RenderFX = 0;
                    weapon.RenderMode = 0;
                    weapon.AnimationUpdateScheduled = false;
                    weapon.AnimatedEveryTick = false;
                    weapon.Effects = 0;
                    Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }

    // Output hooks can use wildcards to match multiple entities
    [GameEventHandler]
    public HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        int playerevent = (int)player.UserId!;

        if (InvisIds.Contains(playerevent))
        {
            SetPlayerInvisible(player);
        }

        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnPlayerMakeSound(EventPlayerSound @event, GameEventInfo info)
    {
        // plus le son est grand plus le joueur est visible longtemps, ensuite il devient invisible
        CCSPlayerController player = @event.Userid!;
        int playerevent = (int)player.UserId!;
        int volume = @event.Radius;

        if (InvisIds.Contains(playerevent) && volume > 550)
        {
            SetPlayerVisibleForLimitedTime(player, invisTimeMultiplier * (float)volume);
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnPlayerShoot(EventWeaponFire @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;
        float volume = @event.Silenced ? 300.0f : 600.0f;
        if (InvisIds.Contains(player.UserId))
        {
            SetPlayerVisibleForLimitedTime(player, invisTimeMultiplier * volume);
        }
        else
        {
            var playPawnValue = player.PlayerPawn.Value!;
            playPawnValue.Health -= 2;
            Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnBombPlanting(EventBombBeginplant @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;
        int playerevent = (int)player.UserId!;
        if (InvisIds.Contains(playerevent))
        {
            SetPlayerVisibleForLimitedTime(player, invisTimeMultiplier * (float)500);
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnBombDefusing(EventBombBegindefuse @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;
        int playerevent = (int)player.UserId!;
        if (InvisIds.Contains(playerevent))
        {
            SetPlayerVisibleForLimitedTime(player, invisTimeMultiplier * (float)500);
        }
        return HookResult.Continue;
    }
    //[GameEventHandler]
    //public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    //{
    //    // when a user is connecting, we desactivate helpers to not show the player if he is in front of him
    //    CCSPlayerController player = @event.Userid!;
    //    var playerPawnValue = player.PlayerPawn.Value;
    //    Utilities.SetStateChanged(playerPawnValue, "CBasePlayer", "m_bShowHints");
    //    // désactiver le radar pour le joueur
    //    playerPawnValue.NextRadarUpdateTime = 0.0f;
    //    return HookResult.Continue;
    //}
    //[GameEventHandler]
    //public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    //{
    //    // when a user is disconnecting, we reactivate helpers to show the player if he is in front of him
    //    CCSPlayerController player = @event.Userid!;
    //    var playerPawnValue = player.PlayerPawn.Value;
    //    Utilities.SetStateChanged(playerPawnValue, "CBasePlayer", "m_bShowHints");
    //    return HookResult.Continue;
    //}
    //[GameEventHandler]
    //public HookResult OnPlayerConnect(EventPlayerConnectFull @event, GameEventInfo info)
    //{
    //    // when a user is connecting, we desactivate helpers to not show the player if he is in front of him
    //    CCSPlayerController player = @event.Userid!;
    //    var playerPawnValue = player.PlayerPawn.Value;
    //    playerPawnValue.NextRadarUpdateTime = 10000.0f;
    //    Utilities.SetStateChanged(playerPawnValue, "CBasePlayer", "m_bShowHints");
    //    // désactiver le radar pour le joueur
    //    return HookResult.Continue;
    //}
    // if player is in t side and start planting the bomb, he becomes visible
    //[GameEventHandler]
    //public HookResult OnBombPlanted(EventBombPlanted @event, GameEventInfo info)
    //{
    //    CCSPlayerController player = @event.Userid!;
    //    int playerevent = (int)player.UserId!;
    //    if (InvisIds.Contains(playerevent))
    //    {
    //        SetPlayerVisibleForLimitedTime(player, 500);
    //    }
    //    return HookResult.Continue;
    //}
    //// if player is in t side and start defusing the bomb, he becomes visible
    //[GameEventHandler]
    //public HookResult OnBombDefused(EventBombDefused @event, GameEventInfo info)
    //{
    //    CCSPlayerController player = @event.Userid!;
    //    int playerevent = (int)player.UserId!;
    //    if (InvisIds.Contains(playerevent))
    //    {
    //        SetPlayerVisibleForLimitedTime(player, 500);
    //    }
    //    return HookResult.Continue;
    //}
}