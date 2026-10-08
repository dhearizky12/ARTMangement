using System.Data;
using System.Data.Common;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BantuBantu.Tests;

public sealed class NpgsqlRetryTests
{
    private const string TransientSqlState = "57P01";

    private static PostgresException CreateTransientException() =>
        new("terminating connection due to administrator command", "FATAL", "FATAL", TransientSqlState);

    private static PostgresException CreateNonTransientException() =>
        new("relation does not exist", "ERROR", "ERROR", "42P01");

    private static DbContextOptions<AppDbContext> CreateOptions(DbConnection connection) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromMilliseconds(10),
                errorCodesToAdd: null))
            .Options;

    [Fact]
    public async Task RetriesOnTransientConnectionOpenFailure()
    {
        var connection = new FaultInjectingConnection(
            "Host=localhost;Database=test;Username=test;Password=test",
            openFailCount: 2,
            CreateTransientException);

        var options = CreateOptions(connection);

        await using var db = new AppDbContext(options);

        var strategy = db.GetService<IExecutionStrategy>();

        var attempts = 0;
        await strategy.ExecuteAsync<object?, object?>(
            null,
            async (_, _, ct) =>
            {
                attempts++;
                await db.Database.OpenConnectionAsync(ct);
                return new object();
            },
            verifySucceeded: null,
            CancellationToken.None);

        Assert.True(connection.OpenCount >= 3,
            $"Expected at least 3 open attempts (1 initial + 2 retries), got {connection.OpenCount}");
        Assert.Equal(3, attempts);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task DoesNotRetryOnNonTransientFailure()
    {
        var connection = new FaultInjectingConnection(
            "Host=localhost;Database=test;Username=test;Password=test",
            openFailCount: 99,
            CreateNonTransientException);

        var options = CreateOptions(connection);

        await using var db = new AppDbContext(options);
        db.Users.Add(new Domain.User
        {
            Email = "non-transient@example.com",
            FullName = "Non Transient"
        });

        await Assert.ThrowsAsync<PostgresException>(() => db.SaveChangesAsync());

        Assert.Equal(1, connection.OpenCount);
    }

    [Fact]
    public async Task SurfacesExceptionAfterMaxRetriesExhausted()
    {
        var connection = new FaultInjectingConnection(
            "Host=localhost;Database=test;Username=test;Password=test",
            openFailCount: 99,
            CreateTransientException);

        var options = CreateOptions(connection);

        await using var db = new AppDbContext(options);
        db.Users.Add(new Domain.User
        {
            Email = "exhausted@example.com",
            FullName = "Exhausted"
        });

        var outer = await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.Storage.RetryLimitExceededException>(
            () => db.SaveChangesAsync());
        var inner = Assert.IsType<PostgresException>(outer.InnerException);
        Assert.Equal(TransientSqlState, inner.SqlState);

        // 1 initial attempt + 3 retries (maxRetryCount: 3) = 4 total
        Assert.Equal(4, connection.OpenCount);
    }

    private sealed class FaultInjectingConnection : DbConnection
    {
        private readonly string connectionString;
        private readonly int openFailCount;
        private readonly Func<NpgsqlException> exceptionFactory;
        private int openCount;
        private bool isOpen;

        public FaultInjectingConnection(string connectionString, int openFailCount, Func<NpgsqlException> exceptionFactory)
        {
            this.connectionString = connectionString;
            this.openFailCount = openFailCount;
            this.exceptionFactory = exceptionFactory;
        }

        public int OpenCount => openCount;

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString
        {
            get => connectionString;
            set => throw new NotSupportedException();
        }

        public override string Database => "test";
        public override string DataSource => "localhost";
        public override string ServerVersion => "16.0";
        public override ConnectionState State => isOpen ? ConnectionState.Open : ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Close() => isOpen = false;

        public override void Open()
        {
            openCount++;
            if (openCount <= openFailCount)
                throw exceptionFactory();
            isOpen = true;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new StubTransaction(this);

        protected override DbCommand CreateDbCommand() => new StubCommand(this);

        protected override void Dispose(bool disposing) { }
    }

    private sealed class StubTransaction(DbConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => connection;
        public override void Commit() { }
        public override void Rollback() { }
        protected override void Dispose(bool disposing) { }
    }

    private sealed class StubCommand(DbConnection connection) : DbCommand
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText
        {
            get => "";
            set { }
        }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => StubParameterCollection.Instance;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }

        public override int ExecuteNonQuery() => 1;
        public override object? ExecuteScalar() => 1;

        protected override DbParameter CreateDbParameter() => new NpgsqlParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
            new StubDataReader();
    }

    private sealed class StubDataReader : DbDataReader
    {
        private bool read;
        public override int Depth => 0;
        public override int FieldCount => 1;
        public override bool HasRows => false;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override object GetValue(int ordinal) => DBNull.Value;
        public override bool IsDBNull(int ordinal) => true;
        public override bool Read()
        {
            if (read) return false;
            read = true;
            return false;
        }
        public override bool NextResult() => false;
        public override void Close() { }
        public override string GetDataTypeName(int ordinal) => "text";
        public override Type GetFieldType(int ordinal) => typeof(string);
        public override string GetName(int ordinal) => "value";
        public override int GetOrdinal(string name) => 0;
        public override object this[int ordinal] => DBNull.Value;
        public override object this[string name] => DBNull.Value;
        public override bool GetBoolean(int ordinal) => false;
        public override byte GetByte(int ordinal) => 0;
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => '\0';
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int ordinal) => Guid.Empty;
        public override short GetInt16(int ordinal) => 0;
        public override int GetInt32(int ordinal) => 0;
        public override long GetInt64(int ordinal) => 0;
        public override float GetFloat(int ordinal) => 0f;
        public override double GetDouble(int ordinal) => 0d;
        public override decimal GetDecimal(int ordinal) => 0m;
        public override string GetString(int ordinal) => "";
        public override DateTime GetDateTime(int ordinal) => DateTime.MinValue;
        public override int GetValues(object[] values)
        {
            values[0] = DBNull.Value;
            return 1;
        }
        public override System.Collections.IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
    }

    private sealed class StubParameterCollection : DbParameterCollection
    {
        public static readonly StubParameterCollection Instance = new();
        private readonly List<DbParameter> parameters = [];

        public override int Count => parameters.Count;
        public override object SyncRoot => parameters;

        public override int Add(object value)
        {
            parameters.Add((DbParameter)value);
            return parameters.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (var v in values) Add(v!);
        }
        public override void Clear() => parameters.Clear();
        public override bool Contains(object value) => parameters.Contains((DbParameter)value);
        public override bool Contains(string value) => parameters.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)parameters).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => parameters.GetEnumerator();
        public override int IndexOf(object value) => parameters.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => parameters.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => parameters.Insert(index, (DbParameter)value);
        public override void Remove(object value) => parameters.Remove((DbParameter)value);
        public override void RemoveAt(int index) => parameters.RemoveAt(index);
        public override void RemoveAt(string parameterName) => parameters.RemoveAll(p => p.ParameterName == parameterName);
        protected override DbParameter GetParameter(int index) => parameters[index];
        protected override DbParameter GetParameter(string parameterName) =>
            parameters.First(p => p.ParameterName == parameterName);
        protected override void SetParameter(int index, DbParameter value) => parameters[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var i = IndexOf(parameterName);
            if (i >= 0) parameters[i] = value;
            else parameters.Add(value);
        }
    }

    private sealed class StubParameter : DbParameter
    {
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ParameterName { get; set; } = "";
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }
}