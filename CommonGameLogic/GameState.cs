using CommonGameLogic.PhotonData;
using System.Collections.Generic;

namespace CommonGameLogic
{
    public interface IBotInformer
    {
        float GetPlayerCash(int playerId);
        List<Coordinates> GetUnownedCellsCoordinates();
        IReadonlyCellState GetCellState(Coordinates coordinates);
        bool AreConditionsForInstantActionFulfilled(int actorPlayerId, int priceOfAction);
        bool IsPutAvailable(Coordinates coordinates);
        bool IsAddPriceToCoinAvailable(Coordinates coordinates, int priceOfAction, int actorPlayerId);
        bool IsOpenCoinAvailable(Coordinates coordinates, int actorPlayerId);
        bool IsCellsCanSwap(Coordinates cell1Coordinates, Coordinates cell2Coordinates);
        bool IsCombo(List<Coordinates> comboCoordinates, bool ignoreFixedState);
        List<Combo> GetPlayerCombos(int playerId, bool onlyOpened);
        List<List<Coordinates>> GetUnownedLinesForCombo();
        int GetMinCoinPrice();
        int GetMaxCoinPrice();
        List<Coordinates> GetCoordinatesForAdd(int playerId);
        int PriceToOpenCell(Coordinates coordinates);
        List<Coordinates> GetSwapHintCellsCoordinates(Coordinates hintBaseCellCoordinates);
        SortedSet<Coordinates> GetPlayerOpenedComboCoordinates(int playerId);
        int GetSwapPrice(Coordinates cell1Coordinates, Coordinates cell2Coordinates);
    }

    public class GameState : IBotInformer
    {
        // constants
        private const int GAME_FIELD_SIZE = 6;
        private const float PLAYER_ACTION_TIMEOUT = 1f;
        private const float PLAYER_CASH_LIMIT = 6f;
        private const float CASH_INCREASE_SPEED = 0.5f;
        private const int MIN_COIN_PRICE = 1;
        private const int MAX_COIN_PRICE = 6;
        private const float START_CASH_VALUE = 0f;
        private const float START_TIMEOUT_VALUE = 0f;

        private readonly CellState[,] _cellsState = new CellState[GAME_FIELD_SIZE, GAME_FIELD_SIZE];
        private readonly Dictionary<int, float> _cashById = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _actionTimeoutById = new Dictionary<int, float>();
        private readonly Dictionary<int, List<Combo>> _openedCombosById = new Dictionary<int, List<Combo>>();
        private readonly SortedSet<Coordinates> _uniqueOpenedComboCoordinates = new SortedSet<Coordinates>();
        private readonly List<int> _playersId = new List<int>();
        private readonly Dictionary<int, List<Combo>> _fixedCombosHistoryById = new Dictionary<int, List<Combo>>();
        public readonly List<List<IReadonlyCellState>> _cachedLinesForComboScan = new List<List<IReadonlyCellState>>();
        private GameScore _gameScore = new GameScore();

        // events
        public delegate void GameFieldCellsChanged();
        public event GameFieldCellsChanged OnGameFieldCellsChanged;

        public delegate void OpenedCombosUpdate(SortedSet<Coordinates> uniqueOpenedComboCells);
        public event OpenedCombosUpdate OnOpenedCombosUpdate;

        public GameState()
        {
            for (int y = 0; y < GAME_FIELD_SIZE; y++)
            {
                for (int x = 0; x < GAME_FIELD_SIZE; x++)
                {
                    _cellsState[x, y] = new CellState(new Coordinates(x, y));
                }
            }

            FillComboScanCache();
        }

        #region IBotInformer

        public float GetPlayerCash(int playerId)
        {
            return _cashById[playerId];
        }

        public List<Coordinates> GetUnownedCellsCoordinates()
        {
            List<Coordinates> result = new List<Coordinates>();

            foreach (var cell in _cellsState)
            {
                if (!cell.IsOwned())
                {
                    result.Add(cell.GetCoordinates());
                }
            }

            return result;
        }

