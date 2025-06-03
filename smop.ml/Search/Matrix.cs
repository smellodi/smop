using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Smop.ML.Search;

/// <summary>
/// Custom implementation of some matrix feature.
/// Could be still buggy and not optimal
/// </summary>
/// <typeparam name="T">Numeric type</typeparam>
public class Matrix<T> : IEnumerable<T>, IEnumerator<T>
    where T : System.Numerics.INumber<T>
{
    public int RowCount => _rows;
    public int ColumnCount => _cols;
    public int Size => _cols * _rows;

    public bool IsColumn => RowCount > 1 && ColumnCount == 1;
    public bool IsRow => RowCount == 1 && ColumnCount > 1;
    public bool IsVector => IsColumn || IsRow;
    public bool IsScalar => RowCount == 1 && ColumnCount == 1;
    public bool IsEmpty => Size == 0;

    public T Current => _m[_enumIndex / ColumnCount, _enumIndex % ColumnCount];

    object IEnumerator.Current => Current;

    public Matrix()
    {
        _rows = 0;
        _cols = 0;
        _m = new T[_rows, _cols];
    }

    public Matrix(T[] m)
    {
        _rows = 1;
        _cols = m.Length;
        _m = new T[_rows, _cols];

        for (int c = 0; c < _cols; c++)
            _m[0, c] = m[c];
    }

    public Matrix(int rows, int cols, T[,] m)
    {
        if (rows * cols != m.Length)
            throw new ArgumentException("Matrix content does not match its size");

        _rows = rows;
        _cols = cols;
        _m = m;
    }

    public Matrix(int rows, int cols, T? initialValue = default)
    {
        _rows = rows;
        _cols = cols;
        _m = new T[_rows, _cols];

        T value = initialValue ?? T.Zero;
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _m[r, c] = value;
    }

    public Matrix(int rows, int cols, Func<int, int, T> getValue)
    {
        _rows = rows;
        _cols = cols;
        _m = new T[_rows, _cols];

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _m[r, c] = getValue(r, c);
    }

    public Matrix<T> Copy()
    {
        var result = new Matrix<T>(RowCount, ColumnCount);
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                result._m[r, c] = _m[r, c];
        return result;
    }

    public T this[int row, int col]
    {
        get => _m[row, col];
        set => _m[row, col] = value;
    }

    public T this[int index]
    {
        get
        {
            if (RowCount == 1)
                return _m[0, index];
            else if (ColumnCount == 1)
                return _m[index, 0];
            else
                throw new ArgumentException("The matrix is not a vector");
        }
        set
        {
            if (RowCount == 1)
                _m[0, index] = value;
            else if (ColumnCount == 1)
                _m[index, 0] = value;
            else
                throw new ArgumentException("The matrix is not a vector");
        }
    }

    public Matrix<T> Row(int row)
    {
        if (row < 0 || row >= _rows)
            throw new ArgumentException("Invalid row index");

        var result = new T[ColumnCount];
        for (int c = 0; c < _cols; c++)
            result[c] = _m[row, c];
        return Matrix<T>.FromRow(result);
    }

    public Matrix<T> Column(int column)
    {
        if (column < 0 || column >= _cols)
            throw new ArgumentException("Invalid column index");

        var result = new T[RowCount];
        for (int r = 0; r < _rows; r++)
            result[r] = _m[r, column];
        return Matrix<T>.FromColumn(result);
    }

    public Matrix<T> StackColumns(Matrix<T> matrix) => StackColumns(this, matrix);
    public Matrix<T> StackRows(Matrix<T> matrix) => StackRows(this, matrix);
    public Matrix<T> ExcludeRow(int row) => ExcludeRow(this, row);
    public Matrix<T> ExcludeColumn(int column) => ExcludeColumn(this, column);

    public Matrix<T> SubSet(Range? rows, Range? columns) => SubSet(this, rows, columns);
    public Matrix<T> SubSet(int row, Range? columns) => SubSet(this, new Range(row, row + 1), columns);
    public Matrix<T> SubSet(Range? rows, int column) => SubSet(this, rows, new Range(column, column + 1));
    public Matrix<T> SubSet(int row, int column) => SubSet(this, new Range(row, row + 1), new Range(column, column + 1));
    public void ReplaceColumn(int col, Matrix<T> source) => ReplaceColumn(this, col, source);
    public void ReplaceRow(int row, Matrix<T> source) => ReplaceRow(this, row, source);

    public Matrix<T> RemoveDuplicates(Direction direction) => RemoveDuplicates(this, direction);
    public Matrix<T> Transpose() => Transpose(this);

    public Matrix<T> Power(double p) => this ^ p; //Power(this, p);
    public double Mean() => Mean(this);

    // Static

    // Operators

    public static Matrix<T> operator *(Matrix<T> matrix, T k) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] * k);
    public static Matrix<T> operator *(T k, Matrix<T> matrix) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] * k);
    public static Matrix<T> operator *(Matrix<T> m1, Matrix<T> m2)  // dot product!!
    {
        if (m1.RowCount != m2.RowCount || m1.ColumnCount != m2.ColumnCount)
            throw new ArgumentException("Matrix operator '*' requires matrices of the same shapes");

        return new(m1.RowCount, m1.ColumnCount, (r, c) => m1[r, c] * m2[r, c]);
    }
    public static Matrix<T> operator /(Matrix<T> matrix, T k) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] / k);
    public static Matrix<T> operator /(Matrix<T> m1, Matrix<T> m2)
    {
        if (m1.RowCount != m2.RowCount || m1.ColumnCount != m2.ColumnCount)
            throw new ArgumentException("Matrix operator '/' requires matrices of the same shapes");

        return new(m1.RowCount, m1.ColumnCount, (r, c) => m1[r, c] / m2[r, c]);
    }

    public static Matrix<T> operator ^(Matrix<T> matrix, double p) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => {
        var v = double.CreateChecked(matrix[r, c]);
        var result = Math.Pow(v, p);
        return T.CreateChecked(result);
    });

    public static Matrix<T> operator -(Matrix<T> a, Matrix<T> b)
    {
        if (a.RowCount != b.RowCount || a.ColumnCount != b.ColumnCount)
            throw new ArgumentException("Matrix operator '-' requires matrices of the same shapes");
        return new(a.RowCount, a.ColumnCount, (r, c) => a[r, c] - b[r, c]);
    }

    public static Matrix<T> operator +(Matrix<T> a, Matrix<T> b)
    {
        if (a.RowCount != b.RowCount || a.ColumnCount != b.ColumnCount)
            throw new ArgumentException("Matrix operator '+' requires matrices of the same shapes");
        return new(a.RowCount, a.ColumnCount, (r, c) => a[r, c] + b[r, c]);
    }

    // Methods

    public static Matrix<T> FromRow(T[] values)
    {
        var rows = 1;
        var cols = values.Length;
        var m = new T[rows, cols];

        for (int c = 0; c < cols; c++)
            m[0, c] = values[c];

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> FromRows(T[][] values)
    {
        var rows = values.Length;
        var cols = values.Length > 0 ? values[0].Length : 0;
        var m = new T[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                m[r, c] = values[r][c];

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> FromColumn(T[] values)
    {
        var rows = values.Length;
        var cols = 1;
        var m = new T[rows, cols];

        for (int r = 0; r < rows; r++)
            m[r, 0] = values[r];

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> FromColumns(T[][] values)
    {
        var cols = values.Length;
        var rows = values.Length > 0 ? values[0].Length : 0; 
        var m = new T[rows, cols];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                m[r, c] = values[c][r];

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> StackColumns(Matrix<T> a, Matrix<T> b)
    {
        var rows = Math.Min(a.RowCount, b.RowCount);
        var cols = a.ColumnCount + b.ColumnCount;
        var m = new T[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < a.ColumnCount; c++)
                m[r, c] = a[r, c];
            for (int c = 0; c < b.ColumnCount; c++)
                m[r, a.ColumnCount + c] = b[r, c];
        }

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> StackRows(Matrix<T> a, Matrix<T> b)
    {
        var rows = a.RowCount + b.RowCount;
        var cols = Math.Min(a.ColumnCount, b.ColumnCount);
        var m = new T[rows, cols];

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < a.RowCount; r++)
                m[r, c] = a[r, c];
            for (int r = 0; r < b.RowCount; r++)
                m[a.RowCount + r, c] = b[r, c];
        }

        return new Matrix<T>(rows, cols, m);
    }

    public static Matrix<T> ExcludeRow(Matrix<T> matrix, int row)
    {
        var result = new T[matrix.RowCount - 1, matrix.ColumnCount];
        for (int r = 0, ri = 0; r < matrix._rows; r++)
        {
            if (r != row)
            {
                for (int c = 0; c < matrix._cols; c++)
                    result[ri, c] = matrix._m[r, c];
                ri++;
            }
        }
        return new Matrix<T>(matrix.RowCount - 1, matrix.ColumnCount, result);
    }

    public static Matrix<T> ExcludeColumn(Matrix<T> matrix, int column)
    {
        var result = new T[matrix.RowCount, matrix.ColumnCount - 1];
        for (int r = 0; r < matrix._rows; r++)
            for (int c = 0, ci = 0; c < matrix._cols; c++)
            {
                if (c != column)
                {
                    result[r, ci] = matrix._m[r, c];
                    ci++;
                }
            }
        return new Matrix<T>(matrix.RowCount, matrix.ColumnCount - 1, result);
    }

    public static Matrix<T> SubSet(Matrix<T> matrix, Range? rows, Range? columns)
    {
        Range rowIndices = rows ?? new Range(0, matrix.RowCount);
        Range columnIndices = columns ?? new Range(0, matrix.ColumnCount);

        var (rowOffset, rowLength) = rowIndices.GetOffsetAndLength(matrix.RowCount);
        var (colOffset, colLength) = columnIndices.GetOffsetAndLength(matrix.ColumnCount);

        return new Matrix<T>(rowLength, colLength,
            (r, c) => matrix[rowOffset + r, colOffset + c]);
    }

    public static void ReplaceColumn(Matrix<T> matrix, int col, Matrix<T> source)
    {
        if (col < 0 || col >= matrix.ColumnCount)
            throw new ArgumentException("Invalid column");
        if (source.ColumnCount != 1 || matrix.RowCount != source.RowCount)
            throw new ArgumentException("The replacing matrix is not a column");

        for (int r = 0; r < matrix.RowCount; r++)
            matrix[r, col] = source[r];
    }

    public static void ReplaceRow(Matrix<T> matrix, int row, Matrix<T> source)
    {
        if (row < 0 || row >= matrix.RowCount)
            throw new ArgumentException("Invalid column");
        if (source.RowCount != 1 || matrix.ColumnCount != source.ColumnCount)
            throw new ArgumentException("The replacing matrix is not a row");

        for (int c = 0; c < matrix.ColumnCount; c++)
            matrix[row, c] = source[c];
    }

    public static Matrix<T> Permutate(T[] vector) => Permutate(Matrix<T>.FromRow(vector));

    public static Matrix<T> Permutate(Matrix<T> vector)
    {
        if (!vector.IsVector)
            throw new ArgumentException("Permutation works with vectors only");

        if (!vector.IsRow)
            vector = vector.Transpose();

        static List<List<T>> AddPermuationRow(Matrix<T> matrix)
        {
            if (matrix.Size > 1)
            {
                var result = new List<List<T>>();
                for (int c = 0; c < matrix.ColumnCount; c++)
                {
                    var rows = AddPermuationRow(matrix.ExcludeColumn(c));
                    foreach (var row in rows)
                    {
                        row.Add(matrix[c]);
                        result.Add(row);
                    }
                }
                return result;
            }
            else
            {
                return new List<List<T>> { new() { matrix[0] } };
            }
        }

        var result = new List<List<T>>();
        for (int c = 0; c < vector.ColumnCount; c++)
        {
            var rows = AddPermuationRow(vector.ExcludeColumn(c));
            foreach (var row in rows)
            {
                row.Add(vector[c]);
                result.Add(row);
            }
        }

        var rowCount = result.Count;
        var colCount = vector.ColumnCount;
        var m = new T[rowCount, colCount];
        for (int r = 0; r < rowCount; r++)
            for (int c = 0; c < colCount; c++)
                m[r, c] = result[r][c];

        return new Matrix<T>(rowCount, colCount, m);
    }

    public static Matrix<T> RemoveDuplicates(Matrix<T> matrix, Direction direction)
    {
        if (direction == Direction.Rows)
        {
            var result = new Matrix<T>(0, matrix.ColumnCount);
            var indices = Enumerable.Range(0, matrix.RowCount).ToList();

            for (int r1 = 0; r1 < matrix.RowCount; r1++)
            {
                if (!indices.Contains(r1))
                    continue;

                var row1 = matrix.Row(r1);
                result = result.StackRows(row1);

                for (int r2 = r1 + 1; r2 < matrix.RowCount; r2++)
                {
                    if (!indices.Contains(r2))
                        continue;

                    var row2 = matrix.Row(r2);
                        
                    bool allSame = true;
                    for (int c = 0; c < matrix.ColumnCount; c++)
                    {
                        if (row1[c] != row2[c])
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (allSame)
                    {
                        indices.Remove(r2);
                    }
                }
            }

            return result;
        }
        else
        {
            var result = new Matrix<T>(matrix.RowCount, 0);
            var indices = Enumerable.Range(0, matrix.ColumnCount).ToList();

            for (int c1 = 0; c1 < matrix.ColumnCount; c1++)
            {
                if (!indices.Contains(c1))
                    continue;

                var col1 = matrix.Column(c1);
                result = result.StackColumns(col1);

                for (int c2 = c1 + 1; c2 < matrix.ColumnCount; c2++)
                {
                    if (!indices.Contains(c2))
                        continue;

                    var col2 = matrix.Column(c2);

                    bool allSame = true;
                    for (int r = 0; r < matrix.RowCount; r++)
                    {
                        if (col1[r] != col2[r])
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (allSame)
                    {
                        indices.Remove(c2);
                    }
                }
            }

            return result;
        }
    }

    public static Matrix<T> Transpose(Matrix<T> matrix) => new(matrix.ColumnCount, matrix.RowCount, (r, c) => matrix[c, r]);

    public static double Mean(Matrix<T> matrix)
    {
        decimal result = 0;
        for (int r = 0; r < matrix.RowCount; r++)
            for (int c = 0; c < matrix.ColumnCount; c++)
                result += decimal.CreateChecked(matrix[r, c]);
        return matrix.Size > 0 ? (double)(result / matrix.Size) : 0;
    }

    // Overrides

    public override bool Equals(object? obj)
    {
        if (obj is Matrix<T> matrix)
        {
            if (RowCount != matrix.RowCount || ColumnCount != matrix.ColumnCount)
                return false;

            for (int r = 0; r < RowCount; r++)
                for (int c = 0; c < ColumnCount; c++)
                    if (_m[r, c] != matrix[r, c])
                        return false;

            return true;
        }

        return base.Equals(obj);
    }

    public override string ToString()
    {
        List<string> lines = [];
        if (IsColumn)
        {
            for (int r = 0; r < RowCount; r++)
            {
                var s = $"{_m[r,0],-8:F2}";
                lines.Add(s.Trim());
            }
        }
        else
        {
            for (int r = 0; r < RowCount; r++)
            {
                string s = string.Empty;
                for (int c = 0; c < ColumnCount; c++)
                    s += $"{_m[r, c],-8:F2}";
                lines.Add(s.Trim());
            }
        }
        return string.Join('\n', lines);
    }

    public override int GetHashCode() => base.GetHashCode();

    // Interfaces 

    public bool MoveNext() => ++_enumIndex < Size;

    public void Reset() => _enumIndex = -1;

    public IEnumerator<T> GetEnumerator() => this;

    IEnumerator IEnumerable.GetEnumerator() => this;

    public void Dispose() {  }

    // Internal

    readonly int _rows;
    readonly int _cols;
    readonly T[,] _m;

    int _enumIndex = -1;
}
