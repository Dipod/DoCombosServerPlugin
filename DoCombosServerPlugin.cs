using CommonGameLogic;
using CommonGameLogic.PhotonData;
using Photon.Hive.Plugin;
using System;
using System.Collections.Generic;

namespace DoCombosServerPlugin
{
    public class DoCombosServerPlugin : PluginBase
    {
        private const int START_GAME_DELAY = 5;
        private const int TICK_INTERVAL_MS = 40; // ~20 frames per second with overhead

        private IPluginLogger _pluginLogger;
        private readonly GameState _gameState = new GameState();
        private readonly Dictionary<int, bool> _playersReadyState = new Dictionary<int, bool>();
        private object _startGameTimeout = null;
        private object _mainGameLoop = null;
        private int _lastTickCount;

        #region PluginBase overrided methods

        public override string Name => "DoCombosServerPlugin";

        public override bool SetupInstance(IPluginHost host, Dictionary<string, string> config, out string errorMsg)
        {
            _pluginLogger = host.CreateLogger(Name);
            GameScore.OnPlayerWin += OnPlayerWin;
            return base.SetupInstance(host, config, out errorMsg);
        }

        public override void OnRaiseEvent(IRaiseEventCallInfo info)
        {
            switch (info.Request.EvCode)
            {
                case (byte)CustomEvents.clientReadyForGame: // message from client, that he is ready for game
                    OnClientReady(info.ActorNr);
                    info.Cancel();
                    break;
                case (byte)CustomEvents.putCoinFromClient: // request from client to put coin
                    TryPutCoin(new PhotonCoinDataEventContent(info.Request.Data));
                    info.Cancel();
                    break;
                case (byte)CustomEvents.addPriceToCoinFromClient: // request from client client to add price to coin 
                    TryAddPriceToCoin(new PhotonCoinDataEventContent(info.Request.Data));
                    info.Cancel();
                    break;
                case (byte)CustomEvents.fixComboFromClient: // request from client to fix combo 
                    TryFixCombo(new PhotonComboEventContent(info.Request.Data));
                    info.Cancel();
                    break;
                case (byte)CustomEvents.openCoinFromClient: // request from client to add price to coin 
                    TryOpenCoin(new PhotonCoinDataEventContent(info.Request.Data));
                    info.Cancel();
                    break;
                case (byte)CustomEvents.swapCoinsFromClient: // request from client to swap coins 
                    TrySwapCoins(new PhotonSwapCoinsDataEventContent(info.Request.Data));
                    info.Cancel();
                    break;
                default:
                    info.Continue();
                    break;
            }
        }

        public override void OnLeave(ILeaveGameCallInfo info)
        {
            if (!(_mainGameLoop is null))
            {
                StopTimer(ref _mainGameLoop);

                Dictionary<int, int> score = _gameState.GetScore();

                string firstPlayerNickname = PluginHost.GameActors[0].Nickname;
                int firstPlayerScore = score[PluginHost.GameActors[0].ActorNr];

                string secondPlayerNickname = PluginHost.GameActors[1].Nickname;
                int secondPlayerScore = score[PluginHost.GameActors[1].ActorNr];

                _pluginLogger.Info(string.Format("Game Over. {0} disconnected. {1} {2}:{3} {4}", info.Nickname, firstPlayerNickname, firstPlayerScore, secondPlayerScore, secondPlayerNickname));

                RaiseEvent((byte)CustomEvents.playerDisconnected, info.Nickname, DefaultSendParameters());
            }

            info.Continue();
        }

        public override void BeforeCloseGame(IBeforeCloseGameCallInfo info)
        {
            StopTimer(ref _startGameTimeout);
            StopTimer(ref _mainGameLoop);
            GameScore.OnPlayerWin -= OnPlayerWin;
            info.Continue();
        }

        #endregion

        #region handlers for client requests
        // handler for put coin request from client
        private void TryPutCoin(PhotonCoinDataEventContent coinData)
        {
            if (_gameState.IsPutAvailable(coinData.coordinates)
                && _gameState.AreConditionsForInstantActionFulfilled(coinData.actorPlayerId, coinData.priceOfAction))
            {
                //_pluginLogger.Info(string.Concat("Put. ", coinData.ToString()));
                PutCoinOnClients(coinData);
                _gameState.PutCoin(coinData.coordinates, new InternalPlayer(coinData.actorPlayerId), coinData.priceOfAction);
            }
            else
            {
                //_pluginLogger.Info(string.Concat("Put error. Sent game state data to player ", coinData.actorPlayerId));
                SendError(coinData.actorPlayerId);
                // sync game state, because client's local gamestate allow make action, that not allowed on server
                SendGameState(coinData.actorPlayerId, new PhotonGameStateData(_gameState, coinData.actorPlayerId));
            }
        }