        public IReadonlyCellState GetCellState(Coordinates coordinates)
        {
            return GetCell(coordinates);
        }

        public bool AreConditionsForInstantActionFulfilled(int actorPlayerId, int priceOfAction)
        {
            float playerCash = GetPlayerCash(actorPlayerId);
            return playerCash >= priceOfAction && GetPlayerActionTimeout(actorPlayerId) <= 0;
        }

        // method to check coin put conditions. Game field cell must be empty.
        public bool IsPutAvailable(Coordinates coordinates)
        {
            return !GetCellState(coordinates).IsOwned();
        }

        // method to check add price to coin conditions. Game field cell must be owned by actor,
        // cell must be opened and coin price on cell must be lower that max coin price.
        public bool IsAddPriceToCoinAvailable(Coordinates coordinates, int priceOfAction, int actorPlayerId)
        {
            IReadonlyCellState cell = GetCellState(coordinates);
            int cellPrice = cell.GetPrice();
            return priceOfAction > 0 && cell.IsOwnedByPlayer(actorPlayerId) && !cell.IsFixed() && cellPrice + priceOfAction <= MAX_COIN_PRICE;
        }

        // method to check open coin conditions. Game field cell must be owned by actor player, that want to open coin.
        public bool IsOpenCoinAvailable(Coordinates coordinates, int actorPlayerId)
        {
            IReadonlyCellState cell = GetCellState(coordinates);
            return cell.IsOwnedByPlayer(actorPlayerId) && cell.IsFixed();
        }

        // method to check swap coin conditions. Game field cells is owned by different players, coins in game field cells on one
        // column or row and have same prices, cells opened.
        public bool IsCellsCanSwap(Coordinates cell1Coordinates, Coordinates cell2Coordinates)
        {
            IReadonlyCellState cell1 = GetCellState(cell1Coordinates);
            IReadonlyCellState cell2 = GetCellState(cell2Coordinates);

            return IsCellsCanSwap(cell1, cell2);
        }

        public bool IsCombo(List<Coordinates> comboCoordinates, bool ignoreFixedState)
        {
            List<IReadonlyCellState> comboCells = new List<IReadonlyCellState>();
            foreach (var coordinates in comboCoordinates)
            {
                comboCells.Add(GetCellState(coordinates));
            }
            return Combo.IsCombo(comboCells, ignoreFixedState);
        }

        public List<Combo> GetPlayerCombos(int playerId, bool onlyOpened)
        {
            if (onlyOpened)
            {
                return _openedCombosById[playerId];
            }
            else
            {
                return FindPlayerCombos(playerId, true);
            }
        }

        public List<List<Coordinates>> GetUnownedLinesForCombo()
        {
            List<List<Coordinates>> result = new List<List<Coordinates>>();

            foreach (var line in _cachedLinesForComboScan)
            {
                for (int x = 0; x < line.Count - Combo.MIN_CELLS_IN_COMBO + 1; x++)
                {
                    List<Coordinates> unownedLineForCombo = new List<Coordinates>();
                    for (int x1 = x; x1 < line.Count; x1++)
                    {
                        if (!line[x1].IsOwned())
                        {
                            unownedLineForCombo.Add(line[x1].GetCoordinates());
                        }
                        else
                        {
                            break;
                        }
                        if (unownedLineForCombo.Count >= Combo.MIN_CELLS_IN_COMBO)
                        {
                            result.Add(unownedLineForCombo);
                            unownedLineForCombo = new List<Coordinates>(unownedLineForCombo);
                        }
                    }
                }
            }

            return result;
        }

        public int GetMinCoinPrice()
        {
            return MIN_COIN_PRICE;
        }

        public int GetMaxCoinPrice()
        {
            return MAX_COIN_PRICE;
        }

