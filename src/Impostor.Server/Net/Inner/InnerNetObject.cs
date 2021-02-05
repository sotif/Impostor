using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner;
using Impostor.Api.Net.Messages;
using Impostor.Server.Net.State;

namespace Impostor.Server.Net.Inner
{
    internal abstract class InnerNetObject : GameObject, IInnerNetObject
    {
        private const int HostInheritId = -2;

        public uint NetId { get; internal set; }

        public int OwnerId { get; internal set; }

        public SpawnFlags SpawnFlags { get; internal set; }

        protected IDictionary<RpcCalls, Rpc> Rpcs { get; } = new Dictionary<RpcCalls, Rpc>();

        public ValueTask<bool> HandleRpc(ClientPlayer sender, ClientPlayer? target, RpcCalls call, IMessageReader reader)
        {
            if (Rpcs.TryGetValue(call, out var rpc))
            {
                if (rpc.Options.CheckOwnership && !sender.IsOwner(this))
                {
                    throw new ImpostorCheatException($"Client sent {call} to an unowned {GetType().Name}");
                }

                if (rpc.Options.RequireHost && !sender.IsHost)
                {
                    throw new ImpostorCheatException($"Client attempted to send {call} as non-host");
                }

                switch (rpc.Options.TargetType)
                {
                    case Rpc.RpcTargetType.Target when target == null:
                        throw new ImpostorCheatException($"Client sent {call} as a broadcast instead to specific player");

                    case Rpc.RpcTargetType.Broadcast when target != null:
                        throw new ImpostorCheatException($"Client sent {call} to a specific player instead of broadcast");

                    case Rpc.RpcTargetType.Both:
                        break;
                }

                return rpc.HandleRpc(sender, target, reader);
            }

            throw new ImpostorCheatException($"Client sent unregistered call: {call}");
        }

        public abstract bool Serialize(IMessageWriter writer, bool initialState);

        public abstract void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState);

        public bool IsOwnedBy(IClientPlayer player)
        {
            return OwnerId == player.Client.Id ||
                   (OwnerId == HostInheritId && player.IsHost);
        }
    }
}
