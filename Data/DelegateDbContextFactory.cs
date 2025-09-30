using System;
using Microsoft.EntityFrameworkCore;

namespace SysJaky_N.Data;

internal sealed class DelegateDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly Func<TContext> _factory;

    public DelegateDbContextFactory(Func<TContext> factory)
        => _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    public TContext CreateDbContext() => _factory();
}
