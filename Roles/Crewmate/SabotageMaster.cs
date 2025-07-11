using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class SabotageMaster : RoleBase
{
    private const int Id = 7000;
    private static List<byte> PlayerIdList = [];

    private static OptionItem SkillLimit;
    private static OptionItem FixesDoors;
    private static OptionItem FixesReactors;
    private static OptionItem FixesOxygens;
    private static OptionItem FixesComms;
    private static OptionItem FixesElectrical;
    public static OptionItem SmAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    private static OptionItem UsesUsedWhenFixingReactorOrO2;
    private static OptionItem UsesUsedWhenFixingLightsOrComms;
    private static OptionItem CanFixSabotageFromAnywhereWithPet;
    private static OptionItem MaxFixedViaPet;
    public static OptionItem CanVent;
    private static OptionItem VentCooldown;
    private static OptionItem MaxInVentTime;

    private static bool DoorsProgressing;
    private bool fixedSabotage;
    private int PetLimit;
    private byte SMId;

    public float UsedSkillCount;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.SabotageMaster);

        SkillLimit = new IntegerOptionItem(Id + 10, "SabotageMasterSkillLimit", new(0, 80, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);

        FixesDoors = new BooleanOptionItem(Id + 11, "SabotageMasterFixesDoors", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        FixesReactors = new BooleanOptionItem(Id + 12, "SabotageMasterFixesReactors", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        FixesOxygens = new BooleanOptionItem(Id + 13, "SabotageMasterFixesOxygens", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        FixesComms = new BooleanOptionItem(Id + 14, "SabotageMasterFixesCommunications", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        FixesElectrical = new BooleanOptionItem(Id + 15, "SabotageMasterFixesElectrical", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        SmAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 16, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 3f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 19, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);

        UsesUsedWhenFixingReactorOrO2 = new FloatOptionItem(Id + 17, "SMUsesUsedWhenFixingReactorOrO2", new(0f, 5f, 0.1f), 4f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);

        UsesUsedWhenFixingLightsOrComms = new FloatOptionItem(Id + 18, "SMUsesUsedWhenFixingLightsOrComms", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);

        CanFixSabotageFromAnywhereWithPet = new BooleanOptionItem(Id + 20, "SMCanFixSabotageFromAnywhereWithPet", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        MaxFixedViaPet = new IntegerOptionItem(Id + 21, "SMMaxFixedViaPet", new(1, 30, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CanFixSabotageFromAnywhereWithPet);

        CanVent = new BooleanOptionItem(Id + 22, "CanVent", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);

        VentCooldown = new FloatOptionItem(Id + 23, "VentCooldown", new(0f, 60f, 0.5f), 0f, TabGroup.CrewmateRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        MaxInVentTime = new FloatOptionItem(Id + 24, "MaxInVentTime", new(0f, 60f, 0.5f), 0f, TabGroup.CrewmateRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        UsedSkillCount = 0;
        SMId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        PetLimit = MaxFixedViaPet.GetInt();
        UsedSkillCount = 0;
        SMId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!CanVent.GetBool()) return;
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        double limit = Math.Round(SkillLimit.GetInt() - UsedSkillCount, 1);
        string colored = Utils.ColorString(Utils.GetRoleColor(CustomRoles.SabotageMaster).ShadeColor(0.25f), limit.ToString(CultureInfo.CurrentCulture));
        return $"({colored}){base.GetProgressText(playerId, comms)}";
    }

    public override void OnPet(PlayerControl pc)
    {
        SystemTypes[] systemTypes = [SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.LifeSupp, SystemTypes.HeliSabotage, SystemTypes.Comms];
        if (!CanFixSabotageFromAnywhereWithPet.GetBool() || !systemTypes.FindFirst(Utils.IsActive, out SystemTypes activeSystem) || PetLimit-- < 1) return;

        switch (activeSystem)
        {
            case SystemTypes.Electrical:
                var switchSystem = ShipStatus.Instance.Systems[SystemTypes.Electrical].CastFast<SwitchSystem>();
                if (switchSystem == null) break;
                switchSystem.ActualSwitches = switchSystem.ExpectedSwitches;
                switchSystem.IsDirty = true;
                break;
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            case SystemTypes.LifeSupp:
                ShipStatus.Instance.UpdateSystem(activeSystem, pc, 16);
                break;
            case SystemTypes.HeliSabotage:
                ShipStatus.Instance.UpdateSystem(activeSystem, pc, 17);
                goto case SystemTypes.Reactor;
            case SystemTypes.Comms:
                if (Main.NormalOptions.MapId is 1 or 5) ShipStatus.Instance.UpdateSystem(activeSystem, pc, 17);
                goto case SystemTypes.Reactor;
        }
    }

    public void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSabotageMasterLimit, SendOption.Reliable);
        writer.Write(SMId);
        writer.Write(UsedSkillCount);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not SabotageMaster sm) return;

        sm.UsedSkillCount = reader.ReadSingle();
    }

    public static void RepairSystem(byte playerId, SystemTypes systemType, byte amount)
    {
        if (Main.PlayerStates[playerId].Role is not SabotageMaster sm) return;

        switch (systemType)
        {
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
            {
                if (!FixesReactors.GetBool()) break;

                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;

                if (amount.HasAnyBit(ReactorSystemType.AddUserOp))
                {
                    ShipStatus.Instance.UpdateSystem(Main.CurrentMap == MapNames.Polus ? SystemTypes.Laboratory : SystemTypes.Reactor, playerId.GetPlayer(), ReactorSystemType.ClearCountdown);
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            }
            case SystemTypes.HeliSabotage:
            {
                if (!FixesReactors.GetBool()) break;

                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;

                var tags = (HeliSabotageSystem.Tags)(amount & HeliSabotageSystem.TagMask);
                if (tags == HeliSabotageSystem.Tags.ActiveBit) sm.fixedSabotage = false;

                if (!sm.fixedSabotage && tags == HeliSabotageSystem.Tags.FixBit)
                {
                    sm.fixedSabotage = true;
                    int consoleId = amount & HeliSabotageSystem.IdMask;
                    int otherConsoleId = (consoleId + 1) % 2;
                    ShipStatus.Instance.UpdateSystem(SystemTypes.HeliSabotage, playerId.GetPlayer(), (byte)(otherConsoleId | (int)HeliSabotageSystem.Tags.FixBit));
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            }
            case SystemTypes.LifeSupp:
            {
                if (!FixesOxygens.GetBool()) break;

                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;

                if (amount.HasAnyBit(LifeSuppSystemType.AddUserOp))
                {
                    ShipStatus.Instance.UpdateSystem(SystemTypes.LifeSupp, playerId.GetPlayer(), LifeSuppSystemType.ClearCountdown);
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            }
            case SystemTypes.Comms when Main.CurrentMap == MapNames.MiraHQ:
            {
                if (!FixesComms.GetBool()) break;

                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingLightsOrComms.GetFloat() - 1 >= SkillLimit.GetFloat()) break;

                var tags = (HqHudSystemType.Tags)(amount & HqHudSystemType.TagMask);
                if (tags == HqHudSystemType.Tags.ActiveBit) sm.fixedSabotage = false;

                if (!sm.fixedSabotage && tags == HqHudSystemType.Tags.FixBit)
                {
                    sm.fixedSabotage = true;
                    int consoleId = amount & HqHudSystemType.IdMask;
                    int otherConsoleId = (consoleId + 1) % 2;
                    ShipStatus.Instance.UpdateSystem(SystemTypes.Comms, playerId.GetPlayer(), (byte)(otherConsoleId | (int)HqHudSystemType.Tags.FixBit));
                    sm.UsedSkillCount += UsesUsedWhenFixingLightsOrComms.GetFloat();
                    sm.SendRPC();
                }

                break;
            }
            case SystemTypes.Doors:
            {
                if (!FixesDoors.GetBool()) break;

                if (DoorsProgressing) break;

                int mapId = Main.NormalOptions.MapId;
                if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) mapId = AmongUsClient.Instance.TutorialMapId;

                var shipStatus = ShipStatus.Instance;

                DoorsProgressing = true;

                switch (mapId)
                {
                    case 2: // Polus
                    {
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 71, 72);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 67, 68);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 64, 66);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 73, 74);
                        break;
                    }
                    case 4: // Airship
                    {
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 64, 67);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 71, 73);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 74, 75);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 76, 78);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 68, 70);
                        RepairSystemPatch.CheckAndOpenDoorsRange(shipStatus, amount, 83, 84);
                        break;
                    }
                    case 5: // Fungle
                    {
                        int openedDoorId = amount & DoorsSystemType.IdMask;
                        OpenableDoor openedDoor = shipStatus.AllDoors.FirstOrDefault(door => door.Id == openedDoorId);

                        if (openedDoor == null)
                            Logger.Warn($"An unknown door has been opened: {openedDoorId}", nameof(SabotageMaster));
                        else
                        {
                            SystemTypes room = openedDoor.Room;

                            foreach (OpenableDoor door in shipStatus.AllDoors)
                            {
                                if (door.Id != openedDoorId && door.Room == room)
                                    door.SetDoorway(true);
                            }
                        }

                        break;
                    }
                }

                DoorsProgressing = false;
                break;
            }
        }
    }

    public static void SwitchSystemRepair(byte playerId, SwitchSystem switchSystem, byte amount)
    {
        if (!FixesElectrical.GetBool() || Main.PlayerStates[playerId].Role is not SabotageMaster sm) return;

        if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingLightsOrComms.GetFloat() - 1 >= SkillLimit.GetFloat()) return;

        if (amount.HasBit(SwitchSystem.DamageSystem)) return;

        int fixbit = 1 << amount;
        switchSystem.ActualSwitches = (byte)(switchSystem.ExpectedSwitches ^ fixbit);

        sm.UsedSkillCount += UsesUsedWhenFixingLightsOrComms.GetFloat();
        sm.SendRPC();
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || CanVent.GetBool();
    }
}
