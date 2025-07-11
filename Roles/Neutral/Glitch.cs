using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Neutral;

public class Glitch : RoleBase
{
    private const int Id = 18125;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem HackCooldown;
    public static OptionItem HackDuration;
    public static OptionItem MimicCooldown;
    public static OptionItem MimicDuration;
    public static OptionItem CanVent;
    public static OptionItem CanVote;
    public static OptionItem CanSabotage;
    private static OptionItem HasImpostorVision;

    private byte GlitchId;

    public int HackCDTimer;
    public int KCDTimer;
    public long LastHack;
    public long LastKill;
    private long LastUpdate;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Glitch);

        KillCooldown = new IntegerOptionItem(Id + 10, "KillCooldown", new(0, 180, 1), 25, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        HackCooldown = new IntegerOptionItem(Id + 11, "HackCooldown", new(0, 180, 1), 20, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        HackDuration = new FloatOptionItem(Id + 14, "HackDuration", new(0f, 60f, 1f), 15f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        MimicCooldown = new IntegerOptionItem(Id + 15, "MimicCooldown", new(0, 180, 1), 30, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        MimicDuration = new FloatOptionItem(Id + 16, "MimicDuration", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);

        CanVote = new BooleanOptionItem(Id + 17, "CanVote", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);

        CanSabotage = new BooleanOptionItem(Id + 18, "CanSabotage", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);

        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        GlitchId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        GlitchId = playerId;

        HackCDTimer = 10;
        KCDTimer = 10;

        long ts = Utils.TimeStamp;

        LastKill = ts;
        LastHack = ts;

        LastUpdate = ts;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.SabotageButton?.ToggleVisible(!Main.PlayerStates[id].IsDead);
        hud.SabotageButton?.OverrideText(Translator.GetString("HackButtonText"));
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return pc.IsAlive() && CanSabotage.GetBool();
    }

    private void SendRPCSyncTimers()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchTimers, SendOption.Reliable);
        writer.Write(GlitchId);
        writer.Write(HackCDTimer);
        writer.Write(KCDTimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncTimers(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not Glitch gc) return;

        gc.HackCDTimer = reader.ReadInt32();
        gc.KCDTimer = reader.ReadInt32();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 1f;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());

        AURoleOptions.ShapeshifterCooldown = MimicCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = MimicDuration.GetInt();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || (KCDTimer > 0 && HackCDTimer > 0)) return false;

        if (killer.CheckDoubleTrigger(target, () =>
        {
            if (HackCDTimer <= 0)
            {
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                HackCDTimer = HackCooldown.GetInt();
                target.BlockRole(HackDuration.GetFloat());
                LastHack = Utils.TimeStamp;
                SendRPCSyncTimers();
            }
        }))
        {
            if (KCDTimer > 0) return false;

            LastKill = Utils.TimeStamp;
            KCDTimer = KillCooldown.GetInt();
            SendRPCSyncTimers();
            return true;
        }

        return false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        long now = Utils.TimeStamp;
        if (LastUpdate == now) return;
        LastUpdate = now;

        if (HackCDTimer is > 180 or < 0) HackCDTimer = 0;
        if (KCDTimer is > 180 or < 0) KCDTimer = 0;

        if (player == null) return;

        if (!player.IsAlive())
        {
            HackCDTimer = 0;
            KCDTimer = 0;
            return;
        }

        if (HackCDTimer <= 0 && KCDTimer <= 0) return;

        try { HackCDTimer = (int)(HackCooldown.GetInt() - (now - LastHack)); }
        catch { HackCDTimer = 0; }

        if (HackCDTimer is > 180 or < 0) HackCDTimer = 0;

        try { KCDTimer = (int)(KillCooldown.GetInt() - (now - LastKill)); }
        catch { KCDTimer = 0; }

        if (KCDTimer is > 180 or < 0) KCDTimer = 0;

        if (player.IsNonHostModdedClient())
            SendRPCSyncTimers();

        if (!player.IsModdedClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer == null || seer.PlayerId != GlitchId || seer.PlayerId != target.PlayerId || !seer.IsAlive() || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;

        var sb = new StringBuilder();

        if (!hud) sb.Append("<size=70%>");

        if (HackCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}\n");
        if (KCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("KCD"), KCDTimer)}\n");

        if (!hud) sb.Append("</size>");

        return sb.ToString();
    }

    public override void AfterMeetingTasks()
    {
        if (Main.PlayerStates[GlitchId].IsDead) return;

        long timestamp = Utils.TimeStamp;
        LastKill = timestamp;
        LastHack = timestamp;
        KCDTimer = 10;
        HackCDTimer = 10;
        SendRPCSyncTimers();
    }
}