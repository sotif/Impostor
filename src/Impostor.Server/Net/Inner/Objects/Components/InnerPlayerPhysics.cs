using System;
using Impostor.Api;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Events.Player;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects.Components
{
    internal partial class InnerPlayerPhysics : InnerNetObject
    {
        private readonly ILogger<InnerPlayerPhysics> _logger;
        private readonly InnerPlayerControl _playerControl;
        private readonly IEventManager _eventManager;
        private readonly Game _game;

        public InnerPlayerPhysics(ILogger<InnerPlayerPhysics> logger, InnerPlayerControl playerControl, IEventManager eventManager, Game game)
        {
            _logger = logger;
            _playerControl = playerControl;
            _eventManager = eventManager;
            _game = game;

            Rpcs[RpcCalls.EnterVent] = Rpc.Async(async (sender, _, reader) =>
            {
                if (!playerControl.PlayerInfo.IsImpostor)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SnapTo)} as crewmate");
                }

                Rpc19EnterVent.Deserialize(reader, out var ventId);
                await _eventManager.CallAsync(new PlayerVentEvent(_game, sender, _playerControl, (VentLocation)ventId, true));
            });

            Rpcs[RpcCalls.ExitVent] = Rpc.Async(async (sender, _, reader) =>
            {
                if (!playerControl.PlayerInfo.IsImpostor)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SnapTo)} as crewmate");
                }

                Rpc20ExitVent.Deserialize(reader, out var ventId);
                await _eventManager.CallAsync(new PlayerVentEvent(_game, sender, _playerControl, (VentLocation)ventId, false));
            });
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            throw new NotImplementedException();
        }
    }
}
