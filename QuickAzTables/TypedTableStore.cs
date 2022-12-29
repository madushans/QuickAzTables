using Azure.Data.Tables;

namespace QuickAzTables
{
    public class TypedTableStore<T> where T : class, new()
    {
        private readonly TableStore store;
        private readonly Func<T, string>? partitionKeySelector;
        private readonly Func<T, string>? rowKeySelector;

        /// <summary>
        /// Creates an instance that allows to store and query a given 
        /// Table Storage table with the shape of the type <see cref="T"/>
        /// </summary>
        /// <param name="tableName">Name of the table this object should use</param>
        /// <param name="connectionString">connection string to the table storage account</param>
        /// <param name="createTableIfNotExists">
        /// Whether to create the table if not exists. If set to <see langword="false"/> 
        /// the table can be created using <see cref="CreateTableIfNotExistsAsync"/> at a 
        /// later time, but before other operations. If set to <see langword="true"/> 
        /// (default) you're expected to make sure either there's only one instance for 
        /// a given table is used, or constructors are called with enough time in between,
        /// as the createIfNotExists operation can return a <code>Conflict</code> error
        /// if multiple creations are requested within a small duration.
        /// </param>
        /// <param name="invalidKeyCharReplacement">
        /// If an operation was invoked with keys that found to have invalid characters, 
        /// what character to be used to replace them so the key is valid for the operaton. 
        /// Default is <code>""</code> which removed the invalid characters. 
        /// See methods on <see cref="TableKey"/> class
        /// </param>
        /// <param name="partitionKeySelector">
        /// Function that given an instance of <see cref="T"/> 
        /// returns its partition key
        /// </param>
        /// <param name="rowKeySelector">
        /// Function that given an instance of <see cref="T"/> 
        /// returns its row key
        /// </param>
        public TypedTableStore(string tableName,
                               string connectionString,
                               Func<T, string>? partitionKeySelector,
                               Func<T, string>? rowKeySelector,
                               bool createTableIfNotExists = true,
                               string invalidKeyCharReplacement = "")
        {
            // Allow specifying null so the versions with explictly specifying keys
            // can still work.
            //ArgumentNullException.ThrowIfNull(partitionKeySelector);
            //ArgumentNullException.ThrowIfNull(rowKeySelector);

            store = new TableStore(tableName,
                                   connectionString,
                                   createTableIfNotExists,
                                   invalidKeyCharReplacement);


            this.partitionKeySelector = partitionKeySelector;
            this.rowKeySelector = rowKeySelector;
        }

        /// <summary>
        /// Creates an instance that allows to store and query a given 
        /// Table Storage table with the shape of the type <see cref="T"/>
        /// </summary>
        /// <param name="tableName">Name of the table this object should use</param>
        /// <param name="sasToken">SAS Token to the table storage account</param>
        /// <param name="accountName">table storage account name</param>
        /// <param name="createTableIfNotExists">
        /// Whether to create the table if not exists. If set to <see langword="false"/> 
        /// the table can be created using <see cref="CreateTableIfNotExistsAsync"/> at a 
        /// later time, but before other operations. If set to <see langword="true"/> 
        /// (default) you're expected to make sure either there's only one instance for 
        /// a given table is used, or constructors are called with enough time in between,
        /// as the createIfNotExists operation can return a <code>Conflict</code> error
        /// if multiple creations are requested within a small duration.
        /// </param>
        /// <param name="invalidKeyCharReplacement">
        /// If an operation was invoked with keys that found to have invalid characters, 
        /// what character to be used to replace them so the key is valid for the operaton. 
        /// Default is <code>""</code> which removed the invalid characters. 
        /// See methods on <see cref="TableKey"/> class
        /// </param>
        /// <param name="partitionKeySelector">
        /// Function that given an instance of <see cref="T"/> 
        /// returns its partition key
        /// </param>
        /// <param name="rowKeySelector">
        /// Function that given an instance of <see cref="T"/> 
        /// returns its row key
        /// </param>
        public TypedTableStore(string tableName,
                               string sasToken,
                               string accountName,
                               Func<T, string> partitionKeySelector,
                               Func<T, string> rowKeySelector,
                               bool createTableIfNotExists = true,
                               string invalidKeyCharReplacement = "")
        {
            ArgumentNullException.ThrowIfNull(partitionKeySelector);
            ArgumentNullException.ThrowIfNull(rowKeySelector);

            store = new TableStore(tableName,
                                   sasToken,
                                   accountName,
                                   createTableIfNotExists,
                                   invalidKeyCharReplacement);

            this.partitionKeySelector = partitionKeySelector;
            this.rowKeySelector = rowKeySelector;
        }