        public List<Coordinates> GetCoordinatesForAdd(int playerId)
        {
            List<Coordinates> result = new List<Coordinates>();

            foreach (var cell in _cellsState)
            {
                Coordinates coordinates = cell.GetCoordinates();
                if (IsAddPriceToCoinAvailable(coordinates, MAX_COIN_PRICE - cell.GetPrice(), playerId))
                {
                    result.Add(cell.GetCoordinates());
                }
            }

            return result;
        }

        public int PriceToOpenCell(Coordinates coordinates)
        {
            return GetCellPrice(coordinates);
        }

        public List<Coordinates> GetSwapHintCellsCoordinates(Coordinates hintBaseCellCoordinates)
        {
            List<Coordinates> result = new List<Coordinates>();
            IReadonlyCellState hintBaseCell = _cellsState[hintBaseCellCoordinates.x, hintBaseCellCoordinates.y];
            for (int i = 0; i < GAME_FIELD_SIZE; i++)
            {
                IReadonlyCellState cellByX = _cellsState[hintBaseCellCoordinates.x, i];
                IReadonlyCellState cellByY = _cellsState[i, hintBaseCellCoordinates.y];

                if (IsCellsCanSwap(hintBaseCell, cellByX))
                {
                    result.Add(cellByX.GetCoordinates());
                }

                if (IsCellsCanSwap(hintBaseCell, cellByY))
                {
                    result.Add(cellByY.GetCoordinates());
                }
            }
            return result;
        }

        public SortedSet<Coordinates> GetPlayerOpenedComboCoordinates(int playerId)
        {
            SortedSet<Coordinates> result = new SortedSet<Coordinates>();
            foreach (var cell in _cellsState)
            {
                if (cell.IsOwnedByPlayer(playerId) && _uniqueOpenedComboCoordinates.Contains(cell.GetCoordinates()))
                {
                    result.Add(cell.GetCoordinates());
                }
            }

            return result;
        }

        public int GetSwapPrice(Coordinates cell1Coordinates, Coordinates cell2Coordinates)
        {
            // now need only one cell to count swap price
            return GetSwapPrice(cell1Coordinates);
        }

        #endregion

        private void FillComboScanCache()
        {
            List<List<IReadonlyCellState>> rows = new List<List<IReadonlyCellState>>();
            List<List<IReadonlyCellState>> columns = new List<List<IReadonlyCellState>>();
            // rows and columns
            for (int y = 0; y < GAME_FIELD_SIZE; y++)
            {
                List<IReadonlyCellState> row = new List<IReadonlyCellState>();
                List<IReadonlyCellState> column = new List<IReadonlyCellState>();
                for (int x = 0; x < GAME_FIELD_SIZE; x++)
                {
                    row.Add(_cellsState[x, y]);
                    column.Add(_cellsState[y, x]);
                }
                rows.Add(row);
                columns.Add(column);
            }

            foreach (var row in rows)
            {
                _cachedLinesForComboScan.Add(row);
            }

            foreach (var column in columns)
            {
                _cachedLinesForComboScan.Add(column);
            }

            // left diagonals
            int startX = 0;
            int startY = 0;
            while (startX < GAME_FIELD_SIZE)
            {
                AddCachedLeftDiagonal(startX, startY);
                startX++;
            }

            startX--;
            startY++;
            while (startY < GAME_FIELD_SIZE)
            {
                AddCachedLeftDiagonal(startX, startY);
                startY++;
            }

            // right diagonals
            startX = 0;
            startY = GAME_FIELD_SIZE - 1;
            while (startX < GAME_FIELD_SIZE)
            {
                AddCachedRightDiagonal(startX, startY);
                startX++;
            }

            startX--;
            startY--;
            while (startY >= 0)
            {
                AddCachedRightDiagonal(startX, startY);
                startY--;
            }
        }

