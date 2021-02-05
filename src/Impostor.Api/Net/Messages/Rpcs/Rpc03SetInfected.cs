namespace Impostor.Api.Net.Messages.Rpcs
{
    public static class Rpc03SetInfected
    {
        public static void Serialize(IMessageWriter writer, byte[] infectedIds)
        {
            writer.WritePacked(infectedIds.Length);

            foreach (var infectedId in infectedIds)
            {
                writer.Write(infectedId);
            }
        }

        public static void Deserialize(IMessageReader reader, out byte[] infectedIds)
        {
            var length = reader.ReadPackedInt32();
            infectedIds = new byte[length];

            for (var i = 0; i < length; i++)
            {
                var infectedId = reader.ReadByte();
                infectedIds[i] = infectedId;
            }
        }
    }
}
