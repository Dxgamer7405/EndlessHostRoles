using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;


namespace EHR.Crewmate;

public class ParityCop : RoleBase
{
    private const int Id = 6900;
    private static List<byte> PlayerIdList = [];
    private static Dictionary<byte, int> RoundCheckLimit = [];
    private static Dictionary<byte, byte> FirstPick = [];

    private static readonly string[] PcEgoistCountMode =
    [
        "EgoistCountMode.Original",
        "EgoistCountMode.Neutral"
    ];

    private static OptionItem TryHideMsg;
    private static OptionItem ParityCheckLimitMax;
    private static OptionItem ParityCheckLimitPerMeeting;
    private static OptionItem ParityCheckTargetKnow;
    private static OptionItem ParityCheckOtherTargetKnow;
    private static OptionItem ParityCheckEgoistCountType;
    public static OptionItem ParityCheckBaitCountType;
    private static OptionItem ParityCheckRevealTargetTeam;
    public static OptionItem ParityAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.ParityCop);

        TryHideMsg = new BooleanOptionItem(Id + 10, "ParityCopTryHideMsg", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetColor(Color.green);

        ParityCheckLimitMax = new IntegerOptionItem(Id + 11, "MaxParityCheckLimit", new(0, 20, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);

        ParityCheckLimitPerMeeting = new IntegerOptionItem(Id + 12, "ParityCheckLimitPerMeeting", new(1, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);

        ParityCheckEgoistCountType = new StringOptionItem(Id + 13, "ParityCheckEgoistickCountMode", PcEgoistCountMode, 0, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);

        ParityCheckBaitCountType = new BooleanOptionItem(Id + 14, "ParityCheckBaitCountMode", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);

        ParityCheckTargetKnow = new BooleanOptionItem(Id + 15, "ParityCheckTargetKnow", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);

        ParityCheckOtherTargetKnow = new BooleanOptionItem(Id + 16, "ParityCheckOtherTargetKnow", true, TabGroup.CrewmateRoles)
            .SetParent(ParityCheckTargetKnow);

        ParityCheckRevealTargetTeam = new BooleanOptionItem(Id + 17, "ParityCheckRevealTarget", false, TabGroup.CrewmateRoles)
            .SetParent(ParityCheckOtherTargetKnow);

        ParityAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 18, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 19, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);

        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
    }

    public static int ParityCheckEgoistInt()
    {
        return ParityCheckEgoistCountType.GetString() == "EgoistCountMode.Original" ? 0 : 1;
    }

    public override void Init()
    {
        PlayerIdList = [];
        RoundCheckLimit = [];
        FirstPick = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(ParityCheckLimitMax.GetFloat());
        RoundCheckLimit.Add(playerId, ParityCheckLimitPerMeeting.GetInt());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void OnReportDeadBody()
    {
        RoundCheckLimit.Clear();
        foreach (byte pc in PlayerIdList) RoundCheckLimit.Add(pc, ParityCheckLimitPerMeeting.GetInt());
    }

    public static bool ParityCheckMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        string originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;

        if (!GameStates.IsInGame || pc == null) return false;

        if (!pc.Is(CustomRoles.ParityCop)) return false;

        int operate; // 1:ID 2:Check
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id"))
            operate = 1;
        else if (CheckCommand(ref msg, "compare|cp|cmp|比较", false))
            operate = 2;
        else
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("ParityCopDead"), pc.PlayerId, sendOption: SendOption.None);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                return true;
            case 2:
            {
                if (TryHideMsg.GetBool()) /*TryHideMsgForCompare();*/
                    ChatManager.SendPreviousMessagesToAll();
                else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                if (!MsgToPlayerAndRole(msg, out byte targetId1, out byte targetId2, out string error))
                {
                    Utils.SendMessage(error, pc.PlayerId);
                    return true;
                }

                PlayerControl target1 = Utils.GetPlayerById(targetId1);
                PlayerControl target2 = Utils.GetPlayerById(targetId2);

                if (target1 != null && target2 != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} checked {target1.GetNameWithRole().RemoveHtmlTags()} and {target2.GetNameWithRole().RemoveHtmlTags()}", "ParityCop");

                    bool outOfUses = pc.GetAbilityUseLimit() < 1;

                    if (outOfUses || RoundCheckLimit[pc.PlayerId] < 1)
                    {
                        if (outOfUses)
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI)
                                    Utils.SendMessage(GetString("ParityCheckMax"), pc.PlayerId);
                                else
                                    pc.ShowPopUp(GetString("ParityCheckMax"));

                                Logger.Msg("Check attempted at max checks per game", "Parity Cop");
                            }, 0.2f, "ParityCop 0");
                        }
                        else
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI)
                                    Utils.SendMessage(GetString("ParityCheckRound"), pc.PlayerId);
                                else
                                    pc.ShowPopUp(GetString("ParityCheckRound"));

                                Logger.Msg("Check attempted at max checks per meeting", "Parity Cop");
                            }, 0.2f, "ParityCop 1");
                        }

                        return true;
                    }

                    if (pc.PlayerId == target1.PlayerId || pc.PlayerId == target2.PlayerId)
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("ParityCheckSelf"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckSelf")) + "\n" + GetString("ParityCheckTitle"));

                            Logger.Msg("Check attempted on self", "Parity Cop");
                        }, 0.2f, "ParityCop 2");

                        return true;
                    }

                    if (target1.PlayerId == target2.PlayerId)
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("ParityCheckSame"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckSame")) + "\n" + GetString("ParityCheckTitle"));

                            Logger.Msg("Check attempted on same player", "Parity Cop");
                        }, 0.2f, "ParityCop 8");

                        return true;
                    }

                    if (target1.GetCustomRole().IsRevealingRole(target1) || target1.GetCustomSubRoles().Any(role => role.IsRevealingRole(target1)) || target2.GetCustomRole().IsRevealingRole(target2) || target2.GetCustomSubRoles().Any(role => role.IsRevealingRole(target2)))
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("ParityCheckReveal"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckReveal")) + "\n" + GetString("ParityCheckTitle"));

                            Logger.Msg("Check attempted on revealed role", "Parity Cop");
                        }, 0.2f, "ParityCop 3");

                        return true;
                    }

                    if (AreInSameTeam(target1, target2))
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(string.Format(GetString("ParityCheckTrue"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTrue")) + "\n" + GetString("ParityCheckTitle"));

                            Logger.Msg("Check attempt, result TRUE", "Parity Cop");
                        }, 0.2f, "ParityCop 4");
                    }
                    else
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(string.Format(GetString("ParityCheckFalse"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckFalse")) + "\n" + GetString("ParityCheckTitle"));

                            Logger.Msg("Check attempt, result FALSE", "Parity Cop");
                        }, 0.2f, "ParityCop 5");
                    }

                    if (ParityCheckTargetKnow.GetBool())
                    {
                        string textToSend = target1.GetRealName();
                        if (ParityCheckOtherTargetKnow.GetBool()) textToSend += $" & {target2.GetRealName()}";

                        textToSend += GetString("ParityCheckTargetMsg");

                        string textToSend1 = target2.GetRealName();
                        if (ParityCheckOtherTargetKnow.GetBool()) textToSend1 += $" & {target1.GetRealName()}";

                        textToSend1 += GetString("ParityCheckTargetMsg");

                        LateTask.New(() =>
                        {
                            Utils.SendMessage(textToSend, target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Utils.SendMessage(textToSend1, target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Logger.Msg("Check attempt, targets notified", "Parity Cop");
                        }, 0.2f, "ParityCop 7");

                        if (ParityCheckRevealTargetTeam.GetBool() && pc.AllTasksCompleted())
                        {
                            LateTask.New(() =>
                            {
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target2.GetRealName(), GetString(target2.GetTeam().ToString())), target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target1.GetRealName(), GetString(target1.GetTeam().ToString())), target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            }, 0.3f, "ParityCop 6");
                        }
                    }

                    pc.RpcRemoveAbilityUse();
                    RoundCheckLimit[pc.PlayerId]--;

                    MeetingManager.OnCompare(target1, target2);
                }

                break;
            }
        }

        return true;
    }

    public static bool AreInSameTeam(PlayerControl first, PlayerControl second)
    {
        CustomRoles firstRole = first.GetCustomRole();
        CustomRoles secondRole = second.GetCustomRole();

        RoleBase firstRoleClass = Main.PlayerStates[first.PlayerId].Role;
        RoleBase secondRoleClass = Main.PlayerStates[second.PlayerId].Role;

        List<CustomRoles> firstSubRoles = first.GetCustomSubRoles();
        List<CustomRoles> secondSubRoles = second.GetCustomSubRoles();

        Team firstTeam = first.GetTeam();
        Team secondTeam = second.GetTeam();

        switch (firstRoleClass)
        {
            case Lawyer when Lawyer.Target[first.PlayerId] == second.PlayerId:
            case Totocalcio tc when tc.BetPlayer == second.PlayerId:
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == second.PlayerId:
            case Necromancer when secondRoleClass is Deathknight:
                return true;
        }

        switch (secondRoleClass)
        {
            case Lawyer when Lawyer.Target[second.PlayerId] == first.PlayerId:
            case Totocalcio tc when tc.BetPlayer == first.PlayerId:
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == first.PlayerId:
            case Necromancer when firstRoleClass is Deathknight:
                return true;
        }

        if (CustomTeamManager.AreInSameCustomTeam(first.PlayerId, second.PlayerId)) return true;
        if (firstSubRoles.Contains(CustomRoles.Bloodlust) || secondSubRoles.Contains(CustomRoles.Bloodlust)) return false;
        if (firstRole.IsNeutral() && secondRole.IsNeutral()) return false;

        if (firstSubRoles.Contains(CustomRoles.Rascal) && secondTeam != Team.Impostor) return false;
        if (secondSubRoles.Contains(CustomRoles.Rascal) && firstTeam != Team.Impostor) return false;

        return firstTeam == secondTeam || firstRole == secondRole;
    }

    private static bool MsgToPlayerAndRole(string msg, out byte id1, out byte id2, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        msg = msg.TrimStart().TrimEnd();
        Logger.Msg(msg, "ParityCop");

        string[] nums = msg.Split(" ");

        if (nums.Length < 2 || !int.TryParse(nums[0], out int num1) || !int.TryParse(nums[1], out int num2))
        {
            Logger.Msg($"nums.Length {nums.Length}, nums0 {nums[0]}, nums1 {nums[1]}", "ParityCop");
            id1 = byte.MaxValue;
            id2 = byte.MaxValue;
            error = GetString("ParityCheckHelp");
            return false;
        }

        id1 = Convert.ToByte(num1);
        id2 = Convert.ToByte(num2);

        PlayerControl target1 = Utils.GetPlayerById(id1);
        PlayerControl target2 = Utils.GetPlayerById(id2);

        if (target1 == null || !target1.IsAlive() || target2 == null || !target2.IsAlive())
        {
            error = GetString("ParityCheckNull");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        string[] comList = command.Split('|');

        foreach (string str in comList)
        {
            if (exact)
            {
                if (msg == "/" + str) return true;
            }
            else
            {
                if (msg.StartsWith("/" + str))
                {
                    msg = msg.Replace("/" + str, string.Empty);
                    return true;
                }
            }
        }

        return false;
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        byte lpcId = reader.ReadByte();
        Logger.Msg($"RPC: Comparing ID {playerId}, Inspector ID {lpcId}", "Inspector UI");
        PickForCompare(playerId, lpcId);
    }

    private static void ParityCopOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Inspector UI");
        byte lpcId = PlayerControl.LocalPlayer.PlayerId;

        if (AmongUsClient.Instance.AmHost)
            PickForCompare(playerId, lpcId);
        else
        {
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ParityCopCommand, SendOption.Reliable, AmongUsClient.Instance.HostId);
            w.Write(playerId);
            w.Write(lpcId);
            AmongUsClient.Instance.FinishRpcImmediately(w);
        }
    }

    private static void PickForCompare(byte playerId, byte lpcId)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;

        if (FirstPick.TryGetValue(lpcId, out byte firstPick))
        {
            ParityCheckMsg(PlayerControl.LocalPlayer, $"/cp {playerId} {firstPick}");
            FirstPick.Remove(lpcId);
        }
        else
            FirstPick.Add(lpcId, playerId);
    }

    private static void CreateParityCopButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates)
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.ParityCopIcon.png", 170f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => ParityCopOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.ParityCop) && PlayerControl.LocalPlayer.IsAlive())
                CreateParityCopButton(__instance);
        }
    }
}
