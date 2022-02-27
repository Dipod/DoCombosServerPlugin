using System;

namespace CommonGameLogic
{
    public class Coordinates : IComparable

    {
        public Coordinates(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public readonly int x;
        public readonly int y;

        public static bool operator ==(Coordinates lhs, Coordinates rhs)
        {
            bool lhsIsNull = lhs is null;
            bool rhsIsNull = rhs is null;

            if (lhsIsNull || rhsIsNull)
            {
                if (lhsIsNull == rhsIsNull)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return lhs.x == rhs.x && lhs.y == rhs.y;
        }

        public static bool operator !=(Coordinates lhs, Coordinates rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            Coordinates coordinates = obj as Coordinates;

            return x == coordinates.x &&
                   y == coordinates.y;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            Coordinates otherCoordinates = obj as Coordinates;
            if (otherCoordinates != null)
            {
                int result = y.CompareTo(otherCoordinates.y);
                if (result == 0)
                {
                    result = x.CompareTo(otherCoordinates.x);
                }
                return result;
            }
            else
            {
                throw new ArgumentException("Object is not a Coordinates");
            }
        }

        public override string ToString()
        {
            return string.Format("[{0},{1}]", x, y);
        }
    }
}