namespace Mgo2Server.Shared.Constants;

/// <summary>Command identifiers of the clan screens.</summary>
public static partial class CommandConstants
{
    /// <summary>Creates a clan.</summary>
    public const ushort CreateClan = 0x4b00;

    /// <summary>Carries the result of creating a clan.</summary>
    public const ushort CreateClanResult = 0x4b01;

    /// <summary>Disbands the caller's clan.</summary>
    public const ushort DisbandClan = 0x4b04;

    /// <summary>Carries the result of disbanding a clan.</summary>
    public const ushort DisbandClanResult = 0x4b05;

    /// <summary>Lists the clans.</summary>
    public const ushort GetClanList = 0x4b10;

    /// <summary>Opens the clan-list reply stream.</summary>
    public const ushort GetClanListStart = 0x4b11;

    /// <summary>Carries one clan-list page.</summary>
    public const ushort GetClanListPage = 0x4b12;

    /// <summary>Closes the clan-list reply stream.</summary>
    public const ushort GetClanListEnd = 0x4b13;

    /// <summary>Returns the full record of one clan.</summary>
    public const ushort GetClanMemberInfo = 0x4b20;

    /// <summary>Carries the full record of one clan.</summary>
    public const ushort GetClanMemberInfoResult = 0x4b21;

    /// <summary>Accepts a pending clan application.</summary>
    public const ushort AcceptClanJoin = 0x4b30;

    /// <summary>Carries the result of accepting an application.</summary>
    public const ushort AcceptClanJoinResult = 0x4b31;

    /// <summary>Declines a pending clan application.</summary>
    public const ushort DeclineClanJoin = 0x4b32;

    /// <summary>Carries the result of declining an application.</summary>
    public const ushort DeclineClanJoinResult = 0x4b33;

    /// <summary>Removes a member from a clan.</summary>
    public const ushort BanishClanMember = 0x4b36;

    /// <summary>Carries the result of removing a member.</summary>
    public const ushort BanishClanMemberResult = 0x4b37;

    /// <summary>Leaves the caller's clan.</summary>
    public const ushort LeaveClan = 0x4b40;

    /// <summary>Carries the result of leaving a clan.</summary>
    public const ushort LeaveClanResult = 0x4b41;

    /// <summary>Applies to join a clan.</summary>
    public const ushort ApplyToClan = 0x4b42;

    /// <summary>Carries the result of an application.</summary>
    public const ushort ApplyToClanResult = 0x4b43;

    /// <summary>Returns the caller's clan membership state.</summary>
    public const ushort UpdateClanState = 0x4b46;

    /// <summary>Carries the caller's clan membership state.</summary>
    public const ushort UpdateClanStateResult = 0x4b47;

    /// <summary>Returns the published emblem of a clan.</summary>
    public const ushort GetClanEmblemLobby = 0x4b48;

    /// <summary>Carries the published emblem of a clan.</summary>
    public const ushort GetClanEmblemLobbyResult = 0x4b49;

    /// <summary>Returns the published emblem of a clan.</summary>
    public const ushort GetClanEmblem = 0x4b4a;

    /// <summary>Carries the published emblem of a clan.</summary>
    public const ushort GetClanEmblemResult = 0x4b4b;

    /// <summary>Returns the emblem a clan is editing.</summary>
    public const ushort GetClanEmblemWorkInProgress = 0x4b4c;

    /// <summary>Carries the emblem a clan is editing.</summary>
    public const ushort GetClanEmblemWorkInProgressResult = 0x4b4d;

    /// <summary>Stores a clan emblem.</summary>
    public const ushort SetClanEmblem = 0x4b50;

    /// <summary>Carries the result of storing a clan emblem.</summary>
    public const ushort SetClanEmblemResult = 0x4b51;

    /// <summary>Returns the clan roster.</summary>
    public const ushort GetClanRoster = 0x4b52;

    /// <summary>Opens the clan-roster reply stream.</summary>
    public const ushort GetClanRosterStart = 0x4b53;

    /// <summary>Carries one clan-roster page.</summary>
    public const ushort GetClanRosterPage = 0x4b54;

    /// <summary>Closes the clan-roster reply stream.</summary>
    public const ushort GetClanRosterEnd = 0x4b55;

    /// <summary>Transfers clan leadership.</summary>
    public const ushort TransferClanLeadership = 0x4b60;

    /// <summary>Carries the result of a leadership transfer.</summary>
    public const ushort TransferClanLeadershipResult = 0x4b61;

    /// <summary>Opens or closes the emblem editor for a member.</summary>
    public const ushort SetEmblemEditor = 0x4b62;

    /// <summary>Carries the result of an emblem-editor change.</summary>
    public const ushort SetEmblemEditorResult = 0x4b63;

    /// <summary>Updates the clan comment.</summary>
    public const ushort UpdateClanComment = 0x4b64;

    /// <summary>Carries the result of a comment update.</summary>
    public const ushort UpdateClanCommentResult = 0x4b65;

    /// <summary>Updates the clan notice.</summary>
    public const ushort UpdateClanNotice = 0x4b66;

    /// <summary>Carries the result of a notice update.</summary>
    public const ushort UpdateClanNoticeResult = 0x4b67;

    /// <summary>Returns the clan statistics.</summary>
    public const ushort GetClanStats = 0x4b70;

    /// <summary>Carries the first clan-statistics packet.</summary>
    public const ushort GetClanStatsResult = 0x4b71;

    /// <summary>Carries the second clan-statistics packet.</summary>
    public const ushort GetClanStatsDetail = 0x4b72;

    /// <summary>Lists the pending clan applicants.</summary>
    public const ushort GetClanApplicants = 0x4b73;

    /// <summary>Opens the applicant-list reply stream.</summary>
    public const ushort GetClanApplicantsStart = 0x4b74;

    /// <summary>Carries one applicant-list page.</summary>
    public const ushort GetClanApplicantsPage = 0x4b75;

    /// <summary>Closes the applicant-list reply stream.</summary>
    public const ushort GetClanApplicantsEnd = 0x4b76;

    /// <summary>Returns the information shown on a clan card.</summary>
    public const ushort GetClanInfo = 0x4b80;

    /// <summary>Carries the information shown on a clan card.</summary>
    public const ushort GetClanInfoResult = 0x4b81;

    /// <summary>Searches for a clan.</summary>
    public const ushort SearchClan = 0x4b90;

    /// <summary>Opens the clan-search reply stream.</summary>
    public const ushort SearchClanStart = 0x4b91;

    /// <summary>Carries one clan-search page.</summary>
    public const ushort SearchClanPage = 0x4b92;

    /// <summary>Closes the clan-search reply stream.</summary>
    public const ushort SearchClanEnd = 0x4b93;
}
