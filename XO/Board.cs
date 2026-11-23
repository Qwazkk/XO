namespace XO
{
    public class Board
    {
        public int Size { get; private set; }
        public int WinLength { get; private set; }
        public int[] Cells { get; private set; }

        public Board(int size, int winLength)
        {
            Size = size;
            WinLength = winLength;
            Cells = new int[Size * Size];
        }

        public int GetCell(int x, int y) => Cells[y * Size + x];
        public void SetCellValue(int x, int y, int symbol) => Cells[y * Size + x] = symbol;

        public bool IsCellEmpty(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size && GetCell(x, y) == 0;

        public bool SetCell(int x, int y, int symbol)
        {
            if (!IsCellEmpty(x, y)) return false;
            SetCellValue(x, y, symbol);
            return true;
        }

        public bool CheckLine(int x, int y, int playerSymbol)
        {
            (int dx, int dy)[] dirs = { (1, 0), (0, 1), (1, 1), (1, -1) };
            foreach (var (dx, dy) in dirs)
            {
                int count = 1;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    for (int step = 1; step < WinLength; step++)
                    {
                        int nx = x + sign * step * dx;
                        int ny = y + sign * step * dy;
                        if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) break;
                        if (GetCell(nx, ny) != playerSymbol) break;
                        count++;
                    }
                }
                if (count >= WinLength) return true;
            }
            return false;
        }

        public bool IsFull()
        {
            foreach (var cell in Cells) if (cell == 0) return false;
            return true;
        }
    }
}