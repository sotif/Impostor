using System.Numerics;
using Impostor.Api;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects.Components
{
    internal partial class InnerCustomNetworkTransform : InnerNetObject
    {
        private readonly ILogger<InnerCustomNetworkTransform> _logger;
        private readonly Game _game;

        private ushort _lastSequenceId;
        private Vector2 _targetSyncPosition;
        private Vector2 _targetSyncVelocity;

        public InnerCustomNetworkTransform(ILogger<InnerCustomNetworkTransform> logger, InnerPlayerControl playerControl, Game game)
        {
            _logger = logger;
            _game = game;

            Rpcs[RpcCalls.SnapTo] = Rpc.Sync((_, _, reader) =>
            {
                if (!playerControl.PlayerInfo.IsImpostor)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SnapTo)} as crewmate");
                }

                Rpc21SnapTo.Deserialize(reader, out var position, out var minSid);

                SnapTo(position, minSid);
            });
        }

        private static bool SidGreaterThan(ushort newSid, ushort prevSid)
        {
            var num = (ushort)(prevSid + (uint) short.MaxValue);

            return (int) prevSid < (int) num
                ? newSid > prevSid && newSid <= num
                : newSid > prevSid || newSid <= num;
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            if (initialState)
            {
                writer.Write(_lastSequenceId);
                writer.Write(_targetSyncPosition);
                writer.Write(_targetSyncVelocity);
                return true;
            }

            // TODO: DirtyBits == 0 return false.
            _lastSequenceId++;

            writer.Write(_lastSequenceId);
            writer.Write(_targetSyncPosition);
            writer.Write(_targetSyncVelocity);
            return true;
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            var sequenceId = reader.ReadUInt16();

            if (initialState)
            {
                _lastSequenceId = sequenceId;
                _targetSyncPosition = reader.ReadVector2();
                _targetSyncVelocity = reader.ReadVector2();
            }
            else
            {
                if (!sender.IsOwner(this))
                {
                    throw new ImpostorCheatException($"Client attempted to send unowned {nameof(InnerCustomNetworkTransform)} data");
                }

                if (target != null)
                {
                    throw new ImpostorCheatException($"Client attempted to send {nameof(InnerCustomNetworkTransform)} data to a specific player, must be broadcast");
                }

                if (!SidGreaterThan(sequenceId, _lastSequenceId))
                {
                    return;
                }

                _lastSequenceId = sequenceId;
                _targetSyncPosition = reader.ReadVector2();
                _targetSyncVelocity = reader.ReadVector2();
            }
        }

        private void SnapTo(Vector2 position, ushort minSid)
        {
            if (!SidGreaterThan(minSid, _lastSequenceId))
            {
                return;
            }

            _lastSequenceId = minSid;
            _targetSyncPosition = position;
            _targetSyncVelocity = Vector2.Zero;
        }
    }
}
