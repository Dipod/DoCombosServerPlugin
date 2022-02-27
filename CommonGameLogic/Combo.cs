using System;
using System.Collections.Generic;

namespace CommonGameLogic
{
    public class Combo : IComparable
    {
        public readonly static int MIN_CELLS_IN_COMBO = 3;
        private readonly List<IReadonlyCellState> _comboCells = new List<IReadonlyCellState>();
        private readonly List<Coordinates> _comboCoordinates = new List<Coordinates>();
        private int _comboFixingBonus = 0;
        private readonly List<int> _cellPrices = new List<int>();

        public Combo(List<IReadonlyCellState> comboCells, bool ignoreFixedState)
        {
            if (!IsCombo(comboCells, ignoreFixedState))
            {
                throw new ArgumentException();
            }

            foreach (var cell in comboCells)
            {
                _comboCells.Add(cell);
            }

            _comboCells.Sort();

            foreach (var cell in _comboCells)
            {
                _cellPrices.Add(cell.GetPrice());
                _comboCoordinates.Add(cell.GetCoordinates());
            }

            UpdateComboFixingBonus();
        }

        public List<Coordinates> GetComboCoordinates()
        {
            return _comboCoordinates;
        }

        public static bool IsCellFitForCombo(IReadonlyCellState cell, bool ignoreFixedState)
        {
            return cell.IsOwned() && (!cell.IsFixed() || ignoreFixedState);
        }

        public static bool IsCombo(List<IReadonlyCellState> cells, bool ignoreFixedState)
        {
            if (cells.Count < MIN_CELLS_IN_COMBO)
            {
                return false;
            }

            foreach (var cell in cells)
            {
                if (!IsCellFitForCombo(cell, ignoreFixedState))
                {
                    return false;
                }
            }

            int owner = cells[0].GetOwner();

            SortedSet<Coordinates> uniqueComboCellCoordinates = new SortedSet<Coordinates>();

            foreach (var cell in cells)
            {
                if (cell.GetOwner() != owner)
                {
                    return false;
                }
                uniqueComboCellCoordinates.Add(cell.GetCoordinates());
            }

            if (uniqueComboCellCoordinates.Count != cells.Count)
            {
                return false;
            }

            bool isHorisontalLine = IsHorisontalLine(uniqueComboCellCoordinates);
            bool isVerticalLine = IsVerticalLine(uniqueComboCellCoordinates);
            bool isDiagonalLine = IsDiagonalLine(uniqueComboCellCoordinates);

            return isHorisontalLine || isVerticalLine || isDiagonalLine;
        }

        private void UpdateComboFixingBonus()
        {
            int step = _comboCells[1].GetPrice() - _comboCells[0].GetPrice();
            bool comboHaveBonus = true;
            for (int i = 2; i < _comboCells.Count; i++)
            {
                if (_comboCells[i].GetPrice() - _comboCells[i - 1].GetPrice() != step)
                {
                    comboHaveBonus = false;
                    break;
                }
            }

            if (comboHaveBonus)
            {
                int result = 0;
                for (int i = MIN_CELLS_IN_COMBO - 1; i < _comboCells.Count; i++)
                {
                    result += _comboCells[i].GetPrice();
                }

                int reverseResult = 0;
                for (int i = _comboCells.Count - MIN_CELLS_IN_COMBO; i >= 0; i--)
                {
                    reverseResult += _comboCells[i].GetPrice();
                }

                _comboFixingBonus = Math.Max(result, reverseResult);
            }
        }

        public int GetOwner()
        {
            return _comboCells[0].GetOwner();
        }

        public bool IsCellInCombo(Coordinates coordinates)
        {
            foreach (var cell in _comboCells)
            {
                if (cell.GetCoordinates() == coordinates)
                {
                    return true;
                }
            }
            return false;
        }

        public int GetComboFixingBonus()
        {
            return _comboFixingBonus;
        }

        public List<IReadonlyCellState> GetComboCells()
        {
            return _comboCells;
        }

        public List<int> GetComboPrices()
        {
            return _cellPrices;
        }

        private static bool IsVerticalLine(SortedSet<Coordinates> comboCells)
        {
            int x = -1;
            int y = -1;
            bool firstIteration = true;
            foreach (var cell in comboCells)
            {
                if (firstIteration)
                {
                    x = cell.x;
                    y = cell.y;
                    firstIteration = false;
                    continue;
                }

                if (x != cell.x || y != cell.y - 1)
                {
                    return false;
                }
                else
                {
                    y = cell.y;
                }
            }
            return true;
        }

        private static bool IsHorisontalLine(SortedSet<Coordinates> comboCells)
        {
            int x = -1;
            int y = -1;
            bool firstIteration = true;
            foreach (var cell in comboCells)
            {
                if (firstIteration)
                {
                    x = cell.x;
                    y = cell.y;
                    firstIteration = false;
                    continue;
                }

                if (y != cell.y || x != cell.x - 1)
                {
                    return false;
                }
                else
                {
                    x = cell.x;
                }
            }
            return true;
        }

        private static bool IsDiagonalLine(SortedSet<Coordinates> comboCells)
        {
            return IsRightDiagonalLine(comboCells) || IsLeftDiagonalLine(comboCells);
        }

        private static bool IsRightDiagonalLine(SortedSet<Coordinates> comboCells)
        {
            int x = -1;
            int y = -1;
            bool firstIteration = true;
            foreach (var cell in comboCells)
            {
                if (firstIteration)
                {
                    x = cell.x;
                    y = cell.y;
                    firstIteration = false;
                    continue;
                }

                if (y != cell.y - 1 || x != cell.x - 1)
                {
                    return false;
                }
                else
                {
                    x = cell.x;
                    y = cell.y;
                }
            }
            return true;
        }

        private static bool IsLeftDiagonalLine(SortedSet<Coordinates> comboCells)
        {
            int x = -1;
            int y = -1;
            bool firstIteration = true;
            foreach (var cell in comboCells)
            {
                if (firstIteration)
                {
                    x = cell.x;
                    y = cell.y;
                    firstIteration = false;
                    continue;
                }

                if (y != cell.y - 1 || x != cell.x + 1)
                {
                    return false;
                }
                else
                {
                    x = cell.x;
                    y = cell.y;
                }
            }
            return true;
        }

        public int CompareTo(object obj) // comparison in descending order
        {
            if (obj == null)
            {
                return -1;
            }

            Combo otherCombo = obj as Combo;
            if (otherCombo != null)
            {
                int result = otherCombo.GetComboFixingBonus().CompareTo(GetComboFixingBonus()); //descending order

                if (result == 0)
                {
                    result = -1 * GetComboCells().Count.CompareTo(otherCombo.GetComboCells().Count);
                }

                if (result == 0)
                {
                    result = GetComboCells()[0].CompareTo(otherCombo.GetComboCells()[0]);
                }
                return result;
            }
            else
            {
                throw new ArgumentException("Object is not a Combo");
            }
        }
    }
}