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
    private readonly Dictionary<int, Timer> playerTimers = new();  // Store individual timers per player
    private readonly Dictionary<int, CancellationTokenSource> playerTokenSources = new(); // Store cancellation tokens per player
    private static HashSet<int?> InvisIds = new HashSet<int?>();
    private static float invisTimeMultiplier = 0.7f;
    private CancellationTokenSource? visibilityTokenSource;
    private readonly Dictionary<int, string> playerIds = new();
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

        string playerName = commandInfo.ArgString;

        // make a for loop to get the player by name
        CCSPlayerController? targetPlayer = GetPlayerByName(name: playerName, commandInfo);
        if (targetPlayer == null)
        {
            commandInfo.ReplyToCommand("Player not found");
        }


            if (!InvisIds.Contains(targetPlayer.UserId))
        {
            commandInfo.ReplyToCommand("User " + targetPlayer.PlayerName + " is now invisible");
            SetPlayerInvisible(targetPlayer);
            InvisIds.Add(targetPlayer.UserId);
            commandInfo.ReplyToCommand("Invisiblity enabled");
        }
    }

    [ConsoleCommand("css_uninvis", "Make Visible command")]
    public void OnUnInvisCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || !player.PawnIsAlive)
            return;

        string playerName = commandInfo.ArgString;

        // make a for loop to get the player by name
        CCSPlayerController? targetPlayer = GetPlayerByName(name: playerName, commandInfo);
        if (targetPlayer == null)
        {
            commandInfo.ReplyToCommand("Player not found");
            return;
        }
        if (InvisIds.Contains(targetPlayer.UserId))
        {
            InvisIds.Remove(targetPlayer.UserId);
            SetPlayerVisible(targetPlayer);
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
    if(!player.UserId.HasValue) return; 
    int playerId = player.UserId.Value;
    
    // Cancel and remove previous timer if it exists for this player
    if (playerTimers.TryGetValue(playerId, out var existingTimer))
    {
        existingTimer.Kill();
        playerTimers.Remove(playerId);
    }
    
    if (playerTokenSources.TryGetValue(playerId, out var existingToken))
    {
        existingToken.Cancel();
        playerTokenSources.Remove(playerId);
    }

    var tokenSource = new CancellationTokenSource();
    var token = tokenSource.Token;
    playerTokenSources[playerId] = tokenSource;

    int totalTime = Math.Max((int)Math.Round(timeInMs / 100), 0);
    int fadeStartTime = totalTime / 2; // Start fade effect at half time
    int remainingTime = totalTime;

    SetPlayerVisible(player);

    string timeMessage = "⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿";
    int totalSegments = 20;

    Timer visibilityTimer = new Timer(0.2f, () =>
    {
        if (remainingTime <= 0 || token.IsCancellationRequested)
        {
            player.PrintToCenter("");
            SetPlayerInvisible(player);
            
            playerTimers.Remove(playerId);
            playerTokenSources.Remove(playerId);
            return;
        }

        // Update visual timer message
        int filledSegments = Math.Max(0, Math.Min(totalSegments, totalSegments - remainingTime));
        timeMessage = new string('⣿', totalSegments - filledSegments) + new string('⠀', filledSegments);
        player.PrintToCenter(timeMessage);

        // Start fade-out effect at halfway mark
        if (remainingTime <= fadeStartTime)
        {
            int alpha = (int)(255 * (remainingTime / (float)fadeStartTime));
            SetPlayerTransparency(player, alpha);
        }

        remainingTime--;

    }, TimerFlags.REPEAT);

    // Store the player's timer
    playerTimers[playerId] = visibilityTimer;
    Timers.Add(visibilityTimer);

    // Final failsafe to ensure player becomes invisible after the time ends
    _ = Task.Delay((int)timeInMs, token).ContinueWith(_ =>
    {
        if (!token.IsCancellationRequested)
        {
            player.PrintToCenter("");
            SetPlayerInvisible(player);

            playerTimers.Remove(playerId);
            playerTokenSources.Remove(playerId);
        }
    }, TaskScheduler.Default);
}
    /*public void SetPlayerVisibleForLimitedTime(CCSPlayerController player, float timeInMs)
    {
        // Annuler les timers précédents
        visibilityTokenSource?.Cancel();
        visibilityTimer?.Kill();
        visibilityTokenSource = new CancellationTokenSource();
        var token = visibilityTokenSource.Token;

        int totalTime = Math.Max((int)Math.Round(timeInMs / 100), 0);
        int fadeStartTime = totalTime / 2; // Le fade commence à la moitié du timer
        int remainingTime = totalTime;

        SetPlayerVisible(player);

        string timeMessage = "⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿";
        int totalSegments = 20;

        visibilityTimer = new Timer(0.2f, () =>
        {
            if (remainingTime <= 0 || token.IsCancellationRequested)
            {
                visibilityTimer?.Kill();
                player.PrintToCenter("");

                // Assurez-vous que le joueur est complètement invisible
                SetPlayerInvisible(player);
                return;
            }

            // Calculer les segments affichés
            int filledSegments = Math.Max(0, Math.Min(totalSegments, totalSegments - remainingTime));
            timeMessage = new string('⣿', totalSegments - filledSegments) + new string('⠀', filledSegments);
            player.PrintToCenter(timeMessage);

            // Début du fade à la moitié du temps
            if (remainingTime <= fadeStartTime)
            {
                // Calculer l'opacité en fonction du temps restant
                int alpha = (int)(255 * (remainingTime / (float)fadeStartTime));
                SetPlayerTransparency(player, alpha);
            }

            remainingTime--;
        }, TimerFlags.REPEAT);

        Timers.Add(visibilityTimer);

        _ = Task.Delay((int)timeInMs, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
            {
                visibilityTimer?.Kill();
                player.PrintToCenter("");

                // Assurez-vous que le joueur est complètement invisible
                SetPlayerInvisible(player);
            }
        }, TaskScheduler.Default);
    }*/

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
        //C4LightEffect_t c4LightEffect_T;
        //c4LightEffect_T = C4LightEffect_t.eLightEffectNone;

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
        if(volume >1100) { volume = 1100 ; }
        
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
            if (player.PlayerPawn.Value != null)
            {
                Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
            }
            if (playPawnValue.Health <= 0)
            {
                player.CommitSuicide(false, true);
            }
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
    public CCSPlayerController? GetPlayerByName(string name, CommandInfo commandInfo)
    {
        var players = Utilities.GetPlayers();
        foreach (var player in players)
        {
            if (player.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return player;
            }

        }
        string[] playerNames = new string[players.Count()]; // Définir la taille du tableau
        int i = 0;
        foreach (var player in players)
        {
            playerNames[i] = player.PlayerName;
            i++;
        }

         // Joindre les noms avec une virgule et répondre à la commande
         commandInfo.ReplyToCommand("Players: " + string.Join(", ", playerNames));

        return null;
    }



    private void SetPlayerTransparency(CCSPlayerController player, int alpha)
    {
        var playerPawnValue = player.PlayerPawn.Value;
        if (playerPawnValue == null) return;

        // Définir la transparence du joueur
        playerPawnValue.Render = Color.FromArgb(alpha, 255, 255, 255);
        Utilities.SetStateChanged(playerPawnValue, "CBaseModelEntity", "m_clrRender");

        // Définir la transparence des armes
        var weapons = playerPawnValue.WeaponServices?.MyWeapons;
        if (weapons != null)
        {
            foreach (var gun in weapons)
            {
                var weapon = gun.Value;
                if (weapon != null)
                {
                    weapon.Render = Color.FromArgb(alpha, 255, 255, 255);
                    Utilities.SetStateChanged(weapon, "CBaseModelEntity", "m_clrRender");
                }
            }
        }
    }
}