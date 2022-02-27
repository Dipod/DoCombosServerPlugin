namespace CommonGameLogic.PhotonData
{
    // class for serialize or deserialize swap coins data
    public class PhotonSwapCoinsDataEventContent
    {
        public readonly Coordinates cell1Coordinates;
        public readonly Coordinates cell2Coordinates;
        public readonly int actorPlayerId;

        public PhotonSwapCoinsDataEventContent(object eventContent)
        {
            int[] parcedEventContent = (int[])eventContent;
            cell1Coordinates = new Coordinates(parcedEventContent[0], parcedEventContent[1]);
            cell2Coordinates = new Coordinates(parcedEventContent[2], parcedEventContent[3]);
            actorPlayerId = parcedEventContent[4];
        }

        public PhotonSwapCoinsDataEventContent(Coordinates cell1Coordinates, Coordinates cell2Coordinates, int actorPlayerId)
        {
            this.cell1Coordinates = cell1Coordinates;
            this.cell2Coordinates = cell2Coordinates;
            this.actorPlayerId = actorPlayerId;
        }

        public int[] EventContent
        {
            get
            {
                int[] eventContent = { cell1Coordinates.x, cell1Coordinates.y, cell2Coordinates.x, cell2Coordinates.y, actorPlayerId };
                return eventContent;
            }
        }
    }
}