        private void AddCachedLeftDiagonal(int startX, int startY)
        {
            List<IReadonlyCellState> leftDiagonal = new List<IReadonlyCellState>();
            for (int x = startX, y = startY; x >= 0 && y < GAME_FIELD_SIZE; x--, y++)
            {
                leftDiagonal.Add(_cellsState[x, y]);
            }

            if (leftDiagonal.Count >= Combo.MIN_CELLS_IN_COMBO)
            {
                _cachedLinesForComboScan.Add(leftDiagonal);
            }
        }

        private void AddCachedRightDiagonal(int startX, int startY)
        {
            List<IReadonlyCellState> rightDiagonal = new List<IReadonlyCellState>();
            for (int x = startX, y = startY; x >= 0 && y >= 0; x--, y--)
            {
                rightDiagonal.Add(_cellsState[x, y]);
            }

            if (rightDiagonal.Count >= Combo.MIN_CELLS_IN_COMBO)
            {
                _cachedLinesForComboScan.Add(rightDiagonal);
            }
        }

        private CellState GetCell(Coordinates coordinates)
        {
            return _cellsState[coordinates.x, coordinates.y];
        }

        private bool IsCellsCanSwap(IReadonlyCellState cell1, IReadonlyCellState cell2)
        {
            bool isCellsOwned = cell1.IsOwned() && cell2.IsOwned();
            bool isCellsEqualPrice = cell1.GetPrice() == cell2.GetPrice();
            bool isCellOwnersDiffers = cell1.GetOwner() != cell2.GetOwner();
            bool isCellsOpened = !cell1.IsFixed() && !cell2.IsFixed();
            bool isCellsOnSameColumnOrRow = cell1.GetCoordinates().x == cell2.GetCoordinates().x
                                            || cell1.GetCoordinates().y == cell2.GetCoordinates().y;

            return isCellsOwned
                && isCellsEqualPrice
                && isCellOwnersDiffers
                && isCellsOpened
                && isCellsOnSameColumnOrRow;
        }

        public int GetCellPrice(Coordinates coordinates)
        {
            return GetCellState(coordinates).GetPrice();
        }

        private int GetSwapPrice(Coordinates cellCoordinates)
        {
            // now need only one cell to count swap price
            return GetCellPrice(cellCoordinates);
        }

        public void PutCoin(Coordinates coordinates, InternalPlayer actorPlayer, int placedCoinPrice)
        {
            // update game field state
            GetCell(coordinates).PutCoin(placedCoinPrice, actorPlayer.isLocal, actorPlayer.id);

            // update player cash
            _cashById[actorPlayer.id] -= placedCoinPrice;

            //player action timeout
            _actionTimeoutById[actorPlayer.id] = PLAYER_ACTION_TIMEOUT;

            UpdateOpenedCombos();

            OnGameFieldCellsChanged?.Invoke();

            _gameScore.AddScoreBonus(actorPlayer.id, placedCoinPrice);
        }

        public void AddPriceToCoin(Coordinates coordinates, int actorPlayerId, int addedCoinPrice)
        {
            // update game field state
            GetCell(coordinates).AddPriceToCoin(addedCoinPrice);

            // update player cash
            _cashById[actorPlayerId] -= addedCoinPrice;

            //player action timeout
            _actionTimeoutById[actorPlayerId] = PLAYER_ACTION_TIMEOUT;


            UpdateOpenedCombos();

            OnGameFieldCellsChanged?.Invoke();

            _gameScore.AddScoreBonus(actorPlayerId, addedCoinPrice);
        }

        public void FixCombo(List<Coordinates> comboCoordinates, int actorPlayerId)
        {
            List<IReadonlyCellState> comboCells = GetCellStatesByCoordinates(comboCoordinates);
            Combo combo = new Combo(comboCells, false);
            _fixedCombosHistoryById[actorPlayerId].Add(combo);
            foreach (var coordinates in comboCoordinates)
            {
                GetCell(coordinates).FixCell();
            }

            UpdateOpenedCombos();

            OnGameFieldCellsChanged?.Invoke();

            _gameScore.AddScoreBonus(actorPlayerId, combo.GetComboFixingBonus());
        }