        // handler for add price to coin request from client
        public void TryAddPriceToCoin(PhotonCoinDataEventContent coinData)
        {
            if (_gameState.IsAddPriceToCoinAvailable(coinData.coordinates, coinData.priceOfAction, coinData.actorPlayerId)
                && _gameState.AreConditionsForInstantActionFulfilled(coinData.actorPlayerId, coinData.priceOfAction))
            {
                AddPriceToCoinOnClients(coinData);
                _gameState.AddPriceToCoin(coinData.coordinates, coinData.actorPlayerId, coinData.priceOfAction);
            }
            else
            {
                SendError(coinData.actorPlayerId);
                // sync game state, because client's local gamestate allow make action, that not allowed on server
                SendGameState(coinData.actorPlayerId, new PhotonGameStateData(_gameState, coinData.actorPlayerId));
            }
        }

        // handler for fix combo request from client
        public void TryFixCombo(PhotonComboEventContent comboData)
        {
            if (Combo.IsCombo(_gameState.GetCellStatesByCoordinates(comboData.comboCoordinates), false))
            {
                FixComboOnClients(comboData);
                _gameState.FixCombo(comboData.comboCoordinates, comboData.actorPlayerId);
            }
            else
            {
                SendError(comboData.actorPlayerId);
                // sync game state, because client's local gamestate allow make action, that not allowed on server
                SendGameState(comboData.actorPlayerId, new PhotonGameStateData(_gameState, comboData.actorPlayerId));
            }
        }

        // handler for open coin request from client
        public void TryOpenCoin(PhotonCoinDataEventContent coinData)
        {
            if (_gameState.IsOpenCoinAvailable(coinData.coordinates, coinData.actorPlayerId)
                && _gameState.AreConditionsForInstantActionFulfilled(coinData.actorPlayerId, _gameState.PriceToOpenCell(coinData.coordinates)))
            {
                OpenCoinOnClients(coinData);
                _gameState.OpenCoin(coinData.coordinates, coinData.actorPlayerId);
            }
            else
            {
                SendError(coinData.actorPlayerId);
                // sync game state, because client's local gamestate allow make action, that not allowed on server
                SendGameState(coinData.actorPlayerId, new PhotonGameStateData(_gameState, coinData.actorPlayerId));
            }
        }

        // handler for open coin request from client
        public void TrySwapCoins(PhotonSwapCoinsDataEventContent swapCoinsData)
        {
            if (_gameState.IsCellsCanSwap(swapCoinsData.cell1Coordinates, swapCoinsData.cell2Coordinates)
                && _gameState.AreConditionsForInstantActionFulfilled(swapCoinsData.actorPlayerId, _gameState.GetSwapPrice(swapCoinsData.cell1Coordinates, swapCoinsData.cell2Coordinates)))
            {
                SwapCoinsOnClients(swapCoinsData);
                _gameState.SwapCoins(swapCoinsData.cell1Coordinates, swapCoinsData.cell2Coordinates, swapCoinsData.actorPlayerId);
            }
            else
            {
                SendError(swapCoinsData.actorPlayerId);
                // sync game state, because client's local gamestate allow make action, that not allowed on server
                SendGameState(swapCoinsData.actorPlayerId, new PhotonGameStateData(_gameState, swapCoinsData.actorPlayerId));
            }
        }

        private void OnClientReady(int ActorNr)
        {
            _playersReadyState.Add(ActorNr, true);
            CheckReadyAndStartGame();
        }

        #endregion

        #region server events

        public void OnPlayerWin(int winnerId)
        {
            StopTimer(ref _mainGameLoop);

            Dictionary<int, int> score = _gameState.GetScore();

            string winnerNickname = "lost nickname";

            string firstPlayerNickname = PluginHost.GameActors[0].Nickname;
            int firstPlayerScore = score[PluginHost.GameActors[0].ActorNr];
            if (winnerId == PluginHost.GameActors[0].ActorNr)
            {
                winnerNickname = firstPlayerNickname;
            }

            string secondPlayerNickname = PluginHost.GameActors[1].Nickname;
            int secondPlayerScore = score[PluginHost.GameActors[1].ActorNr];
            if (winnerId == PluginHost.GameActors[1].ActorNr)
            {
                winnerNickname = secondPlayerNickname;
            }

            _pluginLogger.Info(string.Format("Game Over. {0} win. {1} {2}:{3} {4}", winnerNickname, firstPlayerNickname, firstPlayerScore, secondPlayerScore, secondPlayerNickname));

            RaiseEvent((byte)CustomEvents.playerWin, winnerId, DefaultSendParameters());
        }

