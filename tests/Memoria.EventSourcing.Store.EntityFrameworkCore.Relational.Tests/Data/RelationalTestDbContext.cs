using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;

/// <summary>
/// A minimal concrete <see cref="DomainDbContext"/> so the store's own model can be exercised against
/// a real relational provider. It adds nothing of its own — the schema under test is entirely the one
/// the store configures.
/// </summary>
public sealed class RelationalTestDbContext(
    DbContextOptions<DomainDbContext> options,
    TimeProvider timeProvider,
    IHttpContextAccessor httpContextAccessor)
    : DomainDbContext(options, timeProvider, httpContextAccessor);
