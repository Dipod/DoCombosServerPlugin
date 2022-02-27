using System.Collections.Generic;

namespace CommonGameLogic.PhotonData
{
    // class for serialize or deserialize combo event content
    public class PhotonComboEventContent
    {
        public readonly List<Coordinates> comboCoordinates = new List<Coordinates>();
        public readonly int actorPlayerId;

        public PhotonComboEventContent(object eventContent)
        {
            int[] parcedEventContent = (int[])eventContent;
            int count = parcedEventContent[0];
            actorPlayerId = parcedEventContent[1];

            for (int i = 2; i < count * 2 + 2; i += 2)
            {
                comboCoordinates.Add(new Coordinates(parcedEventContent[i], parcedEventContent[i + 1]));
            }

        }

        public PhotonComboEventContent(List<Coordinates> comboCoordinates, int actorPlayerId)
        {
            this.comboCoordinates = comboCoordinates;
            this.actorPlayerId = actorPlayerId;
        }

        public int[] EventContent
        {
            get
            {
                int[] eventContent = new int[comboCoordinates.Count * 2 + 2];
                eventContent[0] = comboCoordinates.Count;
                eventContent[1] = actorPlayerId;
                int i = 2;
                foreach (var coordinates in comboCoordinates)
                {
                    eventContent[i] = coordinates.x;
                    eventContent[i + 1] = coordinates.y;
                    i += 2;
                }
                return eventContent;
            }
        }
    }
}