        // method to send error from server to client
        private void SendError(int actorPlayerId)
        {
            RaiseEvent((byte)CustomEvents.error, true, new List<int> { actorPlayerId }, DefaultSendParameters());
        }

        // method to send game state from server to client
        private void SendGameState(int actorPlayerId, PhotonGameStateData gameStateData)
        {
            RaiseEvent((byte)CustomEvents.gameStateSync, gameStateData.EventContent, new List<int> { actorPlayerId }, DefaultSendParameters());
        }

        // method to send game state from server to client
        private void SendCountdownStarted()
        {
            RaiseEvent((byte)CustomEvents.launchStartGameCountdown, START_GAME_DELAY, DefaultSendParameters());
        }

        private void SendGameStarted()
        {
            RaiseEvent((byte)CustomEvents.startGame, true, DefaultSendParameters());
        }

        // method to raise put coin event from server to clients
        private void PutCoinOnClients(PhotonCoinDataEventContent coinData)
        {
            RaiseEvent((byte)CustomEvents.putCoinFromServer, coinData.EventContent, DefaultSendParameters());
        }

        // method to raise add price to coin event from server to clients
        private void AddPriceToCoinOnClients(PhotonCoinDataEventContent coinData)
        {
            RaiseEvent((byte)CustomEvents.addPriceToCoinFromServer, coinData.EventContent, DefaultSendParameters());
        }

        // method to raise fix combo event from server to clients
        private void FixComboOnClients(PhotonComboEventContent comboData)
        {
            RaiseEvent((byte)CustomEvents.fixComboFromServer, comboData.EventContent, DefaultSendParameters());
        }

        // method to raise open coin event from server to clients
        private void OpenCoinOnClients(PhotonCoinDataEventContent coinData)
        {
            RaiseEvent((byte)CustomEvents.openCoinFromServer, coinData.EventContent, DefaultSendParameters());
        }

        // method to raise swap coins event from server to clients
        private void SwapCoinsOnClients(PhotonSwapCoinsDataEventContent swapCoinsData)
        {
            RaiseEvent((byte)CustomEvents.swapCoinsFromServer, swapCoinsData.EventContent, DefaultSendParameters());
        }

        #endregion

        private void CheckReadyAndStartGame()
        {
            if (IsRoomFull())
            {
                bool isAllPlayersReady = true;
                foreach (var playerReadyState in _playersReadyState)
                {
                    if (!playerReadyState.Value)
                    {
                        isAllPlayersReady = false;
                        break;
                    }
                }

                if (isAllPlayersReady)
                {
                    AddPlayers();
                    LaunchStartGameCountdown();
                }
            }
        }

        private void AddPlayers()
        {
            foreach (var gameActor in PluginHost.GameActors)
            {
                _gameState.AddPlayer(new InternalPlayer(gameActor.ActorNr, false, gameActor.Nickname));
            }
        }

        private bool IsRoomFull()
        {
            return PluginHost.GameActors.Count == (byte)PluginHost.GameProperties[GameParameters.MaxPlayers];
        }

        private void LaunchStartGameCountdown()
        {
            SendCountdownStarted();
            _startGameTimeout = PluginHost.CreateOneTimeTimer(null, StartGame, START_GAME_DELAY * 1000);
        }

        private void StartGame()
        {
            SendGameStarted();
            StopTimer(ref _startGameTimeout);

            _lastTickCount = Environment.TickCount;
            _mainGameLoop = PluginHost.CreateTimer(Tick, TICK_INTERVAL_MS, TICK_INTERVAL_MS);
        }

        private void Tick()
        {
            float deltaTime = ((float)(Environment.TickCount - _lastTickCount)) / 1000;
            _lastTickCount = Environment.TickCount;

            _gameState.Tick(deltaTime);
        }

        private void StopTimer(ref object timer)
        {
            if (!(timer is null))
            {
                PluginHost.StopTimer(timer);
                timer = null;
            }
        }

