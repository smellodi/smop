using M = Smop.ML.Search.Matrix<double>;

namespace UnitTests.ML;

public class DeMatrix
{
    [Fact]
    public void ConstuctorEmpty()
    {
        M m = new();
        Assert.True(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(0, m.RowCount);
        Assert.Equal(0, m.ColumnCount);
        Assert.Equal(0, m.Size);
    }

    [Fact]
    public void ConstuctorFromArray()
    {
        M m = new([1, 2, 3]);
        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.True(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.True(m.IsVector);
        Assert.Equal(1, m.RowCount);
        Assert.Equal(3, m.ColumnCount);
        Assert.Equal(3, m.Size);
    }

    [Fact]
    public void ConstuctorFromArray2D()
    {
        Assert.Throws<ArgumentException>(() => new M(1, 2, new double[2,2]));

        M m = new(2, 1, new double[2, 1] { { 1 }, { 2 } });

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.True(m.IsColumn);
        Assert.True(m.IsVector);
        Assert.Equal(2, m.RowCount);
        Assert.Equal(1, m.ColumnCount);
        Assert.Equal(2, m.Size);
    }

    [Fact]
    public void ConstuctorFromValue()
    {
        M m = new(3, 2, 2);

        Assert.All(m, v => Assert.Equal(2, v));

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(3, m.RowCount);
        Assert.Equal(2, m.ColumnCount);
        Assert.Equal(6, m.Size);
    }

    [Fact]
    public void ConstuctorFromCallback()
    {
        M m = new(2, 3, (r, c) => r * 3 + c);

        Assert.All(m, (v, i) => Assert.Equal(i, v));

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(2, m.RowCount);
        Assert.Equal(3, m.ColumnCount);
        Assert.Equal(6, m.Size);
    }

    [Fact]
    public void Copy()
    {
        M m1 = new(2, 3, (r, c) => r * 3 + c);
        var m2 = m1.Copy();

        Assert.All(m2, (v, i) => Assert.Equal(i, v));

        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(2, m2.RowCount);
        Assert.Equal(3, m2.ColumnCount);
        Assert.Equal(6, m2.Size);
    }

    [Fact]
    public void GetByIndex()
    {
        M m = new(2, 3);

        Assert.Null(Record.Exception(() => m[0, 0]));
        Assert.Null(Record.Exception(() => m[0, 1]));
        Assert.Null(Record.Exception(() => m[0, 2]));
        Assert.Null(Record.Exception(() => m[1, 0]));
        Assert.Null(Record.Exception(() => m[1, 1]));
        Assert.Null(Record.Exception(() => m[1, 2]));
        Assert.NotNull(Record.Exception(() => m[2, 0]));
        Assert.NotNull(Record.Exception(() => m[1, 3]));
        Assert.NotNull(Record.Exception(() => m[-1, 0]));

        Assert.Null(Record.Exception(() => m[1, 2] = 2));
        Assert.Equal(2, m[1, 2]);
    }

    [Fact]
    public void GetBySingleIndex()
    {
        M m = new(2, 3);
        Assert.NotNull(Record.Exception(() => m[0]));

        M v = new(1, 3);
        Assert.Null(Record.Exception(() => v[0]));
        Assert.Null(Record.Exception(() => v[1]));
        Assert.Null(Record.Exception(() => v[2]));
        Assert.NotNull(Record.Exception(() => v[3]));
        Assert.NotNull(Record.Exception(() => v[-3]));

        Assert.Null(Record.Exception(() => v[1] = 2));
        Assert.Equal(2, v[1]);
    }

    [Fact]
    public void GetColumnByRange()
    {
        M m1 = new(4, 3, new double[,] { { 0, 1, 2 }, { 2, 1, 0 }, { 3, 9, 7 }, { 6, 4, 5 } });

        M m2 = m1[null,1];
        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.True(m2.IsColumn);
        Assert.True(m2.IsVector);
        Assert.Equal(4, m2.RowCount);
        Assert.Equal(1, m2.ColumnCount);
        Assert.Equal(4, m2.Size);
        Assert.Equal(new double[] { 1, 1, 9, 4}, m2);
    }

    [Fact]
    public void GetRowByRange()
    {
        M m1 = new(4, 3, new double[,] { { 0, 1, 2 }, { 2, 1, 0 }, { 3, 9, 7 }, { 6, 4, 5 } });

        M m2 = m1[1, null];
        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.True(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.True(m2.IsVector);
        Assert.Equal(1, m2.RowCount);
        Assert.Equal(3, m2.ColumnCount);
        Assert.Equal(3, m2.Size);
        Assert.Equal(new double[] { 2, 1, 0 }, m2);
    }

    [Fact]
    public void GetSubsetByRange()
    {
        M m1 = new(4, 3, new double[,] { { 0, 1, 2 }, { 2, 1, 0 }, { 3, 9, 7 }, { 6, 4, 5 } });

        M m2 = m1[1..^1, 1..];
        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(2, m2.RowCount);
        Assert.Equal(2, m2.ColumnCount);
        Assert.Equal(4, m2.Size);
        Assert.Equal(new M(2, 2, new double[,] { { 1, 0 }, { 9, 7} }), m2);
    }

    [Fact]
    public void Equals_()
    {
        M m1 = new(2, 3, (r, c) => r * 3 + c);
        M m2 = new(2, 3, (r, c) => r * 3 + c);
        M m3 = new(2, 3, (r, c) => r * 3 + c + 1);
        M m4 = new(2, 4, (r, c) => r * 3 + c);

        Assert.True(m1.Equals(m2));
        Assert.False(m1.Equals(m3));
        Assert.False(m1.Equals(m4));
    }

    [Fact]
    public void ToString_()
    {
        M m1 = new(2, 3, (r, c) => r * 3 + c);

        Assert.Equal("0.00    1.00    2.00\n3.00    4.00    5.00", m1.ToString());
    }

    [Fact]
    public void Row()
    {
        M m = new(2, 3, (r, c) => r * 3 + c);
        var row = m.Row(0);

        Assert.False(row.IsEmpty);
        Assert.False(row.IsScalar);
        Assert.True(row.IsRow);
        Assert.False(row.IsColumn);
        Assert.True(row.IsVector);
        Assert.Equal(1, row.RowCount);
        Assert.Equal(3, row.ColumnCount);
        Assert.Equal(3, row.Size);

        double[] rowVals = [0, 1, 2];
        Assert.All(row, (v, i) => Assert.Equal(rowVals[i], v));

        Assert.Throws<ArgumentException>(() => m.Row(2));
    }

    [Fact]
    public void Column()
    {
        M m = new(2, 3, (r, c) => r * 3 + c);
        var col = m.Column(1);

        Assert.False(col.IsEmpty);
        Assert.False(col.IsScalar);
        Assert.False(col.IsRow);
        Assert.True(col.IsColumn);
        Assert.True(col.IsVector);
        Assert.Equal(2, col.RowCount);
        Assert.Equal(1, col.ColumnCount);
        Assert.Equal(2, col.Size);

        double[] colVals = [1, 4];
        Assert.All(col, (v, i) => Assert.Equal(colVals[i], v));

        Assert.Throws<ArgumentException>(() => m.Column(4));
    }

    [Fact]
    public void StackColumns()
    {
        M m1 = new(2, 3, (r, c) => r * 3 + c);
        M m2 = new(2, 3, (r, c) => r * 3 + c);
        M m3 = M.StackColumns(m1, m2);

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(6, m3.ColumnCount);
        Assert.Equal(12, m3.Size);

        double[] values = [0, 1, 2, 0, 1, 2, 3, 4, 5, 3, 4, 5];
        Assert.All(m3, (v, i) => Assert.Equal(values[i], v));
    }

    [Fact]
    public void StackRows()
    {
        M m1 = new(2, 3, (r, c) => r * 3 + c);
        M m2 = new(2, 3, (r, c) => r * 3 + c);
        M m3 = M.StackRows(m1, m2);

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(4, m3.RowCount);
        Assert.Equal(3, m3.ColumnCount);
        Assert.Equal(12, m3.Size);

        double[] values = [0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5];
        Assert.All(m3, (v, i) => Assert.Equal(values[i], v));
    }

    [Fact]
    public void ReplaceColumn()
    {
        M m = new(4, 5, (r, c) => r * 5 + c);
        M col = new(4, 1, (r, c) => r);

        Assert.NotNull(Record.Exception(() => m.ReplaceColumn(0, new(4, 2))));      // not a column
        Assert.NotNull(Record.Exception(() => m.ReplaceColumn(0, new(3, 1))));      // row count does not match
        Assert.NotNull(Record.Exception(() => m.ReplaceColumn(-1, new(3, 1))));     // row index is not valid

        m.ReplaceColumn(2, col);

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(4, m.RowCount);
        Assert.Equal(5, m.ColumnCount);
        Assert.Equal(20, m.Size);
        Assert.Equal(0, m[0, 2]);
        Assert.Equal(1, m[1, 2]);
        Assert.Equal(2, m[2, 2]);
        Assert.Equal(3, m[3, 2]);
    }

    [Fact]
    public void ReplaceRow()
    {
        M m = new(4, 5, (r, c) => r * 5 + c);
        M row = new(1, 5, (r, c) => c);

        Assert.NotNull(Record.Exception(() => m.ReplaceRow(0, new(4, 2))));      // not a row
        Assert.NotNull(Record.Exception(() => m.ReplaceRow(0, new(1, 6))));      // column count does not match
        Assert.NotNull(Record.Exception(() => m.ReplaceRow(-1, new(1, 5))));     // column index is not valid

        m.ReplaceRow(2, row);

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(4, m.RowCount);
        Assert.Equal(5, m.ColumnCount);
        Assert.Equal(20, m.Size);
        Assert.Equal(0, m[2, 0]);
        Assert.Equal(1, m[2, 1]);
        Assert.Equal(2, m[2, 2]);
        Assert.Equal(3, m[2, 3]);
        Assert.Equal(4, m[2, 4]);
    }

    [Fact]
    public void RemoveDuplicates()
    {
        M m1 = new(4, 3, new double[4, 3] { { 0, 1, 0 }, { 0, 1, 1 }, { 0, 1, 0 }, { 0, 1, 0 } });
        M m2 = m1.RemoveDuplicates(Smop.ML.Search.Direction.Rows);

        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(2, m2.RowCount);
        Assert.Equal(3, m2.ColumnCount);
        Assert.Equal(6, m2.Size);

        M m3 = new(3, 4, new double[3, 4] { { 0, 1, 0, 0 }, { 0, 1, 0, 1 }, { 1, 0, 1, 1 } });
        M m4 = m3.RemoveDuplicates(Smop.ML.Search.Direction.Columns);

        Assert.False(m4.IsEmpty);
        Assert.False(m4.IsScalar);
        Assert.False(m4.IsRow);
        Assert.False(m4.IsColumn);
        Assert.False(m4.IsVector);
        Assert.Equal(3, m4.RowCount);
        Assert.Equal(3, m4.ColumnCount);
        Assert.Equal(9, m4.Size);
    }

    [Fact]
    public void Transpose()
    {
        M m1 = new(4, 3, (r, c) => r * 3 + c);
        M m2 = m1.Transpose();

        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(3, m2.RowCount);
        Assert.Equal(4, m2.ColumnCount);
        Assert.Equal(12, m2.Size);

        Assert.Equal(3, m2[0, 1]);
        Assert.Equal(1, m2[1, 0]);
        Assert.Equal(5, m2[2, 1]);
    }

    [Fact]
    public void Power()
    {
        M m1 = new(2, 2, (r, c) => r * 2 + c);
        M m2 = m1 ^ 2;

        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(2, m2.RowCount);
        Assert.Equal(2, m2.ColumnCount);
        Assert.Equal(4, m2.Size);

        Assert.Equal(0, m2[0, 0]);
        Assert.Equal(1, m2[0, 1]);
        Assert.Equal(4, m2[1, 0]);
        Assert.Equal(9, m2[1, 1]);
    }

    [Fact]
    public void Mean()
    {
        M m = new(2, 2, (r, c) => r * 2 + c);

        Assert.Equal((0d + 1 + 2 + 3) / 4, m.Mean());
    }

    [Fact]
    public void Sum()
    {
        M m1 = new(2, 2, (r, c) => r * 2 + c);
        M m2 = new(2, 2, (r, c) => r * 2 + c);
        M m3 = m1 + m2;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(0, m3[0, 0]);
        Assert.Equal(2, m3[0, 1]);
        Assert.Equal(4, m3[1, 0]);
        Assert.Equal(6, m3[1, 1]);
    }

    [Fact]
    public void Subtract()
    {
        M m1 = new(2, 2, (r, c) => r * 2 + c + 1);
        M m2 = new(2, 2, (r, c) => r * 2 + c);
        M m3 = m1 - m2;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(1, m3[0, 0]);
        Assert.Equal(1, m3[0, 1]);
        Assert.Equal(1, m3[1, 0]);
        Assert.Equal(1, m3[1, 1]);
    }

    [Fact]
    public void Multiply()
    {
        M m1 = new(2, 2, (r, c) => r * 2 + c + 1);
        M m3 = m1 * 2;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(2, m3[0, 0]);
        Assert.Equal(4, m3[0, 1]);
        Assert.Equal(6, m3[1, 0]);
        Assert.Equal(8, m3[1, 1]);

        Assert.NotNull(Record.Exception(() => m1 * new M(2, 3)));

        M m2 = new(2, 2, (r, c) => r * 2 + c);
        m3 = m1 * m2;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(0, m3[0, 0]);
        Assert.Equal(2, m3[0, 1]);
        Assert.Equal(6, m3[1, 0]);
        Assert.Equal(12, m3[1, 1]);
    }

    [Fact]
    public void Divide()
    {
        M m1 = new(2, 2, (r, c) => r * 2 + c + 1);
        M m3 = m1 / 2;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(0.5, m3[0, 0]);
        Assert.Equal(1, m3[0, 1]);
        Assert.Equal(1.5, m3[1, 0]);
        Assert.Equal(2, m3[1, 1]);

        Assert.NotNull(Record.Exception(() => m1 / new M(2, 3)));

        M m2 = new(2, 2, (r, c) => r * 2 + c);
        m3 = m2 / m1;

        Assert.False(m3.IsEmpty);
        Assert.False(m3.IsScalar);
        Assert.False(m3.IsRow);
        Assert.False(m3.IsColumn);
        Assert.False(m3.IsVector);
        Assert.Equal(2, m3.RowCount);
        Assert.Equal(2, m3.ColumnCount);
        Assert.Equal(4, m3.Size);

        Assert.Equal(0, m3[0, 0]);
        Assert.Equal(0.5, m3[0, 1]);
        Assert.Equal(2d/3, m3[1, 0]);
        Assert.Equal(3d/4, m3[1, 1]);
    }

    [Fact]
    public void FromRow()
    {
        M m = M.FromRow([1, 2, 3]);

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.True(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.True(m.IsVector);
        Assert.Equal(1, m.RowCount);
        Assert.Equal(3, m.ColumnCount);
        Assert.Equal(3, m.Size);

        Assert.Equal(1, m[0]);
        Assert.Equal(2, m[1]);
        Assert.Equal(3, m[2]);
    }

    [Fact]
    public void FromRows()
    {
        double[] row = [1, 2, 3];
        M m = M.FromRows(new double[][] { row, row });

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(2, m.RowCount);
        Assert.Equal(3, m.ColumnCount);
        Assert.Equal(6, m.Size);

        Assert.Equal(2, m[0, 1]);
    }

    [Fact]
    public void FromColumn()
    {
        M m = M.FromColumn([1, 2, 3]);

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.True(m.IsColumn);
        Assert.True(m.IsVector);
        Assert.Equal(3, m.RowCount);
        Assert.Equal(1, m.ColumnCount);
        Assert.Equal(3, m.Size);

        Assert.Equal(1, m[0]);
        Assert.Equal(2, m[1]);
        Assert.Equal(3, m[2]);
    }

    [Fact]
    public void FromColumns()
    {
        double[] col = [1, 2, 3];
        M m = M.FromColumns(new double[][] { col, col });

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(3, m.RowCount);
        Assert.Equal(2, m.ColumnCount);
        Assert.Equal(6, m.Size);

        Assert.Equal(2, m[1, 1]);
    }

    [Fact]
    public void Permutate()
    {
        M m = M.Permutate([1, 2, 3]);

        Assert.False(m.IsEmpty);
        Assert.False(m.IsScalar);
        Assert.False(m.IsRow);
        Assert.False(m.IsColumn);
        Assert.False(m.IsVector);
        Assert.Equal(6, m.RowCount);
        Assert.Equal(3, m.ColumnCount);
        Assert.Equal(18, m.Size);

        Assert.Equal(12, m.Column(0).Sum());
        Assert.Equal(12, m.Column(1).Sum());
        Assert.Equal(12, m.Column(2).Sum());
        Assert.Equal(6, m.Row(0).Sum());
        Assert.Equal(6, m.Row(1).Sum());
        Assert.Equal(6, m.Row(2).Sum());
        Assert.Equal(6, m.Row(3).Sum());
        Assert.Equal(6, m.Row(4).Sum());
        Assert.Equal(6, m.Row(5).Sum());
    }

    [Fact]
    public void StackColumns_StackRows_Permutate_RemoveDuplicates()
    {
        M? m = null;

        for (int i = 3; i >= 0; i--)
        {
            var a = new M(1, i, 0);
            var b = new M(1, 3 - i, 10);
            var ab = M.StackColumns(a, b);
            ab = M.Permutate(ab);         
            if (m == null)
                m = ab;
            else
                m = M.StackRows(m, ab);
        }

        M m1 = m!.RemoveDuplicates(Smop.ML.Search.Direction.Rows);

        Assert.False(m1.IsEmpty);
        Assert.False(m1.IsScalar);
        Assert.False(m1.IsRow);
        Assert.False(m1.IsColumn);
        Assert.False(m1.IsVector);
        Assert.Equal(8, m1.RowCount);
        Assert.Equal(3, m1.ColumnCount);
        Assert.Equal(24, m1.Size);

        M m2 = m!.Transpose().RemoveDuplicates(Smop.ML.Search.Direction.Columns);

        Assert.False(m2.IsEmpty);
        Assert.False(m2.IsScalar);
        Assert.False(m2.IsRow);
        Assert.False(m2.IsColumn);
        Assert.False(m2.IsVector);
        Assert.Equal(3, m2.RowCount);
        Assert.Equal(8, m2.ColumnCount);
        Assert.Equal(24, m2.Size);
    }
}
