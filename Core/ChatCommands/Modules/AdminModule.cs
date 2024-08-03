using System.Linq;
using System.Text;
using LinqToDB;
using Vint.Core.ECS.Enums;
using Vint.Core.ECS.Components.Battle.Team;
using Vint.Core.Battles;
using Vint.Core.Battles.Bonus;
using Vint.Core.Battles.Player;
using Vint.Core.ChatCommands.Attributes;
using Vint.Core.Database;
using Vint.Core.Database.Models;
using Vint.Core.ECS.Entities;
using Vint.Core.ECS.Events.Battle;
using Vint.Core.Server;
using Vint.Core.Utils;

namespace Vint.Core.ChatCommands.Modules;

[ChatCommandGroup("admin", "Commands for admins", PlayerGroups.Admin)]
public class AdminModule : ChatCommandModule {
    [ChatCommand("ban", "Ban a player")]
    public async Task Ban(
        ChatCommandContext ctx,
        [Option("username", "Username of player to ban")]
        string username,
        [Option("duration", "Duration of ban", true)]
        string? rawDuration = null,
        [WaitingForText, Option("reason", "Reason for ban", true)]
        string? reason = null) {
        _ = TimeSpanUtils.TryParseDuration(rawDuration, out TimeSpan? duration);

        IPlayerConnection? targetConnection = ctx.Connection.Server.PlayerConnections.Values
            .Where(conn => conn.IsOnline)
            .SingleOrDefault(conn => conn.Player.Username == username);

        Player? targetPlayer = targetConnection?.Player;
        IEntity? notifyChat = null;
        List<IPlayerConnection>? notifiedConnections = null;

        if (targetConnection != null) {
            if (targetConnection.InLobby) {
                Battle battle = targetConnection.BattlePlayer!.Battle;

                notifyChat = targetConnection.BattlePlayer.InBattleAsTank ? battle.BattleChatEntity : battle.LobbyChatEntity;
                notifiedConnections = ChatUtils.GetReceivers(targetConnection, notifyChat).ToList();
            }
        } else {
            await using DbConnection db = new();
            targetPlayer = await db.Players.SingleOrDefaultAsync(player => player.Username == username);
        }

        if (targetPlayer == null) {
            await ctx.SendPrivateResponse("Player not found");
            return;
        }

        if (targetPlayer.IsAdmin) {
            await ctx.SendPrivateResponse($"Player '{username}' is admin");
            return;
        }

        if (!ctx.Connection.Player.IsAdmin && targetPlayer.IsModerator) {
            await ctx.SendPrivateResponse("Moderator cannot punish other moderator");
            return;
        }

        Punishment punishment = await targetPlayer.Ban((targetConnection as SocketPlayerConnection)?.EndPoint.Address.ToString(), reason, duration);
        string punishMessage = $"{username} was {punishment}";
        targetConnection?.Kick(reason);

        await ctx.SendPrivateResponse($"Punishment Id: {punishment.Id}");

        if (notifyChat == null || notifiedConnections == null)
            await ctx.SendPublicResponse(punishMessage);
        else {
            await ctx.SendResponse(punishMessage, notifyChat, notifiedConnections);

            if (ctx.Chat != notifyChat)
                await ctx.SendPrivateResponse(punishMessage);
        }
    }

    [ChatCommand("unban", "Remove ban from player")]
    public async Task UnBan(
        ChatCommandContext ctx,
        [Option("username", "Username of player to unban")]
        string username) {
        IPlayerConnection? targetConnection = ctx.Connection.Server.PlayerConnections.Values
            .Where(conn => conn.IsOnline)
            .SingleOrDefault(conn => conn.Player.Username == username);

        Player? targetPlayer = targetConnection?.Player;
        IEntity? notifyChat = null;
        List<IPlayerConnection>? notifiedConnections = null;

        if (targetConnection != null) {
            if (targetConnection.InLobby) {
                Battle battle = targetConnection.BattlePlayer!.Battle;

                notifyChat = targetConnection.BattlePlayer.InBattleAsTank ? battle.BattleChatEntity : battle.LobbyChatEntity;
                notifiedConnections = ChatUtils.GetReceivers(targetConnection, notifyChat).ToList();
            }
        } else {
            await using DbConnection db = new();
            targetPlayer = await db.Players.SingleOrDefaultAsync(player => player.Username == username);
        }

        if (targetPlayer == null) {
            await ctx.SendPrivateResponse("Player not found");
            return;
        }

        bool successful = await targetPlayer.UnBan();

        if (!successful) {
            await ctx.SendPrivateResponse($"'{username}' is not banned");
            return;
        }

        string punishMessage = $"{username} was unbanned";

        if (notifyChat == null || notifiedConnections == null)
            await ctx.SendPublicResponse(punishMessage);
        else {
            await ctx.SendResponse(punishMessage, notifyChat, notifiedConnections);

            if (ctx.Chat != notifyChat)
                await ctx.SendPrivateResponse(punishMessage);
        }
    }