        #region Raise event wrappers

        private SendParameters DefaultSendParameters()
        {
            SendParameters result = default(SendParameters);
            result.DeliveryMode = PluginDeliveryMode.Reliable;
            return result;
        }

        private void RaiseEvent(byte eventCode,
                                object eventData,
                                SendParameters sendParams = default(SendParameters),
                                byte receiverGroup = ReciverGroup.All,
                                int senderActorNumber = 0,
                                byte cachingOption = CacheOperations.DoNotCache,
                                byte interestGroup = 0)
        {
            Dictionary<byte, object> parameters = new Dictionary<byte, object>();
            parameters.Add(ParameterCode.CustomEventContent, eventData);
            parameters.Add(ParameterCode.ActorNr, senderActorNumber);
            PluginHost.BroadcastEvent(receiverGroup, senderActorNumber, interestGroup, eventCode, parameters, cachingOption, sendParams);
        }

        private void RaiseEvent(byte eventCode,
                                object eventData,
                                IList<int> targetActorsNumbers,
                                SendParameters sendParams = default(SendParameters),
                                int senderActorNumber = 0,
                                byte cachingOption = CacheOperations.DoNotCache)
        {
            Dictionary<byte, object> parameters = new Dictionary<byte, object>();
            parameters.Add(ParameterCode.CustomEventContent, eventData);
            parameters.Add(ParameterCode.ActorNr, senderActorNumber);
            PluginHost.BroadcastEvent(targetActorsNumbers, senderActorNumber, eventCode, parameters, cachingOption, sendParams);
        }

        #endregion
    }

    // Class from Photon.Realtime
    /// <summary>Class for constants. Codes for parameters of Operations and Events.</summary>
    /// <remarks>These constants are used internally.</remarks>
    public class ParameterCode
    {
        /// <summary>(237) A bool parameter for creating games. If set to true, no room events are sent to the clients on join and leave. Default: false (and not sent).</summary>
        public const byte SuppressRoomEvents = 237;

        /// <summary>(236) Time To Live (TTL) for a room when the last player leaves. Keeps room in memory for case a player re-joins soon. In milliseconds.</summary>
        public const byte EmptyRoomTTL = 236;

        /// <summary>(235) Time To Live (TTL) for an 'actor' in a room. If a client disconnects, this actor is inactive first and removed after this timeout. In milliseconds.</summary>
        public const byte PlayerTTL = 235;

        /// <summary>(234) Optional parameter of OpRaiseEvent and OpSetCustomProperties to forward the event/operation to a web-service.</summary>
        public const byte EventForward = 234;

        /// <summary>(233) Optional parameter of OpLeave in async games. If false, the player does abandons the game (forever). By default players become inactive and can re-join.</summary>
        [Obsolete("Use: IsInactive")]
        public const byte IsComingBack = (byte)233;

        /// <summary>(233) Used in EvLeave to describe if a user is inactive (and might come back) or not. In rooms with PlayerTTL, becoming inactive is the default case.</summary>
        public const byte IsInactive = (byte)233;

        /// <summary>(232) Used when creating rooms to define if any userid can join the room only once.</summary>
        public const byte CheckUserOnJoin = (byte)232;

        /// <summary>(231) Code for "Check And Swap" (CAS) when changing properties.</summary>
        public const byte ExpectedValues = (byte)231;

        /// <summary>(230) Address of a (game) server to use.</summary>
        public const byte Address = 230;

        /// <summary>(229) Count of players in this application in a rooms (used in stats event)</summary>
        public const byte PeerCount = 229;

        /// <summary>(228) Count of games in this application (used in stats event)</summary>
        public const byte GameCount = 228;

        /// <summary>(227) Count of players on the master server (in this app, looking for rooms)</summary>
        public const byte MasterPeerCount = 227;

        /// <summary>(225) User's ID</summary>
        public const byte UserId = 225;

        /// <summary>(224) Your application's ID: a name on your own Photon or a GUID on the Photon Cloud</summary>
        public const byte ApplicationId = 224;

        /// <summary>(223) Not used currently (as "Position"). If you get queued before connect, this is your position</summary>
        public const byte Position = 223;

        /// <summary>(223) Modifies the matchmaking algorithm used for OpJoinRandom. Allowed parameter values are defined in enum MatchmakingMode.</summary>
        public const byte MatchMakingType = 223;

        /// <summary>(222) List of RoomInfos about open / listed rooms</summary>
        public const byte GameList = 222;

