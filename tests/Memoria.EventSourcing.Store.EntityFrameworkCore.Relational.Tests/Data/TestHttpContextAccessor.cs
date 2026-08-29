using Microsoft.AspNetCore.Http;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Relational.Tests.Data;

/// <summary>
/// Supplies the audit interceptor with a user, without pulling a mocking library into this project.
/// </summary>
public sealed class TestHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
}
