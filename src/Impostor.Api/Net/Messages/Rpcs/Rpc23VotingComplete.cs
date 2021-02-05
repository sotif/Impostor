using System;

namespace Impostor.Api.Net.Messages.Rpcs
{
    public static class Rpc23VotingComplete
    {
        public static void Serialize(IMessageWriter writer, ReadOnlyMemory<byte> states, byte playerId, bool tie)
        {
            writer.Write(states);
            writer.Write(playerId);
            writer.Write(tie);
        }

        public static void Deserialize(IMessageReader reader, out ReadOnlyMemory<byte> states, out byte playerId, out bool tie)
        {
            states = reader.ReadBytesAndSize();
            playerId = reader.ReadByte();
            tie = reader.ReadBoolean();
        }
    }
}