        public void OpenCoin(Coordinates coordinates, int actorPlayerId)
        {
            // update game field state
            int priceToOpen = PriceToOpenCell(coordinates);
            GetCell(coordinates).OpenCell();

            // update player cash
            _cashById[actorPlayerId] -= priceToOpen;

            //player action timeout
            _actionTimeoutById[actorPlayerId] = PLAYER_ACTION_TIMEOUT;

            UpdateOpenedCombos();

            OnGameFieldCellsChanged?.Invoke();
        }

        public void SwapCoins(Coordinates cell1Coordinates, Coordinates cell2Coordinates, int actorPlayerId)
        {
            // update game field state
            CellState cell1 = GetCell(cell1Coordinates);
            CellState cell2 = GetCell(cell2Coordinates);
            int priceToSwap = GetSwapPrice(cell1Coordinates, cell2Coordinates);

            (int price, int owner, bool isLocalPlayer, bool isFixed) tempCoinData = cell1.GetCellStateData();
            cell1.UpdateCoin(cell2.GetCellStateData());
            cell2.UpdateCoin(tempCoinData);

            if (cell1.GetOwner() == actorPlayerId)
            {
                cell1.FixCell();
            }

            if (cell2.GetOwner() == actorPlayerId)
            {
                cell2.FixCell();
            }

            // update player cash
            _cashById[actorPlayerId] -= priceToSwap;

            //player action timeout
            _actionTimeoutById[actorPlayerId] = PLAYER_ACTION_TIMEOUT;

            UpdateOpenedCombos();

            OnGameFieldCellsChanged?.Invoke();
        }

        public Combo GetCombo(List<Coordinates> comboCoordinates)
        {
            return new Combo(GetCellStatesByCoordinates(comboCoordinates), false);
        }

        public void AddPlayer(InternalPlayer newPlayer)
        {
            _cashById.Add(newPlayer.id, START_CASH_VALUE);
            _actionTimeoutById.Add(newPlayer.id, START_TIMEOUT_VALUE);
            _gameScore.AddPlayer(newPlayer);
            _openedCombosById.Add(newPlayer.id, new List<Combo>());
            _playersId.Add(newPlayer.id);
            _fixedCombosHistoryById.Add(newPlayer.id, new List<Combo>());
        }

        public void UpdateLocalGameStateFromServer(PhotonGameStateData gameStateData, int localPlayerId)
        {
            _cashById[localPlayerId] = gameStateData.cash;
            _actionTimeoutById[localPlayerId] = gameStateData.actionTimeout;

            _gameScore.ForceUpdateGameScore(gameStateData.score);

            bool gameFieldCellsChanged = false;
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 6; y++)
                {
                    if (_cellsState[x, y].UpdateCoin(gameStateData.cellsState[x, y].GetCellStateData()))
                    {
                        gameFieldCellsChanged = true;
                    }
                }
            }

