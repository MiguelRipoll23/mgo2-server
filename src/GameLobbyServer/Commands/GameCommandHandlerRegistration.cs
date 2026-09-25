using Mgo2Server.GameLobbyServer.Commands.Game;
using Mgo2Server.GameLobbyServer.Commands.Game.Characters;
using Mgo2Server.GameLobbyServer.Commands.Game.Chat;
using Mgo2Server.GameLobbyServer.Commands.Game.Clans;
using Mgo2Server.GameLobbyServer.Commands.Game.Events;
using Mgo2Server.GameLobbyServer.Commands.Game.Mail;
using Mgo2Server.GameLobbyServer.Commands.Game.Rooms;
using Mgo2Server.Shared.Constants;
using Mgo2Server.Shared.Tcp;
using Mgo2Server.Shared.Types;
using Microsoft.Extensions.DependencyInjection;

namespace Mgo2Server.GameLobbyServer.Commands;

/// <summary>
/// Binds the command identifiers of the gameplay lobby to the handlers that
/// serve them, grouped by the domain they belong to.
/// </summary>
public static class GameCommandHandlerRegistration
{
    /// <summary>Registers every gameplay lobby handler.</summary>
    /// <param name="registry">Registry to register with.</param>
    public static void RegisterCommandHandlers(CommandRegistry registry)
    {
        RegisterLobbyCommands(registry);
        RegisterCharacterCommands(registry);
        RegisterRoomCommands(registry);
        RegisterMailCommands(registry);
        RegisterClanCommands(registry);
        RegisterEventCommands(registry);
    }

