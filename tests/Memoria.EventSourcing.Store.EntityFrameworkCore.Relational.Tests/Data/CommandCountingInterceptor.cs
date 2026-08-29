using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;

/// <summary>
/// Records every command the store sends to the database. Round trips are the unit several of the
/// open performance items are measured in, so they need to be observable as a test assertion rather
/// than counted by reading the code.
/// </summary>
public sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];

    public IReadOnlyList<string> Commands => _commands;

    public void Clear() => _commands.Clear();

    /// <summary>Commands that read, i.e. SELECTs the store issues before deciding what to write.</summary>
    public IReadOnlyList<string> Reads =>
        _commands.Where(command => command.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)).ToList();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _commands.Add(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result)
    {
        _commands.Add(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result)
    {
        _commands.Add(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
