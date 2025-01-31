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
    private static readonly HashSet<int> InvisIds = new();
    private static readonly Dictionary<int, Timer> playerTimers = new();
    private static readonly Dictionary<int, CancellationTokenSource> playerTokenSources = new();
    private static float invisTimeMultiplier = 0.7f;
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
    [ConsoleCommand("css_invis", "Make a player invisible")]
    public void OnInvisCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (commandInfo.ArgString.Length == 0)
        {
            commandInfo.ReplyToCommand("Usage: css_invis <player>");
            return;
        }

        string playerName = commandInfo.ArgString;
        CCSPlayerController? targetPlayer = GetPlayerByName(playerName, commandInfo);

        if (targetPlayer == null)
        {
            commandInfo.ReplyToCommand("Player not found.");
            return;
        }

        int playerId = targetPlayer.UserId.Value;
        if (!InvisIds.Contains(playerId))
        {
            InvisIds.Add(playerId);
            Server.NextFrame(() => SetPlayerInvisible(targetPlayer));
            commandInfo.ReplyToCommand($"Player {targetPlayer.PlayerName} is now invisible.");
        }
        else
        {
            commandInfo.ReplyToCommand($"Player {targetPlayer.PlayerName} is already invisible.");
        }
    }


    [ConsoleCommand("css_uninvis", "Make a player visible again")]
    public void OnUnInvisCommand(CCSPlayerController player, CommandInfo commandInfo)
    {
        if (commandInfo.ArgString.Length == 0)
        {
            commandInfo.ReplyToCommand("Usage: css_uninvis <player>");
            return;
        }

        string playerName = commandInfo.ArgString;
        CCSPlayerController? targetPlayer = GetPlayerByName(playerName, commandInfo);

        if (targetPlayer == null)
        {
            commandInfo.ReplyToCommand("Player not found.");
            return;
        }

        int playerId = targetPlayer.UserId.Value;
        if (InvisIds.Contains(playerId))
        {
            InvisIds.Remove(playerId);
            RemovePlayerVisibilityTimer(playerId);
            Server.NextFrame(() => SetPlayerVisible(targetPlayer));
            commandInfo.ReplyToCommand($"Player {targetPlayer.PlayerName} is now visible.");
        }
        else
        {
            commandInfo.ReplyToCommand($"Player {targetPlayer.PlayerName} is already visible.");
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
        int playerId = player.UserId.Value;

        if (!InvisIds.Contains(playerId)) return;

        RemovePlayerVisibilityTimer(playerId);

        var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        playerTokenSources[playerId] = tokenSource;

        int remainingTime = Math.Max((int)Math.Round(timeInMs / 100 ), 0);
        int fadeStartTime = remainingTime / 2;

        Server.NextFrame(() => SetPlayerVisible(player));

        // **Timer de gestion de l'affichage uniquement**
        _ = Task.Run(async () =>
        {
            while (remainingTime > 0 && !token.IsCancellationRequested)
            {
                await Task.Delay(200, token);

                // Mettre à jour l'affichage du timer pour le joueur
                int filledSegments = Math.Max(0, 20 - remainingTime);
                string timeMessage = new string('█', filledSegments) + new string('_', 20 - filledSegments);
                Server.NextFrame(() => player.PrintToCenter(timeMessage));

                // Début du fade-out progressif
                if (remainingTime <= fadeStartTime)
                {
                    int alpha = (int)(255 * (remainingTime / (float)fadeStartTime));
                    Server.NextFrame(() => SetPlayerTransparency(player, alpha));
                }

                remainingTime--;
            }

            // Une fois terminé, rendre invisible
            if (!token.IsCancellationRequested)
            {
                Server.NextFrame(() => SetPlayerInvisible(player));
                Server.NextFrame(() => player.PrintToCenter(""));
            }

        }, token);
    }
    private void RemovePlayerVisibilityTimer(int playerId)
    {
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
    }
    public static void SetPlayerVisible(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return;

        playerPawn.Render = Color.FromArgb(255, 255, 255, 255);
        Utilities.SetStateChanged(playerPawn, "CBaseModelEntity", "m_clrRender");

        var weapons = playerPawn.WeaponServices?.MyWeapons;
        if (weapons != null)
        {
            foreach (var gun in weapons)
            {
                gun.Value.Render = Color.FromArgb(255, 255, 255, 255);
                Utilities.SetStateChanged(gun.Value, "CBaseModelEntity", "m_clrRender");
            }
        }
    }

    public static void SetPlayerInvisible(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null || !playerPawn.IsValid) return;

        playerPawn.Render = Color.FromArgb(0, 255, 255, 255);
        playerPawn.NextRadarUpdateTime = 0.0f;
        Utilities.SetStateChanged(playerPawn, "CBaseModelEntity", "m_clrRender");

        var weapons = playerPawn.WeaponServices?.MyWeapons;
        if (weapons != null)
        {
            foreach (var gun in weapons)
            {
                gun.Value.Render = Color.FromArgb(0, 255, 255, 255);
                Utilities.SetStateChanged(gun.Value, "CBaseModelEntity", "m_clrRender");
            }
        }
    }
    // Output hooks can use wildcards to match multiple entities
    [GameEventHandler]
    public HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;

        int playerevent = player.UserId.Value;

        if (InvisIds.Contains(playerevent))
        {
            SetPlayerInvisible(player);
        }

        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnPlayerMakeSound(EventPlayerSound @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;
        int playerId = player.UserId.Value;
        int volume = Math.Min(@event.Radius, 1100);

        if (InvisIds.Contains(playerId) && volume > 550)
        {
            SetPlayerVisibleForLimitedTime(player, invisTimeMultiplier * volume);
        }
        return HookResult.Continue;
    }
    [GameEventHandler]
    public HookResult OnPlayerShoot(EventWeaponFire @event, GameEventInfo info)
    {
        CCSPlayerController player = @event.Userid!;
        float volume = @event.Silenced ? 300.0f : 600.0f;
        if (InvisIds.Contains(player.UserId.Value))
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
        int playerevent = player.UserId.Value;
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