        /// <summary>
        /// Creates this table if it does not exist.
        /// Note that running this operation on the same table at the same time will cause Table Storage 
        /// to return a <code>Conflict</code> error. You're expected to synchronize calls to avoid this.
        /// </summary>
        /// <returns></returns>
        public Task CreateTableIfNotExistsAsync() => store.CreateTableIfNotExistsAsync();

        /// <summary>
        /// Infers the partition and row keys from the given <paramref name="match"/>
        /// and returns the matching row from the table. Both partition and row keys 
        /// must be inferrable from the provided <paramref name="match"/>
        /// The keys will be inferred and sanitized before calling Table Storage.
        /// </summary>
        public Task<T?> QuerySingleAsync(T match)
            => partitionKeySelector is null || rowKeySelector is null
            ? throw new InvalidOperationException($"To query using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.")
            : QuerySingleAsync(partitionKeySelector(match), rowKeySelector(match));

        /// <summary>
        /// Queries table storage with the given <paramref name="partitionKey"/> and
        /// <paramref name="rowKey"/> and returns the matching record if exists.
        /// <see langword="null"/> otherwise. 
        /// If you want multiple rows in the result see <see cref="QueryAsync(string?, string?)"/>
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task<T?> QuerySingleAsync(string partitionKey, string rowKey)
        {
            var item = await store.QuerySingleAsync(partitionKey, rowKey);
            if (item is null) return null;

            return TypeMapping.MapFromTableEntity<T>(item);
        }

        /// <summary>
        /// Infers the partition and row keys from the specified <paramref name="match"/>
        /// and queries table storage for matching records. Atleast one key must be inferrable
        /// from the <paramref name="match"/>.
        /// If only <paramref name="partitionKey"/> was inferred, this will result in a 
        /// partition scan. If only <paramref name="rowKey"/> was inferred this will result in 
        /// a table scan. If you're looking for a single row, you can use 
        /// <see cref="QuerySingleAsync(T)"/> or <see cref="QuerySingleAsync(string, string)"/> 
        /// instead.
        /// The keys will be inferred and sanitized before calling Table Storage.
        /// </summary>
        public IAsyncEnumerable<T> QueryAsync(T match)
        {
            if (partitionKeySelector is null || rowKeySelector is null)
                    throw new InvalidOperationException($"To query using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.");
            var partitionKey = partitionKeySelector(match);
            var rowKey = rowKeySelector(match);

            // validating here as store will still validate this case but the error message may be confusing since the 
            // keys are inferred from selectors
            if (string.IsNullOrWhiteSpace(partitionKey) && string.IsNullOrWhiteSpace(rowKey))
                throw new InvalidOperationException($"Both {nameof(partitionKeySelector)} and {nameof(rowKey)} returned null or empty keys for the specified {nameof(match)} and cannot be used for a query.");

            return QueryAsync(partitionKeySelector(match), rowKeySelector(match));
        }

        /// <summary>
        /// Queries table storage with the given <paramref name="partitionKey"/> or 
        /// <paramref name="rowKey"/>. If only <paramref name="partitionKey"/> was 
        /// specified this will result in a partition scan. If only <paramref name="rowKey"/>
        /// was specified this will result in a table scan. If you're looking for a single
        /// row, you can use <see cref="QuerySingleAsync(string, string)"/> instead.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async IAsyncEnumerable<T> QueryAsync(string? partitionKey = null, string? rowKey = null)
        {
            await foreach (var item in store.QueryAsync(partitionKey, rowKey))
            {
                yield return TypeMapping.MapFromTableEntity<T>(item);
            }
        }

        /// <summary>
        /// Returns all rows in the table. While the data is paged/segmented, use this 
        /// sparingly as this can result in a large dataset if your table is large.
        /// </summary>
        public async IAsyncEnumerable<T> RetrieveFullTable()
        {
            await foreach (var item in store.RetrieveFullTable())
            {
                yield return TypeMapping.MapFromTableEntity<T>(item);
            }
        }

