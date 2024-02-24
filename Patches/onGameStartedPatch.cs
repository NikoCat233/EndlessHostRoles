using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using TOHE.Modules;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using static TOHE.Modules.CustomRoleSelector;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
internal class ChangeRoleSettings
{
    public static void Postfix(AmongUsClient __instance)
    {
        Main.OverrideWelcomeMsg = string.Empty;
        try
        {
            //注:この時点では役職は設定されていません。
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
            if (Options.DisableVanillaRoles.GetBool())
            {
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
                Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
            }

            Main.PlayerStates = [];

            Main.AbilityUseLimit = [];

            Main.HasJustStarted = true;

            Main.AllPlayerKillCooldown = [];
            Main.AllPlayerSpeed = [];
            Main.KillTimers = [];
            Main.WarlockTimer = [];
            Main.AssassinTimer = [];
            Main.UndertakerTimer = [];
            Main.isDoused = [];
            Main.isDraw = [];
            Main.isRevealed = [];
            Main.ArsonistTimer = [];
            Main.RevolutionistTimer = [];
            Main.RevolutionistStart = [];
            Main.RevolutionistLastTime = [];
            Main.RevolutionistCountdown = [];
            Main.TimeMasterBackTrack = [];
            Main.TimeMasterNum = [];
            Farseer.FarseerTimer = [];
            Main.CursedPlayers = [];
            Main.MafiaRevenged = [];
            Main.RetributionistRevenged = [];
            Main.isCurseAndKill = [];
            Main.isCursed = false;
            Main.PuppeteerList = [];
            Main.PuppeteerDelayList = [];
            Main.TaglockedList = [];
            Main.DetectiveNotify = [];
            Main.SleuthMsgs = [];
            Main.ForCrusade = [];
            Main.KillGhoul = [];
            Main.CyberStarDead = [];
            Main.DemolitionistDead = [];
            Main.KilledDiseased = [];
            Main.KilledAntidote = [];
            Main.WorkaholicAlive = [];
            Main.SpeedrunnerAlive = [];
            Main.BaitAlive = [];
            Main.BoobyTrapBody = [];
            BoobyTrap.KillerOfBoobyTrapBody = [];
            Main.CleanerBodies = [];
            Main.MedusaBodies = [];
            Main.InfectedBodies = [];
            Main.VirusNotify = [];
            Main.DontCancelVoteList = [];

            Main.LastEnteredVent = [];
            Main.LastEnteredVentLocation = [];

            Main.AfterMeetingDeathPlayers = [];
            Main.ResetCamPlayerList = [];
            Main.clientIdList = [];

            Main.CheckShapeshift = [];
            Main.ShapeshiftTarget = [];
            Main.SpeedBoostTarget = [];
            Main.MayorUsedButtonCount = [];
            Main.ParaUsedButtonCount = [];
            Main.MarioVentCount = [];
            Main.VeteranInProtect = [];
            Witness.AllKillers = [];
            Main.GrenadierBlinding = [];
            Main.Lighter = [];
            Main.BlockSabo = [];
            Main.BlockedVents = [];
            Grenadier.MadGrenadierBlinding = [];
            Main.JinxSpellCount = [];
            Main.PuppeteerDelay = [];
            Main.PuppeteerMaxPuppets = [];
            Main.OverDeadPlayerList = [];
            Main.Provoked = [];
            Main.ShieldPlayer = Options.ShieldPersonDiedFirst.GetBool() ? Main.FirstDied : byte.MaxValue;
            Main.FirstDied = byte.MaxValue;
            Main.MadmateNum = 0;
            Godfather.GodfatherTarget = byte.MaxValue;
            ChatManager.ResetHistory();

            ReportDeadBodyPatch.CanReport = [];

            Options.UsedButtonCount = 0;

            GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor = false;
            if (Options.CurrentGameMode == CustomGameMode.MoveAndStop) GameOptionsManager.Instance.currentNormalGameOptions.NumImpostors = 0;
            Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);

            Main.introDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = [];

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = [];

            Main.currentDousingTarget = byte.MaxValue;
            Main.currentDrawTarget = byte.MaxValue;
            Main.PlayerColors = [];

            //名前の記録
            //Main.AllPlayerNames = new();
            RPC.SyncAllPlayerNames();

            Camouflage.Init();
            var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId);
            if (invalidColor.Any())
            {
                var msg = GetString("Error.InvalidColor");
                Logger.SendInGame(msg);
                msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.name}"));
                Utils.SendMessage(msg);
                Logger.Error(msg, "CoStartGame");
            }

            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    var pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
            }
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                var colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.FormatNameMode.GetInt() == 1)
                    pc.RpcSetName(Palette.GetColorName(colorId));
                Main.PlayerStates[pc.PlayerId] = new(pc.PlayerId);
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = [];
                pc.cosmetics.nameText.text = pc.name;
                RandomSpawn.CustomNetworkTransformPatch.NumOfTP.Add(pc.PlayerId, 0);
                var outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId, outfit.NamePlateId);
                Main.clientIdList.Add(pc.GetClientId());
            }
            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
                Main.RefixCooldownDelay = 0;
            }
            FallFromLadder.Reset();

            try
            {

                BountyHunter.Init();
                SerialKiller.Init();
                EvilDiviner.Init();
                Kamikaze.Init();
                FireWorks.Init();
                NiceSwapper.Init();
                Pickpocket.Init();
                Sniper.Init();
                Farseer.Init();
                Jailor.Init();
                Monitor.Init();
                Cleanser.Init();
                TimeThief.Init();
                Witch.Init();
                HexMaster.Init();
                SabotageMaster.Init();
                Executioner.Init();
                Lawyer.Init();
                Jackal.Init();
                Sidekick.Init();
                Bandit.Init();
                Sheriff.Init();
                CopyCat.Init();
                SwordsMan.Init();
                EvilTracker.Init();
                Snitch.Init();
                Vampire.Init();
                Poisoner.Init();
                AntiAdminer.Init();
                TimeManager.Init();
                LastImpostor.Init();
                TargetArrow.Init();
                LocateArrow.Init();
                DoubleTrigger.Init();
                Workhorse.Init();
                Pelican.Init();
                Tether.Init();
                Librarian.Init();
                Benefactor.Init();
                Aid.Init();
                DonutDelivery.Init();
                Rabbit.Init();
                Gaulois.Init();
                Analyzer.Init();
                Escort.Init();
                Marshall.Init();
                Consort.Init();
                Drainer.Init();
                Crusader.Init();
                Pursuer.Init();
                Gangster.Init();
                Medic.Init();
                Gamer.Init();
                BallLightning.Init();
                DarkHide.Init();
                Greedier.Init();
                Glitch.Init();
                Collector.Init();
                QuickShooter.Init();
                Camouflager.Init();
                Divinator.Init();
                Doormaster.Init();
                Ricochet.Init();
                Oracle.Init();
                Eraser.Init();
                Spy.Init();
                NiceEraser.Init();
                Assassin.Init();
                Undertaker.Init();
                Sans.Init();
                Juggernaut.Init();
                Hacker.Init();
                NiceHacker.Init();
                Psychic.Init();
                Hangman.Init();
                Judge.Init();
                Councillor.Init();
                Mortician.Init();
                Mediumshiper.Init();
                Swooper.Init();
                Wraith.Init();
                BloodKnight.Init();
                Totocalcio.Init();
                Romantic.Init();
                VengefulRomantic.Init();
                RuthlessRomantic.Init();
                Succubus.Init();
                Necromancer.Init();
                Nullifier.Init();
                Deputy.Init();
                Chronomancer.Init();
                Damocles.Initialize();
                Stressed.Init();
                Amnesiac.Init();
                Monarch.Init();
                Virus.Init();
                Bloodhound.Init();
                Tracker.Init();
                Merchant.Init();
                Mastermind.Init();
                Asthmatic.Init();
                Beacon.Init();
                NSerialKiller.Init();
                SoulHunter.Init();
                Enderman.Init();
                Mycologist.Init();
                Bubble.Init();
                Tornado.Init();
                Sentinel.Init();
                Hookshot.Init();
                Sprayer.Init();
                PlagueDoctor.Init();
                Penguin.Init();
                Stealth.Init();
                Postman.Init();
                Mafioso.Init();
                Magician.Init();
                WeaponMaster.Init();
                Reckless.Init();
                Pyromaniac.Init();
                Eclipse.Init();
                Vengeance.Init();
                HeadHunter.Init();
                Imitator.Init();
                Ignitor.Init();
                Werewolf.Init();
                Maverick.Init();
                Jinx.Init();
                DoubleShot.Init();
                Dazzler.Init();
                YinYanger.Init();
                Blackmailer.Init();
                Cantankerous.Init();
                Swiftclaw.Init();
                Mathematician.Init();
                Duellist.Init();
                Druid.Init();
                GuessManagerRole.Init();
                Randomizer.Init();
                Doppelganger.Init();
                FFF.Init();
                Sapper.Init();
                CameraMan.Init();
                Hitman.Init();
                Gambler.Init();
                RiftMaker.Init();
                Addict.Init();
                Alchemist.Init();
                Deathpact.Init();
                Tracefinder.Init();
                Devourer.Init();
                Ritualist.Init();
                Traitor.Init();
                Spiritualist.Init();
                Vulture.Init();
                Chameleon.Init();
                Wildling.Init();
                Morphling.Init();
                ParityCop.Init(); // *giggle* party cop
                Spiritcaller.Init();
                Enigma.Init();
                Lurker.Init();
                PlagueBearer.Init();
                Doomsayer.Init();
                Agitater.Init();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Init Roles");
            }

            Insight.KnownRolesOfPlayerIds = [];
            Crewpostor.TasksDone = [];
            Express.SpeedNormal = [];
            Express.SpeedUp = [];

            Main.ChangedRole = false;

            SoloKombatManager.Init();
            FFAManager.Init();
            MoveAndStopManager.Init();
            HotPotatoManager.Init();

            CustomWinnerHolder.Reset();
            AntiBlackout.Reset();
            NameNotifyManager.Reset();
            SabotageSystemTypeRepairDamagePatch.Initialize();
            DoorsReset.Initialize();

            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Change Role Setting Postfix");
            Logger.Fatal(ex.ToString(), "Change Role Setting Postfix");
        }
    }
}
[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
internal class SelectRolesPatch
{
    public static void Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            // Initializing CustomRpcSender and RpcSetRoleReplacer
            Dictionary<byte, CustomRpcSender> senders = [];
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false).StartMessage(pc.GetClientId());
            }
            RpcSetRoleReplacer.StartReplace(senders);

            if (Options.EnableGM.GetBool())
            {
                PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                PlayerControl.LocalPlayer.Data.IsDead = true;
                Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }


            SelectCustomRoles();
            SelectAddonRoles();
            CalculateVanillaRoleCount();

            //指定原版特殊职业数量
            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum + addScientistNum, addScientistNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum + addEngineerNum, addEngineerNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            roleOpt.SetRoleRate(RoleTypes.Shapeshifter, ShapeshifterNum + addShapeshifterNum, addShapeshifterNum > 0 ? 100 : roleOpt.GetChancePerGame(RoleTypes.Shapeshifter));

            Dictionary<(byte, byte), RoleTypes> rolesMap = [];

            // Register Desync Impostor Roles
            foreach (var kv in RoleResult.Where(x => x.Value.IsDesyncRole()))
                AssignDesyncRole(kv.Value, kv.Key, senders, rolesMap, BaseRole: kv.Value.GetDYRole());


            MakeDesyncSender(senders, rolesMap);

        }
        catch (Exception e)
        {
            Utils.ErrorEnd("Select Role Prefix");
            Logger.Fatal(e.Message, "Select Role Prefix");
        }
        // Below is the role assignment on the vanilla side.
    }

    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        try
        {
            RevivePreventerPatch.Ignore = true;

            var rd = IRandom.Instance;

            Main.NimblePlayer = byte.MaxValue;
            Main.PhysicistPlayer = byte.MaxValue;

            bool physicistSpawn = false;
            bool nimbleSpawn = false;

            if (rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Physicist, out var x) ? x.GetFloat() : 0) && CustomRoles.Physicist.IsEnable())
            {
                physicistSpawn = true;
            }
            if (rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(CustomRoles.Nimble, out var x2) ? x2.GetFloat() : 0) && CustomRoles.Nimble.IsEnable())
            {
                nimbleSpawn = true;
            }

            if (Options.EveryoneCanVent.GetBool())
            {
                nimbleSpawn = false;
                physicistSpawn = false;
            }

            List<byte> nimbleList = [];
            List<byte> physicistList = [];
            if (nimbleSpawn || physicistSpawn)
            {
                foreach ((PlayerControl PLAYER, RoleTypes _) in RpcSetRoleReplacer.StoragedData.ToArray())
                {
                    var kp = RoleResult.FirstOrDefault(x => x.Key.PlayerId == PLAYER.PlayerId);
                    if (kp.Value.IsCrewmate())
                    {
                        nimbleList.Add(PLAYER.PlayerId);
                        if (kp.Value.GetRoleTypes() == RoleTypes.Crewmate)
                            physicistList.Add(PLAYER.PlayerId);
                    }
                }
            }

            if (nimbleList.Count == 0) nimbleSpawn = false;
            if (physicistList.Count == 0) physicistSpawn = false;

            if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Nimble)))
            {
                nimbleSpawn = true;
                nimbleList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Nimble)).Select(x => x.Key).ToList();
            }

            if (Main.SetAddOns.Values.Any(x => x.Contains(CustomRoles.Physicist)))
            {
                physicistSpawn = true;
                var newPhysicistList = Main.SetAddOns.Where(x => x.Value.Contains(CustomRoles.Physicist)).Select(x => x.Key).ToList();
                if (nimbleList.Count != 1 || physicistList.Count != 1 || nimbleList[0] != newPhysicistList[0])
                {
                    physicistList = newPhysicistList;
                }
            }

            if (nimbleSpawn) Main.NimblePlayer = nimbleList[rd.Next(0, nimbleList.Count)];
            if (physicistSpawn) while (Main.PhysicistPlayer == byte.MaxValue || Main.PhysicistPlayer == Main.NimblePlayer)
                    Main.PhysicistPlayer = physicistList[rd.Next(0, physicistList.Count)];

            List<(PlayerControl, RoleTypes)> newList = [];
            foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in RpcSetRoleReplacer.StoragedData.ToArray())
            {
                var kp = RoleResult.FirstOrDefault(x => x.Key.PlayerId == PLAYER.PlayerId);
                RoleTypes roleType = kp.Value.GetRoleTypes();
                if (Main.NimblePlayer == PLAYER.PlayerId)
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Engineer;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Nimble, their role basis was changed to Engineer", "Nimble");
                    }
                    else
                    {
                        Logger.Info($"{PLAYER.GetRealName()} will be assigned Nimble, but their role is impostor based, so it won't be changed", "Nimble");
                    }
                }
                else if (Main.PhysicistPlayer == PLAYER.PlayerId)
                {
                    if (roleType == RoleTypes.Crewmate)
                    {
                        roleType = RoleTypes.Scientist;
                        Logger.Warn($"{PLAYER.GetRealName()} was assigned Physicist, their role basis was changed to Scientist", "Physicist");
                    }
                }
                if (Options.EveryoneCanVent.GetBool())
                {
                    if (roleType == RoleTypes.Crewmate || (roleType == RoleTypes.Scientist && Options.OverrideScientistBasedRoles.GetBool()))
                    {
                        roleType = RoleTypes.Engineer;
                        Logger.Info($"Everyone can vent => {PLAYER.GetRealName()}'s role was changed to Engineer", "SetRoleReplacer");
                    }
                }
                newList.Add((PLAYER, roleType));
                Logger.Warn(ROLETYPE == roleType ? $"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE}" : $"Register original role type => {PLAYER.GetRealName()}: {ROLETYPE} => {roleType}", "Override Role Select");
            }
            if (Options.EnableGM.GetBool()) newList.Add((PlayerControl.LocalPlayer, RoleTypes.Crewmate));
            RpcSetRoleReplacer.StoragedData = newList;

            RpcSetRoleReplacer.Release(); // Write the saved SetRoleRpc all at once
            RpcSetRoleReplacer.senders.Do(kvp => kvp.Value.SendMessage());

            // Delete unnecessary objects
            RpcSetRoleReplacer.senders = null;
            RpcSetRoleReplacer.OverriddenSenderList = null;
            RpcSetRoleReplacer.StoragedData = null;

            //Utils.ApplySuffix();

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.Data.IsDead = false;
                if (Main.PlayerStates[pc.PlayerId].MainRole != CustomRoles.NotAssigned) continue;
                var role = pc.Data.Role.Role switch
                {
                    RoleTypes.Crewmate => CustomRoles.Crewmate,
                    RoleTypes.Impostor => CustomRoles.Impostor,
                    RoleTypes.Scientist => CustomRoles.Scientist,
                    RoleTypes.Engineer => CustomRoles.Engineer,
                    RoleTypes.GuardianAngel => CustomRoles.GuardianAngel,
                    RoleTypes.Shapeshifter => CustomRoles.Shapeshifter,
                    _ => CustomRoles.NotAssigned,
                };
                if (role == CustomRoles.NotAssigned) Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                Main.PlayerStates[pc.PlayerId].SetMainRole(role);
            }

            // For other gamemodes:
            if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato)
            {
                foreach (var pair in Main.PlayerStates)
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);
                goto EndOfSelectRolePatch;
            }

            foreach (var kv in RoleResult)
            {
                if (kv.Value.IsDesyncRole()) continue;
                AssignCustomRole(kv.Value, kv.Key);
            }

            if (Main.PlayerStates.TryGetValue(Main.NimblePlayer, out var nimbleState)) nimbleState.SetSubRole(CustomRoles.Nimble);
            if (Main.PlayerStates.TryGetValue(Main.PhysicistPlayer, out var physicistState)) physicistState.SetSubRole(CustomRoles.Physicist);

            foreach (var item in Main.SetAddOns)
            {
                if (Main.PlayerStates.TryGetValue(item.Key, out var state))
                {
                    foreach (var role in item.Value)
                    {
                        if (role is CustomRoles.Nimble or CustomRoles.Physicist) continue;
                        state.SetSubRole(role);
                    }
                }
            }

            if (CustomRoles.Lovers.IsEnable() && (CustomRoles.FFF.IsEnable() ? -1 : rd.Next(1, 100)) <= Options.LoverSpawnChances.GetInt()) AssignLoversRolesFromList();
            foreach (CustomRoles role in AddonRolesList.ToArray())
            {
                if (rd.Next(1, 100) <= (Options.CustomAdtRoleSpawnRate.TryGetValue(role, out var sc) ? sc.GetFloat() : 0))
                    if (role.IsEnable()) AssignSubRoles(role);
            }

            //RPCによる同期
            foreach (var pair in Main.PlayerStates)
            {
                ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                foreach (CustomRoles subRole in pair.Value.SubRoles.ToArray())
                {
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
                }
            }

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                try
                {
                    if (pc.Data.Role.Role == RoleTypes.Shapeshifter)
                        Main.CheckShapeshift.Add(pc.PlayerId, false);

                    Utils.AddRoles(pc.PlayerId, pc.GetCustomRole());
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString(), "onGameStartedPatch Add methods");
                }
            }

            Stressed.Add();
            Asthmatic.Add();

        EndOfSelectRolePatch:

            if (Options.CurrentGameMode == CustomGameMode.HotPotato) HotPotatoManager.OnGameStart();

            HudManager.Instance.SetHudActive(true);
            List<PlayerControl> AllPlayers = [];
            CustomRpcSender sender = CustomRpcSender.Create("SelectRoles Sender", SendOption.Reliable);
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.ResetKillCooldown();
            }

            //役職の人数を戻す
            var roleOpt = Main.NormalOptions.roleOptions;
            int ScientistNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Scientist);
            ScientistNum -= addScientistNum;
            roleOpt.SetRoleRate(RoleTypes.Scientist, ScientistNum, roleOpt.GetChancePerGame(RoleTypes.Scientist));
            int EngineerNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Engineer);
            EngineerNum -= addEngineerNum;
            roleOpt.SetRoleRate(RoleTypes.Engineer, EngineerNum, roleOpt.GetChancePerGame(RoleTypes.Engineer));
            int ShapeshifterNum = Options.DisableVanillaRoles.GetBool() ? 0 : roleOpt.GetNumPerGame(RoleTypes.Shapeshifter);
            ShapeshifterNum -= addShapeshifterNum;
            roleOpt.SetRoleRate(RoleTypes.Shapeshifter, ShapeshifterNum, roleOpt.GetChancePerGame(RoleTypes.Shapeshifter));

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:
                    GameEndChecker.SetPredicateToNormal();
                    break;
                case CustomGameMode.SoloKombat:
                    GameEndChecker.SetPredicateToSoloKombat();
                    break;
                case CustomGameMode.FFA:
                    GameEndChecker.SetPredicateToFFA();
                    break;
                case CustomGameMode.MoveAndStop:
                    GameEndChecker.SetPredicateToMoveAndStop();
                    break;
                case CustomGameMode.HotPotato:
                    GameEndChecker.SetPredicateToHotPotato();
                    break;
            }

            GameOptionsSender.AllSenders.Clear();
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                GameOptionsSender.AllSenders.Add(new PlayerGameOptionsSender(pc));
            }

            // Added players with unclassified roles to the list of players who require ResetCam.
            Main.ResetCamPlayerList.AddRange(Main.AllPlayerControls.Where(p => p.GetCustomRole() is CustomRoles.Arsonist or CustomRoles.Revolutionist or CustomRoles.Sidekick or CustomRoles.KB_Normal or CustomRoles.Killer or CustomRoles.Tasker or CustomRoles.Potato or CustomRoles.Innocent || (p.Is(CustomRoles.Witness) && (!Options.UsePets.GetBool() || Options.WitnessUsePet.GetBool()))).Select(p => p.PlayerId));
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();
            SetColorPatch.IsAntiGlitchDisabled = false;

            _ = new LateTask(() =>
            {
                Main.SetRoles = [];
                Main.SetAddOns = [];
            }, 7f, log: false);

            if ((MapNames)Main.NormalOptions.MapId == MapNames.Airship && AmongUsClient.Instance.AmHost && Options.EnableGM.GetBool())
            {
                _ = new LateTask(() => { PlayerControl.LocalPlayer.NetTransform.SnapTo(new(15.5f, 0.0f), (ushort)(PlayerControl.LocalPlayer.NetTransform.lastSequenceId + 8)); }, 15f, "GM Auto-TP Failsafe"); // TP to Main Hall
            }

            _ = new LateTask(() => { Main.HasJustStarted = false; }, 13f, "HasJustStarted to false");
        }
        catch (Exception ex)
        {
            Utils.ErrorEnd("Select Role Postfix");
            Logger.Fatal(ex.ToString(), "Select Role Postfix");
        }
    }
    private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
    {
        if (player == null) return;

        var hostId = PlayerControl.LocalPlayer.PlayerId;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);

        var selfRole = player.PlayerId == hostId ? hostBaseRole : BaseRole;
        var othersRole = player.PlayerId == hostId ? RoleTypes.Crewmate : RoleTypes.Scientist;

        //Desync役職視点
        foreach (PlayerControl target in Main.AllPlayerControls)
        {
            rolesMap[(player.PlayerId, target.PlayerId)] = player.PlayerId != target.PlayerId ? othersRole : selfRole;
        }

        //他者視点
        foreach (var seer in Main.AllPlayerControls.Where(x => player.PlayerId != x.PlayerId).ToArray())
            rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;

        RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
        //ホスト視点はロール決定
        player.SetRole(othersRole);
        player.Data.IsDead = true;

        Logger.Info($"Register Modded Role：{player.Data?.PlayerName} => {role}", "AssignRoles");
    }
    public static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
    {
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            var sender = senders[seer.PlayerId];
            foreach (PlayerControl target in Main.AllPlayerControls)
            {
                if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var role))
                {
                    sender.RpcSetRole(seer, role, target.GetClientId());
                }
            }
        }
    }

    private static void AssignCustomRole(CustomRoles role, PlayerControl player)
    {
        if (player == null) return;
        SetColorPatch.IsAntiGlitchDisabled = true;

        Main.PlayerStates[player.PlayerId].SetMainRole(role);
        Logger.Info($"Register Modded Role：{player?.Data?.PlayerName} => {role}", "AssignRoles");

        SetColorPatch.IsAntiGlitchDisabled = false;
    }