            if (gameFieldCellsChanged)
            {
                UpdateOpenedCombos();
                OnGameFieldCellsChanged?.Invoke();
            }
        }

        public void Tick(float deltaTime)
        {
            foreach (var playerId in _playersId)
            {
                // cash increasing
                if (_cashById[playerId] < PLAYER_CASH_LIMIT)
                {
                    _cashById[playerId] += deltaTime * CASH_INCREASE_SPEED;
                }

                // action timeout decreasing
                if (_actionTimeoutById[playerId] > 0f)
                {
                    _actionTimeoutById[playerId] -= deltaTime;
                }
            }
        }

        public float GetPlayerActionTimeout(int playerId)
        {
            return _actionTimeoutById[playerId];
        }

        public Dictionary<int, int> GetScore()
        {
            return _gameScore.GetScore();
        }

        public CellState[,] GetCellsState()
        {
            return _cellsState;
        }

        private void UpdateOpenedCombos()
        {
            foreach (var playerCombos in _openedCombosById)
            {
                playerCombos.Value.Clear();
            }

            UpdateOpenedCombosInLines();

            OnOpenedCombosUpdate?.Invoke(_uniqueOpenedComboCoordinates);
        }

        // method to update list of opened combos by search in cached gamefield lines
        private void UpdateOpenedCombosInLines()
        {
            _uniqueOpenedComboCoordinates.Clear();

            foreach (var combo in FindCombos())
            {
                AddUniqueOpenedComboCells(combo.GetComboCoordinates());
                _openedCombosById[combo.GetOwner()].Add(combo);
            }
        }

        private List<Combo> FindCombos(bool ignoreFixedState = false)
        {
            List<Combo> result = new List<Combo>();

            List<IReadonlyCellState> comboCells = new List<IReadonlyCellState>();
            foreach (var line in _cachedLinesForComboScan)
            {
                for (int x = 0; x < line.Count - Combo.MIN_CELLS_IN_COMBO + 1; x++)
                {
                    for (int x1 = x; x1 < line.Count; x1++)
                    {
                        comboCells.Add(line[x1]);
                        if (Combo.IsCombo(comboCells, ignoreFixedState))
                        {
                            result.Add(new Combo(comboCells, ignoreFixedState));
                        }
                    }
                    comboCells.Clear();
                }
            }

            return result;
        }

        private List<Combo> FindPlayerCombos(int playerId, bool ignoreFixedState)
        {
            List<Combo> allCombos = FindCombos(ignoreFixedState);
            List<Combo> result = new List<Combo>();
            foreach(var combo in allCombos)
            {
                if (combo.GetOwner() == playerId)
                {
                    result.Add(combo);
                }
            }

            return result;
        }

        public SortedDictionary<int, int> GetPlayerCoins(int id)
        {
            SortedDictionary<int, int> playerCoins = new SortedDictionary<int, int>();

            foreach (var cell in _cellsState)
            {
                if (cell.IsOwnedByPlayer(id))
                {
                    int price = cell.GetPrice();

                    if (playerCoins.ContainsKey(price))
                    {
                        playerCoins[price] += price;
                    }
                    else
                    {
                        playerCoins.Add(price, price);
                    }
                }
            }

            return playerCoins;
        }

        public List<Combo> GetPlayerFixedCombosHistory(int id)
        {
            return _fixedCombosHistoryById[id];
        }

        private void AddUniqueOpenedComboCells(List<Coordinates> comboCoordinates)
        {
            foreach (var coordinates in comboCoordinates)
            {
                _uniqueOpenedComboCoordinates.Add(coordinates);
            }
        }

        public int GetGameFieldSize()
        {
            return GAME_FIELD_SIZE;
        }

        public bool IsCellInCombo(Coordinates cellCoordinates, int owner)
        {
            foreach (var combo in _openedCombosById[owner])
            {
                if (combo.IsCellInCombo(cellCoordinates))
                {
                    return true;
                }
            }
            return false;
        }

        public List<IReadonlyCellState> GetCellStatesByCoordinates(List<Coordinates> coordinates)
        {
            List<IReadonlyCellState> result = new List<IReadonlyCellState>();
            foreach (var coordinate in coordinates)
            {
                result.Add(GetCellState(coordinate));
            }
            return result;
        }

        public bool IsCellOwned(Coordinates cellCoordinates)
        {
            return _cellsState[cellCoordinates.x, cellCoordinates.y].IsOwned();
        }

        public bool IsCellFixed(Coordinates cellCoordinates)
        {
            return _cellsState[cellCoordinates.x, cellCoordinates.y].IsFixed();
        }
    }
}