using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Block
{
    // A table
    public sealed record TableBlock : ParagraphBlock<TableDataBlock>
    {
        /// <summary>
        /// Optional title of this link.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Alias of <see cref="ParagraphBlock{T}.Content"/>.
        /// </summary>
        public TableDataBlock Data => Content;

        /// <summary>
        /// Create a table content instance.
        /// </summary>
        /// <param name="content"></param>
        public TableBlock(IEnumerable<IEnumerable<TableCellBlock>> content) : base(new TableDataBlock(content))
        {
        }

    }

    // The data content in table.
    public sealed record TableDataBlock : ParagraphBlock<TableCellBlock?[,]>
    {
        private readonly List<List<TableCellBlock>> _content;

        internal TableDataBlock(IEnumerable<IEnumerable<TableCellBlock>> content) : base(ToArray(content))
        {
            // read all to avoid conflictions of enumerator modification.
            _content = [.. content.Select(i => i.ToList())];
        }

        private static TableCellBlock?[,] ToArray(IEnumerable<IEnumerable<TableCellBlock>> content)
        {
            // fill spans
            var table = content.Select(i => i.ToList()).ToList();
            for (int rowidx = 0; rowidx < table.Count; rowidx++)
            {
                var row = table[rowidx];
                for (int colidx = 0; colidx < row.Count; colidx++)
                {
                    var cell = row[colidx];
                    if (cell is null)
                    {
                        table[rowidx][colidx] = TableCellBlock.EmptyCell;
                        continue;
                    }
                    if (cell.RowSpan <= 1 && cell.ColSpan <= 1)
                    {
                        continue;
                    }
                    var expandRowCount = rowidx + cell.RowSpan - table.Count;
                    if (expandRowCount > 0)
                    {
                        // DO NOT Repeat
                        table.AddRange(Enumerable.Sequence(1, expandRowCount, 1).Select(i => new List<TableCellBlock>()));
                    }
                    for (int rowspanidx = 0; rowspanidx < cell.RowSpan; rowspanidx++)
                    {
                        var r = table[rowidx + rowspanidx];
                        for (int colspanidx = 0; colspanidx < cell.ColSpan; colspanidx++)
                        {
                            if (rowspanidx == 0 && colspanidx == 0)
                            {
                                // skip cell self
                                continue;
                            }
                            if (colidx > row.Count)
                            {
                                r.AddRange(Enumerable.Repeat(TableCellBlock.EmptyCell, rowidx - row.Count));
                            }
                            TableCellBlock.TableCellSpanSource src = colspanidx == 0
                                ? TableCellBlock.TableCellSpanSource.Up
                                : TableCellBlock.TableCellSpanSource.Left;
                            r.Insert(colidx + colspanidx, SpannedCell(src));
                        }
                    }
                }
            }
            // build grid.
            var maxcols = table.Max(i => i.Count);
            var grid = new TableCellBlock?[table.Count, maxcols];
            for (int i = 0; i < table.Count; i++)
            {
                for (int j = 0; j < table[i].Count; j++)
                {
                    grid[i, j] = table[i][j] ?? TableCellBlock.EmptyCell;
                }
            }
            return grid;

            static TableCellBlock SpannedCell(TableCellBlock.TableCellSpanSource source)
            {
                return TableCellBlock.EmptyCell with { SpanSource = source };
            }
        }

        public TableCellBlock?[,] ToArray()
        {
            return ToArray(this._content);
        }
    }

    /// <summary>
    /// A cell content in table data.
    /// </summary>
    public sealed record TableCellBlock : ParagraphBlock<IParagraphBlock>
    {
        public readonly static TableCellBlock EmptyCell = new(new PlainTextBlock(string.Empty));

        public TableCellSpanSource SpanSource = TableCellSpanSource.None;
        public int RowSpan { get; }
        public int ColSpan { get; }
        public TableCellBlock(IParagraphBlock content, int rowSpan = 1, int colSpan = 1) : base(content)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowSpan);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(colSpan);
            RowSpan = rowSpan;
            ColSpan = colSpan;
        }

        public enum TableCellSpanSource
        {
            /// <summary>
            /// This cell is not merged.
            /// </summary>
            None,
            /// <summary>
            /// This cell is merged with the cell above.
            /// </summary>
            Up,
            /// <summary>
            /// This cell is merged with the cell on the left.
            /// </summary>
            Left,
        }
    }
}