        /// <summary>(221) Internally used to establish encryption</summary>
        public const byte Token = 221;

        /// <summary>(220) Version of your application</summary>
        public const byte AppVersion = 220;

        /// <summary>(210) Internally used in case of hosting by Azure</summary>
        [Obsolete("TCP routing was removed after becoming obsolete.")]
        public const byte AzureNodeInfo = 210;	// only used within events, so use: EventCode.AzureNodeInfo

        /// <summary>(209) Internally used in case of hosting by Azure</summary>
        [Obsolete("TCP routing was removed after becoming obsolete.")]
        public const byte AzureLocalNodeId = 209;

        /// <summary>(208) Internally used in case of hosting by Azure</summary>
        [Obsolete("TCP routing was removed after becoming obsolete.")]
        public const byte AzureMasterNodeId = 208;

        /// <summary>(255) Code for the gameId/roomName (a unique name per room). Used in OpJoin and similar.</summary>
        public const byte RoomName = (byte)255;

        /// <summary>(250) Code for broadcast parameter of OpSetProperties method.</summary>
        public const byte Broadcast = (byte)250;

        /// <summary>(252) Code for list of players in a room.</summary>
        public const byte ActorList = (byte)252;

        /// <summary>(254) Code of the Actor of an operation. Used for property get and set.</summary>
        public const byte ActorNr = (byte)254;

        /// <summary>(249) Code for property set (Hashtable).</summary>
        public const byte PlayerProperties = (byte)249;

        /// <summary>(245) Code of data/custom content of an event. Used in OpRaiseEvent.</summary>
        public const byte CustomEventContent = (byte)245;

        /// <summary>(245) Code of data of an event. Used in OpRaiseEvent.</summary>
        public const byte Data = (byte)245;

        /// <summary>(244) Code used when sending some code-related parameter, like OpRaiseEvent's event-code.</summary>
        /// <remarks>This is not the same as the Operation's code, which is no longer sent as part of the parameter Dictionary in Photon 3.</remarks>
        public const byte Code = (byte)244;

        /// <summary>(248) Code for property set (Hashtable).</summary>
        public const byte GameProperties = (byte)248;

        /// <summary>
        /// (251) Code for property-set (Hashtable). This key is used when sending only one set of properties.
        /// If either ActorProperties or GameProperties are used (or both), check those keys.
        /// </summary>
        public const byte Properties = (byte)251;

        /// <summary>(253) Code of the target Actor of an operation. Used for property set. Is 0 for game</summary>
        public const byte TargetActorNr = (byte)253;

        /// <summary>(246) Code to select the receivers of events (used in Lite, Operation RaiseEvent).</summary>
        public const byte ReceiverGroup = (byte)246;

        /// <summary>(247) Code for caching events while raising them.</summary>
        public const byte Cache = (byte)247;

        /// <summary>(241) Bool parameter of CreateGame Operation. If true, server cleans up roomcache of leaving players (their cached events get removed).</summary>
        public const byte CleanupCacheOnLeave = (byte)241;

        /// <summary>(240) Code for "group" operation-parameter (as used in Op RaiseEvent).</summary>
        public const byte Group = 240;

        /// <summary>(239) The "Remove" operation-parameter can be used to remove something from a list. E.g. remove groups from player's interest groups.</summary>
        public const byte Remove = 239;

        /// <summary>(239) Used in Op Join to define if UserIds of the players are broadcast in the room. Useful for FindFriends and reserving slots for expected users.</summary>
        public const byte PublishUserId = 239;

        /// <summary>(238) The "Add" operation-parameter can be used to add something to some list or set. E.g. add groups to player's interest groups.</summary>
        public const byte Add = 238;

        /// <summary>(218) Content for EventCode.ErrorInfo and internal debug operations.</summary>
        public const byte Info = 218;

        /// <summary>(217) This key's (byte) value defines the target custom authentication type/service the client connects with. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationType = 217;

        /// <summary>(216) This key's (string) value provides parameters sent to the custom authentication type/service the client connects with. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationParams = 216;

        /// <summary>(215) Makes the server create a room if it doesn't exist. OpJoin uses this to always enter a room, unless it exists and is full/closed.</summary>
        // public const byte CreateIfNotExists = 215;

        /// <summary>(215) The JoinMode enum defines which variant of joining a room will be executed: Join only if available, create if not exists or re-join.</summary>
        /// <remarks>Replaces CreateIfNotExists which was only a bool-value.</remarks>
        public const byte JoinMode = 215;

