﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Roles.Crewmate.Randomizer;

namespace TOHE.Roles.Crewmate
{
    internal static class EffectExtenstions
    {
        public static bool IsSpeedChangingEffect(this Effect effect) => effect is
            Effect.SuperSpeedForRandomPlayer or
            Effect.SuperSpeedForAll or
            Effect.FreezeRandomPlayer or
            Effect.InvertControls;

        public static bool IsVisionChangingEffect(this Effect effect) => effect is
            Effect.SuperVisionForRandomPlayer or
            Effect.SuperVisionForAll or
            Effect.BlindnessForRandomPlayer or
            Effect.BlindnessForAll;

        public static void Apply(this Effect effect, PlayerControl randomizer)
        {
            try
            {
                switch (effect)
                {
                    case Effect.ShieldRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        AddEffectForPlayer(pc, effect);
                    }
                        break;
                    case Effect.ShieldAll:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            AddEffectForPlayer(pc, effect);
                            NotifyAboutRNG(pc);
                        }
                    }
                        break;
                    case Effect.TPEveryoneToVents:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            pc.TPtoRndVent();
                            NotifyAboutRNG(pc);
                        }
                    }
                        break;
                    case Effect.PullEveryone:
                        Utils.TPAll(randomizer.Pos());
                        break;
                    case Effect.Twist:
                    {
                        List<byte> changePositionPlayers = [];
                        var rd = IRandom.Instance;
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            if (changePositionPlayers.Contains(pc.PlayerId) || Pelican.IsEaten(pc.PlayerId) || pc.onLadder || pc.inVent || GameStates.IsMeeting) continue;

                            var filtered = Main.AllAlivePlayerControls.Where(a => !a.inVent && !Pelican.IsEaten(a.PlayerId) && !a.onLadder && a.PlayerId != pc.PlayerId && !changePositionPlayers.Contains(a.PlayerId)).ToArray();
                            if (filtered.Length == 0) break;

                            var target = filtered[rd.Next(0, filtered.Length)];

                            changePositionPlayers.Add(target.PlayerId);
                            changePositionPlayers.Add(pc.PlayerId);

                            pc.RPCPlayCustomSound("Teleport");

                            var originPs = target.Pos();
                            target.TP(pc.Pos());
                            pc.TP(originPs);

                            NotifyAboutRNG(target);
                            NotifyAboutRNG(pc);
                        }
                    }
                        break;
                    case Effect.SuperSpeedForRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        RevertSpeedChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.SuperSpeedForAll:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            RevertSpeedChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            Main.AllPlayerSpeed[pc.PlayerId] = 5f;
                            NotifyAboutRNG(pc);
                        }

                        Utils.MarkEveryoneDirtySettings();
                    }
                        break;
                    case Effect.FreezeRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        RevertSpeedChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.SuperVisionForRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        RevertVisionChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.SuperVisionForAll:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            RevertVisionChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            NotifyAboutRNG(pc);
                        }

                        Utils.MarkEveryoneDirtySettings();
                    }
                        break;
                    case Effect.BlindnessForRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        RevertVisionChangesForPlayer(pc, false);
                        AddEffectForPlayer(pc, effect);
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.BlindnessForAll:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            RevertVisionChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            NotifyAboutRNG(pc);
                        }

                        Utils.MarkEveryoneDirtySettings();
                    }
                        break;
                    case Effect.AllKCDsReset:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            if (pc.HasKillButton() && pc.CanUseKillButton())
                            {
                                pc.SetKillCooldown();
                                NotifyAboutRNG(pc);
                            }
                        }
                    }
                        break;
                    case Effect.AllKCDsTo0:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            if (pc.HasKillButton() && pc.CanUseKillButton())
                            {
                                pc.SetKillCooldown(0.1f);
                                NotifyAboutRNG(pc);
                            }
                        }
                    }
                        break;
                    case Effect.Meeting when TimeSinceLastMeeting > Math.Max(Main.NormalOptions.EmergencyCooldown, 30):
                    {
                        var pc = PickRandomPlayer();
                        pc.CmdReportDeadBody(null);
                    }
                        break;
                    case Effect.Rift:
                        Rifts.TryAdd(PickRandomPlayer().Pos(), PickRandomPlayer().Pos());
                        try
                        {
                            var riftsToRemove = new List<Vector2>();
                            foreach (var rift1 in Rifts)
                            {
                                foreach (var rift2 in Rifts)
                                {
                                    if (rift1.Key != rift2.Key && Vector2.Distance(rift1.Key, rift2.Key) <= 4f)
                                    {
                                        riftsToRemove.Add(rift2.Key);
                                    }
                                }
                            }

                            foreach (var rift in riftsToRemove)
                            {
                                Rifts.Remove(rift);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Exception(ex, "Randomizer Rift Manager");
                        }

                        break;
                    case Effect.TimeBomb:
                        Bombs.TryAdd(PickRandomPlayer().Pos(), (Utils.TimeStamp, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration)));
                        break;
                    case Effect.Tornado:
                        Tornado.SpawnTornado(PickRandomPlayer());
                        break;
                    case Effect.RevertToBaseRole when TimeSinceLastMeeting > 40f: // To make this less frequent than the others
                    {
                        var pc = PickRandomPlayer();
                        pc.RpcSetCustomRole(pc.GetCustomRole().GetErasedRole());
                        pc.SyncSettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.InvertControls:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            RevertSpeedChangesForPlayer(pc, false);
                            AddEffectForPlayer(pc, effect);
                            Main.AllPlayerSpeed[pc.PlayerId] = -AllPlayerDefaultSpeed[pc.PlayerId];
                            NotifyAboutRNG(pc);
                        }

                        Utils.MarkEveryoneDirtySettings();
                    }
                        break;
                    case Effect.AddonAssign:
                    {
                        var addons = EnumHelper.GetAllValues<CustomRoles>().Where(x => x.IsAdditionRole() && x != CustomRoles.NotAssigned).ToArray();
                        var pc = PickRandomPlayer();
                        var addon = addons[IRandom.Instance.Next(0, addons.Length)];
                        if (Main.PlayerStates[pc.PlayerId].SubRoles.Contains(addon)) break;
                        Main.PlayerStates[pc.PlayerId].SetSubRole(addon);
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.AddonRemove:
                    {
                        var pc = PickRandomPlayer();
                        var addons = Main.PlayerStates[pc.PlayerId].SubRoles;
                        if (addons.Count == 0) break;
                        var addon = addons[IRandom.Instance.Next(0, addons.Count)];
                        Main.PlayerStates[pc.PlayerId].RemoveSubRole(addon);
                        pc.MarkDirtySettings();
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.HandcuffRandomPlayer:
                    {
                        var pc = PickRandomPlayer();
                        AddEffectForPlayer(pc, effect);
                        Glitch.hackedIdList.TryAdd(pc.PlayerId, Utils.TimeStamp);
                        NotifyAboutRNG(pc);
                    }
                        break;
                    case Effect.HandcuffAll:
                    {
                        var now = Utils.TimeStamp;
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            AddEffectForPlayer(pc, effect);
                            Glitch.hackedIdList.TryAdd(pc.PlayerId, now);
                            NotifyAboutRNG(pc);
                        }
                    }
                        break;
                    case Effect.DonutForAll:
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            DonutDelivery.RandomNotifyTarget(pc);
                        }
                    }
                        break;
                    case Effect.AllDoorsOpen:
                        try
                        {
                            DoorsReset.OpenAllDoors();
                        }
                        catch
                        {
                        }

                        break;
                    case Effect.AllDoorsClose:
                        try
                        {
                            DoorsReset.CloseAllDoors();
                        }
                        catch
                        {
                        }

                        break;
                    case Effect.SetDoorsRandomly:
                        try
                        {
                            DoorsReset.OpenOrCloseAllDoorsRandomly();
                        }
                        catch
                        {
                        }

                        break;
                    case Effect.Patrol:
                    {
                        var pc = PickRandomPlayer();
                        var state = new PatrollingState(pc.PlayerId, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration), RandomFloat, pc);
                        Sentinel.PatrolStates.Add(state);
                        state.StartPatrolling();
                    }
                        break;
                    case Effect.GhostPlayer when TimeSinceLastMeeting > Options.DefaultKillCooldown:
                    {
                        var killer = PickRandomPlayer();
                        var allPc = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId).ToArray();
                        if (allPc.Length == 0) break;
                        var target = allPc[IRandom.Instance.Next(0, allPc.Length)];
                        BallLightning.CheckBallLightningMurder(killer, target, force: true);
                        NotifyAboutRNG(target);
                    }
                        break;
                    case Effect.Camouflage when TimeSinceLastMeeting > Camouflager.CamouflageCooldown.GetFloat():
                        var camouflager = new Camouflager();
                        camouflager.Init();
                        camouflager.Add(randomizer.PlayerId);
                        camouflager.OnShapeshift(randomizer, randomizer, true);
                        _ = new LateTask(camouflager.OnReportDeadBody, IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration), "Randomizer Revert Camo");
                        break;
                    case Effect.Deathpact:
                    {
                        var deathpact = new Deathpact();
                        deathpact.Init();
                        deathpact.Add(randomizer.PlayerId);
                        deathpact.OnShapeshift(randomizer, PickRandomPlayer(), true);
                    }
                        break;
                    case Effect.DevourRandomPlayer:
                    {
                        var devourer = new Devourer();
                        devourer.Init();
                        devourer.Add(randomizer.PlayerId);
                        devourer.OnShapeshift(randomizer, PickRandomPlayer(), true);
                    }
                        break;
                    case Effect.Duel:
                    {
                        var pc1 = PickRandomPlayer();
                        var allPc = Main.AllAlivePlayerControls.Where(x => x.CanUseKillButton() && x.PlayerId != pc1.PlayerId).ToArray();
                        if (allPc.Length == 0) break;
                        var pc2 = allPc[IRandom.Instance.Next(0, allPc.Length)];
                        var duellist = new Duellist();
                        duellist.OnShapeshift(pc1, pc2, true);
                    }
                        break;
                    default:
                        Logger.Info("Effect wasn't applied", "Randomizer");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.CurrentMethod();
                Logger.Exception(ex, "Randomizer");
            }
        }
    }

    internal class Randomizer : RoleBase
    {
        private static int Id => 643490;
        private static List<byte> PlayerIdList = [];

        private static OptionItem EffectFrequencyOpt;
        private static OptionItem EffectDurMin;
        private static OptionItem EffectDurMax;
        private static OptionItem NotifyOpt;

        private static int EffectFrequency;
        public static int MinimumEffectDuration;
        public static int MaximumEffectDuration;
        private static bool Notify;

        public static Dictionary<byte, Dictionary<Effect, (long StartTimeStamp, int Duration)>> CurrentEffects = [];
        public static Dictionary<byte, float> AllPlayerDefaultSpeed = [];

        public static Dictionary<Vector2, Vector2> Rifts = [];
        public static Dictionary<Vector2, (long PlaceTimeStamp, int ExplosionDelay)> Bombs = [];

        public static float TimeSinceLastMeeting;
        private static Dictionary<byte, long> LastEffectPick = [];
        private static Dictionary<byte, long> LastTP = [];
        private static long LastDeathEffect;

        private static string RNGString => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), Translator.GetString("RNGHasSpoken"));

        public static void NotifyAboutRNG(PlayerControl pc)
        {
            if (!Notify) return;
            pc.Notify(text: RNGString, time: IRandom.Instance.Next(2, 7), log: false);
        }

        public static bool Exists => Main.PlayerStates.Values.Any(x => x.Role is Randomizer { IsEnable: true });

        public static float RandomFloat => IRandom.Instance.Next(0, 5) + (IRandom.Instance.Next(0, 10) / 10f);

        public override bool IsEnable => PlayerIdList.Count > 0;

        public static bool IsShielded(PlayerControl pc) => CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && (effects.ContainsKey(Effect.ShieldRandomPlayer) || effects.ContainsKey(Effect.ShieldAll));
        public static bool HasSuperVision(PlayerControl pc) => CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && (effects.ContainsKey(Effect.SuperVisionForRandomPlayer) || effects.ContainsKey(Effect.SuperVisionForAll));
        public static bool IsBlind(PlayerControl pc) => CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && (effects.ContainsKey(Effect.BlindnessForRandomPlayer) || effects.ContainsKey(Effect.BlindnessForAll));

        public enum Effect
        {
            ShieldRandomPlayer,
            ShieldAll,
            TPEveryoneToVents,
            PullEveryone,
            Twist,
            SuperSpeedForRandomPlayer,
            SuperSpeedForAll,
            FreezeRandomPlayer,
            SuperVisionForRandomPlayer,
            SuperVisionForAll,
            BlindnessForRandomPlayer,
            BlindnessForAll,
            AllKCDsReset,
            AllKCDsTo0,
            Meeting,
            Rift,
            TimeBomb,
            Tornado,
            RevertToBaseRole,
            InvertControls,
            AddonAssign,
            AddonRemove,
            HandcuffRandomPlayer,
            HandcuffAll,
            DonutForAll,
            AllDoorsOpen,
            AllDoorsClose,
            SetDoorsRandomly,
            Patrol, // Sentinel ability
            GhostPlayer, // Lightning ability
            Camouflage,
            Deathpact,
            DevourRandomPlayer,
            Duel
        }

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Randomizer);
            EffectFrequencyOpt = IntegerOptionItem.Create(Id + 2, "RandomizerEffectFrequency", new(1, 90, 1), 10, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
            EffectDurMin = IntegerOptionItem.Create(Id + 3, "RandomizerEffectDurMin", new(1, 90, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
            EffectDurMax = IntegerOptionItem.Create(Id + 4, "RandomizerEffectDurMax", new(1, 90, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer])
                .SetValueFormat(OptionFormat.Seconds);
            NotifyOpt = BooleanOptionItem.Create(Id + 5, "RandomizerNotifyOpt", true, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Randomizer]);
        }

        public override void Init()
        {
            var now = Utils.TimeStamp;

            PlayerIdList = [];
            CurrentEffects = [];
            AllPlayerDefaultSpeed = [];
            Rifts = [];
            Bombs = [];

            TimeSinceLastMeeting = 0;
            LastEffectPick = [];
            LastTP = [];
            Main.AllPlayerControls.Do(x => LastTP[x.PlayerId] = now);
            LastDeathEffect = 0;

            EffectFrequency = EffectFrequencyOpt.GetInt();
            MinimumEffectDuration = EffectDurMin.GetInt();
            MaximumEffectDuration = EffectDurMax.GetInt();
            Notify = NotifyOpt.GetBool();
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            AllPlayerDefaultSpeed = Main.AllPlayerSpeed;
        }

        public static PlayerControl PickRandomPlayer()
        {
            var allPc = Main.AllAlivePlayerControls;
            if (allPc.Length == 0) return null;
            var pc = allPc[IRandom.Instance.Next(0, allPc.Length)];
            return pc;
        }

        public static void AddEffectForPlayer(PlayerControl pc, Effect effect)
        {
            if (pc == null) return;
            if (!CurrentEffects.ContainsKey(pc.PlayerId)) CurrentEffects[pc.PlayerId] = [];
            int duration = IRandom.Instance.Next(MinimumEffectDuration, MaximumEffectDuration + 1);
            CurrentEffects[pc.PlayerId].TryAdd(effect, (Utils.TimeStamp, duration));
        }

        private static Effect PickRandomEffect(byte id)
        {
            long now = Utils.TimeStamp;

            LastEffectPick[id] = now;

            var allEffects = EnumHelper.GetAllValues<Effect>();
            var effect = allEffects[IRandom.Instance.Next(0, allEffects.Length)];

            if (effect == Effect.GhostPlayer)
            {
                if (LastDeathEffect + 60 > now) return Effect.AddonRemove;
                LastDeathEffect = now;
            }

            Logger.Info($"Effect: {effect}", "Randomizer");

            return effect;
        }

        public static void RevertSpeedChangesForPlayer(PlayerControl pc, bool sync)
        {
            try
            {
                if (pc == null) return;
                if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && effects.Any(x => x.Key.IsSpeedChangingEffect()))
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = AllPlayerDefaultSpeed[pc.PlayerId];
                    if (sync) pc.MarkDirtySettings();
                    var keys = effects.Keys.AsEnumerable();
                    keys.DoIf(x => x.IsSpeedChangingEffect(), x => effects.Remove(x));
                }
            }
            catch (Exception e)
            {
                Logger.CurrentMethod();
                Logger.Exception(e, "Randomizer");
            }
        }

        public static void RevertVisionChangesForPlayer(PlayerControl pc, bool sync)
        {
            try
            {
                if (pc == null) return;
                if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects) && effects.Any(x => x.Key.IsVisionChangingEffect()))
                {
                    var keys = effects.Keys.AsEnumerable();
                    keys.DoIf(x => x.IsVisionChangingEffect(), x => effects.Remove(x));
                    if (sync) pc.MarkDirtySettings();
                }
            }
            catch (Exception e)
            {
                Logger.CurrentMethod();
                Logger.Exception(e, "Randomizer");
            }
        }

        public static void OnAnyoneDeath(PlayerControl pc)
        {
            try
            {
                if (!Main.PlayerStates.Values.Any(x => x.Role is Randomizer { IsEnable: true })) return;

                RevertSpeedChangesForPlayer(pc, false);
                RevertVisionChangesForPlayer(pc, false);

                Main.AllPlayerSpeed[pc.PlayerId] = AllPlayerDefaultSpeed[pc.PlayerId];
                pc.MarkDirtySettings();
            }
            catch (Exception e)
            {
                Logger.CurrentMethod();
                Logger.Exception(e, "Randomizer");
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            TimeSinceLastMeeting = 0;
            LastDeathEffect = Utils.TimeStamp;
            Rifts.Clear();
            Bombs.Clear();
            foreach (var pc in Main.AllPlayerControls)
            {
                RevertSpeedChangesForPlayer(pc, false);
                RevertVisionChangesForPlayer(pc, false);
            }
        }

        public override void AfterMeetingTasks()
        {
            if (!IsEnable) return;
            TimeSinceLastMeeting = 0;
            LastDeathEffect = Utils.TimeStamp;
        }

        public override void OnGlobalFixedUpdate()
        {
            try
            {
                if (!GameStates.IsInTask || Bombs.Count == 0) return;

                var now = Utils.TimeStamp;
                var randomizer = Utils.GetPlayerById(PlayerIdList.FirstOrDefault());

                foreach (var bomb in Bombs)
                {
                    if (bomb.Value.PlaceTimeStamp + bomb.Value.ExplosionDelay < now)
                    {
                        var players = Utils.GetPlayersInRadius(radius: RandomFloat, from: bomb.Key);
                        foreach (var pc in players)
                        {
                            pc.Suicide(PlayerState.DeathReason.RNG, randomizer);
                        }
                        Bombs.Remove(bomb.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.CurrentMethod();
                Logger.Exception(ex, "Randomizer");
            }
        }

        public static string GetSuffixText(PlayerControl pc, PlayerControl target)
        {
            if (pc == null || pc.PlayerId != target.PlayerId || Bombs.Count == 0) return string.Empty;
            var bomb = Bombs.FirstOrDefault(x => Vector2.Distance(x.Key, pc.Pos()) <= 5f);
            var time = bomb.Value.ExplosionDelay - (Utils.TimeStamp - bomb.Value.PlaceTimeStamp) + 1;
            return time < 0 ? string.Empty : $"<#ffff00>⚠ {time}</color>";
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            try
            {
                if (!IsEnable || !GameStates.IsInTask) return;

                TimeSinceLastMeeting += Time.fixedDeltaTime;

                if (pc == null || !pc.IsAlive() || TimeSinceLastMeeting <= 10f) return;

                long now = Utils.TimeStamp;

                if (LastEffectPick.TryGetValue(pc.PlayerId, out var ts) && ts + EffectFrequency <= now)
                {
                    Effect effect = PickRandomEffect(pc.PlayerId);
                    effect.Apply(randomizer: pc);
                }
                else LastEffectPick.TryAdd(pc.PlayerId, now);
            }
            catch (Exception ex)
            {
                Logger.CurrentMethod();
                Logger.Exception(ex, "Randomizer");
            }
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            try
            {
                if (!IsEnable || !GameStates.IsInTask) return;

                var now = Utils.TimeStamp;
                if (LastTP[pc.PlayerId] + 5 > now) return;

                var pos = pc.Pos();

                foreach (var rift in Rifts)
                {
                    if (Vector2.Distance(pos, rift.Key) < 2f)
                    {
                        pc.TP(rift.Value);
                        LastTP[pc.PlayerId] = now;
                        return;
                    }
                    if (Vector2.Distance(pos, rift.Value) < 2f)
                    {
                        pc.TP(rift.Key);
                        LastTP[pc.PlayerId] = now;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.CurrentMethod();
                Logger.Exception(ex, "Randomizer");
            }
        }

        public static void OnFixedUpdateForPlayers(PlayerControl pc)
        {
            try
            {
                if (pc == null || !pc.IsAlive() || !GameStates.IsInTask) return;

                if (CurrentEffects.TryGetValue(pc.PlayerId, out var effects))
                {
                    var now = Utils.TimeStamp;

                    foreach (var item in effects)
                    {
                        if (item.Value.StartTimeStamp + item.Value.Duration < now)
                        {
                            if (item.Key.IsVisionChangingEffect())
                            {
                                RevertVisionChangesForPlayer(pc, true);
                            }
                            if (item.Key.IsSpeedChangingEffect())
                            {
                                RevertSpeedChangesForPlayer(pc, true);
                            }

                            effects.Remove(item.Key);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.CurrentMethod();
                Logger.Exception(ex, "Randomizer");
            }
        }
    }
}