    [ChatCommand("createInvite", "Create new invite")]
    public async Task CreateInvite(
        ChatCommandContext ctx,
        [Option("code", "Code")] string code,
        [Option("uses", "Maximum uses")] ushort uses) {
        await using DbConnection db = new();
        Invite? invite = await db.Invites.SingleOrDefaultAsync(invite => invite.Code == code);

        if (invite != null) {
            await ctx.SendPrivateResponse($"Already exists: {invite}");
            return;
        }

        invite = new Invite {
            Code = code,
            RemainingUses = uses
        };

        invite.Id = await db.InsertWithInt64IdentityAsync(invite);
        await ctx.SendPrivateResponse($"{invite}");
    }

    [ChatCommand("kickAllFromBattle", "Kicks all players in battle to lobby"), RequireConditions(ChatCommandConditions.InLobby)]
    public async Task KickAllFromBattle(ChatCommandContext ctx) {
        Battle battle = ctx.Connection.BattlePlayer!.Battle;

        foreach (BattlePlayer battlePlayer in battle.Players.Where(battlePlayer => battlePlayer.InBattleAsTank)) {
            await battlePlayer.PlayerConnection.Send(new KickFromBattleEvent(), battlePlayer.BattleUser);
            await battle.RemovePlayer(battlePlayer);
        }
    }

    [ChatCommand("usernames", "Online player usernames")]
    public async Task Usernames(ChatCommandContext ctx) {
        StringBuilder builder = new();
        List<IPlayerConnection> connections = ctx.Connection.Server.PlayerConnections.Values.ToList();
        List<string> onlineUsernames = connections
            .Where(connection => connection.IsOnline)
            .Select(connection => connection.Player.Username)
            .ToList();

        builder.AppendLine($"{connections.Count} players connected, {onlineUsernames.Count} players online:");
        builder.AppendJoin(Environment.NewLine, onlineUsernames);
        await ctx.SendPrivateResponse(builder.ToString());
    }

    [ChatCommand("dropBonus", "Drop bonus"), RequireConditions(ChatCommandConditions.InBattle)]
    public async Task DropBonus(
        ChatCommandContext ctx,
        [Option("type", "Type of the bonus")] BonusType bonusType) {
        bool? isSuccessful = await (ctx.Connection.BattlePlayer?.Battle.BonusProcessor?.DropBonus(bonusType) ?? Task.FromResult(false));

        if (isSuccessful != true) {
            await ctx.SendPrivateResponse($"{bonusType} is not dropped");
            return;
        }

        await ctx.SendPrivateResponse($"{bonusType} dropped");
    }

    [ChatCommand("teamswitch", "Switches player to Team None"), RequireConditions(ChatCommandConditions.InBattle)]
    public async Task TeamSwitch(
        ChatCommandContext ctx,
        [Option("username", "Username ")]
        string username) {
        BattlePlayer? target = ctx.Connection.BattlePlayer!.Battle.Players
            .Where(player => player.InBattleAsTank)
            .SingleOrDefault(conn => conn.PlayerConnection.Player.Username == username);
        if (target == null) {
            await ctx.SendPrivateResponse($"Player '{username}' not found");
            return;
        }
        target.TeamColor = TeamColor.None;
        target.Team = null;
        await ctx.SendPrivateResponse($"Switched '{username}' to team none");
    }
    [ChatCommand("weaponname", "Tries to tell the name of target's shell and weapon"), RequireConditions(ChatCommandConditions.InBattle)]
    public async Task FetchWeaponName(
        ChatCommandContext ctx,
        [Option("username", "Username ")]
        string username) {
        BattlePlayer? target = ctx.Connection.BattlePlayer!.Battle.Players
            .Where(player => player.InBattleAsTank)
            .SingleOrDefault(conn => conn.PlayerConnection.Player.Username == username);
        if (target == null) {
            await ctx.SendPrivateResponse($"Player '{username}' not found");
            return;
        }
        await ctx.SendPrivateResponse($"'{username}' shell is {target.Tank!.Shell.TemplateAccessor?.ConfigPath?.Split("/").Last()}");
        await ctx.SendPrivateResponse($"'{username}' weapon is {target.Tank!.Weapon.TemplateAccessor?.ConfigPath?.Split("/").Last()}");
    }
    [ChatCommand("hullstats", "Tries to tell the stats of the hull"), RequireConditions(ChatCommandConditions.InBattle)]
    public async Task FetchHullStats(
        ChatCommandContext ctx,
        [Option("username", "Username ")]
        string username) {
        BattlePlayer? target = ctx.Connection.BattlePlayer!.Battle.Players
            .Where(player => player.InBattleAsTank)
            .SingleOrDefault(conn => conn.PlayerConnection.Player.Username == username);
        if (target == null) {
            await ctx.SendPrivateResponse($"Player '{username}' not found");
            return;
        }

        await ctx.SendPrivateResponse($"'{username}' hull's weight is {target.Tank!.Weight.Weight}");
        await ctx.SendPrivateResponse($"'{username}' hull's config path is {target.Tank!.Weight}");
    }
}
