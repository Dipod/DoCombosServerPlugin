using System;

namespace CommonGameLogic
{
    public interface IReadonlyCellState
    {
        int GetOwner();
        int GetPrice();
        (int price, int owner, bool isLocalPlayer, bool isFixed) GetCellStateData();
        bool IsOwned();
        bool IsFixed();
        bool IsOwnedByPlayer(int playerId);
        int CompareTo(object obj);
        Coordinates GetCoordinates();
    }

    public class CellState : IComparable, IReadonlyCellState
    {
        private Coordinates _coordinates;

        private int _owner = -1;
        private int _price = 0;
        private bool _isLocalPlayer = false;
        private bool _isFixed = false;

        public delegate void CoinUpdate(Coordinates coordinates, int price, bool isLocalPlayer);
        public static event CoinUpdate OnCoinUpdate;

        public delegate void Fix(Coordinates coordinates);
        public static event Fix OnFix;

        public delegate void Open(Coordinates coordinates);
        public static event Open OnOpen;

        public CellState(Coordinates coordinates)
        {
            _coordinates = coordinates;
        }

        // This constructor for data serialization only
        public CellState(int owner, int price, bool isLocalPlayer, bool isFixed)
        {
            _owner = owner;
            _price = price;
            _isLocalPlayer = isLocalPlayer;
            _isFixed = isFixed;
        }

        public Coordinates GetCoordinates()
        {
            return _coordinates;
        }

        public int GetOwner()
        {
            return _owner;
        }

        public int GetPrice()
        {
            return _price;
        }

        public (int price, int owner, bool isLocalPlayer, bool isFixed) GetCellStateData()
        {
            return (_price, _owner, _isLocalPlayer, _isFixed);
        }

        public bool IsOwned()
        {
            return _owner != -1;
        }

        public bool IsFixed()
        {
            return _isFixed;
        }

        public bool IsOwnedByPlayer(int playerId)
        {
            return _owner == playerId;
        }

        public void PutCoin(int price, bool isLocalPlayer, int owner)
        {
            _price = price;
            _owner = owner;
            _isLocalPlayer = isLocalPlayer;

            OnCoinUpdate?.Invoke(_coordinates, _price, _isLocalPlayer);
        }

        public void AddPriceToCoin(int price)
        {
            _price += price;
            OnCoinUpdate?.Invoke(_coordinates, _price, _isLocalPlayer);
        }

        public void FixCell()
        {
            _isFixed = true;
            OnFix?.Invoke(_coordinates);
        }

        public void OpenCell()
        {
            _isFixed = false;
            OnOpen?.Invoke(_coordinates);
        }

        public bool UpdateCoin((int price, int owner, bool isLocalPlayer, bool isFixed) cellStateData)
        {
            if (CellStateChanged(cellStateData))
            {
                _price = cellStateData.price;
                _owner = cellStateData.owner;
                _isLocalPlayer = cellStateData.isLocalPlayer;
                _isFixed = cellStateData.isFixed;
                OnCoinUpdate?.Invoke(_coordinates, _price, _isLocalPlayer);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CellStateChanged((int price, int owner, bool isLocalPlayer, bool isFixed) cellStateData)
        {
            return !(_price == cellStateData.price
                && _owner == cellStateData.owner
                && _isLocalPlayer == cellStateData.isLocalPlayer
                && _isFixed == cellStateData.isFixed);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            IReadonlyCellState otherCell = obj as IReadonlyCellState;
            if (otherCell != null)
            {
                return GetCoordinates().CompareTo(otherCell.GetCoordinates());
            }
            else
            {
                throw new ArgumentException("Object is not a IReadonlyCellState");
            }
        }
    }
}