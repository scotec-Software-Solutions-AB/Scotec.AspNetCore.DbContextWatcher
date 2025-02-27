﻿using System.Net;
using System.Reflection;
using HarmonyLib;
using Microsoft.EntityFrameworkCore;

// ReSharper disable StaticMemberInGenericType

namespace Scotec.AspNetCore.DbContextWatcher;

/// <inheritdoc />
public class DbContextWatcherMiddleware<TDbContext> : DbContextWatcherMiddleware<TDbContext, IServiceProvider>
    where TDbContext : DbContext
{
    public DbContextWatcherMiddleware(RequestDelegate next) : base(next)
    {
    }
}

/// <summary>
///     The DbContextWatcherMiddleware provides monitoring for changes in the DbContext to enhance safety,
///     idempotency, integrity, and consistency in HTTP methods within ASP.NETCore applications. By tracking database
///     changes, it ensures safe operations (no unintended side effects), maintains idempotency (operations like GET are
///     repeatable without altering state), and upholds data integrity and consistency across HTTP requests like POST or
///     PUT.
/// </summary>
public class DbContextWatcherMiddleware<TDbContext, TSessionContext>
    where TDbContext : DbContext
    where TSessionContext : class?
{
    /// <summary>
    ///     Save and idempotent http methods
    ///     <seealso cref="http://www.iana.org/assignments/http-methods/http-methods.xhtml" />
    /// </summary>
    protected static readonly string[] SaveHttpMethods =
    [
        "GET",
        "HEAD",
        "OPTIONS",
        "PRI",
        "PROPFIND",
        "REPORT",
        "SEARCH",
        "TRACE"
    ];

    private static readonly AsyncLocal<HttpContext?> LocalHttpContext = new();
    private static readonly AsyncLocal<TDbContext?> LocalDbContext = new();
    private static readonly AsyncLocal<TSessionContext?> LocalSessionContext = new();

    private readonly RequestDelegate _next;

    static DbContextWatcherMiddleware()
    {
        var harmony = new Harmony("com.scotec-software.dbcontextwatcher");

        ApplyPatch(harmony, "SaveChangesAsync", [typeof(bool), typeof(CancellationToken)], "Prefix");
        ApplyPatch(harmony, "SaveChanges", [typeof(bool)], "Prefix");
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DbContextWatcherMiddleware{TDbContext, TService}" /> class.
    /// </summary>
    /// <param name="next">The delegate to the next instance in the middleware pipeline.</param>
    public DbContextWatcherMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    protected HttpContext HttpContext => LocalHttpContext.Value!;
    protected TDbContext DbContext => LocalDbContext.Value!;
    protected TSessionContext SessionContext => LocalSessionContext.Value!;

    /// <summary>
    ///     Applies a patch to the specified method.
    /// </summary>
    /// <param name="harmony"></param>
    /// <param name="methodName">The method on which the patch is to be applied.</param>
    /// <param name="parameters">List with the parameter types of the method</param>
    /// <param name="patchName">The name of the patch.</param>
    /// <remarks>
    ///     If the type TDbContext is derived from DbContext, but the method you are looking for is implemented in the
    ///     base class, a MethodInfo is still returned. However, this refers to the derived class (TDbContext) in the
    ///     ‘ReflectedType’ property. We therefore use the declaring type here and search for the desired method again, which
    ///     now references the correct type in the ‘ReflectedType’ property. This procedure is absolutely necessary as Harmony
    ///     assumes that the method is implemented in the reflected type.
    /// </remarks>
    /// <exception cref="NotImplementedException"></exception>
    private static void ApplyPatch(Harmony harmony, string methodName, Type[] parameters, string patchName)
    {
        var dbContextType = typeof(TDbContext)!;
        MethodInfo? method;

        while (true)
        {
            method = dbContextType?.GetMethod(methodName, parameters);

            if (method == null)
            {
                throw new NotImplementedException(
                    $"The type {typeof(TDbContext).Name} does not implement method '{methodName}'");
            }

            if (dbContextType == method.DeclaringType)
            {
                break;
            }

            dbContextType = method.DeclaringType;
        }

        var patchType = typeof(DbContextWatcherPatch<>).MakeGenericType(dbContextType!);
        var patchMethod = patchType.GetMethod(patchName);

        harmony.Patch(method, new HarmonyMethod(patchMethod));
    }

    protected virtual async Task OnInvokeAsync()
    {
        DbContext.ChangeTracker.QueryTrackingBehavior = await CanSaveChangesAsync()
            ? QueryTrackingBehavior.TrackAll
            : QueryTrackingBehavior.NoTrackingWithIdentityResolution;
    }

    /// <summary>
    ///     Returns true if changes can be saved in the database while the request is being processed. By default, saving is
    ///     only permitted during the processing of e.g. POST, PUT, PATCH or DELETE requests.
    /// </summary>
    /// <remarks>
    ///     This method can be overridden to implement your own validation rules.
    /// </remarks>
    protected virtual Task<bool> CanSaveChangesAsync()
    {
        return Task.FromResult(!SaveHttpMethods.Contains(HttpContext.Request.Method));
    }

    /// <summary>
    ///     Returns true if there are changes in the DbContext that are to be sent to the database.
    /// </summary>
    /// <remarks>
    ///     This method can be overwritten to implement your own change detection.
    /// </remarks>
    protected virtual Task<bool> HasChangesAsync()
    {
        return Task.FromResult(DbContext.ChangeTracker.HasChanges());
    }

    /// <summary>
    ///     Send an error response to the client.
    /// </summary>
    protected virtual async Task SendResponseAsync(DbContextWatcherException exception)
    {
        HttpContext.Response.ContentType = "application/json";
        HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var detailed = exception.Cause switch
        {
            DbContextWatcherError.UnsafedData =>
                "The DbContext contains changes that have not yet been sent to the database. The response to the client may contain data that is in an invalid state.",
            DbContextWatcherError.ModifiedData =>
                "The DbContext contains modified data. However, changes are not permitted as the session is in a read-only context.",
            DbContextWatcherError.Forbidden =>
                "Saving changes is not permitted as the session is in the read-only context.",
            _ => throw new ArgumentOutOfRangeException(nameof(DbContextWatcherException.Cause), exception.Cause, null)
        };

        var response = new
        {
            StatusCode = (int)HttpStatusCode.InternalServerError,
            Message = "An error occurred while processing a request.",
            Detailed = detailed
        };
        await HttpContext.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    ///     Invokes the middleware and proceeds to the next instance in the middleware pipeline.
    /// </summary>
    /// <param name="httpContext">The context for the current request.</param>
    /// <param name="dbContext">Tha database context.</param>
    /// <param name="appContext">Application defined context. This can be any registered service.</param>
    /// <returns>The async task.</returns>
    public async Task Invoke(HttpContext httpContext, TDbContext dbContext, TSessionContext? appContext = default)
    {
        LocalHttpContext.Value = httpContext;
        LocalDbContext.Value = dbContext;
        LocalSessionContext.Value = appContext;

        await OnInvokeAsync();

        var responseBodyStream = httpContext.Response.Body;

        dynamic container = DynamicStateContainer.GetContainer(dbContext);
        container.CanSaveChanges = new Func<Task<bool>>(CanSaveChangesAsync);

        httpContext.Response.Body =
            new DbContextWatcherStream(responseBodyStream, HasChangesAsync, CanSaveChangesAsync);

        try
        {
            await _next(httpContext);
        }
        catch (DbContextWatcherException e)
        {
            httpContext.Response.Body = responseBodyStream;
            // The database context should never track any changes while processing
            // a safe and idempotent http method call (e.g. GET or HEAD).
            await SendResponseAsync(e);
        }
        finally
        {
            httpContext.Response.Body = responseBodyStream;
            LocalHttpContext.Value = null;
            LocalDbContext.Value = null;
            LocalSessionContext.Value = null;
        }
    }
}