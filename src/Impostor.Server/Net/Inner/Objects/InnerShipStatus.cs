using System;
using System.Collections.Generic;
using Impostor.Api;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Net.Inner.Objects.Systems;
using Impostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal class InnerShipStatus : InnerNetObject, IInnerShipStatus
    {
        private readonly ILogger<InnerShipStatus> _logger;
        private readonly Game _game;
        private readonly Dictionary<SystemTypes, ISystemType> _systems;

        public InnerShipStatus(ILogger<InnerShipStatus> logger, Game game)
        {
            _logger = logger;
            _game = game;

            _systems = new Dictionary<SystemTypes, ISystemType>
            {
                [SystemTypes.Electrical] = new SwitchSystem(),
                [SystemTypes.MedBay] = new MedScanSystem(),
                [SystemTypes.Reactor] = new ReactorSystemType(),
                [SystemTypes.LifeSupp] = new LifeSuppSystemType(),
                [SystemTypes.Security] = new SecurityCameraSystemType(),
                [SystemTypes.Comms] = new HudOverrideSystemType(),
                [SystemTypes.Doors] = new DoorsSystemType(_game),
            };

            _systems.Add(SystemTypes.Sabotage, new SabotageSystemType(new[]
            {
                (IActivatable)_systems[SystemTypes.Comms], (IActivatable)_systems[SystemTypes.Reactor], (IActivatable)_systems[SystemTypes.LifeSupp], (IActivatable)_systems[SystemTypes.Electrical],
            }));

            Components.Add(this);

            Rpcs[RpcCalls.CloseDoorsOfType] = Rpc.Sync(
                async (sender, target, reader) =>
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CloseDoorsOfType)} to wrong destinition, must be host");
                    }

                    if (!sender.Character.PlayerInfo.IsImpostor)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CloseDoorsOfType)} as crewmate");
                    }

                    Rpc27CloseDoorsOfType.Deserialize(reader, out var systemType);
                },
                new Rpc.RpcOptions
                {
                    CheckOwnership = false, TargetType = Rpc.RpcTargetType.Target,
                }
            );

            Rpcs[RpcCalls.RepairSystem] = Rpc.Sync(
                async (sender, target, reader) =>
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.RepairSystem)} to wrong destinition, must be host");
                    }

                    Rpc28RepairSystem.Deserialize(reader, game, out var systemType, out var player, out var amount);

                    if (systemType == SystemTypes.Sabotage && !sender.Character.PlayerInfo.IsImpostor)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.RepairSystem)} for {systemType} as crewmate");
                    }
                },
                new Rpc.RpcOptions
                {
                    CheckOwnership = false, TargetType = Rpc.RpcTargetType.Target,
                }
            );
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!sender.IsHost)
            {
                throw new ImpostorCheatException($"Client attempted to send data for {nameof(InnerShipStatus)} as non-host");
            }

            if (target != null)
            {
                throw new ImpostorCheatException($"Client attempted to send {nameof(InnerShipStatus)} data to a specific player, must be broadcast");
            }

            if (initialState)
            {
                // TODO: (_systems[SystemTypes.Doors] as DoorsSystemType).SetDoors();
                foreach (var systemType in SystemTypeHelpers.AllTypes)
                {
                    if (_systems.TryGetValue(systemType, out var system))
                    {
                        system.Deserialize(reader, true);
                    }
                }
            }
            else
            {
                var count = reader.ReadPackedUInt32();

                foreach (var systemType in SystemTypeHelpers.AllTypes)
                {
                    // TODO: Not sure what is going on here, check.
                    if ((count & 1 << (int)(systemType & (SystemTypes.ShipTasks | SystemTypes.Doors))) != 0L)
                    {
                        if (_systems.TryGetValue(systemType, out var system))
                        {
                            system.Deserialize(reader, false);
                        }
                    }
                }
            }
        }
    }
}
