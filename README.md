# Scotec.AspNetCore.DbContextWatcher

In a REST WebAPI, the best practice is to write data to the database before sending the response to the client. This approach ensures data consistency and integrity. Here’s why:

- <b>Data Integrity</b>: Ensures that the changes are committed successfully before informing the client. This avoids scenarios where the client is informed of a successful operation, but the data is not actually saved.

- <b>Error Handling</b>: Allows you to handle any errors or exceptions during the database write operation and send an appropriate error response to the client.

- <b>Transactional Consistency</b>: Guarantees that the entire operation (including any business logic and database writes) is completed within a single transaction, ensuring atomicity.


DbContextWatcherMiddleware checks the current status of the DbContext before a response is sent back to the client. If the DbContext contains modified data, the response is not allowed to be sent and an error code is returned instead.


## Safety and idempotency
<a href="http://en.wikipedia.org/wiki/Hypertext_Transfer_Protocol#Safe_methods">Save</a> and <a href="http://en.wikipedia.org/wiki/Idempotence">idempotent</a> methods such as GET or HEAD should never change the status of the server. This also includes creating, modifying or deleting data in the database. The DbContextWatcherMiddleware checks all incoming requests and sets the DbContext" into the readonly state. Attempts to call the Save method lead to an exception.



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



``` csharp
public class MyDbContextWatcherMiddleware : DbContextWatcherMiddleware<MyDbContext>
{
    public MyDbContextWatcherMiddleware(RequestDelegate next) : base(next)
    {
    }

     protected override Task OnInvoke()
    {
        return base.OnInvoke();
    }

    protected override Task<bool> CanSaveChanges()
    {
        return base.CanSaveChanges();
    }

    protected override Task<bool> HasChanges()
    {
        return base.HasChanges();
    }

    protected override Task SendResponseAsync(DbContextWatcherException exception)
    {
        return base.SendResponseAsync(exception);
    }
}
```