    /// <summary>Registers every gameplay lobby handler with the container.</summary>
    /// <param name="services">Container to register with.</param>
    public static IServiceCollection AddCommandHandlers(this IServiceCollection services)
    {
        // Session.
        services.AddTransient<DisconnectHandler>();
        services.AddTransient<KeepAliveHandler>();

        // Lobby lifecycle and lobby select.
        services.AddTransient<EchoHandler>();
        services.AddTransient<GameCheckSessionHandler>();
        services.AddTransient<GetPlayerDataHandler>();
        services.AddTransient<GetLobbyDisconnectHandler>();
        services.AddTransient<TrainingConnectHandler>();
        services.AddTransient<GetGameLobbyInfoHandler>();
        services.AddTransient<ChatEchoHandler>();
        services.AddTransient<SendChatHandler>();

        // Characters.
        services.AddTransient<GetCharacterInfoHandler>();
        services.AddTransient<GetPersonalStatsHandler>();
        services.AddTransient<UpdatePersonalInfoHandler>();
        services.AddTransient<GetCharacterCardHandler>();
        services.AddTransient<GetPostGameInfoHandler>();
        services.AddTransient<CommitOutfitHandler>();
        services.AddTransient<UpdateChatMacrosHandler>();
        services.AddTransient<UpdateGameplayOptionsHandler>();
        services.AddTransient<UpdateUiSettingsHandler>();
        services.AddTransient<AddFriendsBlockedHandler>();
        services.AddTransient<RemoveFriendsBlockedHandler>();
        services.AddTransient<GetFriendsBlockedListHandler>();
        services.AddTransient<SearchPlayerHandler>();
        services.AddTransient<GetMatchHistoryHandler>();
        services.AddTransient<GetMatchDetailsHandler>();

        // Rooms.
        services.AddTransient<GetGameListHandler>();
        services.AddTransient<GetHostSettingsHandler>();
        services.AddTransient<CheckHostSettingsHandler>();
        services.AddTransient<GetGameDetailsHandler>();
        services.AddTransient<CreateGameHandler>();
        services.AddTransient<JoinGameHandler>();
        services.AddTransient<JoinGameFailedHandler>();
        services.AddTransient<QuitGameHandler>();
        services.AddTransient<RateHostHandler>();
        services.AddTransient<HostInGameInfoHandler>();
        services.AddTransient<HostPeerRegistrationHandler>();
        services.AddTransient<HostPlayerDisconnectedHandler>();
        services.AddTransient<HostSetPlayerTeamHandler>();
        services.AddTransient<HostPlayerConnectFinishHandler>();
        services.AddTransient<HostPassHandler>();
        services.AddTransient<PassRoundHandler>();
        services.AddTransient<SetGameHandler>();
        services.AddTransient<UpdatePingsHandler>();
        services.AddTransient<PutClientSettingHandler>();
        services.AddTransient<HostSkillExperienceHandler>();
        services.AddTransient<StartRoundHandler>();
        services.AddTransient<HostUpdateStatsHandler>();
        services.AddTransient<RoundStatisticsProcessor>();
        services.AddTransient<HostWeaponTalliesHandler>();
        services.AddTransient<StartAutomatchHandler>();
        services.AddTransient<CancelAutomatchHandler>();

        // Mail.
        services.AddTransient<SendMessageHandler>();
        services.AddTransient<GetMessagesHandler>();
        services.AddTransient<GetMessageContentsHandler>();
        services.AddTransient<DeleteMessageHandler>();

        // Events.
        services.AddTransient<GetEventInformationHandler>();
        services.AddTransient<GetEventInformationByIdHandler>();
        services.AddTransient<GetTeamCreateInformationHandler>();
        services.AddTransient<EventAdjacentRequestHandler>();
        services.AddTransient<CreateEventTeamHandler>();
        services.AddTransient<JoinEventTeamHandler>();
        services.AddTransient<LeaveEventTeamHandler>();
        services.AddTransient<SetEventEntryDecisionHandler>();
        services.AddTransient<GetEventTeamListHandler>();
        services.AddTransient<GetEventTeamDetailsHandler>();
        services.AddTransient<InviteEventTeamMembersHandler>();
        services.AddTransient<AnswerEventTeamInvitationHandler>();
        services.AddTransient<ReportEventInvitationStatusHandler>();
        services.AddTransient<GetSurvivalBattleListHandler>();
        services.AddTransient<GetBattleTeamInformationHandler>();
        services.AddTransient<GetAssignedGameSnapshotHandler>();
        services.AddTransient<ConfirmEventAssignmentHandler>();
        services.AddTransient<RemoveEventGameEntryHandler>();
        services.AddTransient<GetAssignedMemberInformationHandler>();
        services.AddTransient<GetEventListHandler>();
        services.AddTransient<GetEventDetailHandler>();
        services.AddTransient<GetAssignedGameDetailHandler>();
        services.AddTransient<ReserveTournamentEntryHandler>();
        services.AddTransient<ReportEventGameResultHandler>();
        services.AddTransient<EnterEventHandler>();

        // The same screen the lobby select opens, serving a pending entry.
        services.AddTransient<GetGameEntryInfoHandler>();

        // Clans.
        services.AddTransient<CreateClanHandler>();
        services.AddTransient<DisbandClanHandler>();
        services.AddTransient<LeaveClanHandler>();
        services.AddTransient<ApplyToClanHandler>();
        services.AddTransient<UpdateClanStateHandler>();
        services.AddTransient<TransferClanLeadershipHandler>();
        services.AddTransient<SetEmblemEditorHandler>();
        services.AddTransient<UpdateClanCommentHandler>();
        services.AddTransient<UpdateClanNoticeHandler>();
        services.AddTransient<GetClanEmblemLobbyHandler>();
        services.AddTransient<GetClanEmblemHandler>();
        services.AddTransient<GetClanDetailHandler>();
        services.AddTransient<SetClanEmblemHandler>();
        services.AddTransient<GetClanListHandler>();
        services.AddTransient<GetClanMemberInfoHandler>();
        services.AddTransient<AcceptClanJoinHandler>();
        services.AddTransient<DeclineClanJoinHandler>();
        services.AddTransient<BanishClanMemberHandler>();
        services.AddTransient<GetClanRosterHandler>();
        services.AddTransient<GetClanStatsHandler>();
        services.AddTransient<GetClanInfoHandler>();
        services.AddTransient<SearchClanHandler>();
        services.AddTransient<GetClanApplicantsHandler>();

        return services;
    }

