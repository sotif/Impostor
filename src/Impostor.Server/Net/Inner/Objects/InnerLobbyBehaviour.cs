using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;

namespace Impostor.Server.Net.Inner.Objects
{
    internal class InnerLobbyBehaviour : InnerNetObject, IInnerLobbyBehaviour
    {
        private readonly IGame _game;

        public InnerLobbyBehaviour(IGame game)
        {
            _game = game;

            Components.Add(this);
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new System.NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            throw new System.NotImplementedException();
        }
    }
}
