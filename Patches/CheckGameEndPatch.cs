using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.AddOns.Common;
using EHR.AddOns.GhostRoles;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.CustomWinnerHolder;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class GameEndChecker
{
    private const float EndGameDelay = 0.2f;
    public static GameEndPredicate Predicate;
    public static bool ShouldNotCheck = false;
    public static bool Ended;

    public static bool Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (Predicate == null || ShouldNotCheck || Main.HasJustStarted) return false;
        if (Options.NoGameEnd.GetBool() && WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return false;

        Ended = false;

        Predicate.CheckForGameEnd(out GameOverReason reason);

        if (!CustomGameMode.Standard.IsActiveOrIntegrated())
        {
            if (WinnerIds.Count > 0 || WinnerTeam != CustomWinner.Default)
            {
                Ended = true;
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                Predicate = null;
            }

            return false;
        }

        if (WinnerTeam != CustomWinner.Default)
        {
            Ended = true;
            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, true, true, true));
            
            NameNotifyManager.Reset();
            NotifyRoles(ForceLoop: true);

            int saboWinner = Options.WhoWinsBySabotageIfNoImpAlive.GetValue();

            if (reason == GameOverReason.ImpostorBySabotage && saboWinner != 0 && !Main.AllAlivePlayerControls.Any(x => x.Is(Team.Impostor)))
            {
                bool anyNKAlive = Main.AllAlivePlayerControls.Any(x => x.IsNeutralKiller());
                bool anyCovenAlive = Main.AllPlayerControls.Any(x => x.Is(Team.Coven));

                switch (saboWinner)
                {
                    case 1 when anyNKAlive:
                        NKWins();
                        break;
                    case 2 when anyCovenAlive:
                        CovenWins();
                        break;
                    default:
                        switch (Options.IfSelectedTeamIsDead.GetValue())
                        {
                            case 0:
                                goto Continue;
                            case 1:
                                NKWins();
                                break;
                            case 2:
                                CovenWins();
                                break;
                        }

                        break;
                }

                void NKWins()
                {
                    ResetAndSetWinner(CustomWinner.Neutrals);
                    WinnerIds.UnionWith(Main.AllAlivePlayerControls.Where(x => x.IsNeutralKiller()).Select(x => x.PlayerId));
                }

                void CovenWins()
                {
                    ResetAndSetWinner(CustomWinner.Coven);
                    WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId));
                }
            }

            Continue:

            switch (WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoleTypes.Crewmate) || (pc.Is(CustomRoles.Haunter) && Haunter.CanWinWithCrew(pc))) && !pc.IsMadmate() && !pc.IsConverted() && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Impostor:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.IsMadmate()) && !pc.IsConverted() && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Coven:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(Team.Coven) && !pc.IsMadmate() && !pc.IsConverted() && !pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Succubus:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Succubus) || pc.Is(CustomRoles.Charmed))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Necromancer:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Necromancer) || pc.Is(CustomRoles.Deathknight) || pc.Is(CustomRoles.Undead))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Virus:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || pc.Is(CustomRoles.Contagious))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Jackal:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit))
                        .Select(pc => pc.PlayerId));

                    break;
                case CustomWinner.Spiritcaller:
                    WinnerIds.UnionWith(Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit))
                        .Select(pc => pc.PlayerId));

                    WinnerRoles.Add(CustomRoles.Spiritcaller);
                    break;
                case CustomWinner.RuthlessRomantic:
                    WinnerIds.Add(Romantic.PartnerId);
                    break;
            }

            if (WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.DarkHide when pc.IsAlive() && ((WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorBySabotage)) || WinnerTeam == CustomWinner.DarkHide || (WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.HumansByTask) && Main.PlayerStates[pc.PlayerId].Role is DarkHide { IsWinKill: true } && DarkHide.SnatchesWin.GetBool())):
                            ResetAndSetWinner(CustomWinner.DarkHide);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantasm when pc.GetTaskState().RemainingTasksCount <= 0 && !pc.IsAlive() && Options.PhantomSnatchesWin.GetBool():
                            reason = GameOverReason.ImpostorByKill;
                            ResetAndSetWinner(CustomWinner.Phantom);
                            WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantasm when pc.GetTaskState().RemainingTasksCount <= 0 && !pc.IsAlive() && !Options.PhantomSnatchesWin.GetBool():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Phantom);
                            break;
                        case CustomRoles.Opportunist when pc.IsAlive():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Opportunist);
                            break;
                        case CustomRoles.Pursuer when pc.IsAlive() && WinnerTeam is not CustomWinner.Jester and not CustomWinner.Lovers and not CustomWinner.Terrorist and not CustomWinner.Executioner and not CustomWinner.Collector and not CustomWinner.Innocent and not CustomWinner.Youtuber:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Pursuer);
                            break;
                        case CustomRoles.Sunnyboy when !pc.IsAlive():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Sunnyboy);
                            break;
                        case CustomRoles.Maverick when pc.IsAlive() && Main.PlayerStates[pc.PlayerId].Role is Maverick mr && mr.NumOfKills >= Maverick.MinKillsToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Maverick);
                            break;
                        case CustomRoles.Provocateur when Provocateur.Provoked.TryGetValue(pc.PlayerId, out byte tar) && !WinnerIds.Contains(tar):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                            break;
                        case CustomRoles.FFF when (Main.PlayerStates[pc.PlayerId].Role as FFF).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                            break;
                        case CustomRoles.Totocalcio when Main.PlayerStates[pc.PlayerId].Role is Totocalcio tc && tc.BetPlayer != byte.MaxValue && (WinnerIds.Contains(tc.BetPlayer) || (Main.PlayerStates.TryGetValue(tc.BetPlayer, out PlayerState ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust))))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                            break;
                        case CustomRoles.Romantic when WinnerIds.Contains(Romantic.PartnerId) || (Main.PlayerStates.TryGetValue(Romantic.PartnerId, out PlayerState ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust)))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.HasKilledKiller:
                            WinnerIds.Add(pc.PlayerId);
                            WinnerIds.Add(Romantic.PartnerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                            break;
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out byte lawyertarget) && (WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out PlayerState ps) && (WinnerRoles.Contains(ps.MainRole) || (WinnerTeam == CustomWinner.Bloodlust && ps.SubRoles.Contains(CustomRoles.Bloodlust))))):
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                            break;
                        case CustomRoles.Postman when (Main.PlayerStates[pc.PlayerId].Role as Postman).IsFinished:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Postman);
                            break;
                        case CustomRoles.Impartial when (Main.PlayerStates[pc.PlayerId].Role as Impartial).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Impartial);
                            break;
                        case CustomRoles.Tank when (Main.PlayerStates[pc.PlayerId].Role as Tank).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Tank);
                            break;
                        case CustomRoles.Technician when (Main.PlayerStates[pc.PlayerId].Role as Technician).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Technician);
                            break;
                        case CustomRoles.Backstabber when (Main.PlayerStates[pc.PlayerId].Role as Backstabber).CheckWin():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Backstabber);
                            break;
                        case CustomRoles.Predator when (Main.PlayerStates[pc.PlayerId].Role as Predator).IsWon:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Predator);
                            break;
                        case CustomRoles.Gaslighter when (Main.PlayerStates[pc.PlayerId].Role as Gaslighter).AddAsAdditionalWinner():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Gaslighter);
                            break;
                        case CustomRoles.SoulHunter when (Main.PlayerStates[pc.PlayerId].Role as SoulHunter).Souls >= SoulHunter.NumOfSoulsToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.SoulHunter);
                            break;
                        case CustomRoles.SchrodingersCat when WinnerTeam == CustomWinner.Crewmate && SchrodingersCat.WinsWithCrewIfNotAttacked.GetBool():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.SchrodingersCat);
                            break;
                        case CustomRoles.SchrodingersCat when !pc.IsConverted():
                            WinnerIds.Remove(pc.PlayerId);
                            break;
                        case CustomRoles.Curser when WinnerTeam != CustomWinner.Crewmate:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.Curser);
                            break;
                        case CustomRoles.NoteKiller when !NoteKiller.CountsAsNeutralKiller && NoteKiller.Kills >= NoteKiller.NumKillsNeededToWin:
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.NoteKiller);
                            break;
                        case CustomRoles.NecroGuesser when (Main.PlayerStates[pc.PlayerId].Role as NecroGuesser).GuessedPlayers >= NecroGuesser.NumGuessesToWin.GetInt():
                            WinnerIds.Add(pc.PlayerId);
                            AdditionalWinnerTeams.Add(AdditionalWinners.NecroGuesser);
                            break;
                    }
                }

                if (WinnerTeam == CustomWinner.Impostor)
                {
                    IEnumerable<PlayerControl> aliveImps = Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoleTypes.Impostor));
                    PlayerControl[] imps = aliveImps as PlayerControl[] ?? aliveImps.ToArray();
                    int aliveImpCount = imps.Length;

                    switch (aliveImpCount)
                    {
                        // If there's an Egoist, and there is at least 1 non-Egoist impostor alive, Egoist loses
                        case > 1 when WinnerIds.Any(x => GetPlayerById(x).Is(CustomRoles.Egoist)):
                            WinnerIds.RemoveWhere(x => GetPlayerById(x).Is(CustomRoles.Egoist));
                            break;
                        // If there's only 1 impostor alive, and all living impostors are Egoists, the Egoist wins alone
                        case 1 when imps.All(x => x.Is(CustomRoles.Egoist)):
                            PlayerControl pc = imps[0];
                            reason = GameOverReason.ImpostorByKill;
                            WinnerTeam = CustomWinner.Egoist;
                            WinnerIds.RemoveWhere(x => Main.PlayerStates[x].MainRole.IsImpostor() || x.GetPlayer().IsMadmate());
                            WinnerIds.Add(pc.PlayerId);
                            break;
                    }
                }

                byte[] winningSpecters = GhostRolesManager.AssignedGhostRoles.Where(x => x.Value.Instance is Specter { IsWon: true }).Select(x => x.Key).ToArray();

                if (winningSpecters.Length > 0)
                {
                    AdditionalWinnerTeams.Add(AdditionalWinners.Specter);
                    WinnerIds.UnionWith(winningSpecters);
                }

                if (CustomRoles.God.RoleExist())
                {
                    ResetAndSetWinner(CustomWinner.God);

                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.God) && p.IsAlive())
                        .Do(p => WinnerIds.Add(p.PlayerId));
                }

                if (WinnerTeam != CustomWinner.CustomTeam && CustomTeamManager.EnabledCustomTeams.Count > 0)
                {
                    Main.AllPlayerControls
                        .Select(x => new { Team = CustomTeamManager.GetCustomTeam(x.PlayerId), Player = x })
                        .Where(x => x.Team != null)
                        .GroupBy(x => x.Team)
                        .ToDictionary(x => x.Key, x => x.Select(y => y.Player.PlayerId))
                        .Do(x =>
                        {
                            bool canWin = CustomTeamManager.IsSettingEnabledForTeam(x.Key, CTAOption.WinWithOriginalTeam);

                            if (!canWin)
                                WinnerIds.ExceptWith(x.Value);
                            else
                                WinnerIds.UnionWith(x.Value);
                        });
                }

                if ((WinnerTeam == CustomWinner.Lovers || WinnerIds.Any(x => Main.PlayerStates[x].SubRoles.Contains(CustomRoles.Lovers))) && Main.LoversPlayers.TrueForAll(x => x.IsAlive()) && reason != GameOverReason.HumansByTask)
                {
                    if (WinnerTeam != CustomWinner.Lovers) AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);

                    WinnerIds.UnionWith(Main.LoversPlayers.Select(x => x.PlayerId));
                }

                if (Options.NeutralWinTogether.GetBool() && (WinnerRoles.Any(x => x.IsNeutral()) || WinnerIds.Select(x => GetPlayerById(x)).Any(x => x != null && x.GetCustomRole().IsNeutral() && !x.IsMadmate())))
                    WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole().IsNeutral()).Select(x => x.PlayerId));
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (byte id in WinnerIds.ToArray())
                    {
                        PlayerControl pc = GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;

                        foreach (PlayerControl tar in Main.AllPlayerControls)
                            if (!WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                WinnerIds.Add(tar.PlayerId);
                    }

                    foreach (CustomRoles role in WinnerRoles) WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.GetCustomRole() == role).Select(x => x.PlayerId));
                }

                WinnerIds.RemoveWhere(x => Main.PlayerStates[x].MainRole == CustomRoles.Shifter);
            }

            Camouflage.BlockCamouflage = true;
            ShipStatus.Instance.enabled = false;
            StartEndGame(reason);
            Predicate = null;
        }

        return false;
    }

    private static void StartEndGame(GameOverReason reason)
    {
        try { LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.Ended); }
        catch (Exception e) { ThrowException(e); }

        string msg = GetString("NotifyGameEnding");
        PlayerControl sender = Main.AllAlivePlayerControls.FirstOrDefault();
        if (sender == null) sender = PlayerControl.LocalPlayer;

        Main.AllPlayerControls.DoIf(
            x => x.GetClient() != null && !x.Data.Disconnected,
            x => ChatUpdatePatch.SendMessage(sender, "\n", x.PlayerId, msg));

        SetEverythingUpPatch.LastWinsReason = WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : string.Empty;
        var self = AmongUsClient.Instance;
        self.StartCoroutine(CoEndGame(self, reason).WrapToIl2Cpp());

        Statistics.OnGameEnd();
    }

    private static IEnumerator CoEndGame(InnerNetClient self, GameOverReason reason)
    {
        Silencer.ForSilencer.Clear();

        // Set ghost role
        List<byte> ReviveRequiredPlayerIds = [];
        CustomWinner winner = WinnerTeam;

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (winner == CustomWinner.Draw)
            {
                SetGhostRole(true);
                continue;
            }

            bool canWin = WinnerIds.Contains(pc.PlayerId) || WinnerRoles.Contains(pc.GetCustomRole()) || (winner == CustomWinner.Bloodlust && pc.Is(CustomRoles.Bloodlust));
            bool isCrewmateWin = reason.Equals(GameOverReason.HumansByVote) || reason.Equals(GameOverReason.HumansByTask);
            SetGhostRole(canWin ^ isCrewmateWin); // XOR
            continue;

            void SetGhostRole(bool ToGhostImpostor)
            {
                if (!pc.Data.IsDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);

                if (ToGhostImpostor)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: changed to ImpostorGhost", "ResetRoleAndEndGame");
                    pc.RpcSetRole(RoleTypes.ImpostorGhost);
                }
                else
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: changed to CrewmateGhost", "ResetRoleAndEndGame");
                    pc.RpcSetRole(RoleTypes.CrewmateGhost);
                }
            }
        }

        // Sync of CustomWinnerHolder info
        MessageWriter winnerWriter = self.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame, HazelExtensions.SendOption);
        WriteTo(winnerWriter);
        self.FinishRpcImmediately(winnerWriter);

        // Delay to ensure that resuscitation is delivered after the ghost roll setting
        yield return new WaitForSeconds(EndGameDelay);

        if (ReviveRequiredPlayerIds.Count > 0)
        {
            // Resuscitation Resuscitate one person per transmission to prevent the packet from swelling up and dying
            foreach (byte playerId in ReviveRequiredPlayerIds)
            {
                NetworkedPlayerInfo playerInfo = GameData.Instance.GetPlayerById(playerId);
                // resuscitation
                playerInfo.IsDead = false;
                // transmission
                playerInfo.SetDirtyBit(0b_1u << playerId);
                self.SendAllStreamedObjects();
            }

            // Delay to ensure that the end of the game is delivered at the end of the game
            yield return new WaitForSeconds(EndGameDelay);
        }

        // Start End Game
        GameManager.Instance.RpcEndGame(reason, false);
    }

    public static void SetPredicateToNormal()
    {
        Predicate = new NormalGameEndPredicate();
    }

    public static void SetPredicateToSoloKombat()
    {
        Predicate = new SoloKombatGameEndPredicate();
    }

    public static void SetPredicateToFFA()
    {
        Predicate = new FFAGameEndPredicate();
    }

    public static void SetPredicateToMoveAndStop()
    {
        Predicate = new MoveAndStopGameEndPredicate();
    }

    public static void SetPredicateToHotPotato()
    {
        Predicate = new HotPotatoGameEndPredicate();
    }

    public static void SetPredicateToSpeedrun()
    {
        Predicate = new SpeedrunGameEndPredicate();
    }

    public static void SetPredicateToHideAndSeek()
    {
        Predicate = new HideAndSeekGameEndPredicate();
    }

    public static void SetPredicateToCaptureTheFlag()
    {
        Predicate = new CaptureTheFlagGameEndPredicate();
    }

    public static void SetPredicateToNaturalDisasters()
    {
        Predicate = new NaturalDisastersGameEndPredicate();
    }

    public static void SetPredicateToRoomRush()
    {
        Predicate = new RoomRushGameEndPredicate();
    }

    public static void SetPredicateToAllInOne()
    {
        Predicate = new AllInOneGameEndPredicate();
    }

    private class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (WinnerTeam != CustomWinner.Default) return false;

            return CheckGameEndBySabotage(out reason) || CheckGameEndByTask(out reason) || CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (Main.HasJustStarted) return false;

            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (CustomRoles.Sunnyboy.RoleExist() && aapc.Length > 1) return false;

            if (CustomTeamManager.CheckCustomTeamGameEnd()) return true;

            if (aapc.All(x => Main.LoversPlayers.Exists(l => l.PlayerId == x.PlayerId)) && (!Main.LoversPlayers.TrueForAll(x => x.Is(Team.Crewmate)) || !Lovers.CrewLoversWinWithCrew.GetBool()))
            {
                ResetAndSetWinner(CustomWinner.Lovers);
                WinnerIds.UnionWith(Main.LoversPlayers.ConvertAll(x => x.PlayerId));
                return true;
            }

            int sheriffCount = AlivePlayersCount(CountTypes.Sheriff);

            int Imp = AlivePlayersCount(CountTypes.Impostor);
            int Crew = AlivePlayersCount(CountTypes.Crew) + sheriffCount;
            int Coven = AlivePlayersCount(CountTypes.Coven);

            Dictionary<(CustomRoles? Role, CustomWinner Winner), int> roleCounts = [];

            foreach (CustomRoles role in Enum.GetValues<CustomRoles>())
            {
                if ((!role.IsNK() && role is not CustomRoles.Bloodlust and not CustomRoles.Gaslighter) || role.IsMadmate() || role is CustomRoles.Sidekick) continue;

                CountTypes countTypes = role.GetCountTypes();
                if (countTypes is CountTypes.Crew or CountTypes.Impostor or CountTypes.None or CountTypes.OutOfGame or CountTypes.CustomTeam or CountTypes.Coven) continue;

                CustomRoles? keyRole = role.IsRecruitingRole() ? null : role;
                var keyWinner = (CustomWinner)role;
                int value = AlivePlayersCount(countTypes);

                roleCounts[(keyRole, keyWinner)] = value;
            }

            if (CustomRoles.DualPersonality.IsEnable())
            {
                foreach (PlayerControl x in aapc)
                {
                    if (!x.Is(CustomRoles.DualPersonality)) continue;

                    if (x.Is(Team.Impostor)) Imp++;
                    else if (x.Is(Team.Crewmate)) Crew++;
                    else if (x.Is(Team.Coven)) Coven++;

                    if (x.Is(CustomRoles.Charmed)) roleCounts[(null, CustomWinner.Succubus)]++;
                    if (x.Is(CustomRoles.Undead)) roleCounts[(null, CustomWinner.Necromancer)]++;
                    if (x.Is(CustomRoles.Sidekick)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Recruit)) roleCounts[(null, CustomWinner.Jackal)]++;
                    if (x.Is(CustomRoles.Contagious)) roleCounts[(null, CustomWinner.Virus)]++;
                }
            }

            int totalNKAlive = roleCounts.Values.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

            if (totalNKAlive == 0)
            {
                if (Coven == 0)
                {
                    if (Crew == 0 && Imp == 0)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.None;
                    }
                    else if (Crew <= Imp && sheriffCount == 0)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Impostor;
                    }
                    else if (Imp == 0)
                    {
                        reason = GameOverReason.HumansByVote;
                        winner = CustomWinner.Crewmate;
                    }
                    else
                        return false;

                    Logger.Info($"Crew: {Crew}, Imp: {Imp}, Coven: {Coven}", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    ResetAndSetWinner((CustomWinner)winner);
                }
                else
                {
                    if (Imp >= 1) return false;
                    if (Crew > Coven) return false;
                    if (sheriffCount > 0) return false;

                    Logger.Info($"Crew: {Crew}, Imp: {Imp}, Coven: {Coven}", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    reason = GameOverReason.ImpostorByKill;
                    ResetAndSetWinner(CustomWinner.Coven);
                }

                return true;
            }

            if (Imp >= 1) return false; // both imps and NKs are alive, game must continue

            if (Crew > totalNKAlive) return false; // Imps are dead, but crew still outnumbers NKs, game must continue

            // Imps dead, Crew <= NK, Checking if all NKs alive are in 1 team
            List<int> aliveCounts = roleCounts.Values.Where(x => x > 0).ToList();

            switch (aliveCounts.Count)
            {
                // There are multiple types of NKs alive, the game must continue
                // If the Sheriff keeps the game going, the game must continue
                case > 1:
                case 1 when Sheriff.KeepsGameGoing.GetBool() && sheriffCount > 0:
                    return false;
                // There is only one type of NK alive, they've won
                case 1:
                {
                    if (aliveCounts[0] != roleCounts.Values.Max()) Logger.Warn("There is something wrong here.", "CheckGameEndPatch");

                    foreach (KeyValuePair<(CustomRoles? Role, CustomWinner Winner), int> keyValuePair in roleCounts.Where(keyValuePair => keyValuePair.Value == aliveCounts[0]))
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = keyValuePair.Key.Winner;
                        rl = keyValuePair.Key.Role;
                        break;
                    }

                    break;
                }
                default:
                    Logger.Fatal("Error while selecting NK winner", "CheckGameEndPatch.CheckGameEndByLivingPlayers");
                    Logger.SendInGame("There was an error while selecting the winner. Please report this bug to the developer! (Do /dump to get logs)");
                    ResetAndSetWinner(CustomWinner.Error);
                    return true;
            }

            if (winner != null) ResetAndSetWinner((CustomWinner)winner);

            if (rl != null) WinnerRoles.Add((CustomRoles)rl);

            return true;
        }
    }

    private class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (SoloPVP.RoundTime > 0) return false;

            HashSet<byte> winners = [Main.AllPlayerControls.FirstOrDefault(x => !x.Is(CustomRoles.GM) && SoloPVP.GetRankFromScore(x.PlayerId) == 1)?.PlayerId ?? Main.AllAlivePlayerControls[0].PlayerId];
            int kills = SoloPVP.KBScore[winners.First()];
            winners.UnionWith(SoloPVP.KBScore.Where(x => x.Value == kills).Select(x => x.Key));

            WinnerIds = winners;

            Main.DoBlockNameChange = true;

            return true;
        }
    }

    private class FFAGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (FreeForAll.RoundTime <= 0)
            {
                PlayerControl winner = Main.GM.Value && Main.AllPlayerControls.Length == 1 ? PlayerControl.LocalPlayer : Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => FreeForAll.GetRankFromScore(x.PlayerId)).First();

                byte winnerId = winner.PlayerId;

                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                WinnerIds = [winnerId];

                Main.DoBlockNameChange = true;

                return true;
            }

            if (FreeForAll.FFATeamMode.GetBool())
            {
                IEnumerable<HashSet<byte>> teams = FreeForAll.PlayerTeams.GroupBy(x => x.Value, x => x.Key).Select(x => x.Where(p =>
                {
                    PlayerControl pc = GetPlayerById(p);
                    return pc != null && !pc.Data.Disconnected;
                }).ToHashSet()).Where(x => x.Count > 0);

                foreach (HashSet<byte> team in teams)
                {
                    if (Main.AllAlivePlayerControls.All(x => team.Contains(x.PlayerId)))
                    {
                        WinnerIds = team;

                        Main.DoBlockNameChange = true;
                        return true;
                    }
                }
            }

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                {
                    PlayerControl winner = Main.AllAlivePlayerControls[0];

                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "FFA");

                    WinnerIds =
                    [
                        winner.PlayerId
                    ];

                    Main.DoBlockNameChange = true;

                    return true;
                }
                case 0:
                    FreeForAll.RoundTime = 0;
                    Logger.Warn("No players alive. Force ending the game", "FFA");
                    return false;
                default:
                    return false;
            }
        }
    }

    private class MoveAndStopGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (MoveAndStop.RoundTime <= 0)
            {
                PlayerControl[] apc = Main.AllPlayerControls;
                SetWinner(Main.GM.Value && apc.Length == 1 ? PlayerControl.LocalPlayer : apc.Where(x => !x.Is(CustomRoles.GM) && x != null).OrderBy(x => MoveAndStop.GetRankFromScore(x.PlayerId)).ThenByDescending(x => x.IsAlive()).First());
                return true;
            }

            PlayerControl[] aapc = Main.AllAlivePlayerControls;

            if (aapc.Any(x => x.GetTaskState().IsTaskFinished))
            {
                SetWinner(aapc.First(x => x.GetTaskState().IsTaskFinished));
                return true;
            }

            switch (aapc.Length)
            {
                case 1 when !GameStates.IsLocalGame:
                    SetWinner(aapc[0]);
                    return true;
                case 0:
                    MoveAndStop.RoundTime = 0;
                    Logger.Warn("No players alive. Force ending the game", "MoveAndStop");
                    return false;
            }

            return false;

            void SetWinner(PlayerControl winner)
            {
                Logger.Warn($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "MoveAndStop");
                WinnerIds = [winner.PlayerId];
                Main.DoBlockNameChange = true;
            }
        }
    }

    private class HotPotatoGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                    PlayerControl winner = Main.AllAlivePlayerControls[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "HotPotato");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.Error);
                    Logger.Warn("No players alive. Force ending the game", "HotPotato");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class HideAndSeekGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return CustomHnS.CheckForGameEnd(out reason);
        }
    }

    private class SpeedrunGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return Speedrun.CheckForGameEnd(out reason);
        }
    }

    private class CaptureTheFlagGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            return CaptureTheFlag.CheckForGameEnd(out reason);
        }
    }

    private class NaturalDisastersGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            switch (Main.AllAlivePlayerControls.Length)
            {
                case 1:
                    PlayerControl winner = Main.AllAlivePlayerControls[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "NaturalDisasters");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.Error);
                    Logger.Warn("No players alive. Force ending the game", "NaturalDisasters");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class RoomRushGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            PlayerControl[] appc = Main.AllAlivePlayerControls;

            switch (appc.Length)
            {
                case 1:
                    PlayerControl winner = appc[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "RoomRush");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.None);
                    Logger.Warn("No players alive. Force ending the game", "RoomRush");
                    return true;
                default:
                    return false;
            }
        }
    }

    private class AllInOneGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            return WinnerIds.Count <= 0 && CheckGameEndByLivingPlayers(out reason);
        }

        private static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            PlayerControl[] appc = Main.AllAlivePlayerControls;

            switch (appc.Length)
            {
                case 1:
                    PlayerControl winner = appc[0];
                    Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "AllInOne");
                    WinnerIds = [winner.PlayerId];
                    Main.DoBlockNameChange = true;
                    return true;
                case 0:
                    ResetAndSetWinner(CustomWinner.None);
                    Logger.Warn("No players alive. Force ending the game", "AllInOne");
                    return true;
                default:
                    return false;
            }
        }
    }

    public abstract class GameEndPredicate
    {
        /// <summary>Checks the game ending condition and stores the value in CustomWinnerHolder. </summary>
        /// <params name="reason">GameOverReason used for vanilla game end processing</params>
        /// <returns>Whether the conditions for ending the game are met</returns>
        public abstract bool CheckForGameEnd(out GameOverReason reason);

        /// <summary>Determine whether the task can be won based on GameData.TotalTasks and CompletedTasks.</summary>
        public virtual bool CheckGameEndByTask(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0) return false;

            if (Options.DisableTaskWinIfAllCrewsAreDead.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoleTypes.Crewmate))) return false;

            if (Options.DisableTaskWinIfAllCrewsAreConverted.GetBool() && Main.AllAlivePlayerControls.Where(x => x.Is(Team.Crewmate) && x.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel).All(x => x.IsConverted())) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.HumansByTask;
                ResetAndSetWinner(CustomWinner.Crewmate);
                return true;
            }

            return false;
        }

        /// <summary>Determines whether sabotage victory is possible based on the elements in ShipStatus.Systems.</summary>
        public virtual bool CheckGameEndBySabotage(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (ShipStatus.Instance.Systems == null) return false;

            // TryGetValue is not available
            Il2CppSystem.Collections.Generic.Dictionary<SystemTypes, ISystemType> systems = ShipStatus.Instance.Systems;
            LifeSuppSystemType LifeSupp;

            if (systems.ContainsKey(SystemTypes.LifeSupp) && // Confirmation of sabotage existence
                (LifeSupp = systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>()) != null && // Confirmation that cast is possible
                LifeSupp.Countdown <= 0f) // Time up confirmation
            {
                // oxygen sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                LifeSupp.Countdown = 10000f;
                return true;
            }

            ISystemType sys = null;

            if (systems.ContainsKey(SystemTypes.Reactor))
                sys = systems[SystemTypes.Reactor];
            else if (systems.ContainsKey(SystemTypes.Laboratory))
                sys = systems[SystemTypes.Laboratory];
            else if (systems.ContainsKey(SystemTypes.HeliSabotage)) sys = systems[SystemTypes.HeliSabotage];

            ICriticalSabotage critical;

            if (sys != null && // Confirmation of sabotage existence
                (critical = sys.TryCast<ICriticalSabotage>()) != null && // Confirmation that cast is possible
                critical.Countdown <= 0f) // Time up confirmation
            {
                // reactor sabotage
                ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckEndGameViaTasks))]
internal static class CheckGameEndPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (GameEndChecker.ShouldNotCheck)
        {
            __result = false;
            return false;
        }

        __result = GameEndChecker.Predicate?.CheckGameEndByTask(out _) ?? false;
        return false;
    }
}