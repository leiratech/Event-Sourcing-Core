using Leira.EventSourcing.Abstracts;
using Leira.EventSourcing.Interfaces;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Leira.EventSourcing.Extensions
{
    internal static class CosmosContainersExtensions
    {
        internal static HttpStatusCode[] nonFatalErrors = new HttpStatusCode[]
        {
             HttpStatusCode.TooManyRequests,
             HttpStatusCode.InternalServerError,
             HttpStatusCode.BadGateway,
             HttpStatusCode.GatewayTimeout,
             HttpStatusCode.ServiceUnavailable,
             HttpStatusCode.RequestTimeout

        };

        internal static async Task<(HttpStatusCode StatusCode, string eTag)> CreateItemViaStreamAsync<TItem>(this Container container, TItem item, bool retryOnNonFatalErrors, ItemRequestOptions itemRequestOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where TItem : ICosmosDocument
        {
            ResponseMessage responseMessage = null;
            do
            {
                try
                {
                    responseMessage = await container.CreateItemStreamAsync(item.ToJsonStream(), new PartitionKey(item.PartitionKey), itemRequestOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // client exception
                    // retry
                }

                // Workaround some weird issue in cosmos where you get:
                // 1) Timeout on creation, yet it is created successfully!
                // 2) when you read the item by id and partitionKey, you get a wrong item? (Hence the weird looking while.)
                if (item is Event && responseMessage?.StatusCode == HttpStatusCode.Conflict && (responseMessage?.ErrorMessage?.Contains("Resource with specified id or name already exists.") ?? false))
                {
                    JObject dbObj;
                    int millisecondsToWaitOnFailure = 200;
                    do
                    {
                        await Task.Delay(millisecondsToWaitOnFailure).ConfigureAwait(false);
                        millisecondsToWaitOnFailure = millisecondsToWaitOnFailure >= 1000 ? 1000 : millisecondsToWaitOnFailure * 2;
                        dbObj = await container.GetItemViaStreamAsync<JObject>(item.Id, item.PartitionKey).ConfigureAwait(false);
                        var currentObj = JObject.FromObject(item, ObjectsExtensions.serializer);
                    } while (dbObj == null || item.PartitionKey != dbObj?.GetValue("partitionKey").ToObject<string>());

                    return (HttpStatusCode.Created, dbObj.GetValue("_etag").ToObject<string>());
                }

            } while (retryOnNonFatalErrors && nonFatalErrors.Contains(responseMessage?.StatusCode ?? HttpStatusCode.ServiceUnavailable) && ((!responseMessage?.IsSuccessStatusCode) ?? true));

            JObject itemContentRead = null;
            if (responseMessage?.Content != null)
            {
                itemContentRead = responseMessage.Content.FromJsonStream<Newtonsoft.Json.Linq.JObject>();
            }

            return (responseMessage.StatusCode, itemContentRead?.GetValue("_etag")?.ToObject<string>() ?? "");
        }

        internal static async Task<(HttpStatusCode StatusCode, string eTag)> UpsertItemViaStreamAsync<TItem>(this Container container, TItem item, bool retryOnNonFatalErrors, ItemRequestOptions itemRequestOptions = null, CancellationToken cancellationToken = default(CancellationToken)) where TItem : ICosmosDocument
        {
            ResponseMessage responseMessage = null;

            do
            {
                try
                {
                    responseMessage = await container.UpsertItemStreamAsync(item.ToJsonStream(), new PartitionKey(item.PartitionKey), itemRequestOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // client exception
                    // retry
                }
         
            } while (retryOnNonFatalErrors && nonFatalErrors.Contains(responseMessage?.StatusCode ?? HttpStatusCode.ServiceUnavailable) && ((!responseMessage?.IsSuccessStatusCode) ?? true));

            JObject itemContentRead = null;
            if (responseMessage?.Content != null)
            {
                itemContentRead = responseMessage.Content.FromJsonStream<Newtonsoft.Json.Linq.JObject>();
            }

            return (responseMessage.StatusCode, itemContentRead?.GetValue("_etag")?.ToObject<string>() ?? "");
        }

        internal static async Task<TItem> GetItemViaStreamAsync<TItem>(this Container container, string id, string partitionKey, QueryRequestOptions queryRequestOptions = null) //where TItem : ICosmosDocument
        {
            var results = await container.RunQueryDefinitionViaStreamAsync<TItem>(new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = @partitionKey AND c.id = @id").WithParameter("@id", id).WithParameter("@partitionKey", partitionKey), queryRequestOptions).ConfigureAwait(false);
            return results.FirstOrDefault();
        }

        internal static async Task<IEnumerable<TItem>> RunQueryDefinitionViaStreamAsync<TItem>(this Container container, QueryDefinition queryDefinition, QueryRequestOptions queryRequestOptions = null, string continuationToken = null)
        {
            FeedIterator feedIterator = container.GetItemQueryStreamIterator(queryDefinition, continuationToken, queryRequestOptions);
            List<TItem> result = new List<TItem>();
            while (feedIterator.HasMoreResults)
            {
                var resultSet = await feedIterator.ReadNextAsync().ConfigureAwait(false);
                if (resultSet.IsSuccessStatusCode)
                {
                    var resultItem = resultSet.Content.FromJsonStream<Newtonsoft.Json.Linq.JObject>();
                    var docs = resultItem.GetValue("Documents").ToObject<Newtonsoft.Json.Linq.JArray>();
                    foreach (var doc in docs)
                    {
                        result.Add(doc.ToObject<TItem>());
                    }
                }
            }

            return result;
        }
    }
}
