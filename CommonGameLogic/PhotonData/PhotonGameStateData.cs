using System;
using System.Collections.Generic;

namespace CommonGameLogic.PhotonData
{
    // class for serialize or deserialize game state data
    public class PhotonGameStateData
    {
        public readonly float cash;
        public readonly float actionTimeout;
        public readonly IReadonlyCellState[,] cellsState;
        public readonly Dictionary<int, int> score;
        private readonly int _otherPlayerId;

        public PhotonGameStateData(object eventContent)
        {
            byte[] parcedEventContent = (byte[])eventContent;
            int offset = 0;

            cash = DeserializeFloat(parcedEventContent, ref offset);
            actionTimeout = DeserializeFloat(parcedEventContent, ref offset);

            score = new Dictionary<int, int>();
            for (int i = 0; i < 2; i++)
            {
                int playerId = DeserializeInt(parcedEventContent, ref offset);
                int playerScore = DeserializeInt(parcedEventContent, ref offset);
                score.Add(playerId, playerScore);
            }

            cellsState = new IReadonlyCellState[6, 6];
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    int owner = DeserializeInt(parcedEventContent, ref offset);
                    int price = DeserializeInt(parcedEventContent, ref offset);
                    bool isLocal = DeserializeBool(parcedEventContent, ref offset);
                    bool isFixed = DeserializeBool(parcedEventContent, ref offset);
                    cellsState[x, y] = new CellState(owner, price, isLocal, isFixed);
                }
            }
        }

        public PhotonGameStateData(GameState gameState, int otherPlayerId)
        {
            cash = gameState.GetPlayerCash(otherPlayerId);
            actionTimeout = gameState.GetPlayerActionTimeout(otherPlayerId);
            cellsState = gameState.GetCellsState();
            score = gameState.GetScore();
            _otherPlayerId = otherPlayerId;
        }

        public byte[] EventContent
        {
            get
            {
                if (score.Count != 2 || cellsState.Length != 36) // Now we have only 6 * 6 game field and only 2 players
                {
                    throw new NotImplementedException();
                }

                byte[] eventContent = new byte[388]; // total 384 bytes. see comments below
                int offset = 0;

                SerializeValue(cash, ref eventContent, ref offset); // 4 bytes
                SerializeValue(actionTimeout, ref eventContent, ref offset); // 4 bytes

                foreach (var playerScore in score) // 8*2 bytes for 2 players, total 16 bytes
                {
                    SerializeValue(playerScore.Key, ref eventContent, ref offset);
                    SerializeValue(playerScore.Value, ref eventContent, ref offset);
                }

                for (int x = 0; x < 6; x++) // 10 bytes on iteration, 36 iterations, total 360 bytes
                {
                    for (int y = 0; y < 6; y++)
                    {
                        int owner = cellsState[x, y].GetOwner();
                        SerializeValue(owner, ref eventContent, ref offset);
                        SerializeValue(cellsState[x, y].GetPrice(), ref eventContent, ref offset);
                        bool isLocal = owner == _otherPlayerId;
                        SerializeValue(isLocal, ref eventContent, ref offset);
                        SerializeValue(cellsState[x, y].IsFixed(), ref eventContent, ref offset);
                    }
                }

                return eventContent;
            }
        }

        private void SerializeValue(float value, ref byte[] result, ref int offset)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            buffer.CopyTo(result, offset);
            offset += 4;
        }

        private void SerializeValue(int value, ref byte[] result, ref int offset)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            buffer.CopyTo(result, offset);
            offset += 4;
        }

        private void SerializeValue(bool value, ref byte[] result, ref int offset)
        {
            BitConverter.GetBytes(value).CopyTo(result, offset);
            offset += 1;
        }

        private float DeserializeFloat(byte[] data, ref int offset)
        {
            byte[] buffer = new byte[4];
            Array.Copy(data, offset, buffer, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            offset += 4;
            return BitConverter.ToSingle(buffer, 0);
        }

        private int DeserializeInt(byte[] data, ref int offset)
        {
            byte[] buffer = new byte[4];
            Array.Copy(data, offset, buffer, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }
            offset += 4;
            return BitConverter.ToInt32(buffer, 0);
        }

        private bool DeserializeBool(byte[] data, ref int offset)
        {
            bool result = BitConverter.ToBoolean(data, offset);
            offset++;
            return result;
        }
    }
}