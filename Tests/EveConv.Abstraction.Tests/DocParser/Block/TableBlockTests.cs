using EveConv.Abstraction.DocParser.Block;
using static EveConv.Abstraction.DocParser.Block.TableCellBlock.TableCellSpanSource;

using Cell = (string Content, int RowSpan, int ColSpan, EveConv.Abstraction.DocParser.Block.TableCellBlock.TableCellSpanSource Source);

namespace EveConv.Abstraction.Tests.DocParser.Block
{
    public class TableBlockTests
    {
        [Fact]
        public void TableContent_Normal_Success()
        {
            var table = CreateTableBlock([
                    [("A1", 1, 1, default), ("A2", 1, 1, default), ("A3", 1, 1, default)],
                    [("B1", 1, 1, default), ("B2", 1, 1, default), ("B3", 1, 1, default)],
                ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]{
                    { ("A1", 1, 1, default), ("A2", 1, 1, default), ("A3", 1, 1, default)},
                    { ("B1", 1, 1, default), ("B2", 1, 1, default), ("B3", 1, 1, default)},
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TableContent_RowSpan_Success()
        {
            var table = CreateTableBlock([
                    [("A1", 1, 2, default), ("A3", 1, 1, default)],
                    [("B1", 1, 1, default), ("B2", 1, 2, default)],
                ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]{
                    { ("A1", 1, 2, default), ("", 1, 1, Left), ("A3", 1, 1, default)},
                    { ("B1", 1, 1, default), ("B2", 1, 2, default), ("", 1, 1, Left)},
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TableContent_ColSpan_Success()
        {
            var table = CreateTableBlock([
                    [("A1", 1, 1, default), ("A2", 2, 1, default), ("A3", 1, 1, default)],
                    [("B1", 1, 1, default), ("B3", 1, 1, default)],
                ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]{
                    { ("A1", 1, 1, default), ("A2", 2, 1, default), ("A3", 1, 1, default)},
                    { ("B1", 1, 1, default), ("", 1, 1, Up), ("B3", 1, 1, default)},
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TableContent_RCSpan_Success()
        {
            var table = CreateTableBlock([
                    [("A1", 2, 2, default), ("A3", 1, 1, default)],
                    [("B3", 1, 1, default)],
                ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]{
                    { ("A1", 2, 2, default), ("", 1, 1, Left), ("A3", 1, 1, default)},
                    { ("", 1, 1, Up), ("", 1, 1, Left), ("B3", 1, 1, default)},
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TableContent_ExpandRowSpan_Success()
        {
            var table = CreateTableBlock([
                [("A1", 1, 1, default), ("A2", 1, 1, default)],
                [("B1", 3, 3, default)],
            ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]
                {
                    { ("A1", 1, 1, default), ("A2", 1, 1,default), null },
                    { ("B1", 3, 3, default), ("", 1, 1, Left), ("", 1, 1, Left) },
                    { ("", 1, 1, Up), ("", 1, 1, Left), ("", 1, 1, Left) },
                    { ("", 1, 1, Up), ("", 1, 1, Left), ("", 1, 1, Left) },
                });
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TableContent_FaultRowSpan_Success()
        {
            // B2 is conflicted
            // 1. insert A2 row span to B2 with Up
            // 2. insert B1 row span to B2 with Left
            var table = CreateTableBlock([
                [("A1", 1, 1, default), ("A2", 2, 1, default)],
                [("B1", 1, 2, default)],
            ]);
            var actual = table.Data.Content;
            var expected = CreateTableData(
                new Cell?[,]
                {
                    { ("A1", 1, 1, default), ("A2", 2, 1, default), null },
                    { ("B1", 1, 2, default), ("", 1, 1, Left), ("", 1, 1, Up) },
                });
            Assert.Equal(expected, actual);
        }

        private static TableCellBlock?[,] CreateTableData(Cell?[,] cells)
        {
            ArgumentNullException.ThrowIfNull(cells);
            int rowsize = cells.GetLength(0);
            int colsize = cells.GetLength(1);
            var data = new TableCellBlock?[rowsize, colsize];
            for (int r = 0; r < rowsize; r++)
            {
                for (int c = 0; c < colsize; c++)
                {
                    var cell = cells[r, c];
                    if (cell is null)
                    {
                        data[r, c] = null;
                        continue;
                    }
                    data[r, c] = new TableCellBlock(new PlainTextBlock(cell.Value.Content), cell.Value.RowSpan, cell.Value.ColSpan)
                    {
                        SpanSource = cell.Value.Source,
                    };
                }
            }
            return data;
        }

        private static TableBlock CreateTableBlock(List<List<Cell?>> cells)
        {
            var content = cells.Select(r => r.Select(cell =>
            {
                if (cell is null)
                {
                    return TableCellBlock.EmptyCell;
                }
                return new TableCellBlock(new PlainTextBlock(cell.Value.Content), cell.Value.RowSpan, cell.Value.ColSpan)
                {
                    SpanSource = cell.Value.Source,
                };
            }));
            return new TableBlock(content);
        }
    }
}
