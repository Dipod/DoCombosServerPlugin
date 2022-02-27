namespace CommonGameLogic
{
    // class to represent internal specification of player
    public class InternalPlayer
    {
        public readonly int id = 0;
        public readonly bool isLocal = false;
        public readonly string nickname = "";

        // default constructor
        public InternalPlayer(int id, bool isLocal = false, string nickname = "")
        {
            this.id = id;
            this.isLocal = isLocal;
            this.nickname = nickname;
        }
    }
}