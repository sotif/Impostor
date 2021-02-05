using System.Threading.Tasks;
using Impostor.Api.Net.Messages;
using Impostor.Server.Net.State;

namespace Impostor.Server.Net.Inner
{
    internal class Rpc
    {
        private Rpc(CancellableHandleRpcDelegate handleRpc, RpcOptions options)
        {
            HandleRpc = handleRpc;
            Options = options;
        }

        public delegate ValueTask<bool> CancellableHandleRpcDelegate(ClientPlayer sender, ClientPlayer? target, IMessageReader reader);

        public delegate ValueTask AsynchronousHandleRpcDelegate(ClientPlayer sender, ClientPlayer? target, IMessageReader reader);

        public delegate void SynchronousDelegate(ClientPlayer sender, ClientPlayer? target, IMessageReader reader);

        public CancellableHandleRpcDelegate HandleRpc { get; }

        public RpcOptions Options { get; }

        public static Rpc Cancellable(CancellableHandleRpcDelegate handleRpc, RpcOptions? options = null)
        {
            return new Rpc(handleRpc, options ?? new RpcOptions());
        }

        public static Rpc Async(AsynchronousHandleRpcDelegate handleRpc, RpcOptions? options = null)
        {
            return new Rpc(
                async (sender, target, reader) =>
                {
                    await handleRpc.Invoke(sender, target, reader);
                    return true;
                }, options ?? new RpcOptions());
        }

        public static Rpc Sync(SynchronousDelegate handleRpc, RpcOptions? options = null)
        {
            return new Rpc(
                (sender, target, reader) =>
                {
                    handleRpc.Invoke(sender, target, reader);
                    return new ValueTask<bool>(true);
                }, options ?? new RpcOptions());
        }

        public enum RpcTargetType
        {
            Broadcast,
            Target,
            Both,
        }

        public class RpcOptions
        {
            public RpcTargetType TargetType { get; init; }

            public bool CheckOwnership { get; init; } = true;

            public bool RequireHost { get; init; }
        }
    }
}