        /// <summary>(214) This key's (string or byte[]) value provides parameters sent to the custom authentication service setup in Photon Dashboard. Used in OpAuthenticate</summary>
        public const byte ClientAuthenticationData = 214;

        /// <summary>(203) Code for MasterClientId, which is synced by server. When sent as op-parameter this is code 203.</summary>
        /// <remarks>Tightly related to GamePropertyKey.MasterClientId.</remarks>
        public const byte MasterClientId = (byte)203;

        /// <summary>(1) Used in Op FindFriends request. Value must be string[] of friends to look up.</summary>
        public const byte FindFriendsRequestList = (byte)1;

        /// <summary>(2) Used in Op FindFriends request. An integer containing option-flags to filter the results.</summary>
        public const byte FindFriendsOptions = (byte)2;

        /// <summary>(1) Used in Op FindFriends response. Contains bool[] list of online states (false if not online).</summary>
        public const byte FindFriendsResponseOnlineList = (byte)1;

        /// <summary>(2) Used in Op FindFriends response. Contains string[] of room names ("" where not known or no room joined).</summary>
        public const byte FindFriendsResponseRoomIdList = (byte)2;

        /// <summary>(213) Used in matchmaking-related methods and when creating a room to name a lobby (to join or to attach a room to).</summary>
        public const byte LobbyName = (byte)213;

        /// <summary>(212) Used in matchmaking-related methods and when creating a room to define the type of a lobby. Combined with the lobby name this identifies the lobby.</summary>
        public const byte LobbyType = (byte)212;

        /// <summary>(211) This (optional) parameter can be sent in Op Authenticate to turn on Lobby Stats (info about lobby names and their user- and game-counts).</summary>
        public const byte LobbyStats = (byte)211;

        /// <summary>(210) Used for region values in OpAuth and OpGetRegions.</summary>
        public const byte Region = (byte)210;

        /// <summary>(209) Path of the WebRPC that got called. Also known as "WebRpc Name". Type: string.</summary>
        public const byte UriPath = 209;

        /// <summary>(208) Parameters for a WebRPC as: Dictionary&lt;string, object&gt;. This will get serialized to JSon.</summary>
        public const byte WebRpcParameters = 208;

        /// <summary>(207) ReturnCode for the WebRPC, as sent by the web service (not by Photon, which uses ErrorCode). Type: byte.</summary>
        public const byte WebRpcReturnCode = 207;

        /// <summary>(206) Message returned by WebRPC server. Analog to Photon's debug message. Type: string.</summary>
        public const byte WebRpcReturnMessage = 206;

        /// <summary>(205) Used to define a "slice" for cached events. Slices can easily be removed from cache. Type: int.</summary>
        public const byte CacheSliceIndex = 205;

        /// <summary>(204) Informs the server of the expected plugin setup.</summary>
        /// <remarks>
        /// The operation will fail in case of a plugin mismatch returning error code PluginMismatch 32751(0x7FFF - 16).
        /// Setting string[]{} means the client expects no plugin to be setup.
        /// Note: for backwards compatibility null omits any check.
        /// </remarks>
        public const byte Plugins = 204;

        /// <summary>(202) Used by the server in Operation Responses, when it sends the nickname of the client (the user's nickname).</summary>
        public const byte NickName = 202;

        /// <summary>(201) Informs user about name of plugin load to game</summary>
        public const byte PluginName = 201;

        /// <summary>(200) Informs user about version of plugin load to game</summary>
        public const byte PluginVersion = 200;

        /// <summary>(196) Cluster info provided in OpAuthenticate/OpAuthenticateOnce responses.</summary>
        public const byte Cluster = 196;

        /// <summary>(195) Protocol which will be used by client to connect master/game servers. Used for nameserver.</summary>
        public const byte ExpectedProtocol = 195;

        /// <summary>(194) Set of custom parameters which are sent in auth request.</summary>
        public const byte CustomInitData = 194;

        /// <summary>(193) How are we going to encrypt data.</summary>
        public const byte EncryptionMode = 193;

        /// <summary>(192) Parameter of Authentication, which contains encryption keys (depends on AuthMode and EncryptionMode).</summary>
        public const byte EncryptionData = 192;

        /// <summary>(191) An int parameter summarizing several boolean room-options with bit-flags.</summary>
        public const byte RoomOptionFlags = 191;
    }
}