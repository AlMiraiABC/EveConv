using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.DocParser.Content
{
    // A table
    public sealed record TableContent : Paragraph<TableDataContent>
    {
        /// <summary>
        /// Optional title of this link.
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Alias of <see cref="Paragraph{T}.Content"/>.
        /// </summary>
        public TableDataContent Data => Content;

        /// <summary>
        /// Create a table content instance.
        /// </summary>
        /// <param name="content"></param>
        public TableContent(IEnumerable<IEnumerable<TableCellContent>> content) : base(new TableDataContent(content))
        {
        }

    }

    // The data content in table.
    public sealed record TableDataContent : Paragraph<TableCellContent?[,]>
    {
        private readonly List<List<TableCellContent>> _content;

        internal TableDataContent(IEnumerable<IEnumerable<TableCellContent>> content) : base(ToArray(content))
        {
            // read all to avoid conflictions of enumerator modification.
            _content = [.. content.Select(i => i.ToList())];
        }

        private static TableCellContent?[,] ToArray(IEnumerable<IEnumerable<TableCellContent>> content)
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
                        table[rowidx][colidx] = TableCellContent.EmptyCell;
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
                        table.AddRange(Enumerable.Sequence(1, expandRowCount, 1).Select(i => new List<TableCellContent>()));
                    }
                    for (int rowspanidx = 0; rowspanidx<cell.RowSpan; rowspanidx++)
                    {
                        var r = table[rowidx + rowspanidx];
                        for (int colspanidx = 0; colspanidx<cell.ColSpan; colspanidx++)
                        {
                            if (rowspanidx == 0 && colspanidx == 0)
                            {
                                // skip cell self
                                continue;
                            }
                            if (colidx > row.Count)
                            {
                                r.AddRange(Enumerable.Repeat(TableCellContent.EmptyCell, rowidx - row.Count));
                            }
                            TableCellContent.TableCellSpanSource src = colspanidx == 0
                                ? TableCellContent.TableCellSpanSource.Up
                                : TableCellContent.TableCellSpanSource.Left;
                            r.Insert(colidx + colspanidx, SpannedCell(src));
                        }
                    }
                }
            }
            // build grid.
            var maxcols = table.Max(i => i.Count);
            var grid = new TableCellContent?[table.Count, maxcols];
            for (int i = 0; i < table.Count; i++)
            {
                for (int j = 0; j < table[i].Count; j++)
                {
                    grid[i, j] = table[i][j] ?? TableCellContent.EmptyCell;
                }
            }
            return grid;

            static TableCellContent SpannedCell(TableCellContent.TableCellSpanSource source)
            {
                return TableCellContent.EmptyCell with { SpanSource = source };
            }
        }

        public TableCellContent?[,] ToArray()
        {
            return ToArray(this._content);
        }
    }

    /// <summary>
    /// A cell content in table data.
    /// </summary>
    public sealed record TableCellContent : Paragraph<IParagraph>
{
    public readonly static TableCellContent EmptyCell = new(new PlainTextContent(string.Empty));

    public TableCellSpanSource SpanSource = TableCellSpanSource.None;
    public int RowSpan { get; }
    public int ColSpan { get; }
    public TableCellContent(IParagraph content, int rowSpan = 1, int colSpan = 1) : base(content)
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
