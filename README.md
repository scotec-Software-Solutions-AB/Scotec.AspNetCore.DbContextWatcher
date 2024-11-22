# Scotec.AspNetCore.DbContextWatcher

In a REST WebAPI, the best practice is to write data to the database before sending the response to the client. This approach ensures data consistency and integrity. Here’s why:

- <b>Data Integrity</b>: Ensures that the changes are committed successfully before informing the client. This avoids scenarios where the client is informed of a successful operation, but the data is not actually saved.

- <b>Error Handling</b>: Allows you to handle any errors or exceptions during the database write operation and send an appropriate error response to the client.

- <b>Transactional Consistency</b>: Guarantees that the entire operation (including any business logic and database writes) is completed within a single transaction, ensuring atomicity.


DbContextWatcherMiddleware checks the current status of the DbContext before a response is sent back to the client. If the DbContext contains modified data, the response is not allowed to be sent and an error code is returned instead.


## Safety and idempotency
<a href="http://en.wikipedia.org/wiki/Hypertext_Transfer_Protocol#Safe_methods">Save</a> and <a href="http://en.wikipedia.org/wiki/Idempotence">idempotent</a> methods should never change the status
of the server. This also includes creating, modifying or deleting data in the database. The DbContextWatcherMiddleware checks all incoming requests and sets the DbContext" into the readonly state. Attempts to call the Save method lead to an exception.
<br /><br />
In a REST WebAPI, the best practice is to write data to the database before sending the response to the client. This approach ensures data consistency and integrity. DbContextWatcherMiddleware checks the current status of the DbContext before a response is sent back to the client. If the DbContext contains modified data, the response is not allowed to be sent and an error code is returned instead.