    private static void RegisterLobbyCommands(CommandRegistry registry)
    {
        registry.Register<EchoHandler>(ServerType.GameplayLobby, CommandConstants.Echo);
        registry.Register<GameCheckSessionHandler>(ServerType.GameplayLobby, CommandConstants.GameCheckSession);
        registry.Register<GetPlayerDataHandler>(ServerType.GameplayLobby, CommandConstants.GetPlayerData);
        registry.Register<GetLobbyDisconnectHandler>(ServerType.GameplayLobby, CommandConstants.GetLobbyDisconnect);
        registry.Register<TrainingConnectHandler>(ServerType.GameplayLobby, CommandConstants.TrainingConnect);
        registry.Register<GetGameLobbyInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetGameLobbyInfo);
        registry.Register<ChatEchoHandler>(ServerType.GameplayLobby, CommandConstants.ChatEcho);
        registry.Register<SendChatHandler>(ServerType.GameplayLobby, CommandConstants.SendChat);
        registry.Register<DisconnectHandler>(ServerType.GameplayLobby, CommandConstants.Disconnect);
        registry.Register<KeepAliveHandler>(ServerType.GameplayLobby, CommandConstants.KeepAlive);
    }

    private static void RegisterCharacterCommands(CommandRegistry registry)
    {
        registry.Register<GetCharacterInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetCharacterInfo);
        registry.Register<GetPersonalStatsHandler>(ServerType.GameplayLobby, CommandConstants.GetPersonalStats);
        registry.Register<UpdatePersonalInfoHandler>(ServerType.GameplayLobby, CommandConstants.UpdatePersonalInfo);
        registry.Register<GetCharacterCardHandler>(ServerType.GameplayLobby, CommandConstants.GetCharacterCard);
        registry.Register<GetPostGameInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetPostGameInfo);
        registry.Register<CommitOutfitHandler>(ServerType.GameplayLobby, CommandConstants.CommitOutfit);
        registry.Register<UpdateChatMacrosHandler>(ServerType.GameplayLobby, CommandConstants.UpdateChatMacros);
        registry.Register<UpdateGameplayOptionsHandler>(ServerType.GameplayLobby, CommandConstants.UpdateGameplayOptions);
        registry.Register<UpdateUiSettingsHandler>(ServerType.GameplayLobby, CommandConstants.UpdateUiSettings);
        registry.Register<AddFriendsBlockedHandler>(ServerType.GameplayLobby, CommandConstants.AddFriendsBlocked);
        registry.Register<RemoveFriendsBlockedHandler>(ServerType.GameplayLobby, CommandConstants.RemoveFriendsBlocked);
        registry.Register<GetFriendsBlockedListHandler>(ServerType.GameplayLobby, CommandConstants.GetFriendsBlockedList);
        registry.Register<SearchPlayerHandler>(ServerType.GameplayLobby, CommandConstants.SearchPlayer);
        registry.Register<GetMatchHistoryHandler>(ServerType.GameplayLobby, CommandConstants.GetMatchHistory);
        registry.Register<GetMatchDetailsHandler>(ServerType.GameplayLobby, CommandConstants.GetMatchDetails);
    }

    private static void RegisterRoomCommands(CommandRegistry registry)
    {
        registry.Register<GetGameListHandler>(ServerType.GameplayLobby, CommandConstants.GetGameList);
        registry.Register<GetHostSettingsHandler>(ServerType.GameplayLobby, CommandConstants.GetHostSettings);
        registry.Register<CheckHostSettingsHandler>(ServerType.GameplayLobby, CommandConstants.CheckHostSettings);
        registry.Register<GetGameDetailsHandler>(ServerType.GameplayLobby, CommandConstants.GetGameDetails);
        registry.Register<CreateGameHandler>(ServerType.GameplayLobby, CommandConstants.CreateGame);
        registry.Register<JoinGameHandler>(ServerType.GameplayLobby, CommandConstants.JoinGame);
        registry.Register<JoinGameFailedHandler>(ServerType.GameplayLobby, CommandConstants.JoinGameFailed);
        registry.Register<QuitGameHandler>(ServerType.GameplayLobby, CommandConstants.QuitGame);
        registry.Register<RateHostHandler>(ServerType.GameplayLobby, CommandConstants.RateHost);
        registry.Register<HostInGameInfoHandler>(ServerType.GameplayLobby, CommandConstants.HostInGameInfo);
        registry.Register<HostPeerRegistrationHandler>(ServerType.GameplayLobby, CommandConstants.HostPlayerConnected);
        registry.Register<HostPlayerDisconnectedHandler>(ServerType.GameplayLobby, CommandConstants.HostPlayerDisconnected);
        registry.Register<HostSetPlayerTeamHandler>(ServerType.GameplayLobby, CommandConstants.HostSetPlayerTeam);
        registry.Register<HostPlayerConnectFinishHandler>(ServerType.GameplayLobby, CommandConstants.HostPlayerConnectFinish);
        registry.Register<HostPassHandler>(ServerType.GameplayLobby, CommandConstants.HostPass);
        registry.Register<PassRoundHandler>(ServerType.GameplayLobby, CommandConstants.PassRound);
        registry.Register<SetGameHandler>(ServerType.GameplayLobby, CommandConstants.SetGame);
        registry.Register<UpdatePingsHandler>(ServerType.GameplayLobby, CommandConstants.UpdatePings);
        registry.Register<PutClientSettingHandler>(ServerType.GameplayLobby, CommandConstants.PutClientSetting);
        registry.Register<HostSkillExperienceHandler>(ServerType.GameplayLobby, CommandConstants.HostSkillExperience);
        registry.Register<StartRoundHandler>(ServerType.GameplayLobby, CommandConstants.StartRound);
        registry.Register<StartRoundHandler>(ServerType.GameplayLobby, CommandConstants.StartRoundAlias);
        registry.Register<HostUpdateStatsHandler>(ServerType.GameplayLobby, CommandConstants.HostUpdateStats);
        registry.Register<HostWeaponTalliesHandler>(ServerType.GameplayLobby, CommandConstants.HostWeaponTallies);
        registry.Register<StartAutomatchHandler>(ServerType.GameplayLobby, CommandConstants.StartAutomatch);
        registry.Register<CancelAutomatchHandler>(ServerType.GameplayLobby, CommandConstants.CancelAutomatch);
    }

    private static void RegisterMailCommands(CommandRegistry registry)
    {
        registry.Register<SendMessageHandler>(ServerType.GameplayLobby, CommandConstants.SendMessage);
        registry.Register<GetMessagesHandler>(ServerType.GameplayLobby, CommandConstants.GetMessages);
        registry.Register<GetMessageContentsHandler>(ServerType.GameplayLobby, CommandConstants.GetMessageContents);
        registry.Register<DeleteMessageHandler>(ServerType.GameplayLobby, CommandConstants.DeleteMessage);
    }

    private static void RegisterEventCommands(CommandRegistry registry)
    {
        registry.Register<GetEventInformationHandler>(ServerType.GameplayLobby, CommandConstants.GetEventInformation);
        registry.Register<GetEventInformationByIdHandler>(ServerType.GameplayLobby, CommandConstants.GetEventInformationById);
        registry.Register<GetTeamCreateInformationHandler>(ServerType.GameplayLobby, CommandConstants.GetTeamCreateInformation);

        // One handler answers all seven unrecovered screens; the registry refuses
        // two handlers for one command, not one handler for many.
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetSurvivalAdjacentList);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetTournamentAdjacentList);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetEventAdjacentDetail);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetEventAdjacentState);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetEventAdjacentEntry);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.GetEventAdjacentTeam);
        registry.Register<EventAdjacentRequestHandler>(ServerType.GameplayLobby, CommandConstants.SyncEventViewState);

        registry.Register<CreateEventTeamHandler>(ServerType.GameplayLobby, CommandConstants.CreateEventTeam);
        registry.Register<JoinEventTeamHandler>(ServerType.GameplayLobby, CommandConstants.JoinEventTeam);
        registry.Register<LeaveEventTeamHandler>(ServerType.GameplayLobby, CommandConstants.LeaveEventTeam);
        registry.Register<SetEventEntryDecisionHandler>(ServerType.GameplayLobby, CommandConstants.SetEventEntryDecision);
        registry.Register<GetEventTeamListHandler>(ServerType.GameplayLobby, CommandConstants.GetEventTeamList);
        registry.Register<GetEventTeamDetailsHandler>(ServerType.GameplayLobby, CommandConstants.GetEventTeamDetails);
        registry.Register<InviteEventTeamMembersHandler>(ServerType.GameplayLobby, CommandConstants.InviteEventTeamMembers);
        registry.Register<AnswerEventTeamInvitationHandler>(ServerType.GameplayLobby, CommandConstants.AnswerEventTeamInvitation);
        registry.Register<ReportEventInvitationStatusHandler>(ServerType.GameplayLobby, CommandConstants.ReportEventInvitationStatus);
        registry.Register<GetSurvivalBattleListHandler>(ServerType.GameplayLobby, CommandConstants.GetSurvivalBattleList);
        registry.Register<GetBattleTeamInformationHandler>(ServerType.GameplayLobby, CommandConstants.GetBattleTeamInformation);
        registry.Register<GetAssignedGameSnapshotHandler>(ServerType.GameplayLobby, CommandConstants.GetActiveGameSnapshot);
        registry.Register<ConfirmEventAssignmentHandler>(ServerType.GameplayLobby, CommandConstants.ConfirmActiveGameAssignment);
        registry.Register<RemoveEventGameEntryHandler>(ServerType.GameplayLobby, CommandConstants.RemoveEventGameEntry);
        registry.Register<GetAssignedMemberInformationHandler>(ServerType.GameplayLobby, CommandConstants.GetAssignedMemberInformation);
        registry.Register<GetEventListHandler>(ServerType.GameplayLobby, CommandConstants.GetEventList);
        registry.Register<GetEventDetailHandler>(ServerType.GameplayLobby, CommandConstants.GetEventDetail);
        registry.Register<GetAssignedGameDetailHandler>(ServerType.GameplayLobby, CommandConstants.GetAssignedGameDetail);
        registry.Register<ReserveTournamentEntryHandler>(ServerType.GameplayLobby, CommandConstants.ReserveTournamentEntry);
        registry.Register<ReportEventGameResultHandler>(ServerType.GameplayLobby, CommandConstants.ReportEventGameResult);
        registry.Register<EnterEventHandler>(ServerType.GameplayLobby, CommandConstants.EnterEvent);
        registry.Register<GetGameEntryInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetGameEntryInfo);
    }

    private static void RegisterClanCommands(CommandRegistry registry)
    {
        registry.Register<CreateClanHandler>(ServerType.GameplayLobby, CommandConstants.CreateClan);
        registry.Register<DisbandClanHandler>(ServerType.GameplayLobby, CommandConstants.DisbandClan);
        registry.Register<LeaveClanHandler>(ServerType.GameplayLobby, CommandConstants.LeaveClan);
        registry.Register<ApplyToClanHandler>(ServerType.GameplayLobby, CommandConstants.ApplyToClan);
        registry.Register<UpdateClanStateHandler>(ServerType.GameplayLobby, CommandConstants.UpdateClanState);
        registry.Register<TransferClanLeadershipHandler>(ServerType.GameplayLobby, CommandConstants.TransferClanLeadership);
        registry.Register<SetEmblemEditorHandler>(ServerType.GameplayLobby, CommandConstants.SetEmblemEditor);
        registry.Register<UpdateClanCommentHandler>(ServerType.GameplayLobby, CommandConstants.UpdateClanComment);
        registry.Register<UpdateClanNoticeHandler>(ServerType.GameplayLobby, CommandConstants.UpdateClanNotice);
        registry.Register<GetClanEmblemLobbyHandler>(ServerType.GameplayLobby, CommandConstants.GetClanEmblemLobby);
        registry.Register<GetClanEmblemHandler>(ServerType.GameplayLobby, CommandConstants.GetClanEmblem);
        registry.Register<GetClanDetailHandler>(ServerType.GameplayLobby, CommandConstants.GetClanDetail);
        registry.Register<SetClanEmblemHandler>(ServerType.GameplayLobby, CommandConstants.SetClanEmblem);
        registry.Register<GetClanListHandler>(ServerType.GameplayLobby, CommandConstants.GetClanList);
        registry.Register<GetClanMemberInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetClanMemberInfo);
        registry.Register<AcceptClanJoinHandler>(ServerType.GameplayLobby, CommandConstants.AcceptClanJoin);
        registry.Register<DeclineClanJoinHandler>(ServerType.GameplayLobby, CommandConstants.DeclineClanJoin);
        registry.Register<BanishClanMemberHandler>(ServerType.GameplayLobby, CommandConstants.BanishClanMember);
        registry.Register<GetClanRosterHandler>(ServerType.GameplayLobby, CommandConstants.GetClanRoster);
        registry.Register<GetClanStatsHandler>(ServerType.GameplayLobby, CommandConstants.GetClanStats);
        registry.Register<GetClanInfoHandler>(ServerType.GameplayLobby, CommandConstants.GetClanInfo);
        registry.Register<SearchClanHandler>(ServerType.GameplayLobby, CommandConstants.SearchClan);
        registry.Register<GetClanApplicantsHandler>(ServerType.GameplayLobby, CommandConstants.GetClanApplicants);
    }
}