        /// <summary>
        /// Stores the given item in the table.
        /// The keys will be inferred and sanitized before calling Table Storage.
        /// </summary>
        public async Task StoreSingleAsync(T item)
        {
            if (partitionKeySelector is null || rowKeySelector is null)
                throw new InvalidOperationException($"To store using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.");

            if (item is null) throw new ArgumentNullException(nameof(item));

            string partitionKey = partitionKeySelector(item);
            string rowKey = rowKeySelector(item);

            await StoreSingleAsync(item, partitionKey, rowKey);
        }

        /// <summary>
        /// Stores the given item in the storage against the specified keys.
        /// Provided keys will be used and will not use <see cref="partitionKeySelector"/> 
        /// or <see cref="rowKeySelector"/>.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task StoreSingleAsync(T item, string partitionKey, string rowKey)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            var entity = new TableEntity(partitionKey, rowKey);
            TypeMapping.MapToTableEntity(item, entity);

            await store.StoreSingleAsync(entity, partitionKey, rowKey);
        }

        /// <summary>
        /// Stores all given items in the specified table partition.
        /// If a matching row exists, it will be merged with the new item.
        /// This will automatically group the items by their partition key
        /// and each group will be executed in batches where each batch will
        /// be in an Entity Group Transaction.
        /// The keys will be sanitized before calling Table Storage.
        /// Note that this cannot guarantee that all items will be in the 
        /// same transaction as transactions must only have items for a single
        /// partition and number of items in a batch is limited.
        /// </summary>
        public async Task StoreMultipleAsync(List<T> items)
        {
            if (partitionKeySelector is null || rowKeySelector is null)
                throw new InvalidOperationException($"To store using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.");

            if (items is null) throw new ArgumentNullException(nameof(items));

            var dynamicEntityPartitions = items
                .Select(i =>
                {
                    var entity = new TableEntity(partitionKeySelector(i), rowKeySelector(i));
                    TypeMapping.MapToTableEntity(i, entity);
                    return entity;
                })
                .GroupBy(e => e.PartitionKey);

            foreach (var partition in dynamicEntityPartitions)
            {
                await store.StoreMultipleInPartitionAsync(partition.Select(i => i).ToList(), partition.Key);
            }
        }

        /// <summary>
        /// Deleted the given item in the table.
        /// The keys will be inferred and sanitized before calling Table Storage.
        /// </summary>
        public async Task DeleteSingleAsync(T match)
        {
            if (partitionKeySelector is null || rowKeySelector is null)
                throw new InvalidOperationException($"To store using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.");

            var partitionKey = partitionKeySelector(match);
            var rowKey = rowKeySelector(match);
            await DeleteSingleAsync(partitionKey, rowKey);
        }

        /// <summary>
        /// Stores the specified item in the storage against the specified keys.
        /// Provided keys will be used to indetify the item.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public async Task DeleteSingleAsync(string partitionKey, string rowKey)
        {
            await store.DeleteSingleAsync(partitionKey, rowKey);
        }

        /// <summary>
        /// Deletes all given items in the specified table partition.
        /// This will automatically group the items by their partition key
        /// and each group will be executed in batches where each batch will
        /// be in an Entity Group Transaction.
        /// The keys will be sanitized before calling Table Storage.
        /// Note that this cannot guarantee that all items will be in the 
        /// same transaction as transactions must only have items for a single
        /// partition and number of items in a batch is limited.
        /// </summary>
        public async Task DeleteMultipleAsync(List<T> matches)
        {
            if (partitionKeySelector is null || rowKeySelector is null)
                throw new InvalidOperationException($"To store using the typed object, both {nameof(partitionKeySelector)} and {nameof(rowKeySelector)} must be specified in the constructor.");

            var partitions = matches.Select(m => new
            {
                partitionKey = partitionKeySelector(m),
                rowKey = rowKeySelector(m)
            }).GroupBy(i => i.partitionKey);

            foreach (var partition in partitions)
            {
                await store.DeleteMultipleInPartitionAsync(partition.Key, partition.Select(i => i.rowKey).ToList());
            }
        }

        /// <summary>
        /// Deletes all rows identified in the specified table partition.
        /// Note that all items must be in the same partition as this will be 
        /// executed as a batched Entity Group Transaction.
        /// The keys will be sanitized before calling Table Storage.
        /// </summary>
        public Task DeleteMultipleInPartitionAsync(string partitionKey, List<string> rowKeys) =>
            store.DeleteMultipleInPartitionAsync(partitionKey, rowKeys);
    }
}