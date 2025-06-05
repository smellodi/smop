using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Smop.ML.Search;

/// <summary>
/// Represents a two-dimensional matrix of elements of type <typeparamref name="T"/>. 
/// This is a custom implementation that could be not optimal for some operations.
/// </summary>
/// <remarks>The <see cref="Matrix{T}"/> class provides functionality for creating, manipulating, and performing
/// operations on matrices. It supports various constructors for initialization, indexers for accessing elements, and
/// methods for matrix operations such as transposition, stacking, and removing duplicates. The matrix can represent
/// scalars, vectors, rows, columns, or general two-dimensional arrays.</remarks>
/// <typeparam name="T">Numeric type</typeparam>
/// <typeparam name="T">The type of elements stored in the matrix. Must implement <see cref="System.Numerics.INumber{T}"/>.</typeparam>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class with default dimensions.
    /// </summary>
    /// <remarks>The matrix is initialized with zero rows and zero columns. Use appropriate methods or
    /// properties to resize and populate the matrix after initialization.</remarks>
    public Matrix()
    {
        _rows = 0;
        _cols = 0;
        _m = new T[_rows, _cols];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class using a single-dimensional array.
    /// </summary>
    /// <remarks>The resulting matrix will have one row and a number of columns equal to the length of the
    /// provided array. Each element in the array is assigned to the corresponding column in the matrix.</remarks>
    /// <param name="m">A one-dimensional array of elements used to populate the matrix. The array represents a single row of the
    /// matrix.</param>
    public Matrix(T[] m)
    {
        _rows = 1;
        _cols = m.Length;
        _m = new T[_rows, _cols];

        for (int c = 0; c < _cols; c++)
            _m[0, c] = m[c];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class with the specified dimensions and content.
    /// </summary>
    /// <remarks>This constructor initializes the matrix with the provided dimensions and content. Ensure that
    /// the dimensions and the array size are consistent to avoid an exception.</remarks>
    /// <param name="rows">The number of rows in the matrix. Must be greater than zero.</param>
    /// <param name="cols">The number of columns in the matrix. Must be greater than zero.</param>
    /// <param name="m">A two-dimensional array containing the elements of the matrix. The total number of elements must match <paramref
    /// name="rows"/> × <paramref name="cols"/>.</param>
    /// <exception cref="ArgumentException">Thrown when the total number of elements in <paramref name="m"/> does not match the specified matrix dimensions.</exception>
    public Matrix(int rows, int cols, T[,] m)
    {
        if (rows * cols != m.Length)
            throw new ArgumentException("Matrix content does not match its size");

        _rows = rows;
        _cols = cols;
        _m = m;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class with the specified number of rows and columns, 
    /// and optionally sets all elements to an initial value.
    /// </summary>
    /// <remarks>This constructor creates a matrix with the specified dimensions and initializes all elements
    /// to the provided value.  If <paramref name="initialValue"/> is not provided, the matrix elements are initialized
    /// to the default value of <typeparamref name="T"/>.</remarks>
    /// <param name="rows">The number of rows in the matrix. Must be greater than zero.</param>
    /// <param name="cols">The number of columns in the matrix. Must be greater than zero.</param>
    /// <param name="initialValue">The value to initialize all elements of the matrix. If not specified, the default value of <typeparamref
    /// name="T"/> is used.</param>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class with the specified dimensions and a function to
    /// generate values.
    /// </summary>
    /// <remarks>The matrix is initialized by invoking the <paramref name="getValue"/> function for each
    /// element, using its row and column indices.</remarks>
    /// <param name="rows">The number of rows in the matrix. Must be greater than zero.</param>
    /// <param name="cols">The number of columns in the matrix. Must be greater than zero.</param>
    /// <param name="getValue">A function that generates the value for each element in the matrix. The function takes two parameters: the row
    /// index and the column index, and returns the value of type <typeparamref name="T"/>.</param>
    public Matrix(int rows, int cols, Func<int, int, T> getValue)
    {
        _rows = rows;
        _cols = cols;
        _m = new T[_rows, _cols];

        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _m[r, c] = getValue(r, c);
    }

    /// <summary>
    /// Creates a new matrix that is a copy of the current matrix.
    /// </summary>
    /// <remarks>The returned matrix contains the same dimensions and values as the original matrix. Changes
    /// made to the copied matrix will not affect the original matrix, and vice versa.</remarks>
    /// <returns>A new <see cref="Matrix{T}"/> instance that is identical to the current matrix.</returns>
    public Matrix<T> Copy()
    {
        var result = new Matrix<T>(RowCount, ColumnCount);
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                result._m[r, c] = _m[r, c];
        return result;
    }

    /// <summary>
    /// Gets or sets the value at the specified row and column in the matrix.
    /// </summary>
    /// <param name="row">The zero-based index of the row.</param>
    /// <param name="col">The zero-based index of the column.</param>
    /// <returns></returns>
    public T this[int row, int col]
    {
        get => _m[row, col];
        set => _m[row, col] = value;
    }

    /// <summary>
    /// Gets a submatrix consisting of the specified column and the range of rows.
    /// </summary>
    /// <remarks>If <paramref name="row"/> is <c>null</c>, the entire range of rows (from 0 to <see
    /// cref="RowCount"/>) is used.</remarks>
    /// <param name="row">An optional <see cref="Range"/> specifying the rows to include. If <c>null</c>, all rows are included.</param>
    /// <param name="col">The index of the column to extract. Must be within the bounds of the matrix.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the specified column and the rows defined by <paramref name="row"/>.
    /// The resulting matrix will have a single column and a number of rows equal to the length of the specified range.</returns>
    public Matrix<T> this[Range? row, int col]
    {
        get
        {
            Range range = row ?? new Range(0, RowCount);
            (int offset, int rows) = range.GetOffsetAndLength(RowCount);
            var result = new Matrix<T>(rows, 1);
            for (int r = 0; r < rows; r++)
                result._m[r, 0] = _m[r + offset, col];
            return result;
        }
    }

    /// <summary>
    /// Gets a submatrix representing a single row and a specified range of columns.
    /// </summary>
    /// <remarks>This indexer allows extracting a single row from the matrix as a new submatrix, with the
    /// ability to specify a range of columns. If no range is provided, the entire row is returned.</remarks>
    /// <param name="row">The zero-based index of the row to retrieve. Must be within the bounds of the matrix.</param>
    /// <param name="col">An optional range specifying the columns to include in the submatrix. If <see langword="null"/>, the entire row
    /// is returned. The range must be within the bounds of the matrix's columns.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the specified row and the columns defined by <paramref name="col"/>.
    /// The resulting matrix will have a single row and a number of columns equal to the length of the specified range.</returns>
    public Matrix<T> this[int row, Range? col]
    {
        get
        {
            Range range = col ?? new Range(0, ColumnCount);
            (int offset, int cols) = range.GetOffsetAndLength(ColumnCount);
            var result = new Matrix<T>(1, cols);
            for (int c = 0; c < cols; c++)
                result._m[0, c] = _m[row, c + offset];
            return result;
        }
    }

    /// <summary>
    /// Gets a submatrix of the current matrix based on the specified row and column ranges.
    /// </summary>
    /// <remarks>The ranges are inclusive and zero-based. If the specified ranges exceed the dimensions of the
    /// matrix, an exception may be thrown.</remarks>
    /// <param name="row">The range of rows to include in the submatrix. If <see langword="null"/>, all rows are included.</param>
    /// <param name="col">The range of columns to include in the submatrix. If <see langword="null"/>, all columns are included.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the specified rows and the columns defined by <paramref name="row"/> and <paramref name="col"/>.</returns>
    public Matrix<T> this[Range? row, Range? col]
    {
        get
        {
            if (row == null && col == null)
                return this.Copy();

            Range rangeH = col ?? new Range(0, ColumnCount);
            Range rangeV = row ?? new Range(0, RowCount);
            (int offsetH, int cols) = rangeH.GetOffsetAndLength(ColumnCount);
            (int offsetV, int rows) = rangeV.GetOffsetAndLength(RowCount);

            var result = new Matrix<T>(rows, cols);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    result._m[r, c] = _m[r + offsetV, c + offsetH];
            return result;
        }
    }

    /// <summary>
    /// Gets or sets the value at the specified index in the matrix, treating the matrix as a vector.
    /// </summary>
    /// <remarks>This indexer allows access to the matrix elements when the matrix is either a row vector or a
    /// column vector. If the matrix is not a vector (i.e., it has more than one row and more than one column), an
    /// exception is thrown.</remarks>
    /// <param name="index">The zero-based index of the element to get or set. Must be within the bounds of the vector.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if the matrix is not a vector (i.e., it has more than one row and more than one column).</exception>
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

    /// <summary>
    /// Retrieves a single row from the matrix as a new matrix.
    /// </summary>
    /// <param name="row">The zero-based index of the row to retrieve. Must be within the range of valid row indices.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the elements of the specified row.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="row"/> is less than 0 or greater than or equal to the number of rows in the matrix.</exception>
    public Matrix<T> Row(int row)
    {
        if (row < 0 || row >= _rows)
            throw new ArgumentException("Invalid row index");

        var result = new T[ColumnCount];
        for (int c = 0; c < _cols; c++)
            result[c] = _m[row, c];
        return Matrix<T>.FromRow(result);
    }

    /// <summary>
    /// Extracts a single column from the matrix as a new matrix.
    /// </summary>
    /// <remarks>The resulting matrix will have a single column, with the same number of rows as the original
    /// matrix.</remarks>
    /// <param name="column">The zero-based index of the column to extract. Must be within the range of valid column indices.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the values of the specified column.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="column"/> is less than 0 or greater than or equal to the number of columns in the
    /// matrix.</exception>
    public Matrix<T> Column(int column)
    {
        if (column < 0 || column >= _cols)
            throw new ArgumentException("Invalid column index");

        var result = new T[RowCount];
        for (int r = 0; r < _rows; r++)
            result[r] = _m[r, column];
        return Matrix<T>.FromColumn(result);
    }

    /// <summary>
    /// Combines the columns of the current matrix with the columns of the specified matrix to create a new matrix.
    /// </summary>
    /// <remarks>The resulting matrix will have the same number of rows as the current matrix and the
    /// specified matrix.  Ensure that both matrices have the same number of rows before calling this method.</remarks>
    /// <param name="matrix">The matrix whose columns will be stacked with the columns of the current matrix.</param>
    /// <returns>A new matrix containing the columns of the current matrix followed by the columns of the specified matrix.</returns>
    public Matrix<T> StackColumns(Matrix<T> matrix) => StackColumns(this, matrix);

    /// <summary>
    /// Combines the rows of the current matrix with the rows of the specified matrix to create a new matrix.
    /// </summary>
    /// <remarks>The resulting matrix will have the same number of columns as the current matrix and the
    /// specified matrix. Ensure that both matrices have compatible dimensions before calling this method.</remarks>
    /// <param name="matrix">The matrix whose rows will be stacked below the rows of the current matrix.</param>
    /// <returns>A new matrix containing the rows of the current matrix followed by the rows of the specified matrix.</returns>
    public Matrix<T> StackRows(Matrix<T> matrix) => StackRows(this, matrix);

    /// <summary>
    /// Creates a new matrix by excluding the specified row from the current matrix.
    /// </summary>
    /// <param name="row">The index of the row to exclude. Must be within the bounds of the matrix.</param>
    /// <returns>A new <see cref="Matrix{T}"/> instance that contains all rows of the current matrix except the specified row.</returns>
    public Matrix<T> ExcludeRow(int row) => ExcludeRow(this, row);

    /// <summary>
    /// Creates a new matrix with the specified column excluded.
    /// </summary>
    /// <param name="column">The zero-based index of the column to exclude. Must be within the bounds of the matrix.</param>
    /// <returns>A new <see cref="Matrix{T}"/> instance that contains all columns except the specified column.</returns>
    public Matrix<T> ExcludeColumn(int column) => ExcludeColumn(this, column);

    /// <summary>
    /// Replaces the specified column in the current matrix with the corresponding column from the source matrix.
    /// </summary>
    /// <remarks>This method modifies the current matrix by replacing the specified column with data from the
    /// source matrix. Ensure that the source matrix has the same number of rows as the current matrix to avoid an
    /// exception.</remarks>
    /// <param name="col">The zero-based index of the column to replace. Must be within the bounds of the matrix.</param>
    /// <param name="source">The source matrix of one column from which the replacement is takn.
    /// Must have the same number of rows as the current matrix.</param>
    public void ReplaceColumn(int col, Matrix<T> source) => ReplaceColumn(this, col, source);

    /// <summary>
    /// Replaces the specified row in the current matrix with the corresponding row from the source matrix.
    /// </summary>
    /// <remarks>The method replaces the entire row at the specified index in the current matrix with the
    /// corresponding row from the source matrix.  The dimensions of the source matrix must be compatible with the
    /// current matrix for the operation to succeed.</remarks>
    /// <param name="row">The zero-based index of the row to replace. Must be within the bounds of the matrix.</param>
    /// <param name="source">The source matrix of one row from which the replacement row is taken.
    /// Must have the same number of columns as the current matrix.</param>
    public void ReplaceRow(int row, Matrix<T> source) => ReplaceRow(this, row, source);

    /// <summary>
    /// Removes duplicate elements from the matrix based on the specified direction.
    /// </summary>
    /// <remarks>The method does not modify the original matrix. Instead, it returns a new matrix with the
    /// duplicates removed. Ensure that the <paramref name="direction"/> parameter is valid for the operation;  invalid
    /// directions may result in undefined behavior or exceptions.</remarks>
    /// <param name="direction">The direction in which duplicates are evaluated and removed. Valid values are row-wise or
    /// column-wise.</param>
    /// <returns>A new <see cref="Matrix{T}"/> instance with duplicates removed in the specified direction.</returns>
    public Matrix<T> RemoveDuplicates(Direction direction) => RemoveDuplicates(this, direction);

    /// <summary>
    /// Creates a new matrix that is the transpose of the current matrix.
    /// </summary>
    /// <remarks>The transpose of a matrix is obtained by flipping it over its diagonal, swapping the row and
    /// column indices of each element.</remarks>
    /// <returns>A new <see cref="Matrix{T}"/> instance representing the transposed matrix.</returns>
    public Matrix<T> Transpose() => Transpose(this);

    /// <summary>
    /// Raises each element of the matrix to the specified power.
    /// </summary>
    /// <param name="p">The exponent to which each element of the matrix is raised. Must be a finite number.</param>
    /// <returns>A new matrix where each element is the result of raising the corresponding element of the original matrix to the
    /// power <paramref name="p"/>.</returns>
    public Matrix<T> Power(double p) => this ^ p;

    /// <summary>
    /// Calculates the arithmetic mean of the elements in the current collection.
    /// </summary>
    /// <remarks>This method computes the mean value of the elements in the collection.</remarks>
    /// <returns>The arithmetic mean of the elements in the collection as a <see cref="double"/>.</returns>
    public double Mean() => Mean(this);

    // Static

    // Operators

    public static Matrix<T> operator *(Matrix<T> matrix, T k) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] * k);
    public static Matrix<T> operator *(T k, Matrix<T> matrix) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] * k);
    public static Matrix<T> operator *(Matrix<T> m1, Matrix<T> m2)  // dot product!!
    {
        if (m1.RowCount != m2.RowCount || m1.ColumnCount != m2.ColumnCount)
            throw new ArgumentException("Matrix operator '*' requires matrices of the same shape");

        return new(m1.RowCount, m1.ColumnCount, (r, c) => m1[r, c] * m2[r, c]);
    }
    public static Matrix<T> operator /(Matrix<T> matrix, T k) => new(matrix.RowCount, matrix.ColumnCount, (r, c) => matrix[r, c] / k);
    public static Matrix<T> operator /(Matrix<T> m1, Matrix<T> m2)
    {
        if (m1.RowCount != m2.RowCount || m1.ColumnCount != m2.ColumnCount)
            throw new ArgumentException("Matrix operator '/' requires matrices of the same shape");

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
            throw new ArgumentException("Matrix operator '-' requires matrices of the same shape");
        return new(a.RowCount, a.ColumnCount, (r, c) => a[r, c] - b[r, c]);
    }

    public static Matrix<T> operator +(Matrix<T> a, Matrix<T> b)
    {
        if (a.RowCount != b.RowCount || a.ColumnCount != b.ColumnCount)
            throw new ArgumentException("Matrix operator '+' requires matrices of the same shape");
        return new(a.RowCount, a.ColumnCount, (r, c) => a[r, c] + b[r, c]);
    }

    public static bool operator ==(Matrix<T> a, Matrix<T> b)
    {
        if (a is null && b is null)
            return true;
        if (a is null && b is not null)
            return false;
        if (a is not null && b is null)
            return false;

        if (a.RowCount != b.RowCount || a.ColumnCount != b.ColumnCount)
            return false;

        for (int r = 0; r < a.RowCount; r++)
            for (int c = 0; c < a.ColumnCount; c++)
                if (a._m[r, c] != b._m[r, c])
                    return false;
        return true;
    }

    public static bool operator !=(Matrix<T> a, Matrix<T> b) => !(a == b);

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

    public static Matrix<T> Transpose(Matrix<T> matrix)
    {
        var result = new Matrix<T>(matrix.ColumnCount, matrix.RowCount);
        for (int r = 0; r < matrix.RowCount; r++)
            for (int c = 0; c < matrix.ColumnCount; c++)
                result._m[c, r] = matrix._m[r, c];
        return result;
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
            string s = string.Empty;
            for (int r = 0; r < RowCount; r++)
            {
                s += $"{_m[r,0],-8:F2}";
            }
            lines.Add(s.Trim());
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
