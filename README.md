# Scotec.AspNetCore.DbContextWatcher
The Scotec.AspNetCore.DbContextWatcher provides monitoring for changes in the DbContext to enhance safety, idempotency, integrity, and consistency in HTTP methods within ASP.NETCore applications. By tracking database changes, it ensures safe operations (no unintended side effects), maintains idempotency (operations like GET are repeatable without altering state), and upholds data integrity and consistency across HTTP requests like POST or PUT.

## Safety and idempotency
[Safe](http://en.wikipedia.org/wiki/Hypertext_Transfer_Protocol#Safe_methods) and [idempotent](http://en.wikipedia.org/wiki/Idempotence) methods such as GET or HEAD should never change the status of the server. This also applies to the creation, modification or deletion of data in the database. The DbContextWatcher middleware checks all incoming requests and sets the ‘DbContext’ to the readonly state. Attempting to call the ```Save``` method results in an exception error that is returned to the client.

## Integrity and consistency
In a REST API, the best practice is to write data to the database before sending the response to the client. This approach ensures data consistency and integrity. Here’s why:

- **Data Integrity**: Ensures that the changes are committed successfully before informing the client. This avoids scenarios where the client is informed of a successful operation, but the data is not actually saved.

- **Error Handling**: Allows you to handle any errors or exceptions during the database write operation and send an appropriate error response to the client.

- **Transactional Consistency**: Guarantees that the entire operation (including any business logic and database writes) is completed within a single transaction, ensuring atomicity.


DbContextWatcher middleware checks the current status of the DbContext before a response is sent back to the client. If the DbContext contains modified data, the response is not allowed to be sent and an error code is returned instead.


## Change tracking
When making read or write API calls, the same code parts are often used internally for querying entities. While change tracking is often switched on for write calls (e.g. POST or PUT), it should be deactivated for purely read accesses (e.g. GET) for performance reasons.
However, this means that tracking must be activated or deactivated at these points depending on the request.
DbContextWatcher automatically sets the default behaviour for the tracking behaviour of the DbContext to ```TrackingAll``` for write API calls and to ```NoTrackingWithIdentityResolution``` for read API calls.

## Change detection
When processing read-only API calls, DbContextWatcher checks whether an attempt was made to modify data immediately before returning the response to the client. For this purpose, ```HasChanges``` is called on the change tracker. If any changes are recognised here, DbContextWatcher interprets this as an attempt to manipulate data and returns an error to the client. This can be particularly helpful when developing an app in order to detect potential errors that could lead to data inconsistency or performance issues.

## How to use DbContextWatcher
DbContextWatcher is implemented as middleware. In order to activate the desired monitoring of the DbContext as early as possible, DbContextWatcher should be installed as one of the first middlewares if possible, but at the latest before the first middleware that initiates database calls.

To use the standard behaviour of DbContextWatcher, it is sufficient to register the DbContextWatcher middleware.

``` csharp
app.UseMiddleware<DbContextWatcherMiddleware<MyDbContext>>();
```
DbContextWatcher requires a generic type parameter that specifies the type of your DbContext.

### Create your own...
If you want more control over the behaviour of DbContextWatcher, you can derive your own class from it.

``` csharp
public class MyDbContextWatcherMiddleware : DbContextWatcherMiddleware<MyDbContext>
{
    public MyDbContextWatcherMiddleware(RequestDelegate next) : base(next)
    {
    }

    protected override Task OnInvokeAsync()
    {
        return base.OnInvokeAsync();
    }

    protected override Task<bool> CanSaveChangesAsync()
    {
        return base.CanSaveChangesAsync();
    }

    protected override Task<bool> HasChangesAsync()
    {
        return base.HasChangesAsync();
    }

    protected override Task SendResponseAsync(DbContextWatcherException exception)
    {
        return base.SendResponseAsync(exception);
    }
}
```

To change the behaviour of DbContextWatcher, the following methods can be overridden.

#### OnInvokeAsync
OnInvokeAsync is called immediately after the middleware is invoked. Here you have the option of making settings in the DbContext. DbContextWatcher sets the tracking behaviour of the DbContext to ```TrackAll``` or ```NoTrackingWithIdentityResolution``` depending on the HTTP method (POST, PUT, GET, etc.).
``` csharp
protected virtual async Task OnInvokeAsync()
{
    DbContext.ChangeTracker.QueryTrackingBehavior = await CanSaveChangesAsync() 
        ? QueryTrackingBehavior.TrackAll 
        : QueryTrackingBehavior.NoTrackingWithIdentityResolution;
}
``` 

#### CanSaveChangesAsync

CanSaveChangesAsync determines whether the DbContext may save changes to the database or not.

``` csharp
protected virtual Task<bool> CanSaveChangesAsync()
{
    return Task.FromResult(!SaveHttpMethods.Contains(HttpContext.Request.Method));
}
```

#### HasChangesAsync

HasChangesAsync checks whether the change tracker has detected any modified data.

``` csharp
    protected virtual Task<bool> HasChangesAsync()
    {
        return Task.FromResult(DbContext.ChangeTracker.HasChanges());
    }
```
You can implement your own checks by overriding this method. 
For example, you may want to write log information to the database. In this case, new entries related to the log table could be ignored and the DbContext could be considered unchanged in that case. However, even if this is a valid use case, the use of a separate DbContext for writing into the log table should be considered.

#### SendResponseAsync
SendResponseAsync sends a response with the Http status 500 back to the client.

The method can be overwritten to send back your own response. Alternatively, an exception can also be thrown here, which can then be processed later in an exception handler middleware.

