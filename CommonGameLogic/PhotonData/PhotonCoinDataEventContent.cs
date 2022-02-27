namespace CommonGameLogic.PhotonData
{
    // class for serialize or deserialize coin data for put, add and open events
    public class PhotonCoinDataEventContent
    {
        public readonly Coordinates coordinates;
        public readonly int actorPlayerId;
        public readonly int priceOfAction;

        public PhotonCoinDataEventContent(object eventContent)
        {
            int[] parcedEventContent = (int[])eventContent;
            coordinates = new Coordinates(parcedEventContent[0], parcedEventContent[1]);
            actorPlayerId = parcedEventContent[2];
            priceOfAction = parcedEventContent[3];
        }

        public PhotonCoinDataEventContent(Coordinates coordinates, int actorPlayerId, int priceOfAction)
        {
            this.coordinates = coordinates;
            this.actorPlayerId = actorPlayerId;
            this.priceOfAction = priceOfAction;
        }

        public int[] EventContent
        {
            get
            {
                int[] eventContent = { coordinates.x, coordinates.y, actorPlayerId, priceOfAction };
                return eventContent;
            }
        }

        public override string ToString()
        {
            return string.Format("coordinates {0} playerId {1} price {2}", coordinates.ToString(), actorPlayerId, priceOfAction);
        }
    }
}