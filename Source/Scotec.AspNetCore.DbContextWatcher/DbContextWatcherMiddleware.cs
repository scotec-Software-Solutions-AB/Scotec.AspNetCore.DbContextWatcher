using System.Net;
using HarmonyLib;
using Microsoft.EntityFrameworkCore;

// ReSharper disable StaticMemberInGenericType

namespace Scotec.AspNetCore.DbContextWatcher;

/// <summary>
///     Middleware for watching the database context.
/// </summary>
/// <remarks>
///     <a href="http://en.wikipedia.org/wiki/Hypertext_Transfer_Protocol#Safe_methods">Save</a> and
///     <a href="http://en.wikipedia.org/wiki/Idempotence">idempotent</a> idempotent methods should never change the status
///     of the server. This also includes creating, modifying or deleting data in the database. The
///     DbContextWatcherMiddleware checks all incoming requests and sets the
///     <see cref="Microsoft.EntityFrameworkCore.DbContext" /> into the readonly state. Attempts to
///     call the Save method lead to an exception.<br /><br />
///     In a REST WebAPI, the best practice is to write data to the database before sending the response to the client.
///     This approach ensures data consistency and integrity.
///     DbContextWatcherMiddleware checks the current status of the DbContext before a response is sent back to the client.
///     If the DbContext contains modified data, the response is not allowed to be sent and an error code is returned
///     instead.
/// </remarks>
public class DbContextWatcherMiddleware<TDbContext, TAppContext>
    where TDbContext : DbContext
    where TAppContext : class?
{
    /// <summary>
    ///     Save and idempotent http methods
    ///     <a href="">aaa</a>
    ///     <seealso cref="http://www.iana.org/assignments/http-methods/http-methods.xhtml" />
    /// </summary>
    private static readonly string[] SaveMethods =
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
    private static readonly AsyncLocal<TAppContext?> LocalAppContext = new();

    private readonly RequestDelegate _next;

    static DbContextWatcherMiddleware()
    {
        var prefix = typeof(DbContextPatch<TDbContext>).GetMethod("Prefix");

        var harmony = new Harmony("com.scotec-software.dbcontextwatcher");

        var saveChangesAsync = typeof(TDbContext).GetMethod(nameof(DbContext.SaveChangesAsync), new[]
        {
            typeof(bool), typeof(CancellationToken)
        });

        var saveChanges = typeof(TDbContext).GetMethod(nameof(DbContext.SaveChanges), new[]
        {
            typeof(bool)
        });

        harmony.Patch(saveChangesAsync, new HarmonyMethod(prefix));
        harmony.Patch(saveChanges, new HarmonyMethod(prefix));
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
    protected TAppContext AppContext => LocalAppContext.Value!;

    protected virtual Task OnInvoke()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Returns true if changes may be saved in the database while the request is being processed. By default, saving is
    ///     only permitted during the processing of POST, PUT, PATCH and DELETE requests.
    /// </summary>
    /// <remarks>
    ///     This method can be overridden to implement your own validation rules.
    /// </remarks>
    protected virtual bool CanSaveChanges()
    {
        return !SaveMethods.Contains(HttpContext.Request.Method);
    }

    /// <summary>
    ///     Returns true if there are changes in the DbContext that are to be sent to the database.
    /// </summary>
    /// <remarks>
    ///     This method can be overwritten to implement your own change detection.
    /// </remarks>
    protected virtual bool HasChanges()
    {
        return DbContext.ChangeTracker.HasChanges();
    }

    protected virtual async Task SendResponseAsync(DbContextWatcherError cause)
    {
        HttpContext.Response.ContentType = "application/json";
        HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var detailed = cause switch
        {
            DbContextWatcherError.UnsafedData =>
                "The DbContext contains changes that have not yet been sent to the database. The response to the client may contain data that is in an invalid state.",
            DbContextWatcherError.ModifiedData => "The database context contains modified entities in a readonly context.",
            _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null)
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
    public async Task Invoke(HttpContext httpContext, TDbContext dbContext, TAppContext? appContext = default)
    {
        LocalHttpContext.Value = httpContext;
        LocalDbContext.Value = dbContext;
        LocalAppContext.Value = appContext;

        await OnInvoke();

        var responseBodyStream = httpContext.Response.Body;
        var canSaveChanges = CanSaveChanges();

        dynamic container = DynamicStateContainer.GetContainer(dbContext);
        container.CanSaveChanges = canSaveChanges;

        if (!canSaveChanges)
        {
            httpContext.Response.Body = new DbContextWatcherStream(responseBodyStream, HasChanges);
        }

        try
        {
            await _next(httpContext);

            if (canSaveChanges && HasChanges())
            {
                throw new DbContextWatcherException(DbContextWatcherError.ModifiedData);
            }
        }
        catch (DbContextWatcherException e)
        {
            httpContext.Response.Body = responseBodyStream;
            // The database context should never track any changes while processing a safe and idempotent http method call (e.g. GET or HEAD).
            await SendResponseAsync(e.Cause);
        }
        finally
        {
            httpContext.Response.Body = responseBodyStream;
            LocalHttpContext.Value = null;
            LocalDbContext.Value = null;
            LocalAppContext.Value = null;
        }
    }
}