//    private static void ForceAssignRole(/*CustomRoles role,*/ List<PlayerControl> AllPlayers, CustomRpcSender sender, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate, bool skip = false, int Count = -1)
//    {
//        var count = 1;
//
//        if (Count != -1)
//            count = Count;
//        for (var i = 0; i < count; i++)
//        {
//            if (AllPlayers.Count == 0) break;
//            var rand = IRandom.Instance;
//            var player = AllPlayers[rand.Next(0, AllPlayers.Count)];
//            AllPlayers.Remove(player);
//            if (!skip)
//            {
//                if (!player.IsModClient())
//                {
//                    int playerCID = player.GetClientId();
//                    sender.RpcSetRole(player, BaseRole, playerCID);
//                    //Desyncする人視点で他プレイヤーを科学者にするループ
//                    foreach (var pc in PlayerControl.AllPlayerControls)
//                    {
//                        if (pc == player) continue;
//                        sender.RpcSetRole(pc, RoleTypes.Scientist, playerCID);
//                    }
//                    //他視点でDesyncする人の役職を科学者にするループ
//                    foreach (var pc in PlayerControl.AllPlayerControls)
//                    {
//                        if (pc == player) continue;
//                        if (pc.PlayerId == 0) player.SetRole(RoleTypes.Scientist); //ホスト視点用
//                        else sender.RpcSetRole(player, RoleTypes.Scientist, pc.GetClientId());
//                    }
//                }
//                else
//                {
//                    //ホストは別の役職にする
//                    player.SetRole(hostBaseRole); //ホスト視点用
//                    sender.RpcSetRole(player, hostBaseRole);
//                }
//            }
//        }
//    }

    private static void AssignLoversRolesFromList()
    {
        if (CustomRoles.Lovers.IsEnable())
        {
            Main.LoversPlayers.Clear();
            Main.isLoversDead = false;
            AssignLoversRoles();
        }
    }
    private static void AssignLoversRoles(int RawCount = -1)
    {
        var allPlayers = Main.AllPlayerControls.Where(pc => !pc.Is(CustomRoles.GM) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && !pc.Is(CustomRoles.Dictator) && !pc.Is(CustomRoles.God) && !pc.Is(CustomRoles.FFF) && !pc.Is(CustomRoles.Bomber) && !pc.Is(CustomRoles.Nuker) && !pc.Is(CustomRoles.Provocateur) && (!pc.GetCustomRole().IsCrewmate() || Options.CrewCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsNeutral() || Options.NeutralCanBeInLove.GetBool()) && (!pc.GetCustomRole().IsImpostor() || Options.ImpCanBeInLove.GetBool())).ToList();
        const CustomRoles role = CustomRoles.Lovers;
        var rd = IRandom.Instance;
        var count = Math.Clamp(RawCount, 0, allPlayers.Count);
        if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Count);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[rd.Next(0, allPlayers.Count)];
            Main.LoversPlayers.Add(player);
            allPlayers.Remove(player);
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
            Logger.Info("Add-on assigned: " + player?.Data?.PlayerName + " = " + player.GetCustomRole() + " + " + role, "AssignLovers");
        }
        RPC.SyncLoversPlayers();
    }
    private static void AssignSubRoles(CustomRoles role, int RawCount = -1)
    {
        var allPlayers = Main.AllAlivePlayerControls.Where(x => CustomRolesHelper.CheckAddonConflict(role, x)).ToArray();
        var count = Math.Clamp(RawCount, 0, allPlayers.Length);
        if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, allPlayers.Length);
        if (count <= 0) return;
        for (var i = 0; i < count; i++)
        {
            var player = allPlayers[IRandom.Instance.Next(0, allPlayers.Length)];
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
            Logger.Info($"Assigned add-on: {player?.Data?.PlayerName} = {player.GetCustomRole()} + {role}", $"Assign {role}");
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private class RpcSetRoleReplacer
    {
        private static bool doReplace;
        public static Dictionary<byte, CustomRpcSender> senders;
        public static List<(PlayerControl, RoleTypes)> StoragedData = [];
        // A list of Senders that does not require additional writing because SetRoleRpc has already been written in another process such as role Desync.
        public static List<CustomRpcSender> OverriddenSenderList;
        public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
        {
            if (doReplace && senders != null)
            {
                StoragedData.Add((__instance, roleType));
                return false;
            }

            return true;
        }
        public static void Release()
        {
            foreach (var sender in senders)
            {
                if (OverriddenSenderList.Contains(sender.Value)) continue;
                if (sender.Value.CurrentState != CustomRpcSender.State.InRootMessage)
                    throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                foreach ((PlayerControl PLAYER, RoleTypes ROLETYPE) in StoragedData.ToArray())
                {
                    PLAYER.SetRole(ROLETYPE);
                    sender.Value.AutoStartRpc(PLAYER.NetId, (byte)RpcCalls.SetRole, Utils.GetPlayerById(sender.Key).GetClientId())
                        .Write((ushort)ROLETYPE)
                        .EndRpc();
                }
                sender.Value.EndMessage();
            }
            doReplace = false;
        }
        public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
        {
            RpcSetRoleReplacer.senders = senders;
            StoragedData = [];
            OverriddenSenderList = [];
            doReplace = true;
        }
    }
}